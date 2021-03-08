using System.Collections.Generic;

namespace CoronaDailyStats.module.dailystat
{
    public class DailyStatAllCountries
    {
        public long Id { get; set; }

        public string Date { get; set; }

        public ICollection<DailyStatModel> DailyStatModels { get; set; }
    }
}
