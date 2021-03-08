using CoronaDailyStats.module.dailystat;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

namespace CoronaDailyStats.module.main
{
    class AppDbContext : DbContext
    {
        public DbSet<DailyStatAllCountries> DailyStatAllCountries { get; set; }

        public DbSet<DailyStatModel> DailyStatModels { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dbPath = Path.Combine(appDataDir, "corona_daily_stats.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
