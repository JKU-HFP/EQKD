using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;
using EQKDServer.ViewModels.SettingControlViewModels.TimeTaggerViewModels;
using GalaSoft.MvvmLight.Messaging;
using EQKDServer.Models.Messages;
using System.Threading;
using EQKDServer.Views.SettingControls;
using SecQNet;
using EQKDServer.Models;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using TimeTagger_Library.TimeTagger;
using TimeTagger_Library.Correlation;
using QKD_Library;
using System.IO;
using System.Windows.Media;

namespace EQKDServer.ViewModels.SettingControlViewModels
{
    public class TaggerControlViewModel : ViewModelBase
    {
        //###########################
        // P R I V A T E 
        //###########################
        private EQKDServerModel _EQKDServer;

        ChartValues<ObservablePoint> _correlationChartValues;
        private LineSeries _correlationLineSeries;
        private Kurolator _correlator;

        #region Properties
        //#############################
        //### P R O P E R T I E S   ###
        //#############################

        private bool _overrwriteChecked;
        public bool OverwriteChecked
        {
            get { return _overrwriteChecked; }
            set
            {
                _overrwriteChecked = value;
                OnPropertyChanged("OverwriteChecked");
            }
        }

        private ulong _timeWindow;
        public ulong TimeWindow
        {
            get { return _timeWindow; }
            set
            {
                _timeWindow = value;
                OnPropertyChanged("TimeWindow");
            }
        }

        private ulong _resolution = 10000;
        public ulong Resolution
        {
            get { return _resolution; }
            set
            {
                _resolution = value;
                OnPropertyChanged("Resolution");
            }
        }

        private int _packetSize;
        public int PacketSize
        {
            get { return _packetSize; }
            set
            {
                _packetSize = value;
                OnPropertyChanged("PacketSize");
            }
        }

        private double _linearDriftCoefficient;

        public double LinearDriftCoefficient
        {
            get { return _linearDriftCoefficient; }
            set
            {
                _linearDriftCoefficient = value;
                OnPropertyChanged("LinearDriftCoefficient");
            }
        }

        private double _pVal;

        public double PVal
        {
            get { return _pVal; }
            set
            {
                _pVal = value;
                OnPropertyChanged("PVal");
            }
        }
        #endregion
               
        //Charts
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; }

        //Commands
        public RelayCommand<object> StartSyncCommand { get; private set; }
        public RelayCommand<object> CancelCommand { get; private set; }

        //###########################
        // C O N S T R U C T O R
        //###########################
        public TaggerControlViewModel()
        {
            //Map RelayCommmands
            StartSyncCommand = new RelayCommand<object>(Synchronize, CanSynchrononize);

            CancelCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.StopSynchronize();  
            });

            //Handle Messages
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer= servermsg.EQKDServer;
                //_EQKDServer.DensMeas.BasisCompleted += BasisComplete;

                //Register Events
                _EQKDServer.StateCorr.CostFunctionAquired += CostFunctionAquired;
                _EQKDServer.ServerConfigRead += _EQKDServer_ServerConfigRead;
            });

           

            //Initialize Chart elements
            CorrelationCollection = new SeriesCollection();
            CorrelationSectionsCollection = new SectionsCollection();

            _correlationChartValues = new ChartValues<ObservablePoint> { new ObservablePoint(-1000, 100), new ObservablePoint(1000, 100) };
            _correlationLineSeries = new LineSeries()
            {
                Title = "Sync correlations",
                Values = _correlationChartValues,
                PointGeometrySize = 0.0
            };
            CorrelationCollection.Add(_correlationLineSeries);

        }

        //##########################
        // E V E N T   H A N D L E R
        //##########################

        private void _EQKDServer_ServerConfigRead(object sender, ServerConfigReadEventArgs e)
        {
            LinearDriftCoefficient = e.StartConfig.LinearDriftCoefficient;
            TimeWindow = e.StartConfig.TimeWindow;
            Resolution = e.StartConfig.TimeBin;
            PacketSize = e.StartConfig.PacketSize;
            PVal = e.StartConfig.PVal;
        }

        private void CostFunctionAquired(object sender, CostFunctionAquiredEventArgs e)
        {
            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X / 1E3, Y))));

            CorrelationSectionsCollection.Clear();
        }

        //Event Handler
        private void BasisComplete(object sender, BasisCompletedEventArgs e )
        {
            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X / 1E3, Y))));

            CorrelationSectionsCollection.Clear();

            //Check Dispatcher Target

            //foreach (Peak peak in e.Peaks)
            //{
            //    var axisSection = new AxisSection
            //    {
            //        Value = peak.MeanTime,
            //        SectionWidth = 1,
            //        Stroke = Brushes.Blue,
            //        StrokeThickness = 1,
            //        StrokeDashArray = new DoubleCollection(new[] { 4d })
            //    };
            //    CorrelationSectionsCollection.Add(axisSection);
            //}
        }
       

        private void Synchronize(object o)
        {
            _EQKDServer.PacketSize = PacketSize;

            _EQKDServer.TaggerSynchronization.TimeBin = Resolution;
            _EQKDServer.TaggerSynchronization.ClockSyncTimeWindow = TimeWindow;
            _EQKDServer.TaggerSynchronization.LinearDriftCoefficient = LinearDriftCoefficient;

            _EQKDServer.StartSynchronizeAsync();
        }

        private bool CanSynchrononize(object o)
        {
            return (_EQKDServer != null &&
                     _EQKDServer.ServerTimeTagger.CanCollect &&
                     _EQKDServer.ClientTimeTagger.CanCollect &&
                     _EQKDServer.SecQNetServer.connectionStatus == SecQNetServer.ConnectionStatus.ClientConnected);

        }
               
    }
}