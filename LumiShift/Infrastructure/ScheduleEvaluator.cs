using System;
using System.Collections.Generic;
using System.Linq;
using LumiShift.Models;

namespace LumiShift.Infrastructure
{
    internal class ScheduleMatch
    {
        public int Index { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string PresetName { get; set; }
        public ScheduleSegment Segment { get; set; }
        public string Key => $"{Index}:{PresetName}";
    }

    internal class ScheduleEvaluator
    {
        private readonly List<ScheduleMatch> _segments;

        public ScheduleEvaluator(IEnumerable<ScheduleSegment> segments)
        {
            _segments = new List<ScheduleMatch>();
            if (segments == null) return;

            int index = 0;
            foreach (var segment in segments)
            {
                if (segment != null && TryParse(segment.StartTime, out var start) && TryParse(segment.EndTime, out var end))
                {
                    _segments.Add(new ScheduleMatch
                    {
                        Index = index,
                        Start = start,
                        End = end,
                        PresetName = segment.PresetName,
                        Segment = segment
                    });
                }
                index++;
            }
        }

        public ScheduleMatch FindCurrent(TimeSpan current)
        {
            foreach (var segment in _segments)
            {
                if (segment.Start == segment.End) continue;

                bool inSegment = segment.Start < segment.End
                    ? current >= segment.Start && current < segment.End
                    : current >= segment.Start || current < segment.End;

                if (inSegment)
                    return segment;
            }

            return null;
        }

        public string GetNextSwitchInfo(TimeSpan current)
        {
            if (_segments.Count == 0) return "";

            TimeSpan minDiff = TimeSpan.MaxValue;
            foreach (var segment in _segments)
            {
                TimeSpan diff = segment.Start > current
                    ? segment.Start - current
                    : TimeSpan.FromHours(24) - (current - segment.Start);

                if (diff < minDiff)
                    minDiff = diff;
            }

            if (minDiff == TimeSpan.MaxValue) return "";
            if (minDiff.TotalHours < 1)
                return $"{Math.Max(1, (int)Math.Ceiling(minDiff.TotalMinutes))}分钟后";
            return $"{(int)minDiff.TotalHours}小时{(int)minDiff.Minutes}分钟后";
        }

        public static int ComputeHash(IEnumerable<ScheduleSegment> segments)
        {
            unchecked
            {
                int hash = 17;
                if (segments == null) return hash;

                foreach (var segment in segments)
                {
                    hash = hash * 31 + (segment?.StartTime ?? "").GetHashCode();
                    hash = hash * 31 + (segment?.EndTime ?? "").GetHashCode();
                    hash = hash * 31 + (segment?.PresetName ?? "").GetHashCode();
                    hash = hash * 31 + (segment?.SyncMode.HasValue == true ? segment.SyncMode.Value.GetHashCode() : 0);

                    if (segment?.MonitorPresets == null) continue;
                    foreach (var kv in segment.MonitorPresets.OrderBy(k => k.Key))
                    {
                        hash = hash * 31 + (kv.Key ?? "").GetHashCode();
                        hash = hash * 31 + (kv.Value ?? "").GetHashCode();
                    }
                }
                return hash;
            }
        }

        private static bool TryParse(string value, out TimeSpan result)
        {
            result = default(TimeSpan);
            var parts = value?.Split(':');
            if (parts == null || parts.Length < 2) return false;
            if (!int.TryParse(parts[0], out int hour) || !int.TryParse(parts[1], out int minute)) return false;
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return false;
            result = new TimeSpan(hour, minute, 0);
            return true;
        }
    }
}
