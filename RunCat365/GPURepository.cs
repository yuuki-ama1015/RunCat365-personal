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
using Microsoft.Win32;
using RunCat365.Properties;

namespace RunCat365
{
    struct GPUInfo
    {
        internal float Average { get; set; }
        internal float Maximum { get; set; }
        internal float? MemoryLoad { get; set; }
        internal long? UsedMemory { get; set; }
        internal long? TotalMemory { get; set; }
    }

    internal static class GPUInfoExtension
    {
        internal static string GetDescription(this GPUInfo gpuInfo)
        {
            return $"{Strings.SystemInfo_GPU}: {gpuInfo.Maximum:f1}%";
        }

        internal static List<string> GenerateIndicator(this GPUInfo gpuInfo)
        {
            var hasMemory = gpuInfo.MemoryLoad.HasValue
                && gpuInfo.UsedMemory.HasValue
                && gpuInfo.TotalMemory.HasValue;

            var resultLines = new List<string>
            {
                TreeFormatter.CreateRoot($"{Strings.SystemInfo_GPU}:"),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Average}: {gpuInfo.Average:f1}%", false),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Maximum}: {gpuInfo.Maximum:f1}%", !hasMemory)
            };

            if (hasMemory)
            {
                resultLines.Add(TreeFormatter.CreateNode($"{Strings.SystemInfo_Memory}: {gpuInfo.MemoryLoad.Value:f1}%", false));
                resultLines.Add(TreeFormatter.CreateNode($"{Strings.SystemInfo_Used}: {gpuInfo.UsedMemory.Value.ToByteFormatted()}", false));
                resultLines.Add(TreeFormatter.CreateNode($"{Strings.SystemInfo_Total}: {gpuInfo.TotalMemory.Value.ToByteFormatted()}", true));
            }

