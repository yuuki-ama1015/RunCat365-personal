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

using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using RunCat365.Properties;
using System.Drawing.Imaging;
using System.Text.Json;

namespace RunCat365
{
    internal class SettingsForm : Form
    {
        private const string VirtualHostName = "runcat.settings";
        private readonly Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs;
        private readonly Action<SpeedSource, bool> setIndicatorEnabled;
        private readonly Action<SpeedSource, Runner> setIndicatorRunner;
        private readonly Action<SpeedSource, bool> setColorTintEnabled;
        private readonly Action<SpeedSource, int> setColorTintStrength;
        private readonly Action<SpeedSource, bool> setRunnerSpeedEnabled;
        private readonly Action<SpeedSource, bool> setStillModeEnabled;
        private readonly Action<SpeedSource, string> setStillSet;
        private readonly Action<SpeedSource, bool> setStillCrossfadeEnabled;
        private readonly Action<SpeedSource, string> applyCustomRunner;
        private readonly Func<SpeedSource, bool> isSpeedSourceAvailable;
        private readonly CustomRunnerRepository customRunnerRepository;
        private readonly StillSetRepository stillSetRepository;
        private readonly Action<string?> openCustomRunnerEditor;
        private readonly Action<string> onCustomRunnerDeleted;
        private readonly Action<string?> openStillSetEditor;
        private readonly Action<string> onStillSetDeleted;
        private readonly WebView2 webView = new();
        private bool isInitialized;
        private bool isWebViewReady;

        internal SettingsForm(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Action<SpeedSource, bool> setIndicatorEnabled,
            Action<SpeedSource, Runner> setIndicatorRunner,
            Action<SpeedSource, bool> setColorTintEnabled,
            Action<SpeedSource, int> setColorTintStrength,
            Action<SpeedSource, bool> setRunnerSpeedEnabled,
            Action<SpeedSource, bool> setStillModeEnabled,
            Action<SpeedSource, string> setStillSet,
            Action<SpeedSource, bool> setStillCrossfadeEnabled,
            Action<SpeedSource, string> applyCustomRunner,
            Func<SpeedSource, bool> isSpeedSourceAvailable,
            CustomRunnerRepository customRunnerRepository,
            StillSetRepository stillSetRepository,
            Action<string?> openCustomRunnerEditor,
            Action<string> onCustomRunnerDeleted,
            Action<string?> openStillSetEditor,
            Action<string> onStillSetDeleted
        )
        {
            this.getConfigs = getConfigs;
            this.setIndicatorEnabled = setIndicatorEnabled;
            this.setIndicatorRunner = setIndicatorRunner;
            this.setColorTintEnabled = setColorTintEnabled;
            this.setColorTintStrength = setColorTintStrength;
            this.setRunnerSpeedEnabled = setRunnerSpeedEnabled;
            this.setStillModeEnabled = setStillModeEnabled;
            this.setStillSet = setStillSet;
            this.setStillCrossfadeEnabled = setStillCrossfadeEnabled;
            this.applyCustomRunner = applyCustomRunner;
            this.isSpeedSourceAvailable = isSpeedSourceAvailable;
            this.customRunnerRepository = customRunnerRepository;
            this.stillSetRepository = stillSetRepository;
            this.openCustomRunnerEditor = openCustomRunnerEditor;
            this.onCustomRunnerDeleted = onCustomRunnerDeleted;
            this.openStillSetEditor = openStillSetEditor;
            this.onStillSetDeleted = onStillSetDeleted;

            Text = Strings.Window_Settings;
            Icon = Resources.AppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(880, 560);
            ClientSize = new Size(1040, 680);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;

            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(0xF7, 0xF9, 0xF9);
            Controls.Add(webView);

            Shown += async (_, _) => await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            if (isInitialized) return;
            isInitialized = true;

            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RunCat365",
                    "WebView2"
                );
                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder
                );
                if (IsDisposed || !IsHandleCreated || webView.IsDisposed) return;

                await webView.EnsureCoreWebView2Async(environment);
                if (IsDisposed || !IsHandleCreated || webView.IsDisposed) return;

