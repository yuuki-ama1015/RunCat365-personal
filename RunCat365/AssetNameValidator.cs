// Copyright 2026 Takuto Nakamura
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

namespace RunCat365
{
    /// <summary>
    /// Validates user-entered asset (custom runner / still set) names and resolves
    /// their storage directory safely inside a base folder.
    /// </summary>
    internal static class AssetNameValidator
    {
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        /// <summary>
        /// Returns null when the name is acceptable, otherwise a user-facing reason.
        /// </summary>
        internal static string? Validate(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Strings.AssetName_Empty;
            if (name != name.Trim()) return Strings.AssetName_LeadingTrailingSpace;
            if (name.EndsWith('.')) return Strings.AssetName_TrailingDot;
            if (name.All(c => c == '.')) return Strings.AssetName_DotsOnly;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return Strings.AssetName_InvalidChars;
            var stem = name.Split('.')[0].TrimEnd();
            if (ReservedNames.Contains(stem)) return Strings.AssetName_Reserved;
            return null;
        }

        internal static bool IsValid(string? name) => Validate(name) is null;

        /// <summary>
        /// Combines basePath and the sanitized directory name and returns the full path only
        /// when it is a direct child of basePath. Returns null for anything that escapes.
        /// </summary>
        internal static string? ResolveChildDirectory(string basePath, string sanitizedName)
        {
            if (string.IsNullOrWhiteSpace(sanitizedName)) return null;
            if (sanitizedName.All(c => c == '.')) return null;
            try
            {
                var baseFull = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.GetFullPath(Path.Combine(baseFull, sanitizedName))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parent = Path.GetDirectoryName(target);
                if (parent is null) return null;
                if (!string.Equals(parent, baseFull, StringComparison.OrdinalIgnoreCase)) return null;
                return target;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }
    }
}
