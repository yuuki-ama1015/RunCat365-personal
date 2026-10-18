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
    internal class CustomRunnerRepository
    {
        internal const int MIN_FRAME_COUNT = 2;
        internal const int MAX_FRAME_COUNT = 30;
        private const int MAX_FRAME_HEIGHT = 32;
        private const int MIN_FRAME_WIDTH = 10;
        private const int MAX_FRAME_WIDTH = 32;
        private const string PROFILES_FILE_NAME = "profiles.json";

        private readonly string basePath;
        private List<CustomRunnerProfile> profiles = [];

        internal CustomRunnerRepository()
        {
            basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RunCat365",
                "CustomRunners"
            );
            Directory.CreateDirectory(basePath);
            Load();
        }

        internal List<CustomRunnerProfile> GetAll()
        {
            return [.. profiles];
        }

        internal CustomRunnerProfile? GetByName(string name)
        {
            return profiles.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        internal List<Bitmap> LoadFrames(string name)
        {
            var profile = GetByName(name);
            if (profile is null) return [];

            var runnerDirectory = GetAssetDirectory(name);
            if (runnerDirectory is null) return [];
            var frames = new List<Bitmap>();
            foreach (var fileName in profile.FrameFileNames)
            {
                var filePath = ResolveFramePath(runnerDirectory, fileName);
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
            var runnerDirectory = GetAssetDirectory(name);
            if (runnerDirectory is null) return null;
            var filePath = ResolveFramePath(runnerDirectory, profile.FrameFileNames[0]);
            return filePath is not null && File.Exists(filePath) ? TryLoadBitmap(filePath) : null;
        }

        private static Bitmap? TryLoadBitmap(string filePath)
        {
            try
            {
                return new Bitmap(filePath);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or FileNotFoundException)
            {
                Debug.WriteLine($"Failed to load custom runner frame '{filePath}': {ex.Message}");
                return null;
            }
        }

        internal bool Save(string name, List<Bitmap> frames)
        {
            if (!AssetNameValidator.IsValid(name)) return false;
            if (frames.Count < MIN_FRAME_COUNT || frames.Count > MAX_FRAME_COUNT) return false;

            var runnerDirectory = GetAssetDirectory(name);
            if (runnerDirectory is null) return false;
            Directory.CreateDirectory(runnerDirectory);

            var existingProfile = GetByName(name);
            if (existingProfile is not null)
            {
                DeleteFrameFiles(existingProfile);
                profiles.Remove(existingProfile);
            }

            var profile = new CustomRunnerProfile { Name = name };
            for (int i = 0; i < frames.Count; i++)
            {
                using var resized = ResizeFrame(frames[i]);
                var fileName = $"frame_{i}.png";
                var filePath = Path.Combine(runnerDirectory, fileName);
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
            var runnerDirectory = GetAssetDirectory(name);
            try
            {
                if (runnerDirectory is not null && Directory.Exists(runnerDirectory))
                {
                    Directory.Delete(runnerDirectory, true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to delete runner directory '{runnerDirectory}': {ex.Message}");
            }

            profiles.Remove(profile);
            TrySaveProfiles();
            return true;
        }

        internal bool Exists(string name)
        {
            return GetByName(name) is not null;
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
                profiles = JsonSerializer.Deserialize<List<CustomRunnerProfile>>(json) ?? [];
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Failed to load custom runner profiles: {ex.Message}");
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
                Debug.WriteLine($"Failed to write custom runner profiles: {ex.Message}");
                return false;
            }
        }

        private void DeleteFrameFiles(CustomRunnerProfile profile)
        {
            var runnerDirectory = GetAssetDirectory(profile.Name);
            if (runnerDirectory is null) return;
            foreach (var fileName in profile.FrameFileNames)
            {
                var filePath = ResolveFramePath(runnerDirectory, fileName);
                if (filePath is null) continue;
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Debug.WriteLine($"Failed to delete frame file '{filePath}': {ex.Message}");
                }
            }
        }

        private static Bitmap ResizeFrame(Bitmap original)
        {
            if (original.Height <= MAX_FRAME_HEIGHT &&
                original.Width >= MIN_FRAME_WIDTH &&
                original.Width <= MAX_FRAME_WIDTH)
            {
                return new Bitmap(original);
            }

            var scale = (float)MAX_FRAME_HEIGHT / original.Height;
            var newWidth = Math.Clamp((int)(original.Width * scale), MIN_FRAME_WIDTH, MAX_FRAME_WIDTH);
            var newHeight = MAX_FRAME_HEIGHT;

            var resized = new Bitmap(newWidth, newHeight);
            using var graphics = Graphics.FromImage(resized);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original, 0, 0, newWidth, newHeight);
            return resized;
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
