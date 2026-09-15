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

using RunCat365.Properties;
using System.ComponentModel;

namespace RunCat365
{
    internal class ContextMenuManager : IDisposable
    {
        private readonly CustomToolStripMenuItem systemInfoMenu = new();
        private readonly Dictionary<SpeedSource, TrayIndicator> indicators = [];
        private readonly ContextMenuStrip contextMenuStrip;
        private EndlessGameForm? endlessGameForm;
        private CustomRunnerForm? customRunnerForm;
        private SettingsForm? settingsForm;
        private readonly Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs;
        private readonly Action<SpeedSource, bool> setIndicatorEnabled;
        private readonly Action<SpeedSource, Runner> setIndicatorRunner;
        private readonly Action<SpeedSource, string> applyCustomRunner;
        private readonly CustomRunnerRepository customRunnerRepository;
        private readonly Action<string> onCustomRunnerDeleted;
        private readonly Func<SpeedSource, bool> isSpeedSourceAvailable;

        internal ContextMenuManager(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Action<SpeedSource, bool> setIndicatorEnabled,
            Action<SpeedSource, Runner> setIndicatorRunner,
            CustomRunnerRepository customRunnerRepository,
            Action<SpeedSource, string> applyCustomRunner,
            Action<string> onCustomRunnerDeleted,
            Func<Theme> getSystemTheme,
            Func<Theme> getManualTheme,
            Action<Theme> setManualTheme,
            Func<SpeedSource, bool> isSpeedSourceAvailable,
            Func<FPSMaxLimit> getFPSMaxLimit,
            Action<FPSMaxLimit> setFPSMaxLimit,
            Func<TemperatureUnit> getTemperatureUnit,
            Action<TemperatureUnit> setTemperatureUnit,
            Func<bool> getLaunchAtStartup,
            Func<bool, bool> toggleLaunchAtStartup,
            Action openProjectPage,
            Action onExit
        )
        {
            this.getConfigs = getConfigs;
            this.setIndicatorEnabled = setIndicatorEnabled;
            this.setIndicatorRunner = setIndicatorRunner;
            this.applyCustomRunner = applyCustomRunner;
            this.customRunnerRepository = customRunnerRepository;
            this.onCustomRunnerDeleted = deletedName =>
            {
                onCustomRunnerDeleted(deletedName);
                settingsForm?.NotifyIndicatorsChanged();
            };
            this.isSpeedSourceAvailable = isSpeedSourceAvailable;

            systemInfoMenu.Text = "-\n-\n-\n-\n-";
            systemInfoMenu.Enabled = false;

            var indicatorsMenu = BuildIndicatorsMenu(
                getConfigs,
                setIndicatorEnabled,
                setIndicatorRunner,
                customRunnerRepository,
                applyCustomRunner,
                getSystemTheme,
                getManualTheme,
                isSpeedSourceAvailable
            );

            var themeMenu = new CustomToolStripMenuItem(Strings.Menu_Theme);
            themeMenu.SetupSubMenusFromEnum<Theme>(
                t => t.GetLocalizedString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<Theme>(
                        parent,
                        sender,
                        (string? s, out Theme t) => Enum.TryParse(s, out t),
                        t => setManualTheme(t)
                    );
                    RefreshAllIndicatorIcons(getConfigs, getSystemTheme, getManualTheme, customRunnerRepository);
                },
                t => getManualTheme() == t,
                _ => null
            );

            var fpsMaxLimitMenu = new CustomToolStripMenuItem(Strings.Menu_FPSMaxLimit);
            fpsMaxLimitMenu.SetupSubMenusFromEnum<FPSMaxLimit>(
                f => f.GetString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<FPSMaxLimit>(
                        parent,
                        sender,
                        (string? s, out FPSMaxLimit f) => FPSMaxLimitExtension.TryParse(s, out f),
                        f => setFPSMaxLimit(f)
                    );
                },
                f => getFPSMaxLimit() == f,
                _ => null
            );

