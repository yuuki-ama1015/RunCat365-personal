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

        /// <summary>Incremented on every Save/Delete so callers can invalidate caches.</summary>
        internal int Version { get; private set; }
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

            var setDirectory = GetAssetDirectory(name);
            if (setDirectory is null) return [];
            var frames = new List<Bitmap>();
            foreach (var fileName in profile.FrameFileNames)
            {
                var filePath = ResolveFramePath(setDirectory, fileName);
                if (filePath is null || !File.Exists(filePath)) continue;
                var frame = TryLoadBitmap(filePath);
                if (frame is not null) frames.Add(frame);
            }
            return frames;
        }

        internal Bitmap? LoadFirstFrame(string name)
        {
            var profile = GetByName(name);
            if (profile is null || profile.FrameFileNames.Count == 0) return null;
            var setDirectory = GetAssetDirectory(name);
            if (setDirectory is null) return null;
            var filePath = ResolveFramePath(setDirectory, profile.FrameFileNames[0]);
            return filePath is not null && File.Exists(filePath) ? TryLoadBitmap(filePath) : null;
        }

        private static Bitmap? TryLoadBitmap(string filePath)
        {
            try
            {
                // Load via a memory copy so the PNG file is not locked while the bitmap is alive
                // (preview/tray caches would otherwise block overwrite/delete of the asset).
                using var stream = new MemoryStream(File.ReadAllBytes(filePath));
                using var decoded = new Bitmap(stream);
                return new Bitmap(decoded);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to load still frame '{filePath}': {ex.Message}");
                return null;
            }
        }

        internal bool Save(string name, List<Bitmap> frames)
        {
            if (!AssetNameValidator.IsValid(name)) return false;
            if (frames.Count < MIN_FRAME_COUNT || frames.Count > MAX_FRAME_COUNT) return false;

            var setDirectory = GetAssetDirectory(name);
            if (setDirectory is null) return false;

            // Write into a temp directory first and swap it in, so a failure midway
            // (disk full, GDI+ error) never leaves the existing asset half-deleted.
            var tempDirectory = setDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
            var backupDirectory = setDirectory + ".old-" + Guid.NewGuid().ToString("N");
            var profile = new StillSetProfile { Name = name };
            try
            {
                Directory.CreateDirectory(tempDirectory);
                for (int i = 0; i < frames.Count; i++)
                {
                    using var resized = ResizeFrameForTray(frames[i]);
                    var fileName = $"frame_{i}.png";
                    resized.Save(Path.Combine(tempDirectory, fileName), ImageFormat.Png);
                    profile.FrameFileNames.Add(fileName);
                }

                var hadExisting = Directory.Exists(setDirectory);
                if (hadExisting) Directory.Move(setDirectory, backupDirectory);
                try
                {
                    Directory.Move(tempDirectory, setDirectory);
                }
                catch
                {
                    if (hadExisting && !Directory.Exists(setDirectory)) Directory.Move(backupDirectory, setDirectory);
                    throw;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException or ArgumentException)
            {
                StartupLog.Append($"Failed to save still set '{name}': {ex.GetType().Name}: {ex.Message}");
                TryDeleteDirectory(tempDirectory);
                return false;
            }
            TryDeleteDirectory(backupDirectory);

            var existingProfile = GetByName(name);
            if (existingProfile is not null) profiles.Remove(existingProfile);
            profiles.Add(profile);
            Version++;
            return TrySaveProfiles();
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to delete '{path}': {ex.Message}");
            }
        }

        internal bool Delete(string name)
        {
            var profile = GetByName(name);
            if (profile is null) return false;

            DeleteFrameFiles(profile);
            var setDirectory = GetAssetDirectory(name);
            try
            {
                if (setDirectory is not null && Directory.Exists(setDirectory))
                {
                    Directory.Delete(setDirectory, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to delete still set directory '{setDirectory}': {ex.Message}");
            }

            profiles.Remove(profile);
            Version++;
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
                // Write to a temp file and replace, so a crash mid-write never corrupts profiles.json.
                var tempPath = profilesPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, profilesPath, overwrite: true);
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
            var setDirectory = GetAssetDirectory(profile.Name);
            if (setDirectory is null) return;
            foreach (var fileName in profile.FrameFileNames)
            {
                var filePath = ResolveFramePath(setDirectory, fileName);
                if (filePath is null) continue;
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

        /// <summary>
        /// Storage directory for an asset, or null when the name would resolve outside basePath
        /// (e.g. "." / ".."). Never delete or write when this returns null.
        /// </summary>
        private string? GetAssetDirectory(string name)
        {
            return AssetNameValidator.ResolveChildDirectory(basePath, SanitizeDirectoryName(name));
        }

        private static string? ResolveFramePath(string directory, string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName)) return null;
            if (fileName.All(c => c == '.')) return null;
            return Path.Combine(directory, fileName);
        }

        private static string SanitizeDirectoryName(string name)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return sanitized.ToLowerInvariant();
        }
    }
}
