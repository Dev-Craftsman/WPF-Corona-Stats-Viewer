using CoronaDailyStats.module.stats;
using CoronaDailyStats.module.utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;


namespace CoronaDailyStats.module.main
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        private Model _model;

        internal MainWindowViewModel()
        {         
            _model = new module.main.Model();
            statsPageViewModel = new StatsPageViewModel(_model);
        }

        public StatsPageViewModel statsPageViewModel { get; }


        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ExitCommand => new RelayCommand(exit);

        private static void exit(object obj)
        {
            Application.Current.Shutdown();
        }
    }
}
