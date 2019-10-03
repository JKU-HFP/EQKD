using EQKDServer.Models;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDServer.Models
{
    public class ChannelDiagnosis : INotifyPropertyChanged
    {
        //Private Fields
        private ChartValues<double> _countRateChartValues;
        private LineSeries _countRateLineSeries;

        //Properties
        public int ChanNumber { get; }

        private int _countRate;
        public int CountRate
        {
            get { return _countRate; }
            set
            {
                _countRate = value;
                OnPropertyChanged("CountRate");
                CountrateChanged(_countRate);
            }
        }

        public SeriesCollection CountRateSeriesCollection { get; set; }
        public Func<double, string> RateFormatter { get; set; } = value => (value / 1000).ToString("F2") + "k";

        //Constructor
        public ChannelDiagnosis(int channr)
        {
            ChanNumber = channr;

            //Initialize Chart elements
            CountRateSeriesCollection = new SeriesCollection();

            _countRateChartValues = new ChartValues<double>();
            _countRateLineSeries = new LineSeries()
            {
                Title = "Countrate",
                PointGeometrySize = 0.0,
                Values = _countRateChartValues,
            };
            CountRateSeriesCollection.Add(_countRateLineSeries);
        }

        //Events

        private void CountrateChanged(int countrate)
        {
            if (_countRateChartValues.Count >= 30) _countRateChartValues.RemoveAt(0);
             _countRateChartValues.Add(countrate);    
        }

        public event PropertyChangedEventHandler PropertyChanged;
        internal void OnPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }
    }
}