            return resultLines;
        }
    }

    internal sealed class GPUPerformanceCounters : InstancedPerformanceCounters
    {
        private const string ENGINE_TYPE_FILTER = "engtype_3D";

        protected override string CategoryName => "GPU Engine";
        protected override string CounterName => "Utilization Percentage";

        protected override bool ShouldIncludeInstance(string instanceName)
        {
            return instanceName.Contains(ENGINE_TYPE_FILTER, StringComparison.Ordinal);
        }

        internal static GPUPerformanceCounters? TryCreate()
        {
            var instance = new GPUPerformanceCounters();
            return instance.TryInitialize() ? instance : null;
        }
    }

    internal sealed class GPUAdapterMemoryPerformanceCounters : InstancedPerformanceCounters
    {
        protected override string CategoryName => "GPU Adapter Memory";
        protected override string CounterName => "Dedicated Usage";

        internal static GPUAdapterMemoryPerformanceCounters? TryCreate()
        {
            var instance = new GPUAdapterMemoryPerformanceCounters();
            return instance.TryInitialize() ? instance : null;
        }
    }

    internal class GPURepository
    {
        private const int GPU_INFO_LIST_LIMIT_SIZE = 5;
        private const int REFRESH_INTERVAL_TICKS = 30;
        private const string DISPLAY_ADAPTER_CLASS_KEY =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        private const string MEMORY_SIZE_VALUE_NAME = "HardwareInformation.qwMemorySize";

        private readonly GPUPerformanceCounters? counters;
        private readonly GPUAdapterMemoryPerformanceCounters? memoryCounters;
        private readonly long? totalDedicatedMemory;
        private readonly List<GPUInfo> gpuInfoList = [];
        private int ticksSinceLastRefresh;

        internal bool IsAvailable => counters is not null;

        internal GPURepository()
        {
            counters = GPUPerformanceCounters.TryCreate();
            memoryCounters = GPUAdapterMemoryPerformanceCounters.TryCreate();
            totalDedicatedMemory = TryGetDedicatedVideoMemoryTotal();
        }

        internal void Update()
        {
            if (counters is null) return;

            ticksSinceLastRefresh += 1;
            if (REFRESH_INTERVAL_TICKS <= ticksSinceLastRefresh)
            {
                ticksSinceLastRefresh = 0;
                counters.RefreshInstances();
                memoryCounters?.RefreshInstances();
            }

            var values = counters.ReadValues();
            if (values.Count == 0) return;

            var gpuInfo = new GPUInfo
            {
                Average = Math.Min(100, values.Average()),
                Maximum = Math.Min(100, values.Max())
            };

            ApplyMemoryInfo(ref gpuInfo);

            gpuInfoList.Add(gpuInfo);
            if (GPU_INFO_LIST_LIMIT_SIZE < gpuInfoList.Count)
            {
                gpuInfoList.RemoveAt(0);
            }
        }

        internal GPUInfo? Get()
        {
            if (counters is null || gpuInfoList.Count == 0) return null;

            var latest = gpuInfoList[^1];
            return new GPUInfo
            {
                Average = gpuInfoList.Average(x => x.Average),
                Maximum = gpuInfoList.Max(x => x.Maximum),
                MemoryLoad = latest.MemoryLoad,
                UsedMemory = latest.UsedMemory,
                TotalMemory = latest.TotalMemory
            };
        }

        internal void Close()
        {
            counters?.Close();
            memoryCounters?.Close();
        }

        private void ApplyMemoryInfo(ref GPUInfo gpuInfo)
        {
            if (memoryCounters is null || totalDedicatedMemory is null or <= 0)
            {
                return;
            }

            try
            {
                var rawValues = memoryCounters.ReadRawValues();
                if (rawValues.Count == 0) return;

                // Prefer the adapter instance with the largest Dedicated Usage so dual-GPU
                // systems report the discrete adapter rather than summing iGPU + dGPU.
                var usedMemory = rawValues.Max();
                if (usedMemory < 0) return;

                var memoryLoad = (float)((double)usedMemory / totalDedicatedMemory.Value * 100.0);
                gpuInfo.UsedMemory = usedMemory;
                gpuInfo.TotalMemory = totalDedicatedMemory.Value;
                gpuInfo.MemoryLoad = Math.Clamp(memoryLoad, 0f, 100f);
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"GPURepository.ApplyMemoryInfo failed: {exception.Message}");
            }
        }

        // Total dedicated VRAM from driver registry (HardwareInformation.qwMemorySize).
        // Preferred over Win32_VideoController.AdapterRAM, which is UInt32 and often 4 GB-capped.
        private static long? TryGetDedicatedVideoMemoryTotal()
        {
            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(DISPLAY_ADAPTER_CLASS_KEY);
                if (classKey is null) return null;

                long maxMemory = 0;
                foreach (var subKeyName in classKey.GetSubKeyNames())
                {
                    if (subKeyName.Length != 4 || !subKeyName.All(char.IsDigit))
                    {
                        continue;
                    }

                    using var adapterKey = classKey.OpenSubKey(subKeyName);
                    if (adapterKey is null) continue;
                    if (!TryReadDedicatedMemoryValue(adapterKey, out var memorySize)) continue;

                    if (memorySize > maxMemory)
                    {
                        maxMemory = memorySize;
                    }
                }

                return maxMemory > 0 ? maxMemory : null;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"TryGetDedicatedVideoMemoryTotal failed: {exception.Message}");
                return null;
            }
        }

        private static bool TryReadDedicatedMemoryValue(RegistryKey adapterKey, out long memorySize)
        {
            memorySize = 0;
            var rawValue = adapterKey.GetValue(MEMORY_SIZE_VALUE_NAME);
            switch (rawValue)
            {
                case long longValue when longValue > 0:
                    memorySize = longValue;
                    return true;
                case ulong ulongValue when ulongValue > 0:
                    memorySize = unchecked((long)ulongValue);
                    return true;
                case int intValue when intValue > 0:
                    memorySize = intValue;
                    return true;
                default:
                    return false;
            }
        }
    }
}
