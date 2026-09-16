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
using FormsTimer = System.Windows.Forms.Timer;

namespace RunCat365
{
    internal class TrayIndicator : IDisposable
    {
        private const int ANIMATE_TIMER_DEFAULT_INTERVAL = 200;
        private readonly NotifyIcon notifyIcon = new();
        private readonly List<Icon> icons = [];
        private readonly Lock iconLock = new();
        private readonly FormsTimer animateTimer;
        private int current;
        private List<Bitmap>? sourceFrames;
        private bool ownsSourceFrames;
        private Theme currentTheme = Theme.Light;
        private int? tintStep;
        private bool disposed;

        internal SpeedSource SpeedSource { get; }

        internal TrayIndicator(SpeedSource speedSource, ContextMenuStrip contextMenuStrip)
        {
            SpeedSource = speedSource;
            notifyIcon.Visible = false;
            // Multiple NotifyIcons cannot reliably share ContextMenuStrip via the
            // property assignment, so show the shared menu manually on right-click.
            notifyIcon.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                contextMenuStrip.Show(Cursor.Position);
            };

            animateTimer = new FormsTimer
            {
                Interval = ANIMATE_TIMER_DEFAULT_INTERVAL
            };
            animateTimer.Tick += (_, _) => AdvanceFrame();
        }

        internal bool Visible
        {
            get => notifyIcon.Visible;
            set
            {
                notifyIcon.Visible = value;
                if (value)
                {
                    if (!animateTimer.Enabled) animateTimer.Start();
                }
                else
                {
                    animateTimer.Stop();
                }
            }
        }

        internal bool HasActiveCustomIcons => ownsSourceFrames && sourceFrames is not null;

        internal void SetText(string text)
        {
            // NotifyIcon.Text is limited to 63 characters.
            notifyIcon.Text = text.Length <= 63 ? text : text[..63];
        }

        internal void SetInterval(int interval)
        {
            if (interval < 1) interval = 1;
            animateTimer.Stop();
            animateTimer.Interval = interval;
            if (notifyIcon.Visible) animateTimer.Start();
        }

        internal void ShowBalloonTip(int timeout, string tipTitle, string tipText, ToolTipIcon tipIcon)
        {
            notifyIcon.ShowBalloonTip(timeout, tipTitle, tipText, tipIcon);
        }

        /// <summary>
        /// Apply or clear load tint. Rebuilds icons only when the step changes.
        /// Pass null to disable tint.
        /// </summary>
        internal void SetLoadTintStep(int? step)
        {
            int? normalized = step is null ? null : Math.Clamp(step.Value, 0, 15);
            if (Nullable.Equals(tintStep, normalized)) return;
            tintStep = normalized;
            RebuildIconsFromSource();
        }

        internal void SetIcons(Theme systemTheme, Theme manualTheme, Runner runner)
        {
            ClearSourceFrames();

            var runnerName = runner.GetString();
            var rm = Resources.ResourceManager;
            var capacity = runner.GetFrameNumber();
            var bitmaps = new List<Bitmap>(capacity);
            for (int i = 0; i < capacity; i++)
            {
                var iconName = $"{runnerName}_{i}".ToLower();
                if (rm.GetObject(iconName) is Bitmap bitmap)
                {
                    bitmaps.Add(bitmap);
                }
            }
            sourceFrames = bitmaps;
            ownsSourceFrames = false;
            currentTheme = ResolveTheme(systemTheme, manualTheme);
            RebuildIconsFromSource();
        }

        internal void SetCustomIcons(List<Bitmap> frames, Theme systemTheme, Theme manualTheme)
        {
            ClearSourceFrames();
            sourceFrames = frames.Select(f => new Bitmap(f)).ToList();
            ownsSourceFrames = true;
            currentTheme = ResolveTheme(systemTheme, manualTheme);
            RebuildIconsFromSource();
        }

        internal void RecolorActiveCustomIcons(Theme systemTheme, Theme manualTheme)
        {
            if (!HasActiveCustomIcons) return;
            currentTheme = ResolveTheme(systemTheme, manualTheme);
            RebuildIconsFromSource();
        }

        private void AdvanceFrame()
        {
            lock (iconLock)
            {
                if (icons.Count == 0) return;
                if (icons.Count <= current) current = 0;
                notifyIcon.Icon = icons[current];
                current = (current + 1) % icons.Count;
            }
        }

        private void RebuildIconsFromSource()
        {
            if (sourceFrames is null || sourceFrames.Count == 0)
            {
                ReplaceIconList([]);
                return;
            }

            var color = currentTheme.GetContrastColor();
            var list = new List<Icon>(sourceFrames.Count);
            foreach (var frame in sourceFrames)
            {
                Bitmap themed;
                bool disposeThemed;
                if (currentTheme == Theme.Light)
                {
                    themed = frame;
                    disposeThemed = false;
                }
                else
                {
                    themed = frame.Recolor(color);
                    disposeThemed = true;
                }

                try
                {
                    if (tintStep is int step)
                    {
                        using var tinted = themed.ApplyLoadTint(step);
                        list.Add(tinted.ToIcon());
                    }
                    else
                    {
                        list.Add(themed.ToIcon());
                    }
                }
                finally
                {
                    if (disposeThemed) themed.Dispose();
                }
            }

            ReplaceIconList(list);
        }

        private void ReplaceIconList(List<Icon> list)
        {
            List<Icon> oldIcons;
            lock (iconLock)
            {
                oldIcons = new List<Icon>(icons);
                icons.Clear();
                icons.AddRange(list);
                current = 0;
                if (icons.Count > 0)
                {
                    notifyIcon.Icon = icons[0];
                    current = 1 % icons.Count;
                }
            }

            foreach (var icon in oldIcons) icon.Dispose();
        }

        private void ClearSourceFrames()
        {
            if (sourceFrames is null) return;
            if (ownsSourceFrames)
            {
                foreach (var bitmap in sourceFrames) bitmap.Dispose();
            }
            sourceFrames = null;
            ownsSourceFrames = false;
        }

        private static Theme ResolveTheme(Theme systemTheme, Theme manualTheme)
        {
            return manualTheme == Theme.System ? systemTheme : manualTheme;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                animateTimer.Stop();
                animateTimer.Dispose();

                lock (iconLock)
                {
                    foreach (var icon in icons) icon.Dispose();
                    icons.Clear();
                }

                ClearSourceFrames();
                notifyIcon.Visible = false;
                notifyIcon.Dispose();
            }
            disposed = true;
        }
    }
}
