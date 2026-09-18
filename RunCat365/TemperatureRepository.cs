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
using LibreHardwareMonitor.Hardware;
using RunCat365.Properties;
using System.Globalization;

namespace RunCat365
{
    struct TemperatureInfo
    {
        internal float AverageCelsius { get; set; }
        internal float MaximumCelsius { get; set; }
    }

    internal static class TemperatureInfoExtension
    {
        private const float CELSIUS_TO_FAHRENHEIT_SCALE = 9.0f / 5.0f;
        private const float CELSIUS_TO_FAHRENHEIT_OFFSET = 32.0f;

        internal static string GetDescription(this TemperatureInfo temperatureInfo, TemperatureUnit unit)
        {
            var resolvedUnit = unit.Resolve();
            return $"{Strings.SystemInfo_Temperature}: {temperatureInfo.MaximumCelsius.ToLocalizedTemperatureText(resolvedUnit)}";
        }

        internal static List<string> GenerateIndicator(this TemperatureInfo temperatureInfo, TemperatureUnit unit)
        {
            var resolvedUnit = unit.Resolve();
            return [
                TreeFormatter.CreateRoot($"{Strings.SystemInfo_Temperature}:"),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Average}: {temperatureInfo.AverageCelsius.ToLocalizedTemperatureText(resolvedUnit)}", false),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Maximum}: {temperatureInfo.MaximumCelsius.ToLocalizedTemperatureText(resolvedUnit)}", true)
            ];
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
    /// Falls back to LibreHardwareMonitor CPU sensors when counters are missing
    /// or return no valid readings.
    /// </summary>
    internal class TemperatureRepository
    {
        private const float KELVIN_TO_CELSIUS_OFFSET = 273.15f;
        private const float MIN_VALID_TEMPERATURE_CELSIUS = -50.0f;
        private const float MAX_VALID_TEMPERATURE_CELSIUS = 150.0f;
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
                TryInitializeLhm();
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
                MaximumCelsius = temperaturesCelsius.Max()
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
                };
                next.Open();
                computer = next;

                var sample = ReadFromLhm();
                if (sample is null)
                {
                    next.Close();
                    computer = null;
                    lhmAvailable = false;
                    Debug.WriteLine("TemperatureRepository: LHM opened but no CPU temperature sensors found.");
                    return false;
                }

                temperatureInfo = sample;
                lhmAvailable = true;
                return true;
            }
            catch (Exception exception)
            {
                computer = null;
                lhmAvailable = false;
                Debug.WriteLine($"TemperatureRepository: LHM init failed: {exception.Message}");
                return false;
            }
        }

        private TemperatureInfo? ReadFromLhm()
        {
            if (computer is null) return null;

            try
            {
                float? packageCelsius = null;
                var coreTemps = new List<float>();

                foreach (var hardware in computer.Hardware)
                {
                    CollectCpuTemps(hardware, ref packageCelsius, coreTemps);
                }

                if (packageCelsius is null && coreTemps.Count == 0) return null;

                var maximum = packageCelsius ?? coreTemps.Max();
                var average = coreTemps.Count > 0 ? coreTemps.Average() : maximum;
                return new TemperatureInfo
                {
                    AverageCelsius = average,
                    MaximumCelsius = maximum
                };
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"TemperatureRepository: LHM read failed: {exception.Message}");
                return null;
            }
        }

        private static void CollectCpuTemps(
            IHardware hardware,
            ref float? packageCelsius,
            List<float> coreTemps
        )
        {
            if (hardware.HardwareType != HardwareType.Cpu)
            {
                foreach (var subHardware in hardware.SubHardware)
                {
                    CollectCpuTemps(subHardware, ref packageCelsius, coreTemps);
                }
                return;
            }

            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature) continue;
                if (sensor.Value is not float value) continue;
                if (value is < MIN_VALID_TEMPERATURE_CELSIUS or > MAX_VALID_TEMPERATURE_CELSIUS) continue;

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

            foreach (var subHardware in hardware.SubHardware)
            {
                CollectCpuTemps(subHardware, ref packageCelsius, coreTemps);
            }
        }

        private static bool IsPackageSensorName(string name)
        {
            return name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                || name.Contains("CCD", StringComparison.OrdinalIgnoreCase)
                || name.Equals("CPU", StringComparison.OrdinalIgnoreCase);
        }
    }
}