            var temperatureUnitMenu = new CustomToolStripMenuItem(Strings.Menu_TemperatureUnit);
            temperatureUnitMenu.SetupSubMenusFromEnum<TemperatureUnit>(
                u => u.GetLocalizedString(),
                (parent, sender, e) =>
                {
                    HandleMenuItemSelection<TemperatureUnit>(
                        parent,
                        sender,
                        (string? s, out TemperatureUnit u) => Enum.TryParse(s, out u),
                        u => setTemperatureUnit(u)
                    );
                },
                u => getTemperatureUnit() == u,
                _ => null
            );

            var launchAtStartupMenu = new CustomToolStripMenuItem(Strings.Menu_LaunchAtStartup)
            {
                Checked = getLaunchAtStartup()
            };
            launchAtStartupMenu.Click += (sender, e) => HandleStartupMenuClick(sender, toggleLaunchAtStartup);

            var quickSettingsMenu = new CustomToolStripMenuItem(Strings.Menu_QuickSettings);
            quickSettingsMenu.DropDownItems.AddRange(
                themeMenu,
                indicatorsMenu,
                fpsMaxLimitMenu,
                temperatureUnitMenu,
                launchAtStartupMenu
            );

            var settingsMenu = new CustomToolStripMenuItem(Strings.Menu_Settings);
            settingsMenu.Click += (sender, e) => ShowOrActivateSettingsWindow();

            var customRunnersMenu = new CustomToolStripMenuItem(Strings.Menu_CustomRunners);
            customRunnersMenu.Click += (sender, e) => ShowOrActivateCustomRunnerWindow();

            var endlessGameMenu = new CustomToolStripMenuItem(Strings.Menu_EndlessGame);
            endlessGameMenu.Click += (sender, e) => ShowOrActivateGameWindow(getSystemTheme);

            var appVersionMenu = new CustomToolStripMenuItem(
                $"{Application.ProductName} v{Application.ProductVersion}"
            )
            {
                Enabled = false
            };

            var projectPageMenu = new CustomToolStripMenuItem(Strings.Menu_OpenProjectPage);
            projectPageMenu.Click += (sender, e) => openProjectPage();

            var informationMenu = new CustomToolStripMenuItem(Strings.Menu_Information);
            informationMenu.DropDownItems.AddRange(
                appVersionMenu,
                projectPageMenu
            );

            var exitMenu = new CustomToolStripMenuItem(Strings.Menu_Exit);
            exitMenu.Click += (sender, e) => onExit();

            contextMenuStrip = new ContextMenuStrip(new Container());
            contextMenuStrip.Items.AddRange(
                systemInfoMenu,
                new ToolStripSeparator(),
                customRunnersMenu,
                new ToolStripSeparator(),
                settingsMenu,
                quickSettingsMenu,
                informationMenu,
                endlessGameMenu,
                new ToolStripSeparator(),
                exitMenu
            );
            contextMenuStrip.Renderer = new ContextMenuRenderer();

            foreach (SpeedSource speedSource in Enum.GetValues<SpeedSource>())
            {
                // Always create a tray indicator (including GPU/Temperature) so settings
                // can fall back cleanly; unavailable sources stay hidden.
                var indicator = new TrayIndicator(speedSource, contextMenuStrip);
                indicators[speedSource] = indicator;
            }

