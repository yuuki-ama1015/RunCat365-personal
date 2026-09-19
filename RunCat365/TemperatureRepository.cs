// Copyright 2025 Takuto Nakamura
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System.Diagnostics;
using System.Threading;
using LibreHardwareMonitor.Hardware;
using RunCat365.Properties;
using System.Globalization;

namespace RunCat365
{
    enum TemperatureSource
    {
        System,
        Cpu,
    }

    struct TemperatureInfo
    {
        internal float AverageCelsius { get; set; }
        internal float MaximumCelsius { get; set; }
        internal TemperatureSource Source { get; set; }
    }

    internal static class TemperatureInfoExtension
    {
        private const float CELSIUS_TO_FAHRENHEIT_SCALE = 9.0f / 5.0f;
        private const float CELSIUS_TO_FAHRENHEIT_OFFSET = 32.0f;

        internal static string GetDescription(this TemperatureInfo temperatureInfo, TemperatureUnit unit)
        {
            var resolvedUnit = unit.Resolve();
            var sourceLabel = temperatureInfo.Source.GetLocalizedLabel();
            var temperatureText = temperatureInfo.MaximumCelsius.ToLocalizedTemperatureText(resolvedUnit);
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.SystemInfo_TemperatureWithSourceFormat,
                temperatureText,
                sourceLabel);
        }

