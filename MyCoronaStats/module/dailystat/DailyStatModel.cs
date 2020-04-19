using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace MyCoronaStats.module.dailystat
{
    public class DailyStatModel
    {
        public string countryRegion { get; set; }
        public long confirmed { get; set; }
        public long deaths { get; set; }
        public long recovered { get; set; }
        public long active { get; set; }

        internal double getNewInfection(Dictionary<DateTime, List<DailyStatModel>> dailyStats, string selectedCountry, DateTime date)
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
                return stat.confirmed - statOneDayBefore.confirmed;
            }
            return 0;
        }



        internal double getNewInfectionFlatten(Dictionary<DateTime, List<DailyStatModel>> dailyStats, string selectedCountry, DateTime date, int dayIncluded)
        {
            double divisor = 1;
            double commulated = getNewInfection(dailyStats, selectedCountry, date);

            while (dayIncluded > 0) {
                var v1 = getNewInfection(dailyStats, selectedCountry, date.AddDays(dayIncluded));
                var v2 = getNewInfection(dailyStats, selectedCountry, date.AddDays(-dayIncluded));

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
