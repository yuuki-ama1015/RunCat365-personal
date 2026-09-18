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

namespace RunCat365
{
    /// <summary>
    /// Appends diagnostic lines to %LocalAppData%\RunCat365\startup.log
    /// (same path as fatal startup exception logging in Program).
    /// </summary>
    internal static class StartupLog
    {
        private static readonly object Gate = new();

        internal static void Append(string message)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RunCat365");
                var logPath = Path.Combine(logDir, "startup.log");
                Directory.CreateDirectory(logDir);
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
                lock (Gate)
                {
                    File.AppendAllText(logPath, line);
                }
            }
            catch
            {
                // Logging must never throw.
            }
        }
    }
}
