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

using System.Runtime.InteropServices;
using RunCat365.Properties;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunCat365
{
    internal class TrayIndicator : IDisposable
    {
        private const int ANIMATE_TIMER_DEFAULT_INTERVAL = 200;
        private const int CrossfadeDurationMs = 300;
        private const int CrossfadeIntermediateSteps = 4;
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly NotifyIcon notifyIcon = new();
        private readonly List<Icon> icons = [];
        private readonly Lock iconLock = new();
        private readonly FormsTimer animateTimer;
        private readonly FormsTimer crossfadeTimer;
        private int current;
        private List<Bitmap>? sourceFrames;
        private bool ownsSourceFrames;
        private Theme currentTheme = Theme.Light;
        private int? tintStep;
        private int tintStrength = 100;
        private bool stillMode;
        private bool stillCrossfadeEnabled;
        private int stillFrameIndex;
        private int crossfadeFromIndex;
        private int crossfadeToIndex;
        private int crossfadeStep;
        private Icon? crossfadeTempIcon;
        private bool disposed;

        internal SpeedSource SpeedSource { get; }

        internal TrayIndicator(SpeedSource speedSource, ContextMenuStrip contextMenuStrip)
        {
            SpeedSource = speedSource;
            notifyIcon.Visible = false;
            // Multiple NotifyIcons cannot reliably share ContextMenuStrip via the
            // property assignment, so show the shared menu manually on right-click.
            // SetForegroundWindow is required so outside clicks dismiss the menu.
            notifyIcon.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                contextMenuStrip.AutoClose = true;
                contextMenuStrip.Show(Cursor.Position);
                _ = SetForegroundWindow(contextMenuStrip.Handle);
            };

            animateTimer = new FormsTimer
            {
                Interval = ANIMATE_TIMER_DEFAULT_INTERVAL
            };
            animateTimer.Tick += (_, _) => AdvanceFrame();

            crossfadeTimer = new FormsTimer();
            crossfadeTimer.Tick += (_, _) => AdvanceCrossfade();
        }

        internal bool Visible
        {
            get => notifyIcon.Visible;
            set
            {
                notifyIcon.Visible = value;
                if (value)
                {
                    if (!stillMode && !animateTimer.Enabled) animateTimer.Start();
                }
                else
                {
                    animateTimer.Stop();
                    CancelCrossfade(commitTarget: false);
                }
            }
        }

        internal bool HasActiveCustomIcons => ownsSourceFrames && sourceFrames is not null;

        internal bool IsStillMode => stillMode;

        internal int StillFrameCount
        {
            get
            {
                lock (iconLock)
                {
                    return stillMode ? icons.Count : 0;
                }
            }
        }

        internal void SetText(string text)
        {
            // NotifyIcon.Text is limited to 63 characters.
            notifyIcon.Text = text.Length <= 63 ? text : text[..63];
        }

        internal void SetInterval(int interval)
        {
            if (stillMode) return;
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
        /// Apply or clear load tint. Rebuilds icons only when the step or strength changes.
        /// Pass null step to disable tint. Strength is 0–100 (default 100).
        /// </summary>
        internal void SetLoadTintStep(int? step, int strength = 100)
        {
            if (stillMode) return;
            int? normalized = step is null ? null : Math.Clamp(step.Value, 0, 15);
            var normalizedStrength = Math.Clamp(strength, 0, 100);
            if (Nullable.Equals(tintStep, normalized) && tintStrength == normalizedStrength) return;
            tintStep = normalized;
            tintStrength = normalizedStrength;
            RebuildIconsFromSource();
        }

        /// <summary>
        /// Update tint strength only. Rebuilds when tint is active.
        /// </summary>
        internal void SetLoadTintStrength(int strength)
        {
            if (stillMode) return;
            var normalizedStrength = Math.Clamp(strength, 0, 100);
            if (tintStrength == normalizedStrength) return;
            tintStrength = normalizedStrength;
            if (tintStep is not null) RebuildIconsFromSource();
        }

        internal void SetIcons(Theme systemTheme, Theme manualTheme, Runner runner)
        {
            ExitStillMode();
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
            RestartAnimationIfVisible();
        }

        internal void SetCustomIcons(List<Bitmap> frames, Theme systemTheme, Theme manualTheme)
        {
            ExitStillMode();
            ClearSourceFrames();
            sourceFrames = frames.Select(f => new Bitmap(f)).ToList();
            ownsSourceFrames = true;
            currentTheme = ResolveTheme(systemTheme, manualTheme);
            RebuildIconsFromSource();
            RestartAnimationIfVisible();
        }

        /// <summary>
        /// Load a still set as themed tray icons. Animation stops; frames switch by load.
        /// </summary>
        internal void SetStillIcons(List<Bitmap> frames, Theme systemTheme, Theme manualTheme)
        {
            CancelCrossfade(commitTarget: false);
            ClearSourceFrames();
            sourceFrames = frames.Select(f => new Bitmap(f)).ToList();
            ownsSourceFrames = true;
            currentTheme = ResolveTheme(systemTheme, manualTheme);
            stillMode = true;
            tintStep = null;
            animateTimer.Stop();
            RebuildIconsFromSource();
            stillFrameIndex = 0;
            ShowStillFrame(0);
        }

        internal void SetStillCrossfadeEnabled(bool enabled)
        {
            stillCrossfadeEnabled = enabled;
            if (!enabled)
            {
                CancelCrossfade(commitTarget: true);
            }
        }

        /// <summary>
        /// Switch to the still frame for the current load band.
        /// Hard-cut when crossfade is off; 300ms pseudo blend when on.
        /// </summary>
        internal void SetStillFrameIndex(int index)
        {
            if (!stillMode) return;

            int clamped;
            lock (iconLock)
            {
                if (icons.Count == 0) return;
                clamped = Math.Clamp(index, 0, icons.Count - 1);
            }

            if (!stillCrossfadeEnabled)
            {
                CancelCrossfade(commitTarget: false);
                ShowStillFrame(clamped);
                return;
            }

            if (crossfadeTimer.Enabled && crossfadeToIndex == clamped)
            {
                return;
            }

            if (!crossfadeTimer.Enabled && clamped == stillFrameIndex)
            {
                return;
            }

            StartCrossfade(stillFrameIndex, clamped);
        }

        internal void RecolorActiveCustomIcons(Theme systemTheme, Theme manualTheme)
        {
            if (!HasActiveCustomIcons) return;
            CancelCrossfade(commitTarget: false);
            currentTheme = ResolveTheme(systemTheme, manualTheme);
            RebuildIconsFromSource();
            if (stillMode) ShowStillFrame(stillFrameIndex);
        }

        private void ExitStillMode()
        {
            CancelCrossfade(commitTarget: false);
            stillMode = false;
            stillFrameIndex = 0;
        }

        private void RestartAnimationIfVisible()
        {
            if (notifyIcon.Visible && !stillMode && !animateTimer.Enabled)
            {
                animateTimer.Start();
            }
        }

        private void ShowStillFrame(int index)
        {
            lock (iconLock)
            {
                if (icons.Count == 0) return;
                stillFrameIndex = Math.Clamp(index, 0, icons.Count - 1);
                ClearCrossfadeTempIconLocked();
                notifyIcon.Icon = icons[stillFrameIndex];
            }
        }

        private void StartCrossfade(int fromIndex, int toIndex)
        {
            lock (iconLock)
            {
                if (icons.Count == 0 || sourceFrames is null || sourceFrames.Count == 0) return;
                fromIndex = Math.Clamp(fromIndex, 0, icons.Count - 1);
                toIndex = Math.Clamp(toIndex, 0, icons.Count - 1);
            }

            if (fromIndex == toIndex)
            {
                CancelCrossfade(commitTarget: false);
                ShowStillFrame(toIndex);
                return;
            }

            crossfadeTimer.Stop();
            crossfadeFromIndex = fromIndex;
            crossfadeToIndex = toIndex;
            crossfadeStep = 0;
            // Intermediate steps + final land across 300ms (4 blends + commit).
            crossfadeTimer.Interval = Math.Max(1, CrossfadeDurationMs / (CrossfadeIntermediateSteps + 1));
            crossfadeTimer.Start();
        }

        private void AdvanceCrossfade()
        {
            crossfadeStep++;
            if (crossfadeStep > CrossfadeIntermediateSteps)
            {
                crossfadeTimer.Stop();
                ShowStillFrame(crossfadeToIndex);
                return;
            }

            var amount = crossfadeStep / (float)(CrossfadeIntermediateSteps + 1);
            Bitmap? fromThemed = null;
            Bitmap? toThemed = null;
            Bitmap? blended = null;
            Icon? blendedIcon = null;
            try
            {
                fromThemed = CreateThemedFrame(crossfadeFromIndex);
                toThemed = CreateThemedFrame(crossfadeToIndex);
                if (fromThemed is null || toThemed is null) return;
                blended = fromThemed.Blend(toThemed, amount);
                blendedIcon = blended.ToIcon();

                lock (iconLock)
                {
                    ClearCrossfadeTempIconLocked();
                    crossfadeTempIcon = blendedIcon;
                    blendedIcon = null;
                    notifyIcon.Icon = crossfadeTempIcon;
                }
            }
            finally
            {
                blendedIcon?.Dispose();
                blended?.Dispose();
                fromThemed?.Dispose();
                toThemed?.Dispose();
            }
        }

        private Bitmap? CreateThemedFrame(int index)
        {
            if (sourceFrames is null || index < 0 || index >= sourceFrames.Count) return null;
            var frame = sourceFrames[index];
            if (currentTheme == Theme.Light)
            {
                return new Bitmap(frame);
            }
            return frame.Recolor(currentTheme.GetContrastColor());
        }

        private void CancelCrossfade(bool commitTarget)
        {
            var wasRunning = crossfadeTimer.Enabled;
            crossfadeTimer.Stop();
            if (commitTarget && wasRunning)
            {
                ShowStillFrame(crossfadeToIndex);
                return;
            }
            lock (iconLock)
            {
                ClearCrossfadeTempIconLocked();
                if (stillMode && icons.Count > 0)
                {
                    stillFrameIndex = Math.Clamp(stillFrameIndex, 0, icons.Count - 1);
                    notifyIcon.Icon = icons[stillFrameIndex];
                }
            }
        }

        private void ClearCrossfadeTempIconLocked()
        {
            if (crossfadeTempIcon is null) return;
            var temp = crossfadeTempIcon;
            crossfadeTempIcon = null;
            temp.Dispose();
        }

        private void AdvanceFrame()
        {
            if (stillMode) return;
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
                    if (!stillMode && tintStep is int step)
                    {
                        using var tinted = themed.ApplyLoadTint(step, tintStrength);
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
                ClearCrossfadeTempIconLocked();
                oldIcons = new List<Icon>(icons);
                icons.Clear();
                icons.AddRange(list);
                current = 0;
                if (icons.Count > 0)
                {
                    if (stillMode)
                    {
                        stillFrameIndex = Math.Clamp(stillFrameIndex, 0, icons.Count - 1);
                        notifyIcon.Icon = icons[stillFrameIndex];
                    }
                    else
                    {
                        notifyIcon.Icon = icons[0];
                        current = 1 % icons.Count;
                    }
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
                crossfadeTimer.Stop();
                crossfadeTimer.Dispose();

                lock (iconLock)
                {
                    ClearCrossfadeTempIconLocked();
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
