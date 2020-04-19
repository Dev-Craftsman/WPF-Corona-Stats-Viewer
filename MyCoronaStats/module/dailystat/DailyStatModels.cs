using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace MyCoronaStats.module.dailystat
{
    public class DailyStatModels
    {
        public Dictionary<DateTime, List<DailyStatModel>> dailyStats = new Dictionary<DateTime, List<DailyStatModel>>();

        internal void addDailyStats(DailyStat[] dailyData, DateTime date)
        {
            if (dailyData != null && dailyData.Length > 0)
            {
                dailyData.ToList().ForEach(data => addDailyStat(data, date));
            }
        }

        private void addDailyStat(DailyStat data, DateTime date)
        {
            if (!dailyStats.Keys.Contains(date))
            {
                dailyStats.Add(date, new List<DailyStatModel>());
            }

            Func<DailyStatModel, bool> countryPredicate = d => d.countryRegion == data.countryRegion;
            bool existsData = dailyStats[date].Any(countryPredicate);

            if (existsData)
            {
                DailyStatModel existingData = dailyStats[date].Single(countryPredicate);
                existingData.confirmed += getLong(data.confirmed);
                existingData.deaths += getLong(data.deaths);
                existingData.recovered += getLong(data.recovered);
                existingData.active += getLong(data.active);
            }
            else {
                DailyStatModel newData = new DailyStatModel();
                newData.confirmed = getLong(data.confirmed);
                newData.deaths = getLong(data.deaths);
                newData.recovered = getLong(data.recovered);
                newData.active = getLong(data.active);
                newData.countryRegion = data.countryRegion;
                dailyStats[date].Add(newData);
            }
        }

        private long getLong(string nullableText)
        {
            if (nullableText != null && long.TryParse(nullableText, out long res)) {
                return res;
            }
            return 0;
        }
    }
}
