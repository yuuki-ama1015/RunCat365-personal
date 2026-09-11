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

namespace RunCat365
{
    struct StorageInfo
    {
        internal string DriveLetter { get; set; }
        internal long TotalSize { get; set; }
        internal long AvailableSpaceSize { get; set; }
        internal long UsedSpaceSize { get; set; }

        internal string GetLocalizedLabel()
        {
            return string.Format(
                Strings.SystemInfo_DriveFormat,
                DriveLetter
            );
        }
    }

    internal static class StorageInfoExtension
    {
        internal static List<string> GenerateIndicator(this List<StorageInfo> storageInfoList)
        {
            var resultLines = new List<string>
            {
                TreeFormatter.CreateRoot($"{Strings.SystemInfo_Storage}:")
            };

            if (storageInfoList.Count == 0) return resultLines;

            for (int i = 0; i < storageInfoList.Count; i++)
            {
                var info = storageInfoList[i];
                var isLastItem = (i == storageInfoList.Count - 1);
                var percentage = ((double)info.UsedSpaceSize / info.TotalSize) * 100.0;
                resultLines.Add(TreeFormatter.CreateNode($"{info.GetLocalizedLabel()}: {percentage:f1}%", isLastItem));
                resultLines.Add(TreeFormatter.CreateNestedNode($"{Strings.SystemInfo_Used}: {info.UsedSpaceSize.ToByteFormatted()}", isLastItem, false));
                resultLines.Add(TreeFormatter.CreateNestedNode($"{Strings.SystemInfo_Available}: {info.AvailableSpaceSize.ToByteFormatted()}", isLastItem, true));
            }

            return resultLines;
        }
    }

    internal class StorageRepository
    {
        private readonly List<StorageInfo> storageInfoList = [];

        internal StorageRepository() { }

        internal void Update()
        {
            storageInfoList.Clear();
            var allDrives = DriveInfo.GetDrives();
            foreach (var driveInfo in allDrives)
            {
                if (!driveInfo.IsReady || driveInfo.DriveType != DriveType.Fixed)
                {
                    continue;
                }

                try
                {
                    var driveLetter = driveInfo.Name.Length > 0
                        ? driveInfo.Name[0].ToString()
                        : driveInfo.Name;
                    var storageInfo = new StorageInfo
                    {
                        DriveLetter = driveLetter,
                        TotalSize = driveInfo.TotalSize,
                        AvailableSpaceSize = driveInfo.AvailableFreeSpace,
                        UsedSpaceSize = driveInfo.TotalSize - driveInfo.AvailableFreeSpace
                    };
                    storageInfoList.Add(storageInfo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        internal List<StorageInfo> Get()
        {
            return storageInfoList;
        }
    }
}
