using System;
using System.Collections.Generic;
using System.Linq;

namespace CoronaDailyStats.module.dailystat
{
    public class DailyStatModels
    {
        public SortedDictionary<DateTime, List<DailyStatModel>> dailyStats = new();

        internal void addDailyStats(DailyStat[] dailyData, DateTime date)
        {
            dailyStats.Add(date,
                dailyData
                    .GroupBy(dd => dd.countryRegion)
                    .Select(group => new DailyStatModel
                    {
                        countryRegion = group.Key,
                        confirmed = group.Sum(data => getLong(data.confirmed)),
                        deaths = group.Sum(data => getLong(data.deaths)),
                        recovered = group.Sum(data => getLong(data.recovered)),
                        active = group.Sum(data => getLong(data.active))
                    })
                    .ToList());
        }

        private long getLong(string nullableText)
        {
            if (nullableText != null && long.TryParse(nullableText, out long res))
            {
                return res;
            }
            return 0;
        }
    }
}
