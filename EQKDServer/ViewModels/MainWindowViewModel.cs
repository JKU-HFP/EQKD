using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using EQKDServer.ViewModels.SettingControlViewModels;
using SecQNet;
using TimeTagger_Library;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using LiveCharts.Defaults;
using GalaSoft.MvvmLight.Messaging;
using EQKDServer.Models.Messages;
using LiveCharts.Events;
using System.Windows.Input;
using EQKDServer.Models;
using TimeTaggerWPF_Library;
using TimeTagger_Library.Correlation;

namespace EQKDServer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        //PRIVATE FIELDS
        private EQKDServerModel EQKDServer;
        private int _rateUpdateCounter = 0;
        private int _rateUpdateCounterLimit = 1;
        private DateTime _lastTimeTagsReveivedTime = DateTime.Now;

        ChartValues<ObservablePoint> _correlationChartValues;
        private LineSeries _correlationLineSeries;
        private List<Peak> _peaks;

        private ChartValues<double> _clientTagsReceiveRateChartValues;
        private LineSeries _clientTagsReceiveRateLineSeries;

        //PROPERTIES
        private string _messages;
        public string Messages
        {
            get { return _messages; }
            private set
            {
                _messages = value;
                OnPropertyChanged("Messages");
            }
        }

        //Network status
        private bool _networkConnected;
        public bool NetworkConnected
        {
            get { return _networkConnected; }
            private set
            {
                _networkConnected = value;
                OnPropertyChanged("NetworkConnected");
            }
        }

        private string _networkStatus;
        public string NetworkStatus
        {
            get { return _networkStatus; }
            private set
            {
                _networkStatus = value;
                OnPropertyChanged("NetworkStatus");
            }
        }

        //Gauges
        private int _serverBufferStatus;
        public int ServerBufferStatus
        {
            get { return _serverBufferStatus; }
            set
            {
                _serverBufferStatus = value;
                OnPropertyChanged("ServerBufferStatus");
            }
        }

        private int _serverBufferSize;
        public int ServerBufferSize
        {
            get { return _serverBufferSize; }
            set
            {
                _serverBufferSize = value;
                OnPropertyChanged("ServerBufferSize");
            }
        }

        private int _clientBufferStatus;
        public int ClientBufferStatus
        {
            get { return _clientBufferStatus; }
            set
            {
                _clientBufferStatus = value;
                OnPropertyChanged("ClientBufferStatus");
            }
        }

        private int _clientBufferSize;
        public int ClientBufferSize
        {
            get { return _clientBufferSize; }
            set
            {
                _clientBufferSize = value;
                OnPropertyChanged("ClientBufferSize");
            }
        }

        private int _receivedClientTagsBufferStatus;
        public int ReceivedClientTagsBufferStatus
        {
            get { return _receivedClientTagsBufferStatus; }
            set
            {
                _receivedClientTagsBufferStatus = value;
                OnPropertyChanged("ReceivedClientTagsBufferStatus");
            }
        }

        private int _reiceivedClientTagsBufferSize;
        public int ReceivedClientTagsBufferSize
        {
            get { return _reiceivedClientTagsBufferSize; }
            set
            {
                _reiceivedClientTagsBufferSize = value;
                OnPropertyChanged("ReceivedClientTagsBufferSize");
            }
        }

        private int _clientTagsReceiveRate;
        public int ClientTagsReceiveRate
        {
            get { return _clientTagsReceiveRate; }
            set
            {
                _clientTagsReceiveRate = value;
                OnPropertyChanged("ClientTagsReceiveRate");
            }
        }

        private string _meanClientTagsReceiveRate;
        public string MeanClientTagsReceiveRate
        {
            get { return _meanClientTagsReceiveRate; }
            set
            {
                _meanClientTagsReceiveRate = value;
                OnPropertyChanged("MeanClientTagsReceiveRate");
            }
        }

        //Charts
        public SeriesCollection ClientTagsReceiveRateCollection { get; set; }
        public Func<double,string> RateFormatter { get; set; } = value => (value / 1000).ToString("F2") + "k";

        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; } = new SectionsCollection();
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; } = new VisualElementsCollection();

        //COMMANDS
        public RelayCommand<object> WindowLoadedCommand { get; private set; }
        public RelayCommand<ChartPoint> CorrelationDataClickCommand { get; private set; }
        
        public RelayCommand<object> Settings_ServerTagger_Command { get; private set; }
        public RelayCommand<object> Settings_ClientTagger_Command { get; private set; }

        //Contructor
        public MainWindowViewModel()
        {
            //Create EKQDServer
            EQKDServer = new EQKDServerModel(LogMessage);
            EQKDServer.SyncFinished += SecQNetSyncFinished;
            EQKDServer.secQNetServer.ConnectionStatusChanged += SecQNetConnectionStatusChanged;
            EQKDServer.secQNetServer.TimeTagsReceived += TimeTagsReceived;

            //Handle Messages
            Messenger.Default.Register<string>(this, (s) => LogMessage(s));
                    
            //Route Relay Commands
            WindowLoadedCommand = new RelayCommand<object>(OnMainWindowLoaded);
            CorrelationDataClickCommand = new RelayCommand<ChartPoint>(CorrelationDataClicked);

            Settings_ServerTagger_Command = new RelayCommand<object>(On_Settings_ServerTagger_Command);
            Settings_ClientTagger_Command = new RelayCommand<object>(On_Settings_ClientTagger_Command);

            NetworkConnected = false;
            NetworkStatus = "Not connected";
            ServerBufferSize = 1000;
            ServerBufferStatus = 0;
            ClientBufferSize = 1000;
            ClientBufferStatus = 0;
         
            //Initialize Chart elements
            ClientTagsReceiveRateCollection = new SeriesCollection();           
            CorrelationCollection = new SeriesCollection();

            Application.Current.DispatcherUnhandledException += new DispatcherUnhandledExceptionEventHandler((sender, e) =>
           {
               LogMessage(e.Exception.Message);
               e.Handled = true;
           });
        }


        private void LogMessage(string mess)
        {
            Messages += DateTime.Now.ToString("HH:mm:ss")+": "+ mess + "\n";
        }

        //Eventhandler

        private void OnMainWindowLoaded(object o)
        {
            _correlationChartValues = new ChartValues<ObservablePoint> { new ObservablePoint(-1000, 100), new ObservablePoint(1000,100) };
            _correlationLineSeries = new LineSeries()
            {
                Title = "Sync correlations",
                Values = _correlationChartValues,
            };
            CorrelationCollection.Add(_correlationLineSeries);

            _clientTagsReceiveRateChartValues = new ChartValues<double>() { 100000, 200000 };
            _clientTagsReceiveRateLineSeries = new LineSeries()
            {
                Title = "TimeTags receiving rate",
                Values = _clientTagsReceiveRateChartValues,
            };
            ClientTagsReceiveRateCollection.Add(_clientTagsReceiveRateLineSeries);

            Messenger.Default.Send<EQKDServerCreatedMessage>(new EQKDServerCreatedMessage(EQKDServer));

            LogMessage("Application started");
        }

        private void On_Settings_ServerTagger_Command(object o)
        {
            TimeTaggerFactory timeTaggerFactory = new TimeTaggerFactory("ServerTagger", LogMessage) { SecQNetServer = EQKDServer.secQNetServer};

            EQKDServer.ServerTimeTagger = timeTaggerFactory.Modify(EQKDServer.ServerTimeTagger, new Views.TimeTaggerModifyView());
        }

        private void On_Settings_ClientTagger_Command(object o)
        {
            TimeTaggerFactory timeTaggerFactory = new TimeTaggerFactory("ClientTagger", LogMessage) { SecQNetServer = EQKDServer.secQNetServer };

            EQKDServer.ClientTimeTagger = timeTaggerFactory.Modify(EQKDServer.ClientTimeTagger, new Views.TimeTaggerModifyView());
        }

        #region ChartEventhandler

        private void SecQNetConnectionStatusChanged(object sender, ServerConnStatChangedEventArgs e)
        {
            switch (e.connectionStatus)
            {
                case SecQNetServer.ConnectionStatus.NotConnected:
                    NetworkConnected = false;
                    NetworkStatus = "Not connected";
                    break;
                case SecQNetServer.ConnectionStatus.Listening:
                    NetworkConnected = false;
                    NetworkStatus = "Listening on local Socket " + EQKDServer.secQNetServer.ServerIP +":"+ EQKDServer.secQNetServer.Port.ToString();
                    break;
                case SecQNetServer.ConnectionStatus.ClientConnected:
                    NetworkConnected = true;
                    NetworkStatus = "Connected to " + e.ClientIPAddress;
                    break;
                case SecQNetServer.ConnectionStatus.ClientDisconnected:
                    NetworkConnected = false;
                    NetworkStatus = "Disconnected";
                    break;
            }
        }

        private void SecQNetSyncFinished(object sender, SyncFinishedEventArgs e)
        {

            CorrelationSectionsCollection.Clear();
            CorrelationVisualElementsCollection.Clear();

            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X, Y))));

            _peaks = e.Peaks;

            foreach (Peak peak in _peaks)
            {
                var axisSection = new AxisSection
                {
                    Value = peak.MeanTime,
                    SectionWidth = 1,
                    Stroke = peak.MeanTime == e.MinPeak.MeanTime ? Brushes.Red : Brushes.Blue,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection(new[] { 4d })
                };
                CorrelationSectionsCollection.Add(axisSection);
            }
        }

        private void CorrelationDataClicked(ChartPoint chartp)
        {
            Peak tmp_peak = _peaks?.Where(p => Math.Abs(p.MeanTime - chartp.X) < p.FWHM / 10.0).FirstOrDefault();

            if (tmp_peak != null)
            {
                var b = new Border();
                TextBox tb = new TextBox()
                {
                    Text = "Rel: " + tmp_peak.Height_Relative.ToString("F2") + "\n" +
                           "FWHM: " + tmp_peak.FWHM.ToString("F2"),
                };
                b.Child = tb;
                VisualElement ve = new VisualElement()
                {
                    X = tmp_peak.MeanTime,
                    Y = tmp_peak.Height_Absolute,
                    UIElement = b
                };
                CorrelationVisualElementsCollection.Add(ve);
            }
        }

        private void TimeTagsReceived(object sender, TimeTagsReceivedEventArgs e)
        {
            ServerBufferSize = EQKDServer.ServerTimeTagger.BufferSize;
            ServerBufferStatus = EQKDServer.ServerTimeTagger.BufferFillStatus;

            ReceivedClientTagsBufferSize = EQKDServer.ServerTimeTagger.BufferSize;
            ReceivedClientTagsBufferStatus = EQKDServer.ServerTimeTagger.BufferFillStatus;

            ClientBufferSize = e.BufferSize;
            ClientBufferStatus = e.BufferStatus;

            //Update Rate Chart
            if (_rateUpdateCounter >= _rateUpdateCounterLimit - 1)
            {
                double rate = 1000.0 * e.PacketSize * _rateUpdateCounterLimit / (double)((DateTime.Now - _lastTimeTagsReveivedTime).Milliseconds);
                _lastTimeTagsReveivedTime = DateTime.Now;

                if (_clientTagsReceiveRateChartValues.Count >= 100) _clientTagsReceiveRateChartValues.RemoveAt(0);
                if (!double.IsNaN(rate) && !double.IsInfinity(rate))
                {
                    _clientTagsReceiveRateChartValues.Add(rate);
                    MeanClientTagsReceiveRate = RateFormatter(_clientTagsReceiveRateChartValues.Average());
                }

                _rateUpdateCounter = 0;
            }
            else
            {
                _rateUpdateCounter++;
            }
        }

        #endregion

    }
}