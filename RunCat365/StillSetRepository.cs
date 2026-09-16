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
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.Json;

namespace RunCat365
{
    internal class StillSetRepository
    {
        internal const int MIN_FRAME_COUNT = 2;
        internal const int MAX_FRAME_COUNT = 16;
        private const int TRAY_SIZE = 32;
        private const string PROFILES_FILE_NAME = "profiles.json";

        private readonly string basePath;
        private List<StillSetProfile> profiles = [];

        internal StillSetRepository()
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RunCat365",
                "StillSets"
            );
            Directory.CreateDirectory(basePath);
            Load();
        }

        internal List<StillSetProfile> GetAll()
        {
            return [.. profiles];
        }

        internal StillSetProfile? GetByName(string name)
        {
            return profiles.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        internal List<Bitmap> LoadFrames(string name)
        {
            var profile = GetByName(name);
            if (profile is null) return [];

            var setDirectory = Path.Combine(basePath, SanitizeDirectoryName(name));
            var frames = new List<Bitmap>();
            foreach (var fileName in profile.FrameFileNames)
            {
                var filePath = Path.Combine(setDirectory, fileName);
                if (!File.Exists(filePath)) continue;
                var frame = TryLoadBitmap(filePath);
                if (frame is not null) frames.Add(frame);
            }
            return frames;
        }

        internal Bitmap? LoadFirstFrame(string name)
        {
            var profile = GetByName(name);
            if (profile is null || profile.FrameFileNames.Count == 0) return null;
            var setDirectory = Path.Combine(basePath, SanitizeDirectoryName(name));
            var filePath = Path.Combine(setDirectory, profile.FrameFileNames[0]);
            return File.Exists(filePath) ? TryLoadBitmap(filePath) : null;
        }

        private static Bitmap? TryLoadBitmap(string filePath)
        {
            try
            {
                return new Bitmap(filePath);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or FileNotFoundException)
            {
                Debug.WriteLine($"Failed to load still frame '{filePath}': {ex.Message}");
                return null;
            }
        }

        internal bool Save(string name, List<Bitmap> frames)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (frames.Count < MIN_FRAME_COUNT || frames.Count > MAX_FRAME_COUNT) return false;

            var setDirectory = Path.Combine(basePath, SanitizeDirectoryName(name));
            Directory.CreateDirectory(setDirectory);

            var existingProfile = GetByName(name);
            if (existingProfile is not null)
            {
                DeleteFrameFiles(existingProfile);
                profiles.Remove(existingProfile);
            }

            var profile = new StillSetProfile { Name = name };
            for (int i = 0; i < frames.Count; i++)
            {
                using var resized = ResizeFrameForTray(frames[i]);
                var fileName = $"frame_{i}.png";
                var filePath = Path.Combine(setDirectory, fileName);
                resized.Save(filePath, ImageFormat.Png);
                profile.FrameFileNames.Add(fileName);
            }

            profiles.Add(profile);
            return TrySaveProfiles();
        }

        internal bool Delete(string name)
        {
            var profile = GetByName(name);
            if (profile is null) return false;

            DeleteFrameFiles(profile);
            var setDirectory = Path.Combine(basePath, SanitizeDirectoryName(name));
            try
            {
                if (Directory.Exists(setDirectory))
                {
                    Directory.Delete(setDirectory, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to delete still set directory '{setDirectory}': {ex.Message}");
            }

            profiles.Remove(profile);
            TrySaveProfiles();
            return true;
        }

        internal bool Exists(string name)
        {
            return GetByName(name) is not null;
        }

        /// <summary>
        /// Map load 0–100 across N frames (hard cut). Same band style as tint steps.
        /// </summary>
        internal static int LoadToFrameIndex(float load, int frameCount)
        {
            if (frameCount <= 0) return 0;
            var clamped = Math.Clamp(load, 0f, 100f);
            return Math.Min(frameCount - 1, (int)(clamped / 100f * frameCount));
        }

        private void Load()
        {
            var profilesPath = Path.Combine(basePath, PROFILES_FILE_NAME);
            if (!File.Exists(profilesPath))
            {
                profiles = [];
                return;
            }
            try
            {
                var json = File.ReadAllText(profilesPath);
                profiles = JsonSerializer.Deserialize<List<StillSetProfile>>(json) ?? [];
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to load still set profiles: {ex.Message}");
                profiles = [];
            }
        }

        private bool TrySaveProfiles()
        {
            var profilesPath = Path.Combine(basePath, PROFILES_FILE_NAME);
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(profiles, options);
                File.WriteAllText(profilesPath, json);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to write still set profiles: {ex.Message}");
                return false;
            }
        }

        private void DeleteFrameFiles(StillSetProfile profile)
        {
            var setDirectory = Path.Combine(basePath, SanitizeDirectoryName(profile.Name));
            foreach (var fileName in profile.FrameFileNames)
            {
                var filePath = Path.Combine(setDirectory, fileName);
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"Failed to delete still frame file '{filePath}': {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Aspect-preserving fit into 32×32 with transparent padding (tray-sized).
        /// </summary>
        internal static Bitmap ResizeFrameForTray(Bitmap original)
        {
            var scale = Math.Min(
                (float)TRAY_SIZE / Math.Max(1, original.Width),
                (float)TRAY_SIZE / Math.Max(1, original.Height)
            );
            var newWidth = Math.Max(1, (int)Math.Round(original.Width * scale));
            var newHeight = Math.Max(1, (int)Math.Round(original.Height * scale));
            newWidth = Math.Min(newWidth, TRAY_SIZE);
            newHeight = Math.Min(newHeight, TRAY_SIZE);

            var canvas = new Bitmap(TRAY_SIZE, TRAY_SIZE, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(canvas);
            graphics.Clear(Color.Transparent);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var x = (TRAY_SIZE - newWidth) / 2;
            var y = (TRAY_SIZE - newHeight) / 2;
            graphics.DrawImage(original, x, y, newWidth, newHeight);
            return canvas;
        }

        private static string SanitizeDirectoryName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return sanitized.ToLowerInvariant();
        }
    }
}
