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
using EQKDServer.Views.SettingControls;
using EQKDServer.ViewModels.SettingControlViewModels.TimeTaggerViewModels;

namespace EQKDServer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        //#################################################
        //##  P R I V A T E S
        //#################################################

        private EQKDServerModel _EQKDServer;
        private int _rateUpdateCounter = 0;
        private int _rateUpdateCounterLimit = 1;
        private DateTime _lastTimeTagsReveivedTime = DateTime.Now;

        ChartValues<ObservablePoint> _correlationChartValues;
        private LineSeries _correlationLineSeries;
        ChartValues<ObservablePoint> _fittingChartValues;
        private LineSeries _fittingLineSeries;
        private List<Peak> _peaks;

        private ChartValues<double> _linearDriftCompChartValues;
        private LineSeries _linearDriftCompLineSeries;

        private TimeTaggerChannelView _channelView;
        private ChannelViewModel _channelViewModel;

        private bool _isUpdating = false;

        #region Propterties
        //#################################################
        //## P R O P E R T I E S
        //#################################################
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

        private double _corrChartXMin = -10000;
        public double CorrChartXMin
        {
            get { return _corrChartXMin; }
            set
            {
                _corrChartXMin = value;
                OnPropertyChanged("CorrChartXMin");
            }
        }

        private double _corrChartXMax = 10000;
        public double CorrChartXMax
        {
            get { return _corrChartXMax; }
            set
            {
                _corrChartXMax = value;
                OnPropertyChanged("CorrChartXMax");
            }
        }
        #endregion


        //Charts
        public SeriesCollection LinearDriftCompCollection { get; set; }
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; } = new SectionsCollection();
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; } = new VisualElementsCollection();

        //COMMANDS
        public RelayCommand<object> WindowLoadedCommand { get; private set; }
        public RelayCommand<object> WindowClosingCommand { get; private set; }
        public RelayCommand<object> SaveSettingsCommand { get; private set; }
        public RelayCommand<object> ReloadSettingsCommand { get; private set; }
        public RelayCommand<object> OpenCountrateWindowCommand { get; private set; }

        //#################################################
        //##  C O N S T R U C T O R 
        //#################################################
        public MainWindowViewModel()
        {
            //Create EKQDServer
            _EQKDServer = new EQKDServerModel(LogMessage);
            _EQKDServer.SecQNetServer.ConnectionStatusChanged += SecQNetConnectionStatusChanged;
            _EQKDServer.TaggerSynchronization.SyncClocksComplete += SyncClocksComplete;
            _EQKDServer.densMeas.BasisCompleted += BasisComplete;
            _EQKDServer.StateCorr.LossFunctionAquired += StateCorr_LossFunctionAquired;

            //Handle Messages
            Messenger.Default.Register<string>(this, (s) => LogMessage(s));
                    
            //Route Relay Commands
            WindowLoadedCommand = new RelayCommand<object>(OnMainWindowLoaded);
            WindowClosingCommand = new RelayCommand<object>(OnMainWindowClosing);

            SaveSettingsCommand = new RelayCommand<object>((o) => _EQKDServer.SaveServerConfig());
            ReloadSettingsCommand = new RelayCommand<object>((o) => _EQKDServer.ReadServerConfig());
            OpenCountrateWindowCommand = new RelayCommand<object>(On_OpenCountrateWindowCommand);

            NetworkConnected = false;
            NetworkStatus = "Not connected";
            ServerBufferSize = 1000;
            ServerBufferStatus = 0;
            ClientBufferSize = 1000;
            ClientBufferStatus = 0;
            
            //Initialize Chart elements
            LinearDriftCompCollection = new SeriesCollection();           
            CorrelationCollection = new SeriesCollection();

            //Exception Handling
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Exception ex = e.ExceptionObject as Exception ?? new Exception($"AppDomainUnhandledException: Unknown exception: {e.ExceptionObject}");
                MessageBox.Show(ex.Message, "Unhandled CurrentDomain exeption", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            Dispatcher.CurrentDispatcher.UnhandledException += (sender, e) =>
            {
                MessageBox.Show(e.Exception.Message, "Unhandled UI Dispatcher exception", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                MessageBox.Show(e.Exception.Message + "\nInner Exception: " + e.Exception.InnerException.Message, "Unhandled TaskScheduler exception", MessageBoxButton.OK, MessageBoxImage.Error);
            };
        }

        private void StateCorr_LossFunctionAquired(object sender, LossFunctionAquiredEventArgs e)
        {
            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            CorrChartXMin = e.HistogramX[0] / 1000.0;
            CorrChartXMax = e.HistogramX[e.HistogramX.Length - 1] / 1000.0;
        }

        private void On_OpenCountrateWindowCommand(object obj)
        {
            if (_channelView != null) if (_channelView.IsVisible) return;
            
            _channelView = new TimeTaggerChannelView();
            if (_channelViewModel == null) _channelViewModel = new ChannelViewModel(_EQKDServer);
            _channelView.DataContext = _channelViewModel;
            _channelView.Title = "TimeTagger Stats";
            _channelView.Show();
            
        }

        private void LogMessage(string mess)
        {
            Messages += DateTime.Now.ToString("HH:mm:ss")+": "+ mess + "\n";
        }

        //Eventhandler

        private void OnMainWindowLoaded(object o)
        {
            _correlationChartValues = new ChartValues<ObservablePoint> { };
            _correlationLineSeries = new LineSeries()
            {
                Title = "Sync correlations",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _correlationChartValues
                
            };
            CorrelationCollection.Add(_correlationLineSeries);

            _fittingChartValues = new ChartValues<ObservablePoint> { };
            _fittingLineSeries = new LineSeries()
            {
                Title = "Sync correlations",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _fittingChartValues
            };
            CorrelationCollection.Add(_fittingLineSeries);

            _linearDriftCompChartValues = new ChartValues<double>() { };
            _linearDriftCompLineSeries = new LineSeries()
            {
                Title = "Linear Clock Drift compensation factor",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _linearDriftCompChartValues,
            };
            LinearDriftCompCollection.Add(_linearDriftCompLineSeries);

            Messenger.Default.Send<EQKDServerCreatedMessage>(new EQKDServerCreatedMessage(_EQKDServer));


            LogMessage("Application started");
        }

        private void OnMainWindowClosing(object obj)
        {

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
                    NetworkStatus = "Listening on local Socket " + _EQKDServer.SecQNetServer.ServerIP +":"+ _EQKDServer.SecQNetServer.Port.ToString();
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
            if (_isUpdating) return;

            _isUpdating = true;

            //-------------------------
            // Correlation Chart
            //-------------------------

            CorrelationSectionsCollection.Clear();
            CorrelationVisualElementsCollection.Clear();

            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramY, (X, Y) => new ObservablePoint(X/1000.0, Y))));

            _fittingChartValues.Clear();
            if(e.SyncRes.HistogramYFit!=null) _fittingChartValues.AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramYFit, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            CorrChartXMin = e.SyncRes.HistogramX[0]/1000.0;
            CorrChartXMax = e.SyncRes.HistogramX[e.SyncRes.HistogramX.Length-1]/1000.0;


            if (e.SyncRes.Peaks != null)
            {
                foreach (Peak p in e.SyncRes.Peaks)
                {
                    var axisSection = new AxisSection
                    {
                        Value = p.MeanTime / 1000.0,
                        SectionWidth = 0.1,
                        Stroke = Brushes.Red,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection(new[] { 4d })
                    };
                    CorrelationSectionsCollection.Add(axisSection);
                }
            }

            if(e.SyncRes.MiddlePeak!=null)
            {
                var b = new Border();
                TextBox tb = new TextBox()
                {
                    Text = "Pos: " + e.SyncRes.MiddlePeak.MeanTime.ToString() + "\n" +
                           $"Sigma: {e.SyncRes.Sigma.val:F2}({e.SyncRes.Sigma.err:F2})" + "\n" +
                           $"FWHM: {e.SyncRes.Sigma.val*2.3548:F2}({e.SyncRes.Sigma.err*2.3548:F2})" + "\n" +
                           $"Iterations: {e.SyncRes.NumIterations}" + "\n" +
                           $"GroundLevel: ({e.SyncRes.GroundLevel.val}, max:{e.SyncRes.GroundLevel.max}) " + "\n" +
                           "InSync: " + (e.SyncRes.IsClocksSync ? "true" : "false")
                };
                b.Child = tb;
                VisualElement ve = new VisualElement()
                {
                    X = e.SyncRes.MiddlePeak.MeanTime/1000.0,
                    Y = e.SyncRes.MiddlePeak.Height_Absolute,
                    UIElement = b
                };
                CorrelationVisualElementsCollection.Add(ve);
            }


            //-------------------------
            // Linear Drift Comp Chart
            //-------------------------

            if (_linearDriftCompChartValues.Count >= 20) _linearDriftCompChartValues.RemoveAt(0);
            _linearDriftCompChartValues.Add(e.SyncRes.NewLinearDriftCoeff);

            _isUpdating = false;
        }

        private void BasisComplete(object sender, BasisCompletedEventArgs e)
        {
            ShowCorrelationPeaks(e.HistogramX, e.HistogramY, e.Peaks);        
        }

        private void ShowCorrelationPeaks(long[] HistogramX, long[] HistogramY, List<Peak> peaks)
        {
            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(HistogramX.Zip(HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            CorrelationSectionsCollection.Clear();
            CorrelationVisualElementsCollection.Clear();


            CorrChartXMin = HistogramX[0] / 1000.0;
            CorrChartXMax = HistogramX[HistogramX.Length - 1] / 1000.0;

            foreach (Peak peak in peaks)
            {
                var axisSection = new AxisSection
                {
                    Value = peak.MeanTime / 1000.0,
                    SectionWidth = 0.1,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection(new[] { 4d })
                };
                CorrelationSectionsCollection.Add(axisSection);
            }
        }

        //private void TimeTagsReceived(object sender, TimeTagsReceivedEventArgs e)
        //{
        //    ServerBufferSize = _EQKDServer.ServerTimeTagger.BufferSize;
        //    ServerBufferStatus = _EQKDServer.ServerTimeTagger.BufferFillStatus;

        //    ReceivedClientTagsBufferSize = _EQKDServer.ServerTimeTagger.BufferSize;
        //    ReceivedClientTagsBufferStatus = _EQKDServer.ServerTimeTagger.BufferFillStatus;

        //    ClientBufferSize = e.BufferSize;
        //    ClientBufferStatus = e.BufferStatus;
        //}

        #endregion

    }
}