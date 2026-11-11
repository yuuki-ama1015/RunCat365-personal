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

using Microsoft.Win32;
using Windows.ApplicationModel;

namespace RunCat365
{
    internal interface ILaunchAtStartupManager
    {
        bool GetEnabled();
        bool Toggle(bool currentlyEnabled);
    }

    internal sealed class PackagedLaunchAtStartupManager : ILaunchAtStartupManager
    {
        private static StartupTask? startupTask;

        public bool GetEnabled()
        {
            startupTask ??= Task.Run(async () => await StartupTask.GetAsync("RunCatStartup")).Result;
            if (startupTask is null) return false;
            if (startupTask.State == StartupTaskState.Enabled) return true;
            return false;
        }

        public bool Toggle(bool currentlyEnabled)
        {
            startupTask ??= Task.Run(async () => await StartupTask.GetAsync("RunCatStartup")).Result;
            if (currentlyEnabled)
            {
                if (startupTask.State == StartupTaskState.Enabled) startupTask.Disable();
                return true;
            }
            else
            {
                switch (startupTask.State)
                {
                    case StartupTaskState.Enabled:
                        return true;
                    case StartupTaskState.Disabled:
                        var newStartupState = Task.Run(async () => await startupTask.RequestEnableAsync()).Result;
                        if (newStartupState == StartupTaskState.Enabled)
                        {
                            return true;
                        }
                        else
                        {
                            throw new InvalidOperationException("Launch at Startup could not be activated.");
                        }
                    case StartupTaskState.DisabledByUser:
                        throw new InvalidOperationException("Launch at startup was disabled by the user, enable it in Task Manager > Startup, search RunCat 365 and enable it.");
                    case StartupTaskState.DisabledByPolicy:
                        throw new InvalidOperationException("Launch at startup was disabled by policy.");
                    default:
                        return false;
                }
            }
        }
    }

    internal sealed class UnpackagedLaunchAtStartupManager : ILaunchAtStartupManager
    {
        public bool GetEnabled()
        {
            var keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var rKey = Registry.CurrentUser.OpenSubKey(keyName);
            if (rKey is null) return false;
            var value = (rKey.GetValue(Application.ProductName) is not null);
            rKey.Close();
            return value;
        }

        public bool Toggle(bool currentlyEnabled)
        {
            var productName = Application.ProductName;
            if (productName is null) return false;
            var keyName = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using var rKey = Registry.CurrentUser.OpenSubKey(keyName, true);
            if (rKey is null) return false;
            if (currentlyEnabled)
            {
                rKey.DeleteValue(productName, false);
            }
            else
            {
                var fileName = Environment.ProcessPath;
                if (fileName is not null)
                {
                    rKey.SetValue(productName, $"\"{fileName}\"");
                }
            }
            rKey.Close();
            return true;
        }
    }

    internal class LaunchAtStartupManager
    {
        private readonly ILaunchAtStartupManager launchAtStartupManager;

        public LaunchAtStartupManager()
        {
            launchAtStartupManager = IsRunningAsPackaged()
                ? new PackagedLaunchAtStartupManager()
                : new UnpackagedLaunchAtStartupManager();
        }

        public bool GetStartup() => launchAtStartupManager.GetEnabled();

        public bool ToggleStartup(bool currentlyEnabled) => launchAtStartupManager.Toggle(currentlyEnabled);

        private static bool IsRunningAsPackaged()
        {
            try
            {
                _ = Package.Current;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
