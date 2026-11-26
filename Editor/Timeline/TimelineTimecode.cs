using System;
using System.Globalization;
using UnityEngine;

namespace HonamiAnimationSystem.Editor.Timeline
{
    public static class TimelineTimecode
    {
        private const float FrameEpsilon = 0.0001f;

        public static int ToFrame(float time, float frameRate) => Mathf.FloorToInt(time * frameRate + FrameEpsilon);

        public static string Format(float time, float frameRate, TimelineTimeFormat format)
        {
            frameRate = Mathf.Max(1f, frameRate);
            switch (format)
            {
                case TimelineTimeFormat.Frames:
                    return ToFrame(time, frameRate).ToString(CultureInfo.InvariantCulture) + "f";
                case TimelineTimeFormat.Seconds:
                    return time.ToString("0.00", CultureInfo.InvariantCulture) + "s";
                default:
                    return FormatTimecode(time, frameRate);
            }
        }

        public static string FormatTimecode(float time, float frameRate)
        {
            bool negative = time < 0f;
            int totalFrames = ToFrame(Mathf.Abs(time), frameRate);
            int fps = Mathf.Max(1, Mathf.RoundToInt(frameRate));
            int frames = totalFrames % fps;
            int totalSeconds = totalFrames / fps;
            int seconds = totalSeconds % 60;
            int minutes = totalSeconds / 60 % 60;
            int hours = totalSeconds / 3600;
            string sign = negative ? "-" : string.Empty;
            return hours > 0
                ? $"{sign}{hours:00}:{minutes:00}:{seconds:00}:{frames:00}"
                : $"{sign}{minutes:00}:{seconds:00}:{frames:00}";
        }

        public static string FormatRulerLabel(float time, float frameRate, TimelineTimeFormat format, bool showFrames)
        {
            switch (format)
            {
                case TimelineTimeFormat.Frames:
                    return ToFrame(time, frameRate).ToString(CultureInfo.InvariantCulture) + "f";
                case TimelineTimeFormat.Seconds:
                    return showFrames
                        ? time.ToString("0.###", CultureInfo.InvariantCulture) + "s"
                        : time.ToString("0.#", CultureInfo.InvariantCulture) + "s";
                default:
                    int fps = Mathf.Max(1, Mathf.RoundToInt(frameRate));
                    int total = ToFrame(time, frameRate);
                    int frames = total % fps;
                    int totalSeconds = total / fps;
                    string clock = totalSeconds >= 3600
                        ? $"{totalSeconds / 3600}:{totalSeconds / 60 % 60:00}:{totalSeconds % 60:00}"
                        : $"{totalSeconds / 60}:{totalSeconds % 60:00}";
                    return showFrames && frames != 0 ? $"{clock}:{frames:00}" : clock;
            }
        }

        // Accepts "12" in the current format, "120f", "2.5s", "1:12" (seconds:frames), "00:01:05:12" (hh:mm:ss:ff).
        public static bool TryParse(string text, float frameRate, TimelineTimeFormat format, out float time)
        {
            time = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim().Replace(',', '.');
            frameRate = Mathf.Max(1f, frameRate);

            if (text.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryFloat(text.Substring(0, text.Length - 1), out float frames)) return false;
                time = frames / frameRate;
                return true;
            }

            if (text.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryFloat(text.Substring(0, text.Length - 1), out float seconds)) return false;
                time = seconds;
                return true;
            }

            if (text.Contains(":"))
            {
                var parts = text.Split(':');
                float total = 0f;
                float framePart = 0f;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!TryFloat(parts[i], out float value)) return false;
                    bool isFrames = i == parts.Length - 1 && parts.Length >= 2;
                    if (isFrames)
                    {
                        framePart = value;
                        continue;
                    }
                    total = total * 60f + value;
                }
                time = total + framePart / frameRate;
                return true;
            }

            if (!TryFloat(text, out float number)) return false;
            time = format == TimelineTimeFormat.Seconds ? number : number / frameRate;
            return true;
        }

        private static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
