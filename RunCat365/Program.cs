// Copyright 2020 Takuto Nakamura
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

using Microsoft.Win32;
using RunCat365.Properties;
using System.Diagnostics;
using System.Globalization;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunCat365
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
#if DEBUG
            var defaultCultureInfo = SupportedLanguage.English.GetDefaultCultureInfo();
#else
            var defaultCultureInfo = SupportedLanguageExtension.GetCurrentLanguage().GetDefaultCultureInfo();
#endif
            CultureInfo.CurrentUICulture = defaultCultureInfo;
            CultureInfo.CurrentCulture = defaultCultureInfo;

            using var procMutex = new Mutex(true, "_RUNCAT_MUTEX", out var result);
            if (!result) return;

            try
            {
                ApplicationConfiguration.Initialize();
                Application.SetColorMode(SystemColorMode.System);
                Application.Run(new RunCat365ApplicationContext());
            }
            finally
            {
                procMutex?.ReleaseMutex();
            }
        }
    }

    internal class RunCat365ApplicationContext : ApplicationContext
    {
        private const int FETCH_TIMER_DEFAULT_INTERVAL = 1000;
        private const int FETCH_COUNTER_SIZE = 5;
        private const float TemperatureIdleCelsius = 40.0f;
        private const float TemperatureFullCelsius = 95.0f;
        private readonly CPURepository cpuRepository;
        private readonly GPURepository gpuRepository;
        private readonly MemoryRepository memoryRepository;
        private readonly TemperatureRepository temperatureRepository;
        private readonly StorageRepository storageRepository;
        private readonly NetworkRepository networkRepository;
        private readonly CustomRunnerRepository customRunnerRepository;
        private readonly LaunchAtStartupManager launchAtStartupManager;
        private readonly ContextMenuManager contextMenuManager;
        private readonly FormsTimer fetchTimer;
        private readonly Dictionary<SpeedSource, IndicatorConfig> indicatorConfigs = [];
        private Theme manualTheme = Theme.System;
        private TemperatureUnit temperatureUnit = TemperatureUnit.System;
        private FPSMaxLimit fpsMaxLimit = FPSMaxLimit.FPS40;
        private int fetchCounter = 5;
        private bool isFetching;

        public RunCat365ApplicationContext()
        {
            UserSettings.Default.Reload();
            _ = Enum.TryParse(UserSettings.Default.Theme, out manualTheme);
            _ = Enum.TryParse(UserSettings.Default.TemperatureUnit, out temperatureUnit);
            _ = Enum.TryParse(UserSettings.Default.FPSMaxLimit, out fpsMaxLimit);

            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(UserPreferenceChanged);

            cpuRepository = new CPURepository();
            gpuRepository = new GPURepository();
            memoryRepository = new MemoryRepository();
            temperatureRepository = new TemperatureRepository();
            storageRepository = new StorageRepository();
            networkRepository = new NetworkRepository();
            customRunnerRepository = new CustomRunnerRepository();
            launchAtStartupManager = new LaunchAtStartupManager();

            LoadIndicatorConfigs();

            contextMenuManager = new ContextMenuManager(
                () => indicatorConfigs,
                (source, enabled) => ChangeIndicatorEnabled(source, enabled),
                (source, runner) => ChangeIndicatorRunner(source, runner),
                (source, enabled) => ChangeColorTintEnabled(source, enabled),
                (source, strength) => ChangeColorTintStrength(source, strength),
                (source, enabled) => ChangeRunnerSpeedEnabled(source, enabled),
                customRunnerRepository,
                (source, name) => ApplyCustomRunner(source, name),
                deletedName => HandleCustomRunnerDeleted(deletedName),
                () => GetSystemTheme(),
                () => manualTheme,
                t => ChangeManualTheme(t),
                s => IsSpeedSourceAvailable(s),
                () => fpsMaxLimit,
                f => ChangeFPSMaxLimit(f),
                () => temperatureUnit,
                u => ChangeTemperatureUnit(u),
                () => launchAtStartupManager.GetStartup(),
                s => launchAtStartupManager.ToggleStartup(s),
                () => OpenProjectPage(),
                () => Application.Exit()
            );

            fetchTimer = new FormsTimer
            {
                Interval = FETCH_TIMER_DEFAULT_INTERVAL
            };
            fetchTimer.Tick += new EventHandler(FetchTick);
            fetchTimer.Start();

#if DEBUG
            VerifyTemperatureToLoadMapping();
#endif
            ShowBalloonTipIfNeeded();
        }

        private void LoadIndicatorConfigs()
        {
            if (!UserSettings.Default.IndicatorsMigrated)
            {
                MigrateFromLegacySettings();
            }
            else
            {
                indicatorConfigs[SpeedSource.CPU] = new IndicatorConfig(
                    SpeedSource.CPU,
                    UserSettings.Default.CpuIndicatorEnabled,
                    ParseRunner(UserSettings.Default.CpuRunner, Runner.Cat),
                    NullIfEmpty(UserSettings.Default.CpuCustomRunnerName),
                    UserSettings.Default.CpuColorTintEnabled,
                    UserSettings.Default.CpuRunnerSpeedEnabled,
                    UserSettings.Default.CpuColorTintStrength
                );
                indicatorConfigs[SpeedSource.GPU] = new IndicatorConfig(
                    SpeedSource.GPU,
                    UserSettings.Default.GpuIndicatorEnabled,
                    ParseRunner(UserSettings.Default.GpuRunner, Runner.Parrot),
                    NullIfEmpty(UserSettings.Default.GpuCustomRunnerName),
                    UserSettings.Default.GpuColorTintEnabled,
                    UserSettings.Default.GpuRunnerSpeedEnabled,
                    UserSettings.Default.GpuColorTintStrength
                );
                indicatorConfigs[SpeedSource.Memory] = new IndicatorConfig(
                    SpeedSource.Memory,
                    UserSettings.Default.MemoryIndicatorEnabled,
                    ParseRunner(UserSettings.Default.MemoryRunner, Runner.Horse),
                    NullIfEmpty(UserSettings.Default.MemoryCustomRunnerName),
                    UserSettings.Default.MemoryColorTintEnabled,
                    UserSettings.Default.MemoryRunnerSpeedEnabled,
                    UserSettings.Default.MemoryColorTintStrength
                );
                indicatorConfigs[SpeedSource.Temperature] = new IndicatorConfig(
                    SpeedSource.Temperature,
                    UserSettings.Default.TemperatureIndicatorEnabled,
                    ParseRunner(UserSettings.Default.TemperatureRunner, Runner.Cat),
                    NullIfEmpty(UserSettings.Default.TemperatureCustomRunnerName),
                    UserSettings.Default.TemperatureColorTintEnabled,
                    UserSettings.Default.TemperatureRunnerSpeedEnabled,
                    UserSettings.Default.TemperatureColorTintStrength
                );
            }

            foreach (var config in indicatorConfigs.Values)
            {
                if (config.Enabled && !IsSpeedSourceAvailable(config.SpeedSource))
                {
                    config.Enabled = false;
                }
                EnsureRunnerOrTint(config);
            }

            EnsureAtLeastOneEnabled();
            SaveIndicatorSettings();
        }

        private void MigrateFromLegacySettings()
        {
            _ = Enum.TryParse(UserSettings.Default.SpeedSource, out SpeedSource legacySource);
            if (!Enum.IsDefined(legacySource)) legacySource = SpeedSource.CPU;

            _ = Enum.TryParse(UserSettings.Default.Runner, out Runner legacyRunner);
            if (!Enum.IsDefined(legacyRunner)) legacyRunner = Runner.Cat;

            var legacyCustom = NullIfEmpty(UserSettings.Default.CustomRunnerName);

            foreach (SpeedSource source in Enum.GetValues<SpeedSource>())
            {
                var enabled = source == legacySource;
                var runner = enabled ? legacyRunner : IndicatorConfig.DefaultRunnerFor(source);
                var customName = enabled ? legacyCustom : null;
                indicatorConfigs[source] = new IndicatorConfig(source, enabled, runner, customName);
            }

            UserSettings.Default.IndicatorsMigrated = true;
        }

        private static Runner ParseRunner(string value, Runner fallback)
        {
            return Enum.TryParse(value, out Runner runner) && Enum.IsDefined(runner) ? runner : fallback;
        }

        private static string? NullIfEmpty(string? value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private void EnsureAtLeastOneEnabled()
        {
            if (indicatorConfigs.Values.Any(c => c.Enabled && IsSpeedSourceAvailable(c.SpeedSource)))
            {
                return;
            }

            if (indicatorConfigs.TryGetValue(SpeedSource.CPU, out var cpuConfig))
            {
                cpuConfig.Enabled = true;
            }
        }

        private void SaveIndicatorSettings()
        {
            if (indicatorConfigs.TryGetValue(SpeedSource.CPU, out var cpu))
            {
                UserSettings.Default.CpuIndicatorEnabled = cpu.Enabled;
                UserSettings.Default.CpuRunner = cpu.Runner.ToString();
                UserSettings.Default.CpuCustomRunnerName = cpu.CustomRunnerName ?? string.Empty;
                UserSettings.Default.CpuColorTintEnabled = cpu.ColorTintEnabled;
                UserSettings.Default.CpuRunnerSpeedEnabled = cpu.RunnerSpeedEnabled;
                UserSettings.Default.CpuColorTintStrength = cpu.ColorTintStrength;
            }
            if (indicatorConfigs.TryGetValue(SpeedSource.GPU, out var gpu))
            {
                UserSettings.Default.GpuIndicatorEnabled = gpu.Enabled;
                UserSettings.Default.GpuRunner = gpu.Runner.ToString();
                UserSettings.Default.GpuCustomRunnerName = gpu.CustomRunnerName ?? string.Empty;
                UserSettings.Default.GpuColorTintEnabled = gpu.ColorTintEnabled;
                UserSettings.Default.GpuRunnerSpeedEnabled = gpu.RunnerSpeedEnabled;
                UserSettings.Default.GpuColorTintStrength = gpu.ColorTintStrength;
            }
            if (indicatorConfigs.TryGetValue(SpeedSource.Memory, out var memory))
            {
                UserSettings.Default.MemoryIndicatorEnabled = memory.Enabled;
                UserSettings.Default.MemoryRunner = memory.Runner.ToString();
                UserSettings.Default.MemoryCustomRunnerName = memory.CustomRunnerName ?? string.Empty;
                UserSettings.Default.MemoryColorTintEnabled = memory.ColorTintEnabled;
                UserSettings.Default.MemoryRunnerSpeedEnabled = memory.RunnerSpeedEnabled;
                UserSettings.Default.MemoryColorTintStrength = memory.ColorTintStrength;
            }
            if (indicatorConfigs.TryGetValue(SpeedSource.Temperature, out var temperature))
            {
                UserSettings.Default.TemperatureIndicatorEnabled = temperature.Enabled;
                UserSettings.Default.TemperatureRunner = temperature.Runner.ToString();
                UserSettings.Default.TemperatureCustomRunnerName = temperature.CustomRunnerName ?? string.Empty;
                UserSettings.Default.TemperatureColorTintEnabled = temperature.ColorTintEnabled;
                UserSettings.Default.TemperatureRunnerSpeedEnabled = temperature.RunnerSpeedEnabled;
                UserSettings.Default.TemperatureColorTintStrength = temperature.ColorTintStrength;
            }
            UserSettings.Default.IndicatorsMigrated = true;
            UserSettings.Default.Save();
        }

        private static Theme GetSystemTheme()
        {
            var keyName = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            using var rKey = Registry.CurrentUser.OpenSubKey(keyName);
            if (rKey is null) return Theme.Light;
            var value = rKey.GetValue("SystemUsesLightTheme");
            if (value is null) return Theme.Light;
            return (int)value == 0 ? Theme.Dark : Theme.Light;
        }

        private bool IsSpeedSourceAvailable(SpeedSource speedSource)
        {
            return speedSource switch
            {
                SpeedSource.CPU => true,
                SpeedSource.GPU => gpuRepository.IsAvailable,
                SpeedSource.Memory => true,
                SpeedSource.Temperature => temperatureRepository.IsAvailable,
                _ => false,
            };
        }

        private void ShowBalloonTipIfNeeded()
        {
            if (!cpuRepository.IsAvailable)
            {
                contextMenuManager.ShowBalloonTip(BalloonTipType.CPUInfoUnavailable);
            }
            else if (UserSettings.Default.FirstLaunch)
            {
                contextMenuManager.ShowBalloonTip(BalloonTipType.AppLaunched);
                UserSettings.Default.FirstLaunch = false;
                UserSettings.Default.Save();
            }
        }

        private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            contextMenuManager.RefreshThemeIcons(
                () => indicatorConfigs,
                GetSystemTheme(),
                manualTheme,
                customRunnerRepository
            );
        }

        private static void OpenProjectPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://github.com/runcat-dev/RunCat365",
                    UseShellExecute = true
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        private void ChangeIndicatorEnabled(SpeedSource source, bool enabled)
        {
            if (!indicatorConfigs.TryGetValue(source, out var config)) return;

            if (!enabled)
            {
                var otherEnabled = indicatorConfigs.Values.Any(c =>
                    c.SpeedSource != source
                    && c.Enabled
                    && IsSpeedSourceAvailable(c.SpeedSource));
                if (!otherEnabled)
                {
                    // Keep at least one indicator enabled.
                    config.Enabled = true;
                    SaveIndicatorSettings();
                    return;
                }
            }

            if (enabled && !IsSpeedSourceAvailable(source))
            {
                config.Enabled = false;
                SaveIndicatorSettings();
                return;
            }

            config.Enabled = enabled;
            SaveIndicatorSettings();
            contextMenuManager.SetIndicatorVisible(source, config.Enabled);
            if (config.Enabled)
            {
                if (!string.IsNullOrEmpty(config.CustomRunnerName))
                {
                    ApplyCustomRunner(source, config.CustomRunnerName);
                }
                else
                {
                    contextMenuManager.ApplyBuiltInIcons(source, GetSystemTheme(), manualTheme, config.Runner);
                }
            }
        }

        private void ChangeIndicatorRunner(SpeedSource source, Runner runner)
        {
            if (!indicatorConfigs.TryGetValue(source, out var config)) return;
            config.Runner = runner;
            config.CustomRunnerName = null;
            SaveIndicatorSettings();
            contextMenuManager.ApplyBuiltInIcons(source, GetSystemTheme(), manualTheme, runner);
        }

        private void ChangeColorTintEnabled(SpeedSource source, bool enabled)
        {
            if (!indicatorConfigs.TryGetValue(source, out var config)) return;
            config.ColorTintEnabled = enabled;
            EnsureRunnerOrTint(config);
            SaveIndicatorSettings();
            if (!config.ColorTintEnabled)
            {
                contextMenuManager.SetIndicatorLoadTint(source, null);
            }
            else
            {
                // Placeholder until the next fetch applies the real load step.
                contextMenuManager.SetIndicatorLoadTint(source, 0, config.ColorTintStrength);
            }
        }

        private void ChangeColorTintStrength(SpeedSource source, int strength)
        {
            if (!indicatorConfigs.TryGetValue(source, out var config)) return;
            config.ColorTintStrength = Math.Clamp(strength, 0, 100);
            SaveIndicatorSettings();
            if (config.ColorTintEnabled)
            {
                contextMenuManager.SetIndicatorLoadTintStrength(source, config.ColorTintStrength);
            }
        }

        private void ChangeRunnerSpeedEnabled(SpeedSource source, bool enabled)
        {
            if (!indicatorConfigs.TryGetValue(source, out var config)) return;
            config.RunnerSpeedEnabled = enabled;
            EnsureRunnerOrTint(config);
            SaveIndicatorSettings();
        }

        private static void EnsureRunnerOrTint(IndicatorConfig config)
        {
            if (config.Enabled && !config.RunnerSpeedEnabled && !config.ColorTintEnabled)
            {
                config.RunnerSpeedEnabled = true;
            }
        }

        private void ApplyCustomRunner(SpeedSource source, string name)
        {
            if (!indicatorConfigs.TryGetValue(source, out var config)) return;
            var frames = customRunnerRepository.LoadFrames(name);
            if (frames.Count == 0) return;
            config.CustomRunnerName = name;
            SaveIndicatorSettings();
            contextMenuManager.ApplyCustomIcons(source, frames, GetSystemTheme(), manualTheme);
            foreach (var frame in frames) frame.Dispose();
        }

        private void HandleCustomRunnerDeleted(string deletedName)
        {
            foreach (var config in indicatorConfigs.Values)
            {
                if (!string.Equals(config.CustomRunnerName, deletedName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                config.CustomRunnerName = null;
                SaveIndicatorSettings();
                if (config.Enabled)
                {
                    contextMenuManager.ApplyBuiltInIcons(
                        config.SpeedSource,
                        GetSystemTheme(),
                        manualTheme,
                        config.Runner
                    );
                }
            }
        }

        private void ChangeManualTheme(Theme t)
        {
            manualTheme = t;
            UserSettings.Default.Theme = manualTheme.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeTemperatureUnit(TemperatureUnit u)
        {
            temperatureUnit = u;
            UserSettings.Default.TemperatureUnit = temperatureUnit.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeFPSMaxLimit(FPSMaxLimit f)
        {
            fpsMaxLimit = f;
            UserSettings.Default.FPSMaxLimit = fpsMaxLimit.ToString();
            UserSettings.Default.Save();
        }

        private string GetIndicatorDescription(
            SpeedSource speedSource,
            CPUInfo cpuInfo,
            GPUInfo? gpuInfo,
            MemoryInfo memoryInfo,
            TemperatureInfo? temperatureInfo
        )
        {
            if (speedSource == SpeedSource.Temperature)
            {
                return temperatureInfo?.GetDescription(temperatureUnit) ?? "";
            }

            var baseDescription = speedSource switch
            {
                SpeedSource.CPU => cpuInfo.GetDescription(),
                SpeedSource.GPU => gpuInfo?.GetDescription() ?? "",
                SpeedSource.Memory => memoryInfo.GetDescription(),
                _ => "",
            };

            var temperatureDescription = temperatureInfo?.GetDescription(temperatureUnit) ?? "";
            return string.IsNullOrEmpty(temperatureDescription)
                ? baseDescription
                : $"{baseDescription}\n{temperatureDescription}";
        }

        private static float GetLoad(
            SpeedSource speedSource,
            CPUInfo cpuInfo,
            GPUInfo? gpuInfo,
            MemoryInfo memoryInfo,
            TemperatureInfo? temperatureInfo
        )
        {
            return speedSource switch
            {
                SpeedSource.CPU => cpuInfo.Total,
                SpeedSource.GPU => gpuInfo?.Maximum ?? 0f,
                SpeedSource.Memory => memoryInfo.MemoryLoad,
                SpeedSource.Temperature => temperatureInfo.HasValue
                    ? TemperatureToLoad(temperatureInfo.Value.MaximumCelsius)
                    : 0f,
                _ => 0f,
            };
        }

        private static float TemperatureToLoad(float maximumCelsius)
        {
            var load = (maximumCelsius - TemperatureIdleCelsius)
                / (TemperatureFullCelsius - TemperatureIdleCelsius)
                * 100.0f;
            return Math.Clamp(load, 0.0f, 100.0f);
        }

#if DEBUG
        /// <summary>
        /// Tiny runnable check for TemperatureToLoad clamp/scale.
        /// Runs automatically on DEBUG startup (Debug.Assert).
        /// Manual: build Debug and launch, or call VerifyTemperatureToLoadMapping() from a Debug session.
        /// </summary>
        private static void VerifyTemperatureToLoadMapping()
        {
            static void Expect(float celsius, float expected)
            {
                var actual = TemperatureToLoad(celsius);
                Debug.Assert(
                    Math.Abs(actual - expected) < 0.01f,
                    $"TemperatureToLoad({celsius}) expected {expected}, got {actual}"
                );
            }

            Expect(40f, 0f);
            Expect(95f, 100f);
            Expect(20f, 0f);
            Expect(120f, 100f);
        }
#endif

        private int CalculateInterval(float load)
        {
            var speed = (float)Math.Max(1.0f, (load / 5.0f) * fpsMaxLimit.GetRate());
            return (int)(500.0f / speed);
        }

        private void FetchSystemInfo()
        {
            var cpuInfo = cpuRepository.Get();
            var gpuInfo = gpuRepository.Get();
            var memoryInfo = memoryRepository.Get();
            var temperatureInfo = temperatureRepository.Get();
            var storageInfo = storageRepository.Get();
            var networkInfo = networkRepository.Get();

            foreach (var config in indicatorConfigs.Values)
            {
                if (!config.Enabled || !IsSpeedSourceAvailable(config.SpeedSource)) continue;

                var description = GetIndicatorDescription(
                    config.SpeedSource,
                    cpuInfo,
                    gpuInfo,
                    memoryInfo,
                    temperatureInfo
                );
                contextMenuManager.SetIndicatorText(config.SpeedSource, description);

                var load = GetLoad(config.SpeedSource, cpuInfo, gpuInfo, memoryInfo, temperatureInfo);

                var forced = false;
                if (config.Enabled && !config.RunnerSpeedEnabled && !config.ColorTintEnabled)
                {
                    config.RunnerSpeedEnabled = true;
                    forced = true;
                }
                if (forced) SaveIndicatorSettings();

                int interval;
                if (config.RunnerSpeedEnabled)
                {
                    interval = CalculateInterval(load);
                }
                else if (config.ColorTintEnabled)
                {
                    // Idle / very-low-load animation when only tint is active.
                    interval = CalculateInterval(0f);
                }
                else
                {
                    interval = CalculateInterval(load);
                }
                contextMenuManager.SetIndicatorInterval(config.SpeedSource, interval);

                if (config.ColorTintEnabled)
                {
                    contextMenuManager.SetIndicatorLoadTint(
                        config.SpeedSource,
                        BitmapExtension.LoadToTintStep(load),
                        config.ColorTintStrength
                    );
                }
                else
                {
                    contextMenuManager.SetIndicatorLoadTint(config.SpeedSource, null);
                }
            }

            var systemInfoValues = new List<string>();
            systemInfoValues.AddRange(cpuInfo.GenerateIndicator());
            if (gpuInfo.HasValue)
            {
                systemInfoValues.AddRange(gpuInfo.Value.GenerateIndicator());
            }
            systemInfoValues.AddRange(memoryInfo.GenerateIndicator());
            if (temperatureInfo.HasValue)
            {
                systemInfoValues.AddRange(temperatureInfo.Value.GenerateIndicator(temperatureUnit));
            }
            systemInfoValues.AddRange(storageInfo.GenerateIndicator());
            if (networkInfo.HasValue)
            {
                systemInfoValues.AddRange(networkInfo.Value.GenerateIndicator());
            }
            contextMenuManager.SetSystemInfoMenuText(string.Join("\n", [.. systemInfoValues]));
        }

        private async void FetchTick(object? sender, EventArgs e)
        {
            if (isFetching) return;
            isFetching = true;
            try
            {
                var doHeavySlice = await Task.Run(() =>
                {
                    cpuRepository.Update();
                    gpuRepository.Update();
                    fetchCounter += 1;
                    if (fetchCounter < FETCH_COUNTER_SIZE) return false;
                    fetchCounter = 0;
                    temperatureRepository.Update();
                    memoryRepository.Update();
                    storageRepository.Update();
                    networkRepository.Update();
                    return true;
                });

                if (!doHeavySlice) return;

                FetchSystemInfo();
            }
            catch (Exception exception)
            {
                Console.WriteLine($"FetchTick failed: {exception.Message}");
            }
            finally
            {
                isFetching = false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;

                fetchTimer?.Stop();
                fetchTimer?.Dispose();

                cpuRepository?.Close();
                gpuRepository?.Close();
                temperatureRepository?.Close();

                contextMenuManager?.HideNotifyIcons();
                contextMenuManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
