using System;
using System.Collections.Generic;

namespace CoronaDailyStats.module.dailystat
{
    public class DailyStatModels
    {
        public SortedDictionary<DateTime, List<DailyStatModel>> dailyStats = new();

        internal void addDailyStats(DateTime date, List<DailyStatModel> dailyStatModels)
        {
            dailyStats.Add(date, dailyStatModels);
        }
    }
}
