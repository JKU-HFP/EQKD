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
using TimeTagger_Library.Correlation;
using QKD_Library;
using EQKDServer.Views.SettingControls;
using EQKDServer.ViewModels.SettingControlViewModels.TimeTaggerViewModels;
using QKD_Library.Synchronization;
using QKD_Library.Characterization;
using System.IO;

namespace EQKDServer.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        //#################################################
        //##  P R I V A T E S
        //#################################################

        private EQKDServerModel _EQKDServer;

        ChartValues<ObservablePoint> _correlationChartValues;
        private LineSeries _correlationLineSeries;
        ChartValues<ObservablePoint> _fittingChartValues;
        private LineSeries _fittingLineSeries;
        private List<Peak> _peaks;

        private ChartValues<double> _linearDriftCompChartValues;
        private LineSeries _linearDriftCompLineSeries;
        private ChartValues<double> _globalOffsetChartValues;
        private LineSeries _globalOffsetLineSeries;

        private TimeTaggerChannelView _channelView;
        private ChannelViewModel _channelViewModel;

        private bool _isUpdating = false;

        private object _messageLock = new object();
        public StatusWindowViewModel _statusWindow = new StatusWindowViewModel();

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

        #endregion

        //Logging
        public string LogFolder { get; set; } = "Log";
        public string LogFile { get; private set; } = "";

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
            //Create EKQDServer and register Events
            _EQKDServer = new EQKDServerModel(LogMessage, UserPrompt);
            _EQKDServer.SecQNetServer.ConnectionStatusChanged += SecQNetConnectionStatusChanged;
            _EQKDServer.AliceBobSync.SyncClocksComplete += SyncClocksComplete;
            _EQKDServer.AliceBobSync.SyncCorrComplete += SyncCorrComplete;
            _EQKDServer.AliceBobDensMatrix.BasisCompleted += BasisComplete;
            _EQKDServer.KeysGenerated += _EQKDServer_KeysGenerated;

            if (_EQKDServer.Hardware.ServerTimeTagger != null)
            {
                _EQKDServer.Hardware.ServerTimeTagger.TimeTagsCollected += (sender, e) =>
                {
                    _statusWindow.ServerBufferSize = _EQKDServer.Hardware.ServerTimeTagger.BufferSize;
                    _statusWindow.ServerBufferStatus = _EQKDServer.Hardware.ServerTimeTagger.BufferFillStatus;
                };
            }

            if (_EQKDServer.Hardware.ClientTimeTagger != null)
            {
                _EQKDServer.Hardware.ClientTimeTagger.TimeTagsCollected += (sender, e) =>
                {
                    _statusWindow.ClientBufferSize = _EQKDServer.Hardware.ClientTimeTagger.BufferSize;
                    _statusWindow.ClientBufferStatus = _EQKDServer.Hardware.ClientTimeTagger.BufferFillStatus;
                };
            }

            _EQKDServer.SecQNetServer.TimeTagsReceived += (sender, e) =>
            {
                _statusWindow.ReceivedClientTagsBufferSize = e.BufferSize;
                _statusWindow.ReceivedClientTagsBufferStatus = e.BufferStatus;
            };
            
            //Handle Messages
            Messenger.Default.Register<string>(this, (s) => LogMessage(s));

            //Route Relay Commands
            WindowLoadedCommand = new RelayCommand<object>(OnMainWindowLoaded);
            WindowClosingCommand = new RelayCommand<object>(OnMainWindowClosing);

            SaveSettingsCommand = new RelayCommand<object>((o) => _EQKDServer.SaveServerConfig());
            ReloadSettingsCommand = new RelayCommand<object>((o) => _EQKDServer.ReadServerConfig());
            OpenCountrateWindowCommand = new RelayCommand<object>(On_OpenCountrateWindowCommand);


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

            //Logging
            if (!string.IsNullOrEmpty(LogFolder))
            {
                if (!Directory.Exists(LogFolder)) Directory.CreateDirectory(LogFolder);
                LogFile = System.IO.Path.Combine(LogFolder, "Log_" + DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt");
            }
        }

        private void _EQKDServer_KeysGenerated(object sender, KeysGeneratedEventArgs e)
        {
            _correlationChartValues?.Clear();
            _fittingChartValues?.Clear();
            _correlationChartValues?.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            _statusWindow.CorrChartXMin = e.HistogramX[0] / 1000.0;
            _statusWindow.CorrChartXMax = e.HistogramX[e.HistogramX.Length - 1] / 1000.0;
        }

        private void On_OpenCountrateWindowCommand(object obj)
        {
            //TimeTagger ready?
            if (!_EQKDServer.Hardware.ServerTimeTagger.CanCollect) return;

            if (_channelViewModel == null) _channelViewModel = new ChannelViewModel(_EQKDServer.Hardware.ServerTimeTagger);

            if (_channelView != null) if (_channelView.IsVisible) return;

            _channelView = new TimeTaggerChannelView();
            _channelView.DataContext = _channelViewModel;
            _channelView.Title = "TimeTagger Stats";
            _channelView.Show();
        }

        private void LogMessage(string mess)
        {
            lock (_messageLock)
            {
                Messages += DateTime.Now.ToString("HH:mm:ss") + ": " + mess + "\n";
            }

            if (Messages.Length > 10000) _dumpMessages(20);
        }

        private void _dumpMessages(int remain)
        {
            lock (_messageLock)
            {
                string[] messages = Messages.Split('\n');
                if (messages.Length <= remain) return;

                if (!string.IsNullOrEmpty(LogFile)) File.AppendAllLines(LogFile, messages.Take(messages.Length - remain));

                Messages = string.Join("\n", messages.Skip(messages.Length - remain).ToArray());
            }
        }

        private int UserPrompt(string mess, string senderID)
        {
            MessageBoxResult res = MessageBox.Show(mess, senderID, MessageBoxButton.OKCancel);
            return (int)res;
        }

        //Eventhandler

        private void OnMainWindowLoaded(object o)
        {
            _correlationChartValues = new ChartValues<ObservablePoint> { };
            _correlationLineSeries  = new LineSeries()
            {
                Title = "Sync correlations",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _correlationChartValues

            };
            _statusWindow.CorrelationCollection.Add(_correlationLineSeries);

            _fittingChartValues = new ChartValues<ObservablePoint> { };
            _fittingLineSeries = new LineSeries()
            {
                Title = "Sync correlations",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _fittingChartValues
            };
            _statusWindow.CorrelationCollection.Add(_fittingLineSeries);

            _linearDriftCompChartValues = new ChartValues<double>() { };
            _linearDriftCompLineSeries = new LineSeries()
            {
                Title = "Linear Clock Drift compensation factor",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _linearDriftCompChartValues,
            };
            _statusWindow.LinearDriftCompCollection.Add(_linearDriftCompLineSeries);

            _globalOffsetChartValues = new ChartValues<double>() { };
            _globalOffsetLineSeries = new LineSeries()
            {
                Title = "Global Time Offset",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _globalOffsetChartValues,
            };
            _statusWindow.GlobalOffsetCollection.Add(_globalOffsetLineSeries);

            Messenger.Default.Send<EQKDServerCreatedMessage>(new EQKDServerCreatedMessage(_EQKDServer));

            _EQKDServer.ReadServerConfig();

            LogMessage("Application started");
        }

        private void OnMainWindowClosing(object obj)
        {
            _dumpMessages(0);
        }

        #region ChartEventhandler
        private void SecQNetConnectionStatusChanged(object sender, ServerConnStatChangedEventArgs e)
        {
            switch (e.connectionStatus)
            {
                case SecQNetServer.ConnectionStatus.NotConnected:
                    _statusWindow.NetworkConnected = false;
                    _statusWindow.NetworkStatus = "Not connected";
                    break;
                case SecQNetServer.ConnectionStatus.Listening:
                    _statusWindow.NetworkConnected = false;
                    _statusWindow.NetworkStatus = "Listening on local Socket " + _EQKDServer.SecQNetServer.ServerIP + ":" + _EQKDServer.SecQNetServer.Port.ToString();
                    break;
                case SecQNetServer.ConnectionStatus.ClientConnected:
                    _statusWindow.NetworkConnected = true;
                    _statusWindow.NetworkStatus = "Connected to " + e.ClientIPAddress;
                    break;
                case SecQNetServer.ConnectionStatus.ClientDisconnected:
                    _statusWindow.NetworkConnected = false;
                    _statusWindow.NetworkStatus = "Disconnected";
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

            _statusWindow.CorrelationSectionsCollection?.Clear();
            _statusWindow.CorrelationVisualElementsCollection?.Clear();

            _correlationChartValues?.Clear();
            _correlationChartValues?.AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            _fittingChartValues?.Clear();
            if (e.SyncRes.HistogramYFit != null) _fittingChartValues?.AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramYFit, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            _statusWindow.CorrChartXMin = e.SyncRes.HistogramX[0] / 1000.0;
            _statusWindow.CorrChartXMax = e.SyncRes.HistogramX[e.SyncRes.HistogramX.Length - 1] / 1000.0;


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
                    _statusWindow.CorrelationSectionsCollection?.Add(axisSection);
                }
            }

            if (e.SyncRes.MiddlePeak != null)
            {
                var b = new Border();
                TextBox tb = new TextBox()
                {
                    Text = "Pos: " + e.SyncRes.MiddlePeak.MeanTime.ToString() + "\n" +
                           $"Sigma: {e.SyncRes.Sigma.val:F2}({e.SyncRes.Sigma.err:F2})" + "\n" +
                           $"FWHM: {e.SyncRes.Sigma.val * 2.3548:F2}({e.SyncRes.Sigma.err * 2.3548:F2})" + "\n" +
                           $"Iterations: {e.SyncRes.NumIterations}" + "\n" +
                           $"GroundLevel: ({e.SyncRes.GroundLevel.val}, max:{e.SyncRes.GroundLevel.max:F1}) " + "\n" +
                           "InSync: " + (e.SyncRes.IsClocksSync ? "true" : "false")
                };
                b.Child = tb;
                VisualElement ve = new VisualElement()
                {
                    X = e.SyncRes.MiddlePeak.MeanTime / 1000.0,
                    Y = e.SyncRes.MiddlePeak.Height_Absolute,
                    UIElement = b
                };
                _statusWindow.CorrelationVisualElementsCollection?.Add(ve);
            }


            //-------------------------
            // Linear Drift Comp Chart
            //-------------------------

            if (_linearDriftCompChartValues?.Count >= 20) _linearDriftCompChartValues?.RemoveAt(0);
            _linearDriftCompChartValues?.Add(e.SyncRes.NewLinearDriftCoeff);

            _isUpdating = false;
        }

        private void SyncCorrComplete(object sender, SyncCorrCompleteEventArgs e)
        {
            if (_globalOffsetChartValues?.Count >= 100) _globalOffsetChartValues?.RemoveAt(0);

            _globalOffsetChartValues?.Add(_EQKDServer.AliceBobSync.GlobalClockOffset_Relative);

        }

        private void BasisComplete(object sender, BasisCompletedEventArgs e)
        {
            ShowCorrelationPeaks(e.HistogramX, e.HistogramY, e.Peaks);
        }

        private void ShowCorrelationPeaks(long[] HistogramX, long[] HistogramY, List<Peak> peaks)
        {
            _correlationChartValues?.Clear();
            _correlationChartValues?.AddRange(new ChartValues<ObservablePoint>(HistogramX.Zip(HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            _statusWindow.CorrelationSectionsCollection?.Clear();
            _statusWindow.CorrelationVisualElementsCollection?.Clear();


            _statusWindow.CorrChartXMin = HistogramX[0] / 1000.0;
            _statusWindow.CorrChartXMax = HistogramX[HistogramX.Length - 1] / 1000.0;

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
                _statusWindow.CorrelationSectionsCollection?.Add(axisSection);
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