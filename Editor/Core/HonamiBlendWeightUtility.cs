using System;
using System.Collections.Generic;
using HonamiAnimationSystem.Runtime.Core;

namespace HonamiAnimationSystem.Editor
{
    /// <summary>
    /// Allocation-free 1D blend weight solver shared by the Timeline preview and the Blend Tree
    /// editor: linearly interpolates motion weights around the parameter value between neighbours.
    /// </summary>
    public static class HonamiBlendWeightUtility
    {
        // Assumes the weights span is pre-zeroed by the caller.
        public static void CalculateWeights(IReadOnlyList<HonamiBlendTreeMotion> motions, float parameterValue, Span<float> weights, Span<(float threshold, int index)> sorted)
        {
            int count = motions?.Count ?? 0;
            if (count == 0) return;
            if (count == 1)
            {
                weights[0] = 1f;
                return;
            }

            for (int i = 0; i < count; i++)
                sorted[i] = (motions[i].threshold, i);

            for (int i = 1; i < count; i++)
            {
                var key = sorted[i];
                int j = i - 1;
                while (j >= 0 && sorted[j].threshold > key.threshold)
                {
                    sorted[j + 1] = sorted[j];
                    j--;
                }
                sorted[j + 1] = key;
            }

            if (parameterValue <= sorted[0].threshold)
            {
                weights[sorted[0].index] = 1f;
                return;
            }

            if (parameterValue >= sorted[count - 1].threshold)
            {
                weights[sorted[count - 1].index] = 1f;
                return;
            }

            for (int i = 0; i < count - 1; i++)
            {
                float t1 = sorted[i].threshold;
                float t2 = sorted[i + 1].threshold;
                if (parameterValue < t1 || parameterValue > t2) continue;

                float t = t1 == t2 ? 0f : (parameterValue - t1) / (t2 - t1);
                weights[sorted[i].index] = 1f - t;
                weights[sorted[i + 1].index] = t;
                return;
            }
        }
    }
}
