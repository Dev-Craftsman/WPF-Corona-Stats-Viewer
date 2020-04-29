using System;
using System.Collections.Generic;
using System.Text;

namespace CoronaDailyStats.module.dailystat
{
    public class DailyStat
    {
        public string fips { get; set; }
        public string admin2 { get; set; }
        public string provinceState { get; set; }
        public string countryRegion { get; set; }
        public string confirmed { get; set; }
        public string deaths { get; set; }
        public string recovered { get; set; }
        public string active { get; set; }
        public string combinedKey { get; set; }
    }
}
