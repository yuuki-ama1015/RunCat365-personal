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
    internal readonly struct TemperatureRange
    {
        internal const int MinimumCelsius = 0;
        internal const int MaximumCelsius = 120;
        internal const int MinimumSpanCelsius = 5;
        internal const int DefaultIdleCelsius = 40;
        internal const int DefaultFullCelsius = 95;

        internal int IdleCelsius { get; }
        internal int FullCelsius { get; }

        private TemperatureRange(int idleCelsius, int fullCelsius)
        {
            IdleCelsius = idleCelsius;
            FullCelsius = fullCelsius;
        }

        internal static TemperatureRange Default => new(DefaultIdleCelsius, DefaultFullCelsius);

        internal static TemperatureRange Create(int idleCelsius, int fullCelsius)
        {
            var idle = Math.Clamp(idleCelsius, MinimumCelsius, MaximumCelsius - MinimumSpanCelsius);
            var full = Math.Clamp(fullCelsius, idle + MinimumSpanCelsius, MaximumCelsius);
            return new TemperatureRange(idle, full);
        }

        internal float ToLoad(float celsius)
        {
            var span = Math.Max(MinimumSpanCelsius, FullCelsius - IdleCelsius);
            var load = (celsius - IdleCelsius) / span * 100.0f;
            return Math.Clamp(load, 0.0f, 100.0f);
        }
    }
}
