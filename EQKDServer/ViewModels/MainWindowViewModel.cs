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
using QKD_Library;

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

        private ChartValues<double> _linearDriftCompChartValues;
        private LineSeries _linearDriftCompLineSeries;

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

    
        //Charts
        public SeriesCollection LinearDriftCompCollection { get; set; }
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; } = new SectionsCollection();
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; } = new VisualElementsCollection();

        //COMMANDS
        public RelayCommand<object> WindowLoadedCommand { get; private set; }
        public RelayCommand<object> WindowClosingCommand { get; private set; }
        public RelayCommand<object> Settings_ServerTagger_Command { get; private set; }
        public RelayCommand<object> Settings_ClientTagger_Command { get; private set; }

        //Contructor
        public MainWindowViewModel()
        {
            //Create EKQDServer
            EQKDServer = new EQKDServerModel(LogMessage);
            EQKDServer.SecQNetServer.ConnectionStatusChanged += SecQNetConnectionStatusChanged;
            EQKDServer.TaggerSynchronization.SyncClocksComplete += SyncClocksComplete;

            //Handle Messages
            Messenger.Default.Register<string>(this, (s) => LogMessage(s));
                    
            //Route Relay Commands
            WindowLoadedCommand = new RelayCommand<object>(OnMainWindowLoaded);
            WindowClosingCommand = new RelayCommand<object>(OnMainWindowClosing);

            Settings_ServerTagger_Command = new RelayCommand<object>(On_Settings_ServerTagger_Command);
            Settings_ClientTagger_Command = new RelayCommand<object>(On_Settings_ClientTagger_Command);

            NetworkConnected = false;
            NetworkStatus = "Not connected";
            ServerBufferSize = 1000;
            ServerBufferStatus = 0;
            ClientBufferSize = 1000;
            ClientBufferStatus = 0;

            //Initialize Chart elements
            LinearDriftCompCollection = new SeriesCollection();           
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

            _linearDriftCompChartValues = new ChartValues<double>() { 100000, 200000 };
            _linearDriftCompLineSeries = new LineSeries()
            {
                Title = "Linear Clock Drift compensation factor",
                Values = _linearDriftCompChartValues,
            };
            LinearDriftCompCollection.Add(_linearDriftCompLineSeries);

            Messenger.Default.Send<EQKDServerCreatedMessage>(new EQKDServerCreatedMessage(EQKDServer));

            LogMessage("Application started");
        }

        private void OnMainWindowClosing(object obj)
        {
            EQKDServer.SaveServerConfig();
        }

        private void On_Settings_ServerTagger_Command(object o)
        {
            TimeTaggerFactory timeTaggerFactory = new TimeTaggerFactory("ServerTagger", LogMessage) { SecQNetServer = EQKDServer.SecQNetServer};

            EQKDServer.ServerTimeTagger = timeTaggerFactory.Modify(EQKDServer.ServerTimeTagger, new Views.TimeTaggerModifyView());
        }

        private void On_Settings_ClientTagger_Command(object o)
        {
            TimeTaggerFactory timeTaggerFactory = new TimeTaggerFactory("ClientTagger", LogMessage) { SecQNetServer = EQKDServer.SecQNetServer };

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
                    NetworkStatus = "Listening on local Socket " + EQKDServer.SecQNetServer.ServerIP +":"+ EQKDServer.SecQNetServer.Port.ToString();
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

        private void SyncClocksComplete(object sender, SyncClocksCompleteEventArgs e)
        {

            //-------------------------
            // Correlation Chart
            //-------------------------

            CorrelationSectionsCollection.Clear();
            CorrelationVisualElementsCollection.Clear();

            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramY, (X, Y) => new ObservablePoint(X, Y))));

            var axisSection = new AxisSection
            {
                Value = e.SyncRes.MeanTime,
                SectionWidth = 1,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection(new[] { 4d })
            };
            CorrelationSectionsCollection.Add(axisSection);

            var b = new Border();
            TextBox tb = new TextBox()
            {
                Text = "Pos: " + e.SyncRes.MeanTime.ToString() + "\n" +
                       "FWHM: " + e.SyncRes.FWHM.ToString("F2"),
            };
            b.Child = tb;
            VisualElement ve = new VisualElement()
            {
                X = e.SyncRes.MeanTime,
                Y = 0,
                UIElement = b
            };
            CorrelationVisualElementsCollection.Add(ve);

            //-------------------------
            // Linear Drift Comp Chart
            //-------------------------
                       
            if (_linearDriftCompChartValues.Count >= 100) _linearDriftCompChartValues.RemoveAt(0);
            _linearDriftCompChartValues.Add(e.SyncRes.CurrentLinearDriftCoeff);              
        }


        private void TimeTagsReceived(object sender, TimeTagsReceivedEventArgs e)
        {
            ServerBufferSize = EQKDServer.ServerTimeTagger.BufferSize;
            ServerBufferStatus = EQKDServer.ServerTimeTagger.BufferFillStatus;

            ReceivedClientTagsBufferSize = EQKDServer.ServerTimeTagger.BufferSize;
            ReceivedClientTagsBufferStatus = EQKDServer.ServerTimeTagger.BufferFillStatus;

            ClientBufferSize = e.BufferSize;
            ClientBufferStatus = e.BufferStatus;
        }

        #endregion

    }
}