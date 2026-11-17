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

namespace RunCat365
{
    internal static class StartupCommand
    {
        internal static string ForExecutable(string executablePath)
        {
            return $"\"{executablePath}\"";
        }

        internal static string? ResolveReplacement(
            string registeredCommand,
            string currentExecutablePath,
            Func<string, bool> fileExists
        )
        {
            var expectedCommand = ForExecutable(currentExecutablePath);
            if (registeredCommand == expectedCommand) return null;

            var registeredPath = ExtractExecutablePath(registeredCommand);
            if (registeredPath is null) return null;

            var pointsAtCurrentExecutable = string.Equals(
                registeredPath,
                currentExecutablePath,
                StringComparison.OrdinalIgnoreCase
            );
            if (!pointsAtCurrentExecutable && fileExists(registeredPath)) return null;
            return expectedCommand;
        }

        private static string? ExtractExecutablePath(string command)
        {
            var trimmedCommand = command.Trim();
            if (!trimmedCommand.StartsWith('"')) return trimmedCommand;

            var closingQuoteIndex = trimmedCommand.IndexOf('"', 1);
            var hasNothingAfterPath = closingQuoteIndex == trimmedCommand.Length - 1;
            return hasNothingAfterPath ? trimmedCommand[1..closingQuoteIndex] : null;
        }
    }
}
