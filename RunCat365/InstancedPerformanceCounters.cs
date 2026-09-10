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

namespace RunCat365
{
    internal abstract class InstancedPerformanceCounters
    {
        private readonly Dictionary<string, PerformanceCounter> countersByInstance = [];

        protected abstract string CategoryName { get; }
        protected abstract string CounterName { get; }

        protected virtual bool ShouldIncludeInstance(string instanceName) => true;

        internal int Count => countersByInstance.Count;

        protected bool TryInitialize()
        {
            try
            {
                _ = new PerformanceCounterCategory(CategoryName);
            }
            catch
            {
                return false;
            }

            RefreshInstances();
            return Count != 0;
        }

        internal void RefreshInstances()
        {
            string[] currentInstanceNames;
            try
            {
                var category = new PerformanceCounterCategory(CategoryName);
                currentInstanceNames = category.GetInstanceNames()
                    .Where(ShouldIncludeInstance)
                    .ToArray();
            }
            catch (Exception exception) when (IsExpectedCounterException(exception))
            {
                Debug.WriteLine($"{GetType().Name}.RefreshInstances failed: {exception.Message}");
                return;
            }

            var currentSet = new HashSet<string>(currentInstanceNames, StringComparer.Ordinal);
            var staleInstances = countersByInstance.Keys
                .Where(name => !currentSet.Contains(name))
                .ToList();
            foreach (var instanceName in staleInstances)
            {
                countersByInstance[instanceName].Close();
                countersByInstance.Remove(instanceName);
            }

            foreach (var instanceName in currentInstanceNames)
            {
                if (countersByInstance.ContainsKey(instanceName)) continue;
                PerformanceCounter? counter = null;
                try
                {
                    counter = new PerformanceCounter(CategoryName, CounterName, instanceName);
                    _ = counter.NextValue();
                    countersByInstance[instanceName] = counter;
                    counter = null;
                }
                catch (Exception exception) when (IsExpectedCounterException(exception))
                {
                    Debug.WriteLine($"{GetType().Name}: failed to create counter for {instanceName}: {exception.Message}");
                }
                finally
                {
                    counter?.Close();
                }
            }
        }

        internal List<float> ReadValues()
        {
            var values = new List<float>(countersByInstance.Count);
            var deadInstances = new List<string>();
            foreach (var pair in countersByInstance)
            {
                try
                {
                    values.Add(pair.Value.NextValue());
                }
                catch (Exception exception) when (IsExpectedCounterException(exception))
                {
                    Debug.WriteLine($"{GetType().Name}: counter {pair.Key} failed: {exception.Message}");
                    deadInstances.Add(pair.Key);
                }
            }
            foreach (var instanceName in deadInstances)
            {
                countersByInstance[instanceName].Close();
                countersByInstance.Remove(instanceName);
            }
            return values;
        }

        internal List<long> ReadRawValues()
        {
            var values = new List<long>(countersByInstance.Count);
            var deadInstances = new List<string>();
            foreach (var pair in countersByInstance)
            {
                try
                {
                    values.Add(pair.Value.NextSample().RawValue);
                }
                catch (Exception exception) when (IsExpectedCounterException(exception))
                {
                    Debug.WriteLine($"{GetType().Name}: counter {pair.Key} failed: {exception.Message}");
                    deadInstances.Add(pair.Key);
                }
            }
            foreach (var instanceName in deadInstances)
            {
                countersByInstance[instanceName].Close();
                countersByInstance.Remove(instanceName);
            }
            return values;
        }

        internal void Close()
        {
            foreach (var counter in countersByInstance.Values) counter.Close();
            countersByInstance.Clear();
        }

        private static bool IsExpectedCounterException(Exception exception)
        {
            return exception is InvalidOperationException
                or System.ComponentModel.Win32Exception
                or UnauthorizedAccessException;
        }
    }
}
