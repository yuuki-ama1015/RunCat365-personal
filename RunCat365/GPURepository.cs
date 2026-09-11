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
using RunCat365.Properties;

namespace RunCat365
{
    struct GPUInfo
    {
        internal float Average { get; set; }
        internal float Maximum { get; set; }
        // Dedicated Usage (max across GPU Adapter Memory instances). Used-only; no total/%.
        internal long? UsedMemory { get; set; }
    }

    internal static class GPUInfoExtension
    {
        internal static string GetDescription(this GPUInfo gpuInfo)
        {
            return $"{Strings.SystemInfo_GPU}: {gpuInfo.Maximum:f1}%";
        }

        internal static List<string> GenerateIndicator(this GPUInfo gpuInfo)
        {
            var hasMemory = gpuInfo.UsedMemory.HasValue;

            var resultLines = new List<string>
            {
                TreeFormatter.CreateRoot($"{Strings.SystemInfo_GPU}:"),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Average}: {gpuInfo.Average:f1}%", false),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Maximum}: {gpuInfo.Maximum:f1}%", !hasMemory)
            };

            if (hasMemory)
            {
                // Reuse SystemInfo_Memory + SystemInfo_Used: "Memory: X Used"
                resultLines.Add(
                    TreeFormatter.CreateNode(
                        $"{Strings.SystemInfo_Memory}: {gpuInfo.UsedMemory.Value.ToByteFormatted()} {Strings.SystemInfo_Used}",
                        true
                    )
                );
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

        private readonly GPUPerformanceCounters? counters;
        private readonly GPUAdapterMemoryPerformanceCounters? memoryCounters;
        private readonly List<GPUInfo> gpuInfoList = [];
        private int ticksSinceLastRefresh;

        internal bool IsAvailable => counters is not null;

        internal GPURepository()
        {
            counters = GPUPerformanceCounters.TryCreate();
            memoryCounters = GPUAdapterMemoryPerformanceCounters.TryCreate();
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
                UsedMemory = latest.UsedMemory
            };
        }

        internal void Close()
        {
            counters?.Close();
            memoryCounters?.Close();
        }

        private void ApplyMemoryInfo(ref GPUInfo gpuInfo)
        {
            if (memoryCounters is null)
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

                gpuInfo.UsedMemory = usedMemory;
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"GPURepository.ApplyMemoryInfo failed: {exception.Message}");
            }
        }
    }
}
