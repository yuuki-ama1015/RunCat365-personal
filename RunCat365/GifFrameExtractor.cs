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

using System.Drawing.Imaging;

namespace RunCat365
{
    /// <summary>
    /// Loads animated GIF frames as independent bitmaps and evenly subsamples when over a max count.
    /// </summary>
    internal static class GifFrameExtractor
    {
        /// <summary>
        /// Extracts every frame from a GIF into cloned bitmaps. Caller owns the returned list.
        /// </summary>
        internal static List<Bitmap> ExtractFrames(string path)
        {
            using var image = Image.FromFile(path);
            var frameCount = image.GetFrameCount(FrameDimension.Time);
            var frames = new List<Bitmap>(frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                image.SelectActiveFrame(FrameDimension.Time, i);
                frames.Add(new Bitmap(image));
            }
            return frames;
        }

        /// <summary>
        /// When <paramref name="frames"/> exceeds <paramref name="maxCount"/>, keeps evenly spaced
        /// frames (first and last included) and disposes the rest. Returns the kept list
        /// (same instances); clears <paramref name="frames"/> so callers do not double-dispose.
        /// </summary>
        internal static List<Bitmap> EvenlySample(List<Bitmap> frames, int maxCount)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
            if (frames.Count <= maxCount)
            {
                return frames;
            }

            var keep = new bool[frames.Count];
            for (int i = 0; i < maxCount; i++)
            {
                // Include first and last; avoid divide-by-zero when maxCount == 1.
                var index = maxCount == 1
                    ? 0
                    : (int)((long)i * (frames.Count - 1) / (maxCount - 1));
                keep[index] = true;
            }

            var sampled = new List<Bitmap>(maxCount);
            for (int i = 0; i < frames.Count; i++)
            {
                if (keep[i])
                {
                    sampled.Add(frames[i]);
                }
                else
                {
                    frames[i].Dispose();
                }
            }
            frames.Clear();
            return sampled;
        }

        internal static void DisposeAll(IEnumerable<Bitmap> frames)
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
        }
    }
}
