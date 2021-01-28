using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CoronaDailyStats.module.dailystat
{
    public class DailyStatModel
    {
        public string countryRegion { get; set; }
        public long confirmed { get; set; }
        public long deaths { get; set; }
        public long recovered { get; set; }
        public long active { get; set; }

        internal double GetDataOnDay(SortedDictionary<DateTime, List<DailyStatModel>> dailyStats, string selectedCountry, DateTime date, Func<DailyStatModel, long> dataSelection)
        {
            DateTime oneDayBefore = date.AddDays(-1);
            if (!dailyStats.Keys.Contains(date) || !dailyStats.Keys.Contains(oneDayBefore))
            {
                return 0;
            }


            var statOneDayBefore = dailyStats[oneDayBefore].FirstOrDefault(d => d.countryRegion == selectedCountry);
            var stat = dailyStats[date].FirstOrDefault(d => d.countryRegion == selectedCountry);
            if (stat != null && statOneDayBefore != null)
            {
                return dataSelection(stat) - dataSelection(statOneDayBefore);
            }
            return 0;
        }

        internal double GetDataOnDayFlatten(SortedDictionary<DateTime, List<DailyStatModel>> dailyStats, string selectedCountry, DateTime date, int dayIncluded, Func<DailyStatModel, long> dataSelection)
        {
            double divisor = 1;
            double commulated = GetDataOnDay(dailyStats, selectedCountry, date, dataSelection);

            while (dayIncluded > 0) {
                var v1 = GetDataOnDay(dailyStats, selectedCountry, date.AddDays(dayIncluded), dataSelection);
                var v2 = GetDataOnDay(dailyStats, selectedCountry, date.AddDays(-dayIncluded), dataSelection);

                if (v1 > 0.01)
                {
                    commulated += v1; 
                    divisor += 1;
                }

                if (v2 > 0.01)
                {
                    commulated += v2;
                    divisor += 1;
                }

                dayIncluded--;
            }

            return commulated / divisor;
        }
    }
}
