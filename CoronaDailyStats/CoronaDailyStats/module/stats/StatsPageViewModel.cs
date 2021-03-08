using CoronaDailyStats.module.countries;
using CoronaDailyStats.module.dailystat;
using CoronaDailyStats.module.main;
using CoronaDailyStats.module.utils;
using Microsoft.EntityFrameworkCore;
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
using System.Net.Http.Json;
using System.Text.Json;
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
            _eventDebouncerOxyPlotModels = new Debouncer(TimeSpan.FromSeconds(.75), fireEventOxyPlotModels);
        }

        private Debouncer _eventDebouncerOxyPlotModels;

        private void fireEventOxyPlotModels()
        {
            OnPropertyChanged(nameof(OxyPlotModelCommulated));
            OnPropertyChanged(nameof(OxyPlotModelDailyInfections));
            OnPropertyChanged(nameof(OxyPlotModelDailyDeaths));
            OnPropertyChanged(nameof(OxyPlotModelRValuesFor7Days));
            OnPropertyChanged(nameof(OxyPlotModelIncidenceValuesFor7Days));
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

        public bool IsSliderEnabled => _model.dailyStats != null && _model.dailyStats.dailyStats.Keys.Any();

        private int _dataSliderLowerValue = 0;
        public int DataSliderLowerValue
        {
            get
            {
                return _dataSliderLowerValue;
            }
            set
            {
                _dataSliderLowerValue = value;
                OnPropertyChanged(nameof(DataSliderLowerValue));
                OnPropertyChanged(nameof(LowerDate));
                _eventDebouncerOxyPlotModels.Invoke();
            }
        }

        private int _dataSliderUpperValue = 0;
        public int DataSliderUpperValue
        {
            get
            {
                return _dataSliderUpperValue;
            }
            set
            {
                _dataSliderUpperValue = value;
                OnPropertyChanged(nameof(UpperDate));
                OnPropertyChanged(nameof(DataSliderUpperValue));
                _eventDebouncerOxyPlotModels.Invoke();
            }
        }

        private bool _clearResponseCache;
        public bool ClearResponseCache
        {
            get => _clearResponseCache;
            set
            {
                _clearResponseCache = value;
                OnPropertyChanged();
            }
        }

        public int DataSliderMaximum
        {
            get
            {
                return _model.dailyStats != null ? _model.dailyStats.dailyStats.Keys.Count() - 1 : 0;
            }
        }

        public int DataSliderMinimum => 0;

        public String LowerDate
        {
            get
            {
                return getDateBySlider(DataSliderLowerValue).ToShortDateString();
            }
        }
        public String UpperDate
        {
            get
            {
                return getDateBySlider(DataSliderUpperValue).ToShortDateString();
            }
        }

        private DateTime getDateBySlider(int sliderValue)
        {
            if (_model.dailyStats != null && _model.dailyStats.dailyStats.Keys.Any())
            {
                return _model.dailyStats.dailyStats.Keys.ToList()[sliderValue];
            }
            return DateTime.Now;
        }

        private async void collectDataAsync(object obj)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            MainGridIsEnabled = false;
            using var dbContext = new AppDbContext();

            if (_clearResponseCache)
            {
                await dbContext.Database.EnsureDeletedAsync();
            }

            await dbContext.Database.EnsureCreatedAsync();

            var responseCache = await dbContext
                .DailyStatResponses
                .ToDictionaryAsync(d => d.Date, d => d.Response);

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
                _model.countries = await client.GetFromJsonAsync<Countries>(URL_COUNTRIES);
                _model.dailyStats = new DailyStatModels();
                completedSteps++;
                CollectDataProgress = (100 * completedSteps / totalSteps);

                IEnumerable<Task> tasks = dates.Select(async date =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        string datePart = date.ToString("MM-dd-yyyy");

                        string cachedDailyStatResponse;
                        if (!responseCache.TryGetValue(datePart, out cachedDailyStatResponse))
                        {
                            cachedDailyStatResponse = await client.GetStringAsync(URL_DAILY_DATA + datePart);
                            dbContext.DailyStatResponses.Add(new DailyStatResponse
                            {
                                Date = datePart,
                                Response = cachedDailyStatResponse
                            });
                        }
                        else
                        {
                            // necessary for updating UI, when using cache
                            await Task.Delay(1);
                        }

                        DailyStat[] dailyData = JsonSerializer.Deserialize<DailyStat[]>(cachedDailyStatResponse);
                        _model.dailyStats.addDailyStats(dailyData, date);
                    }
                    catch (HttpRequestException)
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

            await dbContext.SaveChangesAsync();
            ClearResponseCache = false;
            OnPropertyChanged(nameof(Countries));

            MainGridIsEnabled = true;
            if (_model.countries.countries.Any(country => country.name.Equals("Germany")))
            {
                SelectedCountry = "Germany";
            }

            DataSliderUpperValue = _model.dailyStats.dailyStats.Keys.Count() - 1;
            DataSliderLowerValue = _model.dailyStats.dailyStats.Keys.Count() > 100 ? _model.dailyStats.dailyStats.Keys.Count() - 100 : 0;

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
                OnPropertyChanged(nameof(OxyPlotModelDailyInfections));
                OnPropertyChanged(nameof(OxyPlotModelDailyDeaths));
                OnPropertyChanged(nameof(OxyPlotModelRValuesFor7Days));
                OnPropertyChanged(nameof(OxyPlotModelIncidenceValuesFor7Days));
                OnPropertyChanged(nameof(DataSliderMinimum));
                OnPropertyChanged(nameof(DataSliderMaximum));
                OnPropertyChanged(nameof(DataSliderLowerValue));
                OnPropertyChanged(nameof(DataSliderUpperValue));
                OnPropertyChanged(nameof(LowerDate));
                OnPropertyChanged(nameof(UpperDate));
                OnPropertyChanged(nameof(IsSliderEnabled));
            }
        }

        public class DateValue
        {
            public DateTime Date { get; set; }
            public double Value { get; set; }
        }


        private bool isInRange(DateTime dateToCheck)
        {
            return dateToCheck.CompareTo(getDateBySlider(DataSliderLowerValue)) >= 0 && dateToCheck.CompareTo(getDateBySlider(DataSliderUpperValue)) <= 0;
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
                    if (!isInRange(date))
                    {
                        return;
                    }

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

        public OxyPlot.PlotModel OxyPlotModelDailyInfections
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
                    if (!isInRange(date))
                    {
                        return;
                    }

                    var stat = _model.dailyStats.dailyStats[date].FirstOrDefault(d => d.countryRegion == SelectedCountry);
                    if (stat != null)
                    {
                        dataNew.Add(new DateValue { Date = date, Value = stat.GetDataOnDay(_model.dailyStats.dailyStats, SelectedCountry, date, x => x.confirmed) });
                        dataNewFlatten1.Add(new DateValue { Date = date, Value = stat.GetDataOnDayFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 1, x => x.confirmed) });
                        dataNewFlatten2.Add(new DateValue { Date = date, Value = stat.GetDataOnDayFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 2, x => x.confirmed) });
                        dataNewFlatten3.Add(new DateValue { Date = date, Value = stat.GetDataOnDayFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 3, x => x.confirmed) });
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

        public OxyPlot.PlotModel OxyPlotModelDailyDeaths
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
                    if (!isInRange(date))
                    {
                        return;
                    }

                    var stat = _model.dailyStats.dailyStats[date].FirstOrDefault(d => d.countryRegion == SelectedCountry);
                    if (stat != null)
                    {
                        dataNew.Add(new DateValue { Date = date, Value = stat.GetDataOnDay(_model.dailyStats.dailyStats, SelectedCountry, date, x => x.deaths) });
                        dataNewFlatten1.Add(new DateValue { Date = date, Value = stat.GetDataOnDayFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 1, x => x.deaths) });
                        dataNewFlatten2.Add(new DateValue { Date = date, Value = stat.GetDataOnDayFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 2, x => x.deaths) });
                        dataNewFlatten3.Add(new DateValue { Date = date, Value = stat.GetDataOnDayFlatten(_model.dailyStats.dailyStats, SelectedCountry, date, 3, x => x.deaths) });
                    }
                });


                var plotModel = new OxyPlot.PlotModel();
                plotModel.Title = "deaths - " + SelectedCountry;
                plotModel.LegendTitle = "Legend";
                plotModel.LegendPosition = LegendPosition.LeftTop;
                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataNew, plotModel, "deaths", OxyColor.FromRgb(255, 0, 0));
                addDateToPlotModel(dataNewFlatten1, plotModel, "deaths (1-day-smoothing)", OxyColor.FromRgb(255, 255, 0));
                addDateToPlotModel(dataNewFlatten2, plotModel, "deaths (2-day-smoothing)", OxyColor.FromRgb(255, 0, 255));
                addDateToPlotModel(dataNewFlatten3, plotModel, "deaths (3-day-smoothing)", OxyColor.FromRgb(0, 255, 255));
                return plotModel;
            }
        }


        public OxyPlot.PlotModel OxyPlotModelRValuesFor7Days
        {
            get
            {
                if (_model.dailyStats == null || !_model.dailyStats.dailyStats.Any())
                {
                    return null;
                }

                var dataNew = new Collection<DateValue>();

                _model.dailyStats.dailyStats.Keys.ToList().ForEach(date =>
                {
                    if (!isInRange(date))
                    {
                        return;
                    }

                    var stat = _model.dailyStats.dailyStats[date].FirstOrDefault(d => d.countryRegion == SelectedCountry);
                    if (stat != null)
                    {
                        dataNew.Add(new DateValue { Date = date, Value = stat.GetRValueFor7Days(_model.dailyStats.dailyStats, SelectedCountry, date) });
                    }
                });


                var plotModel = new OxyPlot.PlotModel();
                plotModel.Title = "R value for 7 days - " + SelectedCountry;
                plotModel.LegendTitle = "Legend";
                plotModel.LegendPosition = LegendPosition.LeftTop;
                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataNew, plotModel, "R value", OxyColor.FromRgb(255, 0, 0));
                return plotModel;
            }
        }

        public OxyPlot.PlotModel OxyPlotModelIncidenceValuesFor7Days
        {
            get
            {
                if (_model.dailyStats == null || !_model.dailyStats.dailyStats.Any())
                {
                    return null;
                }

                var dataNew = new Collection<DateValue>();

                _model.dailyStats.dailyStats.Keys.ToList().ForEach(date =>
                {
                    if (!isInRange(date))
                    {
                        return;
                    }

                    var stat = _model.dailyStats.dailyStats[date].FirstOrDefault(d => d.countryRegion == SelectedCountry);
                    if (stat != null)
                    {
                        dataNew.Add(new DateValue { Date = date, Value = stat.GetIncidenceValueFor7Days(_model.dailyStats.dailyStats, SelectedCountry, date) });
                    }
                });


                var plotModel = new OxyPlot.PlotModel();
                plotModel.Title = "incidence value for 7 days for 100.000 people - " + SelectedCountry;
                plotModel.LegendTitle = "Legend";
                plotModel.LegendPosition = LegendPosition.LeftTop;
                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataNew, plotModel, "incidence value", OxyColor.FromRgb(255, 0, 0));
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
