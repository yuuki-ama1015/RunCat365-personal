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

namespace RunCat365
{
    internal class SettingsForm : Form
    {
        private const string VirtualHostName = "runcat.settings";
        private readonly WebView2 webView = new();
        private bool isInitialized;

        internal SettingsForm()
        {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                webView.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
