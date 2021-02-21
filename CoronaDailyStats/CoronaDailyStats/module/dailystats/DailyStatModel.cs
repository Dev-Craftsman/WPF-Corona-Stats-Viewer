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
                return Math.Max(dataSelection(stat) - dataSelection(statOneDayBefore), 0d);
            }
            return 0d;
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
        
        internal double GetIncidenceValueFor7Days(SortedDictionary<DateTime, List<DailyStatModel>> dailyStats, string selectedCountry, DateTime date)
        {
            // todo works only for Germany at the moment.
            // next step: connect to public available rest api with population info
            Dictionary<string, double> populationPerCountry = new Dictionary<string, double>();
            populationPerCountry.Add("Germany", 82300000);

            if (populationPerCountry.ContainsKey(selectedCountry))
            {
                var last7Exists = true;

                for (int i = 0; i < 7; i++)
                {
                    DateTime dateToCheck = date.AddDays(-i);
                    if (!dailyStats.Keys.Contains(dateToCheck) || dailyStats[dateToCheck].FirstOrDefault(d => d.countryRegion == selectedCountry) == null)
                    {
                        last7Exists = false;
                        break;
                    }
                }

                if (last7Exists)
                {
                    double currentWeek = 0;
                    for (int i = 0; i < 7; i++)
                    {
                        DateTime dateToCheck = date.AddDays(-i);
                        currentWeek += GetDataOnDay(dailyStats, selectedCountry, dateToCheck, d => d.confirmed);
                    }
                    return 100000d * currentWeek / populationPerCountry[selectedCountry];
                }
            }
            return 0d;
        }

        internal double GetRValueFor7Days(SortedDictionary<DateTime, List<DailyStatModel>> dailyStats, string selectedCountry, DateTime date)
        {
            var last15Exists = true;

            for (int i = 0; i < 15; i++)
            {
                DateTime dateToCheck = date.AddDays(-i);
                if (!dailyStats.Keys.Contains(dateToCheck) || dailyStats[dateToCheck].FirstOrDefault(d => d.countryRegion == selectedCountry) == null)
                {
                    last15Exists = false;
                    break;
                }
            }

            if (last15Exists)
            {
                double currentWeek = 0;
                double previousWeek = 0;
                for (int i = 0; i < 7; i++)
                {
                    DateTime dateToCheck = date.AddDays(-i);
                    currentWeek += GetDataOnDay(dailyStats, selectedCountry, dateToCheck, d => d.confirmed);
                }
                for (int i = 7; i < 14; i++)
                {
                    DateTime dateToCheck = date.AddDays(-i);
                    previousWeek += GetDataOnDay(dailyStats, selectedCountry, dateToCheck, d => d.confirmed);
                }
                return previousWeek == 0d ? 0d : Math.Max(Math.Min(currentWeek / previousWeek, 3d), 0d);
            }
            return 0d;
        }
    }
}
