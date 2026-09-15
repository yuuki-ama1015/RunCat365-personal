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
using System.Text.Json;

namespace RunCat365
{
    internal class SettingsForm : Form
    {
        private const string VirtualHostName = "runcat.settings";
        private readonly Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs;
        private readonly Action<SpeedSource, bool> setIndicatorEnabled;
        private readonly Func<SpeedSource, bool> isSpeedSourceAvailable;
        private readonly WebView2 webView = new();
        private bool isInitialized;
        private bool isWebViewReady;

        internal SettingsForm(
            Func<IReadOnlyDictionary<SpeedSource, IndicatorConfig>> getConfigs,
            Action<SpeedSource, bool> setIndicatorEnabled,
            Func<SpeedSource, bool> isSpeedSourceAvailable
        )
        {
            this.getConfigs = getConfigs;
            this.setIndicatorEnabled = setIndicatorEnabled;
            this.isSpeedSourceAvailable = isSpeedSourceAvailable;

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
                var items = Enum.GetValues<SpeedSource>()
                    .Select(speedSource =>
                    {
                        var enabled = configs.TryGetValue(speedSource, out var config) && config.Enabled;
                        var available = isSpeedSourceAvailable(speedSource);
                        return new
                        {
                            id = ToIndicatorId(speedSource),
                            enabled,
                            available
                        };
                    })
                    .ToArray();

                var payload = JsonSerializer.Serialize(new
                {
                    type = "indicators",
                    items
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