        internal static List<string> GenerateIndicator(this TemperatureInfo temperatureInfo, TemperatureUnit unit)
        {
            var resolvedUnit = unit.Resolve();
            var sourceLabel = temperatureInfo.Source.GetLocalizedLabel();
            var root = string.Format(
                CultureInfo.CurrentCulture,
                Strings.SystemInfo_TemperatureSourceRootFormat,
                sourceLabel);
            return [
                TreeFormatter.CreateRoot(root),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Average}: {temperatureInfo.AverageCelsius.ToLocalizedTemperatureText(resolvedUnit)}", false),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Maximum}: {temperatureInfo.MaximumCelsius.ToLocalizedTemperatureText(resolvedUnit)}", true)
            ];
        }

        internal static string GetLocalizedLabel(this TemperatureSource source)
        {
            return source switch
            {
                TemperatureSource.Cpu => Strings.TemperatureSource_Cpu,
                _ => Strings.TemperatureSource_System,
            };
        }

        private static string ToLocalizedTemperatureText(this float temperatureCelsius, TemperatureUnit resolvedUnit)
        {
            var useFahrenheit = resolvedUnit == TemperatureUnit.Fahrenheit;
            var value = useFahrenheit
                ? temperatureCelsius * CELSIUS_TO_FAHRENHEIT_SCALE + CELSIUS_TO_FAHRENHEIT_OFFSET
                : temperatureCelsius;
            var format = useFahrenheit
                ? Strings.SystemInfo_TemperatureFahrenheitFormat
                : Strings.SystemInfo_TemperatureCelsiusFormat;
            return string.Format(CultureInfo.CurrentCulture, format, value);
        }
    }

    internal sealed class TemperaturePerformanceCounters : InstancedPerformanceCounters
    {
        protected override string CategoryName => "Thermal Zone Information";
        protected override string CounterName => "Temperature";

        internal static TemperaturePerformanceCounters? TryCreate()
        {
            var instance = new TemperaturePerformanceCounters();
            return instance.TryInitialize() ? instance : null;
        }
    }

    /// <summary>
    /// Prefers Windows "Thermal Zone Information" performance counters.
    /// Falls back to LibreHardwareMonitor (CPU + Motherboard/SuperIO sensors)
    /// when counters are missing or return no valid readings.
    /// LHM-derived values are labeled as CPU in the UI.
    /// </summary>
    internal class TemperatureRepository
    {
        private const float KELVIN_TO_CELSIUS_OFFSET = 273.15f;
        private const float MIN_VALID_TEMPERATURE_CELSIUS = -50.0f;
        private const float MAX_VALID_TEMPERATURE_CELSIUS = 150.0f;
        private const float MIN_PLAUSIBLE_CPU_CELSIUS = 5.0f;
        private const float MAX_PLAUSIBLE_CPU_CELSIUS = 115.0f;
        private const int REFRESH_INTERVAL_TICKS = 30;

        private readonly TemperaturePerformanceCounters? counters;
        private Computer? computer;
        private bool lhmAvailable;
        private bool lhmInitAttempted;
        private TemperatureInfo? temperatureInfo;
        private int ticksSinceLastRefresh;

        internal bool IsAvailable => counters is not null || lhmAvailable;

        internal TemperatureRepository()
        {
            counters = TemperaturePerformanceCounters.TryCreate();
            if (counters is null)
            {
                StartupLog.Append("Temperature: Thermal Zone Information counters unavailable; trying LHM.");
                TryInitializeLhm();
            }
            else
            {
                StartupLog.Append("Temperature: Thermal Zone Information counters initialized.");
            }
        }

        internal void Update()
        {
            TemperatureInfo? fromCounters = null;
            if (counters is not null)
            {
                ticksSinceLastRefresh += 1;
                if (REFRESH_INTERVAL_TICKS <= ticksSinceLastRefresh)
                {
                    ticksSinceLastRefresh = 0;
                    counters.RefreshInstances();
                }
                fromCounters = ReadFromCounters();
            }

            if (fromCounters is not null)
            {
                temperatureInfo = fromCounters;
                return;
            }

            if (!lhmAvailable && !lhmInitAttempted)
            {
                StartupLog.Append("Temperature: counters yielded no valid reading; trying LHM.");
                TryInitializeLhm();
            }

            temperatureInfo = lhmAvailable ? ReadFromLhm() : null;
        }

        internal TemperatureInfo? Get()
        {
            return temperatureInfo;
        }

        internal void Close()
        {
            counters?.Close();
            if (computer is not null)
            {
                try
                {
                    computer.Close();
                }
                catch (Exception exception)
                {
                    Debug.WriteLine($"TemperatureRepository LHM Close failed: {exception.Message}");
                }
                computer = null;
            }
            lhmAvailable = false;
        }

        private TemperatureInfo? ReadFromCounters()
        {
            if (counters is null) return null;

            var rawValues = counters.ReadValues();
            var temperaturesCelsius = new List<float>(rawValues.Count);
            foreach (var temperatureKelvin in rawValues)
            {
                if (temperatureKelvin <= 0) continue;
                var temperatureCelsius = temperatureKelvin - KELVIN_TO_CELSIUS_OFFSET;
                if (temperatureCelsius is < MIN_VALID_TEMPERATURE_CELSIUS or > MAX_VALID_TEMPERATURE_CELSIUS) continue;
                temperaturesCelsius.Add(temperatureCelsius);
            }

            if (temperaturesCelsius.Count == 0) return null;

            return new TemperatureInfo
            {
                AverageCelsius = temperaturesCelsius.Average(),
                MaximumCelsius = temperaturesCelsius.Max(),
                Source = TemperatureSource.System
            };
        }

        private bool TryInitializeLhm()
        {
            lhmInitAttempted = true;
            try
            {
                var next = new Computer
                {
                    IsCpuEnabled = true,
                    IsMotherboardEnabled = true,
                };
                next.Open();
                computer = next;

                // Sensors often appear with null values on the first Update; refresh twice.
                RefreshLhmSensors(next);
                Thread.Sleep(100);
                RefreshLhmSensors(next);

                var sample = ReadFromLhm(refreshFirst: false);
                if (sample is null)
                {
                    // One more delayed pass before giving up.
                    Thread.Sleep(200);
                    RefreshLhmSensors(next);
                    sample = ReadFromLhm(refreshFirst: false);
                }

                if (sample is null)
                {
                    var sensorSummary = SummarizeTemperatureSensors(next);
                    next.Close();
                    computer = null;
                    lhmAvailable = false;
                    StartupLog.Append(
                        $"Temperature: LHM opened but no usable CPU/Motherboard temperature sensors. {sensorSummary}");
                    return false;
                }

                temperatureInfo = sample;
                lhmAvailable = true;
                StartupLog.Append(
                    $"Temperature: LHM available (labeled CPU). max={sample.Value.MaximumCelsius:F1}C avg={sample.Value.AverageCelsius:F1}C");
                return true;
            }
            catch (Exception exception)
            {
                computer = null;
                lhmAvailable = false;
                StartupLog.Append($"Temperature: LHM init failed: {exception.GetType().Name}: {exception.Message}");
                return false;
            }
        }

        private TemperatureInfo? ReadFromLhm(bool refreshFirst = true)
        {
            if (computer is null) return null;

            try
            {
                if (refreshFirst)
                {
                    RefreshLhmSensors(computer);
                }

                float? packageCelsius = null;
                var coreTemps = new List<float>();
                var motherboardCpuLikeTemps = new List<float>();
                var motherboardOtherTemps = new List<float>();

                foreach (var hardware in computer.Hardware)
                {
                    CollectTemps(
                        hardware,
                        ref packageCelsius,
                        coreTemps,
                        motherboardCpuLikeTemps,
                        motherboardOtherTemps,
                        updateHardware: false);
                }

                if (packageCelsius is not null || coreTemps.Count > 0)
                {
                    var maximum = packageCelsius ?? coreTemps.Max();
                    var average = coreTemps.Count > 0 ? coreTemps.Average() : maximum;
                    return new TemperatureInfo
                    {
                        AverageCelsius = average,
                        MaximumCelsius = maximum,
                        Source = TemperatureSource.Cpu
                    };
                }

                if (motherboardCpuLikeTemps.Count > 0)
                {
                    var maximum = motherboardCpuLikeTemps.Max();
                    return new TemperatureInfo
                    {
                        AverageCelsius = motherboardCpuLikeTemps.Average(),
                        MaximumCelsius = maximum,
                        Source = TemperatureSource.Cpu
                    };
                }

                var plausible = motherboardOtherTemps
                    .Where(t => t is >= MIN_PLAUSIBLE_CPU_CELSIUS and <= MAX_PLAUSIBLE_CPU_CELSIUS)
                    .ToList();
                if (plausible.Count == 0) return null;

                return new TemperatureInfo
                {
                    AverageCelsius = plausible.Average(),
                    MaximumCelsius = plausible.Max(),
                    Source = TemperatureSource.Cpu
                };
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"TemperatureRepository: LHM read failed: {exception.Message}");
                return null;
            }
        }

        private static void RefreshLhmSensors(Computer computer)
        {
            computer.Accept(new LhmUpdateVisitor());
        }

        private sealed class LhmUpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Accept(this);
                }
            }

            public void VisitSensor(ISensor sensor)
            {
            }

            public void VisitParameter(IParameter parameter)
            {
            }
        }

        private static void CollectTemps(
            IHardware hardware,
            ref float? packageCelsius,
            List<float> coreTemps,
            List<float> motherboardCpuLikeTemps,
            List<float> motherboardOtherTemps,
            bool updateHardware = true
        )
        {
            if (hardware.HardwareType == HardwareType.Cpu)
            {
                if (updateHardware) hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (!TryReadCelsius(sensor, out var value)) continue;
                    var name = sensor.Name ?? string.Empty;
                    if (IsPackageSensorName(name))
                    {
                        packageCelsius = packageCelsius is null
                            ? value
                            : Math.Max(packageCelsius.Value, value);
                    }
                    else
                    {
                        coreTemps.Add(value);
                    }
                }
            }
            else if (hardware.HardwareType == HardwareType.Motherboard
                     || hardware.HardwareType == HardwareType.SuperIO)
            {
                if (updateHardware) hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (!TryReadCelsius(sensor, out var value)) continue;
                    var name = sensor.Name ?? string.Empty;
                    if (IsMotherboardCpuLikeName(name))
                    {
                        motherboardCpuLikeTemps.Add(value);
                    }
                    else
                    {
                        motherboardOtherTemps.Add(value);
                    }
                }
            }

            foreach (var subHardware in hardware.SubHardware)
            {
                CollectTemps(
                    subHardware,
                    ref packageCelsius,
                    coreTemps,
                    motherboardCpuLikeTemps,
                    motherboardOtherTemps,
                    updateHardware);
            }
        }

        private static bool TryReadCelsius(ISensor sensor, out float value)
        {
            value = 0;
            if (sensor.SensorType != SensorType.Temperature) return false;
            if (sensor.Value is not float reading) return false;
            if (reading is < MIN_VALID_TEMPERATURE_CELSIUS or > MAX_VALID_TEMPERATURE_CELSIUS) return false;
            value = reading;
            return true;
        }

        private static bool IsPackageSensorName(string name)
        {
            return name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                || name.Contains("CCD", StringComparison.OrdinalIgnoreCase)
                || name.Equals("CPU", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMotherboardCpuLikeName(string name)
        {
            return name.Contains("CPU", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                || name.Contains("PECI", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Core", StringComparison.OrdinalIgnoreCase);
        }

        private static string SummarizeTemperatureSensors(Computer computer)
        {
            try
            {
                RefreshLhmSensors(computer);
                var parts = new List<string>();
                foreach (var hardware in computer.Hardware)
                {
                    AppendSensorSummary(hardware, parts);
                }
                return parts.Count == 0
                    ? "hardware=none"
                    : "seen=" + string.Join("; ", parts.Take(12));
            }
            catch (Exception exception)
            {
                return $"summary-failed={exception.Message}";
            }
        }

        private static void AppendSensorSummary(IHardware hardware, List<string> parts)
        {
            hardware.Update();
            var temps = hardware.Sensors
                .Where(s => s.SensorType == SensorType.Temperature)
                .Select(s => $"{s.Name}={(s.Value.HasValue ? s.Value.Value.ToString("F1", CultureInfo.InvariantCulture) : "null")}")
                .ToList();
            if (temps.Count > 0)
            {
                parts.Add($"{hardware.HardwareType}:{hardware.Name}[{string.Join(",", temps.Take(6))}]");
            }
            foreach (var sub in hardware.SubHardware)
            {
                AppendSensorSummary(sub, parts);
            }
        }
    }
}
