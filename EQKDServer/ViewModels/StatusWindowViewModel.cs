using LiveCharts.Wpf;
using LiveCharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EQKDServer.Views;

namespace EQKDServer.ViewModels
{
    public class StatusWindowViewModel : ViewModelBase
    {
        //Network status
        private bool _networkConnected;
        private string _networkStatus;
        public bool NetworkConnected
        {
            get { return _networkConnected; }
            set
            {
                _networkConnected = value;
                OnPropertyChanged("NetworkConnected");
            }
        }
        public string NetworkStatus
        {
            get { return _networkStatus; }
            set
            {
                _networkStatus = value;
                OnPropertyChanged("NetworkStatus");
            }
        }

        //Gauges
        private int _serverBufferStatus;
        private int _serverBufferSize;
        private int _clientBufferStatus;
        private int _clientBufferSize;
        private int _receivedClientTagsBufferStatus;
        private int _reiceivedClientTagsBufferSize;
        private double _corrChartXMin = -10000;
        private double _corrChartXMax = 10000;
        public int ServerBufferStatus
        {
            get { return _serverBufferStatus; }
            set
            {
                _serverBufferStatus = value;
                OnPropertyChanged("ServerBufferStatus");
            }
        }
        public int ServerBufferSize
        {
            get { return _serverBufferSize; }
            set
            {
                _serverBufferSize = value;
                OnPropertyChanged("ServerBufferSize");
            }
        }
        public int ClientBufferStatus
        {
            get { return _clientBufferStatus; }
            set
            {
                _clientBufferStatus = value;
                OnPropertyChanged("ClientBufferStatus");
            }
        }
        public int ClientBufferSize
        {
            get { return _clientBufferSize; }
            set
            {
                _clientBufferSize = value;
                OnPropertyChanged("ClientBufferSize");
            }
        }
        public int ReceivedClientTagsBufferStatus
        {
            get { return _receivedClientTagsBufferStatus; }
            set
            {
                _receivedClientTagsBufferStatus = value;
                OnPropertyChanged("ReceivedClientTagsBufferStatus");
            }
        }
        public int ReceivedClientTagsBufferSize
        {
            get { return _reiceivedClientTagsBufferSize; }
            set
            {
                _reiceivedClientTagsBufferSize = value;
                OnPropertyChanged("ReceivedClientTagsBufferSize");
            }
        }
        public double CorrChartXMin
        {
            get { return _corrChartXMin; }
            set
            {
                _corrChartXMin = value;
                OnPropertyChanged("CorrChartXMin");
            }
        }
        public double CorrChartXMax
        {
            get { return _corrChartXMax; }
            set
            {
                _corrChartXMax = value;
                OnPropertyChanged("CorrChartXMax");
            }
        }

        //Charts
        public SeriesCollection LinearDriftCompCollection { get; set; }
        public SeriesCollection GlobalOffsetCollection { get; set; }
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; }
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; }

        public StatusWindowViewModel()
        {
            //Initialize Network elements
            NetworkConnected = false;
            NetworkStatus = "Not connected";

            //Initialize Buffer elements
            ServerBufferSize = 1000;
            ServerBufferStatus = 0;
            ClientBufferSize = 1000;
            ClientBufferStatus = 0;

            //Initialize Chart elements
            LinearDriftCompCollection = new SeriesCollection();
            CorrelationCollection = new SeriesCollection();
            GlobalOffsetCollection = new SeriesCollection();
            CorrelationSectionsCollection = new SectionsCollection();
            CorrelationVisualElementsCollection = new VisualElementsCollection();
        }
    }
}