            SyncFromConfigs(getConfigs, getSystemTheme, getManualTheme, customRunnerRepository, isSpeedSourceAvailable);
        }

        private CustomToolStripMenuItem BuildIndicatorsMenu(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Action<SpeedSource, bool> setIndicatorEnabled,
            Action<SpeedSource, Runner> setIndicatorRunner,
            CustomRunnerRepository customRunnerRepository,
            Action<SpeedSource, string> applyCustomRunner,
            Func<Theme> getSystemTheme,
            Func<Theme> getManualTheme,
            Func<SpeedSource, bool> isSpeedSourceAvailable
        )
        {
            var indicatorsMenu = new CustomToolStripMenuItem(Strings.Menu_Indicators);

            foreach (SpeedSource speedSource in Enum.GetValues<SpeedSource>())
            {
                if (!isSpeedSourceAvailable(speedSource)) continue;

                var metricMenu = new CustomToolStripMenuItem(speedSource.GetLocalizedString())
                {
                    Tag = speedSource
                };

                var enabledMenu = new CustomToolStripMenuItem(Strings.Menu_IndicatorEnabled)
                {
                    Tag = speedSource,
                    Checked = getConfigs().TryGetValue(speedSource, out var cfg) && cfg.Enabled
                };
                enabledMenu.Click += (sender, e) =>
                {
                    if (sender is not ToolStripMenuItem item) return;
                    if (item.Tag is not SpeedSource source) return;
                    var nextEnabled = !item.Checked;
                    setIndicatorEnabled(source, nextEnabled);
                    // Re-read after Program enforces "at least one" rule.
                    // ChangeIndicatorEnabled already syncs visibility/runner for this indicator;
                    // DropDownOpening refreshes checkbox states when the menu reopens.
                    var configs = getConfigs();
                    item.Checked = configs.TryGetValue(source, out var updated) && updated.Enabled;
                    settingsForm?.NotifyIndicatorsChanged();
                };

                var runnersMenu = new CustomToolStripMenuItem(Strings.Menu_Runner)
                {
                    Tag = speedSource
                };
                runnersMenu.SetupSubMenusFromEnum<Runner>(
                    r => r.GetLocalizedString(),
                    (parent, sender, e) =>
                    {
                        HandleMenuItemSelection<Runner>(
                            parent,
                            sender,
                            (string? s, out Runner r) => Enum.TryParse(s, out r),
                            r =>
                            {
                                setIndicatorRunner(speedSource, r);
                                ApplyRunnerToIndicator(speedSource, getConfigs, getSystemTheme, getManualTheme, customRunnerRepository);
                                settingsForm?.NotifyIndicatorsChanged();
                            }
                        );
                    },
                    r =>
                    {
                        if (!getConfigs().TryGetValue(speedSource, out var config)) return false;
                        return config.CustomRunnerName is null && config.Runner == r;
                    },
                    r => GetRunnerThumbnailBitmap(getSystemTheme(), r)
                );
                runnersMenu.DropDownOpening += (sender, e) => RefreshCustomRunnerMenu(
                    runnersMenu,
                    speedSource,
                    customRunnerRepository,
                    ResolveTheme(getSystemTheme(), getManualTheme()),
                    getConfigs,
                    applyCustomRunner
                );

                metricMenu.DropDownItems.AddRange(enabledMenu, runnersMenu);
                indicatorsMenu.DropDownItems.Add(metricMenu);
            }

            indicatorsMenu.DropDownOpening += (sender, e) => RefreshEnabledCheckStates(indicatorsMenu, getConfigs);
            return indicatorsMenu;
        }

        private static void RefreshEnabledCheckStates(
            CustomToolStripMenuItem indicatorsMenu,
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs
        )
        {
            var configs = getConfigs();
            foreach (ToolStripItem item in indicatorsMenu.DropDownItems)
            {
                if (item is not ToolStripMenuItem metricMenu) continue;
                if (metricMenu.Tag is not SpeedSource speedSource) continue;
                if (metricMenu.DropDownItems.Count > 0
                    && metricMenu.DropDownItems[0] is ToolStripMenuItem enabledItem)
                {
                    enabledItem.Checked = configs.TryGetValue(speedSource, out var config) && config.Enabled;
                }
            }
        }

        private void SyncFromConfigs(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Func<Theme> getSystemTheme,
            Func<Theme> getManualTheme,
            CustomRunnerRepository customRunnerRepository,
            Func<SpeedSource, bool> isSpeedSourceAvailable
        )
        {
            var configs = getConfigs();
            foreach (var (speedSource, indicator) in indicators)
            {
                if (!isSpeedSourceAvailable(speedSource)
                    || !configs.TryGetValue(speedSource, out var config)
                    || !config.Enabled)
                {
                    indicator.Visible = false;
                    continue;
                }

                ApplyRunnerToIndicator(speedSource, getConfigs, getSystemTheme, getManualTheme, customRunnerRepository);
                indicator.Visible = true;
            }
        }

        private void ApplyRunnerToIndicator(
            SpeedSource speedSource,
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Func<Theme> getSystemTheme,
            Func<Theme> getManualTheme,
            CustomRunnerRepository customRunnerRepository
        )
        {
            if (!indicators.TryGetValue(speedSource, out var indicator)) return;
            if (!getConfigs().TryGetValue(speedSource, out var config)) return;

            if (!string.IsNullOrEmpty(config.CustomRunnerName))
            {
                var frames = customRunnerRepository.LoadFrames(config.CustomRunnerName);
                if (frames.Count > 0)
                {
                    indicator.SetCustomIcons(frames, getSystemTheme(), getManualTheme());
                    foreach (var frame in frames) frame.Dispose();
                    return;
                }
            }

            indicator.SetIcons(getSystemTheme(), getManualTheme(), config.Runner);
        }

        private void RefreshAllIndicatorIcons(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Func<Theme> getSystemTheme,
            Func<Theme> getManualTheme,
            CustomRunnerRepository customRunnerRepository
        )
        {
            foreach (var speedSource in indicators.Keys)
            {
                if (!getConfigs().TryGetValue(speedSource, out var config) || !config.Enabled) continue;
                ApplyRunnerToIndicator(speedSource, getConfigs, getSystemTheme, getManualTheme, customRunnerRepository);
            }
        }

        internal void RefreshThemeIcons(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Theme systemTheme,
            Theme manualTheme,
            CustomRunnerRepository customRunnerRepository
        )
        {
            foreach (var (speedSource, indicator) in indicators)
            {
                if (!getConfigs().TryGetValue(speedSource, out var config) || !config.Enabled) continue;
                if (indicator.HasActiveCustomIcons)
                {
                    indicator.RecolorActiveCustomIcons(systemTheme, manualTheme);
                }
                else if (!string.IsNullOrEmpty(config.CustomRunnerName))
                {
                    ApplyRunnerToIndicator(
                        speedSource,
                        getConfigs,
                        () => systemTheme,
                        () => manualTheme,
                        customRunnerRepository
                    );
                }
                else
                {
                    indicator.SetIcons(systemTheme, manualTheme, config.Runner);
                }
            }
        }

        private static void HandleMenuItemSelection<T>(
            ToolStripMenuItem parentMenu,
            object? sender,
            CustomTryParseDelegate<T> tryParseMethod,
            Action<T> assignValueAction
        )
        {
            if (sender is null) return;
            var item = (ToolStripMenuItem)sender;
            foreach (ToolStripItem childItem in parentMenu.DropDownItems)
            {
                if (childItem is ToolStripMenuItem menuItem)
                {
                    menuItem.Checked = false;
                }
            }
            item.Checked = true;

            if (item.Tag is T tagValue)
            {
                assignValueAction(tagValue);
            }
            else if (tryParseMethod(item.Text, out T parsedValue))
            {
                assignValueAction(parsedValue);
            }
        }

        private static Bitmap? GetRunnerThumbnailBitmap(Theme systemTheme, Runner runner)
        {
            var color = systemTheme.GetContrastColor();
            var iconName = $"{runner.GetString()}_0".ToLower();
            var obj = Resources.ResourceManager.GetObject(iconName);
            if (obj is not Bitmap bitmap) return null;
            return systemTheme == Theme.Light ? bitmap : bitmap.Recolor(color);
        }

        private void RefreshCustomRunnerMenu(
            CustomToolStripMenuItem runnersMenu,
            SpeedSource speedSource,
            CustomRunnerRepository customRunnerRepository,
            Theme theme,
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Action<SpeedSource, string> applyCustomRunner
        )
        {
            IndicatorConfig? config = getConfigs().TryGetValue(speedSource, out var c) ? c : null;

            foreach (ToolStripItem item in runnersMenu.DropDownItems)
            {
                if (item is ToolStripMenuItem menuItem && item.Tag is Runner runner)
                {
                    menuItem.Checked = config is not null
                        && config.CustomRunnerName is null
                        && config.Runner == runner;
                }
            }

            var customItems = runnersMenu.DropDownItems
                .Cast<ToolStripItem>()
                .Where(item => item.Tag is CustomRunnerMenuTag)
                .ToList();
            foreach (var item in customItems)
            {
                runnersMenu.DropDownItems.Remove(item);
                if (item is ToolStripMenuItem menuItem)
                {
                    menuItem.Image?.Dispose();
                }
                item.Dispose();
            }

            var profiles = customRunnerRepository.GetAll().OrderBy(p => p.Name).ToList();
            if (profiles.Count == 0) return;

            runnersMenu.DropDownItems.Add(new ToolStripSeparator { Tag = new CustomRunnerMenuTag("") });
            foreach (var profile in profiles)
            {
                var name = profile.Name;
                var item = new CustomToolStripMenuItem(name)
                {
                    Tag = new CustomRunnerMenuTag(name),
                    Checked = config is not null
                        && string.Equals(config.CustomRunnerName, name, StringComparison.OrdinalIgnoreCase),
                    Image = CreateCustomRunnerThumbnail(customRunnerRepository, name, theme)
                };
                item.Click += (sender, e) =>
                {
                    applyCustomRunner(speedSource, name);
                    // Clear built-in checks; DropDownOpening will refresh next time.
                    foreach (ToolStripItem child in runnersMenu.DropDownItems)
                    {
                        if (child is ToolStripMenuItem mi)
                        {
                            if (child.Tag is Runner) mi.Checked = false;
                            if (child.Tag is CustomRunnerMenuTag tag)
                            {
                                mi.Checked = string.Equals(tag.Name, name, StringComparison.OrdinalIgnoreCase);
                            }
                        }
                    }
                    settingsForm?.NotifyIndicatorsChanged();
                };
                runnersMenu.DropDownItems.Add(item);
            }
        }

        private static Bitmap? CreateCustomRunnerThumbnail(CustomRunnerRepository repository, string name, Theme theme)
        {
            var firstFrame = repository.LoadFirstFrame(name);
            if (firstFrame is null) return null;
            if (theme == Theme.Light) return firstFrame;
            using (firstFrame)
            {
                return firstFrame.Recolor(theme.GetContrastColor());
            }
        }

        private static Theme ResolveTheme(Theme systemTheme, Theme manualTheme)
        {
            return manualTheme == Theme.System ? systemTheme : manualTheme;
        }

        private static void HandleStartupMenuClick(object? sender, Func<bool, bool> toggleLaunchAtStartup)
        {
            if (sender is null) return;
            var item = (ToolStripMenuItem)sender;
            try
            {
                if (toggleLaunchAtStartup(item.Checked))
                {
                    item.Checked = !item.Checked;
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, Strings.Message_Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowOrActivateGameWindow(Func<Theme> getSystemTheme)
        {
            if (endlessGameForm is null)
            {
                endlessGameForm = new EndlessGameForm(getSystemTheme());
                endlessGameForm.FormClosed += (sender, e) =>
                {
                    endlessGameForm = null;
                };
                endlessGameForm.Show();
            }
            else
            {
                endlessGameForm.Activate();
            }
        }

        private void ShowOrActivateCustomRunnerWindow(string? selectName = null)
        {
            if (customRunnerForm is null)
            {
                customRunnerForm = new CustomRunnerForm(customRunnerRepository, onCustomRunnerDeleted);
                customRunnerForm.FormClosed += (sender, e) =>
                {
                    customRunnerForm = null;
                    settingsForm?.NotifyIndicatorsChanged();
                };
                customRunnerForm.Show();
            }
            else
            {
                if (customRunnerForm.WindowState == FormWindowState.Minimized)
                {
                    customRunnerForm.WindowState = FormWindowState.Normal;
                }
                customRunnerForm.Activate();
            }
            customRunnerForm.SelectRunnerByName(selectName);
        }

        private void ShowOrActivateSettingsWindow()
        {
            if (settingsForm is null)
            {
                settingsForm = new SettingsForm(
                    getConfigs,
                    setIndicatorEnabled,
                    setIndicatorRunner,
                    applyCustomRunner,
                    isSpeedSourceAvailable,
                    customRunnerRepository,
                    openCustomRunnerEditor: selectName => ShowOrActivateCustomRunnerWindow(selectName),
                    onCustomRunnerDeleted: this.onCustomRunnerDeleted
                );
                settingsForm.FormClosed += (sender, e) =>
                {
                    settingsForm = null;
                };
                settingsForm.Show();
            }
            else
            {
                if (settingsForm.WindowState == FormWindowState.Minimized)
                {
                    settingsForm.WindowState = FormWindowState.Normal;
                }
                settingsForm.Activate();
            }
        }

        internal void ShowBalloonTip(BalloonTipType balloonTipType)
        {
            var info = balloonTipType.GetInfo();
            var target = indicators.Values.FirstOrDefault(i => i.Visible)
                ?? indicators.Values.FirstOrDefault();
            target?.ShowBalloonTip(5000, info.Title, info.Text, info.Icon);
        }

        internal void SetSystemInfoMenuText(string text)
        {
            systemInfoMenu.Text = text;
        }

        internal void SetIndicatorText(SpeedSource speedSource, string text)
        {
            if (indicators.TryGetValue(speedSource, out var indicator))
            {
                indicator.SetText(text);
            }
        }

        internal void SetIndicatorInterval(SpeedSource speedSource, int interval)
        {
            if (indicators.TryGetValue(speedSource, out var indicator))
            {
                indicator.SetInterval(interval);
            }
        }

        internal void ApplyCustomIcons(
            SpeedSource speedSource,
            List<Bitmap> frames,
            Theme systemTheme,
            Theme manualTheme
        )
        {
            if (!indicators.TryGetValue(speedSource, out var indicator)) return;
            indicator.SetCustomIcons(frames, systemTheme, manualTheme);
        }

        internal void ApplyBuiltInIcons(
            SpeedSource speedSource,
            Theme systemTheme,
            Theme manualTheme,
            Runner runner
        )
        {
            if (!indicators.TryGetValue(speedSource, out var indicator)) return;
            indicator.SetIcons(systemTheme, manualTheme, runner);
        }

        internal void SetIndicatorVisible(SpeedSource speedSource, bool visible)
        {
            if (indicators.TryGetValue(speedSource, out var indicator))
            {
                indicator.Visible = visible;
            }
        }

        internal void HideNotifyIcons()
        {
            foreach (var indicator in indicators.Values)
            {
                indicator.Visible = false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var indicator in indicators.Values)
                {
                    indicator.Dispose();
                }
                indicators.Clear();

                contextMenuStrip?.Dispose();
                endlessGameForm?.Dispose();
                customRunnerForm?.Dispose();
                settingsForm?.Dispose();
            }
        }

        private delegate bool CustomTryParseDelegate<T>(string? value, out T result);

        private sealed record CustomRunnerMenuTag(string Name);
    }
}
