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
    internal class IndicatorConfig
    {
        internal SpeedSource SpeedSource { get; }
        internal bool Enabled { get; set; }
        internal Runner Runner { get; set; }
        internal string? CustomRunnerName { get; set; }
        internal bool ColorTintEnabled { get; set; }
        internal int ColorTintStrength { get; set; }
        internal bool RunnerSpeedEnabled { get; set; }

        internal IndicatorConfig(
            SpeedSource speedSource,
            bool enabled,
            Runner runner,
            string? customRunnerName,
            bool colorTintEnabled = false,
            bool runnerSpeedEnabled = true,
            int colorTintStrength = 100
        )
        {
            SpeedSource = speedSource;
            Enabled = enabled;
            Runner = runner;
            CustomRunnerName = customRunnerName;
            ColorTintEnabled = colorTintEnabled;
            RunnerSpeedEnabled = runnerSpeedEnabled;
            ColorTintStrength = Math.Clamp(colorTintStrength, 0, 100);
        }

        internal static Runner DefaultRunnerFor(SpeedSource speedSource)
        {
            return speedSource switch
            {
                SpeedSource.CPU => Runner.Cat,
                SpeedSource.GPU => Runner.Parrot,
                SpeedSource.Memory => Runner.Horse,
                SpeedSource.Temperature => Runner.Cat,
                _ => Runner.Cat,
            };
        }
    }
}
