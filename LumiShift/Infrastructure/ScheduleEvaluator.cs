using System;
using System.Collections.Generic;
using System.Linq;
using LumiShift.Models;
using LumiShift.Services;

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
            double minutes = MinutesToNextSwitch(current);
            if (minutes == double.MaxValue) return "";

            var minDiff = TimeSpan.FromMinutes(minutes);
            if (minDiff.TotalHours < 1)
                return Lang.F("{0}分钟后", Math.Max(1, (int)Math.Ceiling(minDiff.TotalMinutes)));
            return Lang.F("{0}小时{1}分钟后", (int)minDiff.TotalHours, (int)minDiff.Minutes);
        }

        /// <summary>
        /// 返回距下一次时段切换的分钟数；无可用时段返回 double.MaxValue。
        /// 用于调度器自适应调整轮询间隔。
        /// </summary>
        public double MinutesToNextSwitch(TimeSpan current)
        {
            if (_segments.Count == 0) return double.MaxValue;

            double minMinutes = double.MaxValue;
            foreach (var segment in _segments)
            {
                if (segment.Start == segment.End) continue;

                double minutesToStart = segment.Start > current
                    ? (segment.Start - current).TotalMinutes
                    : (TimeSpan.FromHours(24) - (current - segment.Start)).TotalMinutes;

                if (minutesToStart < minMinutes)
                    minMinutes = minutesToStart;
            }
            return minMinutes;
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