                var contentRoot = Path.Combine(AppContext.BaseDirectory, "settings-ui");
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    VirtualHostName,
                    contentRoot,
                    CoreWebView2HostResourceAccessKind.Allow
                );
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                webView.CoreWebView2.Navigate($"https://{VirtualHostName}/index.html");
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) when (IsDisposed || !IsHandleCreated || webView.IsDisposed)
            {
            }
            catch (Exception ex)
            {
                if (IsDisposed || !IsHandleCreated) return;

                MessageBox.Show(
                    $"Failed to initialize settings UI.\n{ex.Message}",
                    Strings.Message_Warning,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated || webView.IsDisposed) return;
            if (!e.IsSuccess) return;
            isWebViewReady = true;
            PostIndicatorsState();
        }

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated || webView.IsDisposed) return;

            try
            {
                using var document = JsonDocument.Parse(e.WebMessageAsJson);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var typeElement)) return;

                var type = typeElement.GetString();
                if (type == "getIndicators")
                {
                    PostIndicatorsState();
                    return;
                }

                if (type == "setIndicatorEnabled")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("enabled", out var enabledElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    setIndicatorEnabled(speedSource, enabledElement.GetBoolean());
                    PostIndicatorsState();
                    return;
                }

                if (type == "setRunner")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("runner", out var runnerElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;
                    if (!Enum.TryParse(runnerElement.GetString(), ignoreCase: true, out Runner runner)) return;

                    setIndicatorRunner(speedSource, runner);
                    PostIndicatorsState();
                    return;
                }

                if (type == "setColorTintEnabled")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("enabled", out var enabledElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    setColorTintEnabled(speedSource, enabledElement.GetBoolean());
                    PostIndicatorsState();
                    return;
                }

                if (type == "setColorTintStrength")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("strength", out var strengthElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    setColorTintStrength(speedSource, strengthElement.GetInt32());
                    PostIndicatorsState();
                    return;
                }

                if (type == "setRunnerSpeedEnabled")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("enabled", out var enabledElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    setRunnerSpeedEnabled(speedSource, enabledElement.GetBoolean());
                    PostIndicatorsState();
                    return;
                }

                if (type == "setStillModeEnabled")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("enabled", out var enabledElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    setStillModeEnabled(speedSource, enabledElement.GetBoolean());
                    PostIndicatorsState();
                    return;
                }

                if (type == "setStillSet")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("name", out var nameElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name)) return;

                    setStillSet(speedSource, name);
                    PostIndicatorsState();
                    return;
                }

                if (type == "setStillCrossfadeEnabled")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("enabled", out var enabledElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    setStillCrossfadeEnabled(speedSource, enabledElement.GetBoolean());
                    PostIndicatorsState();
                    return;
                }

                if (type == "setCustomRunner")
                {
                    if (!root.TryGetProperty("id", out var idElement)) return;
                    if (!root.TryGetProperty("name", out var nameElement)) return;
                    if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name)) return;

                    applyCustomRunner(speedSource, name);
                    PostIndicatorsState();
                    return;
                }

                if (type == "openCustomRunnerEditor")
                {
                    string? selectName = null;
                    if (root.TryGetProperty("name", out var nameElement))
                    {
                        selectName = nameElement.GetString();
                        if (string.IsNullOrWhiteSpace(selectName)) selectName = null;
                    }
                    openCustomRunnerEditor(selectName);
                    return;
                }

                if (type == "deleteCustomRunner")
                {
                    if (!root.TryGetProperty("name", out var nameElement)) return;
                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name)) return;

                    if (customRunnerRepository.Delete(name))
                    {
                        onCustomRunnerDeleted(name);
                    }
                    PostIndicatorsState();
                    return;
                }

                if (type == "openStillSetEditor")
                {
                    string? selectName = null;
                    if (root.TryGetProperty("name", out var nameElement))
                    {
                        selectName = nameElement.GetString();
                        if (string.IsNullOrWhiteSpace(selectName)) selectName = null;
                    }
                    openStillSetEditor(selectName);
                    return;
                }

                if (type == "deleteStillSet")
                {
                    if (!root.TryGetProperty("name", out var nameElement)) return;
                    var name = nameElement.GetString();
                    if (string.IsNullOrWhiteSpace(name)) return;

                    if (stillSetRepository.Delete(name))
                    {
                        onStillSetDeleted(name);
                    }
                    PostIndicatorsState();
                    return;
                }

                if (type == "getPreview")
                {
                    HandleGetPreview(root);
                }
            }
            catch (JsonException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) when (IsDisposed || webView.IsDisposed)
            {
            }
        }

        internal void NotifyIndicatorsChanged()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                BeginInvoke(NotifyIndicatorsChanged);
                return;
            }
            PostIndicatorsState();
        }

        private void PostIndicatorsState()
        {
            if (IsDisposed || !IsHandleCreated || webView.IsDisposed) return;
            if (!isWebViewReady || webView.CoreWebView2 is null) return;

            try
            {
                var configs = getConfigs();
                var customProfiles = customRunnerRepository.GetAll();
                var customNames = new HashSet<string>(
                    customProfiles.Select(p => p.Name),
                    StringComparer.OrdinalIgnoreCase
                );
                var stillProfiles = stillSetRepository.GetAll();
                var stillNames = new HashSet<string>(
                    stillProfiles.Select(p => p.Name),
                    StringComparer.OrdinalIgnoreCase
                );

                var items = Enum.GetValues<SpeedSource>()
                    .Select(speedSource =>
                    {
                        configs.TryGetValue(speedSource, out var config);
                        var enabled = config is not null && config.Enabled;
                        var available = isSpeedSourceAvailable(speedSource);
                        var runner = config?.Runner.GetString() ?? Runner.Cat.GetString();
                        string? customRunnerName = null;
                        if (config is not null
                            && !string.IsNullOrEmpty(config.CustomRunnerName)
                            && customNames.Contains(config.CustomRunnerName))
                        {
                            customRunnerName = config.CustomRunnerName;
                        }

                        string? stillSetName = null;
                        if (config is not null
                            && !string.IsNullOrEmpty(config.StillSetName)
                            && stillNames.Contains(config.StillSetName))
                        {
                            stillSetName = config.StillSetName;
                        }

                        return new
                        {
                            id = ToIndicatorId(speedSource),
                            enabled,
                            available,
                            temperatureSetupRequired = speedSource == SpeedSource.Temperature && !available && !TemperatureRepository.IsPawnIoReady,
                            runner,
                            customRunnerName,
                            colorTintEnabled = config?.ColorTintEnabled ?? false,
                            colorTintStrength = config?.ColorTintStrength ?? 100,
                            runnerSpeedEnabled = config?.RunnerSpeedEnabled ?? true,
                            stillModeEnabled = config?.StillModeEnabled ?? false,
                            stillSetName,
                            stillCrossfadeEnabled = config?.StillCrossfadeEnabled ?? false
                        };
                    })
                    .ToArray();

                var builtin = Enum.GetValues<Runner>()
                    .Select(runner => new
                    {
                        id = runner.GetString(),
                        label = runner.GetLocalizedString()
                    })
                    .ToArray();

                var custom = customProfiles
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new { name = p.Name, frameCount = p.FrameFileNames.Count })
                    .ToArray();

                var stillSets = stillProfiles
                    .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(p => new { name = p.Name, frameCount = p.FrameFileNames.Count })
                    .ToArray();

                var payload = JsonSerializer.Serialize(new
                {
                    type = "indicators",
                    items,
                    runners = new { builtin, custom },
                    stillSets
                });
                webView.CoreWebView2.PostWebMessageAsJson(payload);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) when (IsDisposed || webView.IsDisposed)
            {
            }
        }

        private void HandleGetPreview(JsonElement root)
        {
            if (!root.TryGetProperty("id", out var idElement)) return;
            if (!TryParseIndicatorId(idElement.GetString(), out var speedSource)) return;

            var load = 0f;
            if (root.TryGetProperty("load", out var loadElement)
                && loadElement.ValueKind == JsonValueKind.Number)
            {
                load = loadElement.GetSingle();
            }
            load = Math.Clamp(load, 0f, 100f);

            var tick = 0;
            if (root.TryGetProperty("tick", out var tickElement)
                && tickElement.ValueKind == JsonValueKind.Number)
            {
                tick = Math.Max(0, tickElement.GetInt32());
            }

            if (!isWebViewReady || webView.CoreWebView2 is null) return;
            var preview = BuildPreviewPayload(speedSource, load, tick);

            try
            {
                var payload = JsonSerializer.Serialize(preview);
                webView.CoreWebView2.PostWebMessageAsJson(payload);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException) when (IsDisposed || webView.IsDisposed)
            {
            }
        }

        private object BuildPreviewPayload(SpeedSource speedSource, float load, int tick)
        {
            var configs = getConfigs();
            configs.TryGetValue(speedSource, out var config);
            var stillMode = config?.StillModeEnabled ?? false;
            var tintEnabled = !stillMode && (config?.ColorTintEnabled ?? false);
            var tintStrength = config?.ColorTintStrength ?? 100;
            var runnerSpeedEnabled = config?.RunnerSpeedEnabled ?? true;
            var id = ToIndicatorId(speedSource);

            string? imageDataUrl = null;
            string label = "プレビュー";
            int? intervalMs = null;

            if (stillMode)
            {
                var stillName = config?.StillSetName;
                if (!string.IsNullOrWhiteSpace(stillName))
                {
                    var frames = stillSetRepository.LoadFrames(stillName);
                    try
                    {
                        if (frames.Count > 0)
                        {
                            var index = StillSetRepository.LoadToFrameIndex(load, frames.Count);
                            imageDataUrl = ToPngDataUrl(frames[index]);
                            label = $"{stillName} · #{index + 1}/{frames.Count}";
                        }
                        else
                        {
                            label = "静止画セットなし";
                        }
                    }
                    finally
                    {
                        foreach (var frame in frames) frame.Dispose();
                    }
                }
                else
                {
                    label = "静止画セットを選択";
                }
            }
            else
            {
                List<Bitmap>? frames = null;
                var ownsFrames = false;
                try
                {
                    var customName = config?.CustomRunnerName;
                    if (!string.IsNullOrEmpty(customName))
                    {
                        frames = customRunnerRepository.LoadFrames(customName);
                        ownsFrames = true;
                        label = customName;
                    }
                    else
                    {
                        var runner = config?.Runner ?? Runner.Cat;
                        frames = LoadBuiltInFrames(runner);
                        ownsFrames = false;
                        label = runner.GetLocalizedString();
                    }

                    if (frames is null || frames.Count == 0)
                    {
                        label = "素材なし";
                    }
                    else
                    {
                        var frameIndex = tick % frames.Count;
                        using var rendered = RenderPreviewFrame(
                            frames[frameIndex],
                            tintEnabled,
                            load,
                            tintStrength
                        );
                        imageDataUrl = ToPngDataUrl(rendered);
                        label = tintEnabled
                            ? $"{label} · tint {BitmapExtension.LoadToTintStep(load) + 1}/16"
                            : label;
                        intervalMs = PreviewIntervalMs(
                            runnerSpeedEnabled ? load : 0f
                        );
                    }
                }
                finally
                {
                    if (ownsFrames && frames is not null)
                    {
                        foreach (var frame in frames) frame.Dispose();
                    }
                }
            }

            return new
            {
                type = "preview",
                id,
                load,
                imageDataUrl,
                label,
                intervalMs,
                stillMode,
                tintEnabled
            };
        }

        private static List<Bitmap> LoadBuiltInFrames(Runner runner)
        {
            var rm = Resources.ResourceManager;
            var capacity = runner.GetFrameNumber();
            var bitmaps = new List<Bitmap>(capacity);
            var runnerName = runner.GetString();
            for (int i = 0; i < capacity; i++)
            {
                var iconName = $"{runnerName}_{i}".ToLowerInvariant();
                if (rm.GetObject(iconName) is Bitmap bitmap)
                {
                    bitmaps.Add(bitmap);
                }
            }
            return bitmaps;
        }

        private static Bitmap RenderPreviewFrame(
            Bitmap source,
            bool tintEnabled,
            float load,
            int tintStrength
        )
        {
            if (!tintEnabled) return new Bitmap(source);
            var step = BitmapExtension.LoadToTintStep(load);
            return source.ApplyLoadTint(step, tintStrength);
        }

        private static int PreviewIntervalMs(float load)
        {
            var speed = Math.Max(1.0f, (load / 5.0f));
            return Math.Max(40, (int)(500.0f / speed));
        }

        private static string? ToPngDataUrl(Bitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        }

        private static string ToIndicatorId(SpeedSource speedSource)
        {
            return speedSource switch
            {
                SpeedSource.CPU => "cpu",
                SpeedSource.GPU => "gpu",
                SpeedSource.Memory => "memory",
                SpeedSource.Temperature => "temperature",
                _ => speedSource.ToString().ToLowerInvariant(),
            };
        }

        private static bool TryParseIndicatorId(string? id, out SpeedSource speedSource)
        {
            SpeedSource? parsed = id switch
            {
                "cpu" => SpeedSource.CPU,
                "gpu" => SpeedSource.GPU,
                "memory" => SpeedSource.Memory,
                "temperature" => SpeedSource.Temperature,
                _ => null,
            };

            if (parsed is SpeedSource value)
            {
                speedSource = value;
                return true;
            }

            speedSource = SpeedSource.CPU;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isWebViewReady = false;
                webView.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
