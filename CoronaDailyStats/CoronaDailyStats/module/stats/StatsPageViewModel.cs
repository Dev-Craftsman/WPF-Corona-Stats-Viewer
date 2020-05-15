using CoronaDailyStats.module.countries;
using CoronaDailyStats.module.dailystat;
using CoronaDailyStats.module.utils;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CoronaDailyStats.module.stats
{
    class StatsPageViewModel : ViewModelBase
    {
        internal StatsPageViewModel(main.Model model)
        {
            MainGridIsEnabled = true;
            _model = model;
        }

        private const string URL_COUNTRIES = "https://covid19.mathdro.id/api/countries";

        // example https://covid19.mathdro.id/api/daily/04-14-2020
        private const string URL_DAILY_DATA = "https://covid19.mathdro.id/api/daily/";

        private readonly module.main.Model _model;

        public ICommand CollectDataCommand => new RelayCommand(collectDataAsync);

        private int collectDataProgress;
        public int CollectDataProgress
        {
            get
            {
                return collectDataProgress;
            }

            set
            {
                collectDataProgress = value;
                OnPropertyChanged();
            }
        }

        private string collectDataButtonLabel = "Collect Data";
        public string CollectDataButtonLabel
        {
            get
            {
                return collectDataButtonLabel;
            }

            set
            {
                collectDataButtonLabel = value;
                OnPropertyChanged();
            }
        }

        private bool mainGridIsEnabled;
        public bool MainGridIsEnabled
        {
            get
            {
                return mainGridIsEnabled;
            }

            set
            {
                mainGridIsEnabled = value;
                OnPropertyChanged();
            }
        }

        private async void collectDataAsync(object obj)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            MainGridIsEnabled = false;

            DateTime startDate = new DateTime(2020, 2, 1);

            List<DateTime> dates = Enumerable.Range(0, (DateTime.Now - startDate).Days + 1)
                                             .Select(offset => startDate.AddDays(offset))
                                             .ToList();

            int totalSteps = dates.Count + 1 /* first get countries step */;

            int completedSteps = 0;
            CollectDataProgress = 0;

            const int MAX_PARALLEL_DOWNLOADS = 20;
            ServicePointManager.DefaultConnectionLimit = MAX_PARALLEL_DOWNLOADS;

            using (HttpClient client = new HttpClient())
            using (var semaphore = new SemaphoreSlim(MAX_PARALLEL_DOWNLOADS))
            {
                string countriesString = await client.GetStringAsync(URL_COUNTRIES);

                _model.countries = JsonConvert.DeserializeObject<Countries>(countriesString);
                _model.dailyStats = new DailyStatModels();
                completedSteps++;
                CollectDataProgress = (100 * completedSteps / totalSteps);

                IEnumerable<Task> tasks = dates.Select(async date =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        string datePart = date.ToString("MM-dd-yyyy");
                        string dailyStatSring = await client.GetStringAsync(URL_DAILY_DATA + datePart);
                        DailyStat[] dailyData = JsonConvert.DeserializeObject<DailyStat[]>(dailyStatSring);
                        _model.dailyStats.addDailyStats(dailyData, date);
                    }
                    catch (HttpRequestException e)
                    {
                        // ignore
                    }
                    finally
                    {
                        completedSteps++;
                        CollectDataProgress = (100 * completedSteps / totalSteps);
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            OnPropertyChanged(nameof(Countries));

            MainGridIsEnabled = true;
            if (_model.countries.countries.Any(country => country.name.Equals("Germany")))
            {
                SelectedCountry = "Germany";
            }

            stopWatch.Stop();
            double seconds = Math.Round(stopWatch.Elapsed.TotalSeconds, 1, MidpointRounding.AwayFromZero);
            CollectDataButtonLabel = $"Collect Data took {seconds} secs";
        }

        public IEnumerable<string> Countries => _model.countries != null ? _model.countries.countries.Select(country => country.name) : null;

        private string _selectedCountry;
        public string SelectedCountry
        {
            get
            {
                return _selectedCountry;
            }

            set
            {
                _selectedCountry = value;
                OnPropertyChanged(nameof(SelectedCountry));
                OnPropertyChanged(nameof(OxyPlotModelCommulated));
                OnPropertyChanged(nameof(OxyPlotModelDaily));
            }
        }

        public class DateValue
        {
            public DateTime Date { get; set; }
            public double Value { get; set; }
        }

        public OxyPlot.PlotModel OxyPlotModelCommulated
        {
            get
            {
                if (_model.dailyStats == null || !_model.dailyStats.dailyStats.Any())
                {
                    return null;
                }

                var dataConfirmed = new Collection<DateValue>();
                var dataActive = new Collection<DateValue>();
                var dataDeath = new Collection<DateValue>();
                var dataRecovered = new Collection<DateValue>();
                _model.dailyStats.dailyStats.Keys.ToList().ForEach(date =>
                {
                    var stat = _model.dailyStats.dailyStats[date].FirstOrDefault(d => d.countryRegion == SelectedCountry);
                    if (stat != null)
                    {
                        dataConfirmed.Add(new DateValue { Date = date, Value = stat.confirmed });
                        dataActive.Add(new DateValue { Date = date, Value = stat.active });
                        dataDeath.Add(new DateValue { Date = date, Value = stat.deaths });
                        dataRecovered.Add(new DateValue { Date = date, Value = stat.recovered });
                    }
                });


                var plotModel = new OxyPlot.PlotModel();
                plotModel.Title = "Corona Data - " + SelectedCountry;
                plotModel.LegendTitle = "Legend";
                plotModel.LegendPosition = LegendPosition.LeftTop;

                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataConfirmed, plotModel, "confirmed infections", OxyColor.FromRgb(245, 255, 0));
                addDateToPlotModel(dataActive, plotModel, "active infections", OxyColor.FromRgb(255, 0, 0));
                addDateToPlotModel(dataDeath, plotModel, "deaths after infection", OxyColor.FromRgb(0, 0, 0));
                addDateToPlotModel(dataRecovered, plotModel, "recovered after infection", OxyColor.FromRgb(0, 255, 0));
                return plotModel;
            }
        }

        public OxyPlot.PlotModel OxyPlotModelDaily
        {
            get
            {
                if (_model.dailyStats == null || !_model.dailyStats.dailyStats.Any())
                {
                    return null;
                }

                var dataNew = new Collection<DateValue>();
                var dataNewFlatten1 = new Collection<DateValue>();
                var dataNewFlatten2 = new Collection<DateValue>();
                var dataNewFlatten3 = new Collection<DateValue>();
                _model.dailyStats.dailyStats.Keys.ToList().ForEach(date =>
                {
                    var stat = _model.dailyStats.dailyStats[date].FirstOrDefault(d => d.countryRegion == SelectedCountry);
                    if (stat != null)
                    {
                        dataNew.Add(new DateValue { Date = date, Value = stat.getNewInfection(_model.dailyStats.dailyStats, SelectedCountry, date) });
                        dataNewFlatten1.Add(new DateValue { Date = date, Value = stat.getNewInfectionFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 1) });
                        dataNewFlatten2.Add(new DateValue { Date = date, Value = stat.getNewInfectionFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 2) });
                        dataNewFlatten3.Add(new DateValue { Date = date, Value = stat.getNewInfectionFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 3) });
                    }
                });


                var plotModel = new OxyPlot.PlotModel();
                plotModel.Title = "new infections - " + SelectedCountry;
                plotModel.LegendTitle = "Legend";
                plotModel.LegendPosition = LegendPosition.LeftTop;
                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataNew, plotModel, "new infections", OxyColor.FromRgb(255, 0, 0));
                addDateToPlotModel(dataNewFlatten1, plotModel, "new infections (1-day-smoothing)", OxyColor.FromRgb(255, 255, 0));
                addDateToPlotModel(dataNewFlatten2, plotModel, "new infections (2-day-smoothing)", OxyColor.FromRgb(255, 0, 255));
                addDateToPlotModel(dataNewFlatten3, plotModel, "new infections (3-day-smoothing)", OxyColor.FromRgb(0, 255, 255));
                return plotModel;
            }
        }


        private static void addDateToPlotModel(Collection<DateValue> data, PlotModel plotModel, string title, OxyColor color)
        {
            var lineSeries = new LineSeries
            {
                Title = title,
                Color = color,
                MarkerFill = color,
                MarkerStroke = OxyPlot.OxyColors.ForestGreen,
                MarkerType = OxyPlot.MarkerType.Circle,
                StrokeThickness = 1,
                DataFieldX = "Date",
                DataFieldY = "Value",
                ItemsSource = data
            };
            plotModel.Series.Add(lineSeries);
        }
    }
}
