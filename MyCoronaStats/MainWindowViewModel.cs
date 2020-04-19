using MyCoronaStats.module.countries;
using MyCoronaStats.module.dailystat;
using MyCoronaStats.module.main;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using OxyPlot;

namespace MyCoronaStats
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        private const string URL_COUNTRIES = "https://covid19.mathdro.id/api/countries";

        // example https://covid19.mathdro.id/api/daily/04-14-2020
        private const string URL_DAILY_DATA = "https://covid19.mathdro.id/api/daily/";

        private readonly module.main.Model _model = new module.main.Model();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ICommand CollectDataCommand => new RelayCommand(collectData, o => true);

        private void collectData(object obj)
        {
            WebClient client = new WebClient();
            string countriesString = client.DownloadString(URL_COUNTRIES);
            _model.countries = JsonSerializer.Deserialize<Countries>(countriesString);

            DateTime date = new DateTime(2020, 2, 1);
            while (date < DateTime.Now)
            {
                string datePart = date.ToString("MM-dd-yyyy");

                try
                {
                    string dailyStatSring = client.DownloadString(URL_DAILY_DATA + datePart);
                    DailyStat[] dailyData = JsonSerializer.Deserialize<DailyStat[]>(dailyStatSring);
                    _model.dailyStats.addDailyStats(dailyData, date);

                }
                catch (WebException e)
                {
                    e.ToString();
                }
                date = date.AddDays(1);
            }

            OnPropertyChanged(nameof(Countries));
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
                if (!_model.dailyStats.dailyStats.Any())
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
                plotModel.Title = "Zahlen - " + SelectedCountry;
                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataConfirmed, plotModel, "bestätigte Infektionen", OxyColor.FromRgb(245, 255, 0));
                addDateToPlotModel(dataActive, plotModel, "aktive Infektionen", OxyColor.FromRgb(255, 0, 0));
                addDateToPlotModel(dataDeath, plotModel, "Tote nach Infektionen", OxyColor.FromRgb(0, 0, 0));
                addDateToPlotModel(dataRecovered, plotModel, "Gesenene nach Infektionen", OxyColor.FromRgb(0, 255, 0));
                return plotModel;
            }
        }

        public OxyPlot.PlotModel OxyPlotModelDaily
        {
            get
            {
                if (!_model.dailyStats.dailyStats.Any())
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
                plotModel.Title = "Neuinfektionen - " + SelectedCountry;
                var dateTimeAxis1 = new DateTimeAxis
                {
                    CalendarWeekRule = CalendarWeekRule.FirstFourDayWeek,
                    FirstDayOfWeek = DayOfWeek.Monday,
                    Position = AxisPosition.Bottom
                };
                plotModel.Axes.Add(dateTimeAxis1);
                addDateToPlotModel(dataNew, plotModel, "Neuinfektionen", OxyColor.FromRgb(255, 0, 0));
                addDateToPlotModel(dataNewFlatten1, plotModel, "Neuinfektionen (1-Tag-Glättung)", OxyColor.FromRgb(255, 255, 0));
                addDateToPlotModel(dataNewFlatten2, plotModel, "Neuinfektionen (2-Tage-Glättung)", OxyColor.FromRgb(255, 0, 255));
                addDateToPlotModel(dataNewFlatten3, plotModel, "Neuinfektionen (3-Tage-Glättung)", OxyColor.FromRgb(0, 255, 255));
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
