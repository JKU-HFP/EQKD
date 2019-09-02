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
using Entanglement_Library;
using System.IO;
using System.Windows.Media;

namespace EQKDServer.ViewModels.SettingControlViewModels
{
    public class TaggerControlViewModel : ViewModelBase
    {
        //Private fields
        private EQKDServerModel _EQKDServer;

        ChartValues<ObservablePoint> _correlationChartValues;
        private LineSeries _correlationLineSeries;
        private Kurolator _correlator;

        //TimeTaggerChannelView _serverChannelView;
        //TimeTaggerChannelView _clientChannelView;
        //ChannelViewModel _serverChannelViewModel;
        //ChannelViewModel _clientChannelViewModel;

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

        private ulong _shotTime = 100000000000;

        public ulong ShotTime
        {
            get { return _shotTime; }
            set
            {
                _shotTime = value;
                OnPropertyChanged("ShotTime");

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




        //Charts
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; }

        //Commands
        public RelayCommand<object> StartCollectingCommand { get; private set; }
        public RelayCommand<object> StopCollectingCommand { get; private set; }
        public RelayCommand<object> CancelCommand { get; private set; }

        //Contructor
        public TaggerControlViewModel()
        {
            //Map RelayCommmands
            StartCollectingCommand = new RelayCommand<object>( (o) =>
            {
                //_EQKDServer.ServerTimeTagger.StartCollectingTimeTagsAsync();
                _EQKDServer.MeasureDensityMatrix();
            });
            StopCollectingCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.sync.TimeWindow = TimeWindow;
                _EQKDServer.sync.Bin = Resolution;          
                _EQKDServer.sync.ShotTime = ShotTime;
                _EQKDServer.sync.LinearDriftCoefficient = LinearDriftCoefficient;
                _EQKDServer.sync.PVal = PVal;
                _EQKDServer.sync.MeasureCorrelationAsync();
            });
            CancelCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.sync.Cancel();
            });

            //Handle Messages
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer= servermsg.EQKDServer;
                _EQKDServer.DensMeas.BasisCompleted += BasisComplete;

                _EQKDServer.StateCorr.CostFunctionAquired += CostFunctionAquired;

                _EQKDServer.sync.SyncComplete += SyncComplete;
                //_EQKDServer.secQNetServer.TimeTagsReceived += TimeTagsReceived;
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


            TimeWindow = 10000000;
            Resolution = 10000;
            ShotTime = 100000000000;
            LinearDriftCoefficient = 0;
            PVal = 0;

            //_serverChannelView = new TimeTaggerChannelView();
            //_serverChannelViewModel = new ChannelViewModel();
            //_serverChannelView.DataContext = _serverChannelViewModel;
            //_serverChannelView.Title = "Server TimeTagger Stats";
            //_serverChannelView.Show();

            //_clientChannelView = new TimeTaggerChannelView();
            //_clientChannelViewModel = new ChannelViewModel();
            //_clientChannelView.DataContext = _clientChannelViewModel;
            //_clientChannelView.Title = "Client TimeTagger Stats";
            //_clientChannelView.Show();
        }

        private void SyncComplete(object sender, SyncCompleteEventArgs e)
        {
            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X / 1E3, Y))));

            CorrelationSectionsCollection.Clear();

            LinearDriftCoefficient = e.CurrentLinearDriftCoeff;

            Directory.CreateDirectory("Sync");
            File.WriteAllLines($"Sync_{DateTime.Now:yy_MM_dd_hh_mm_ss}.txt", e.HistogramX.Zip(e.HistogramY, (x, y) => x.ToString() + "\t" + y.ToString()));
            File.AppendAllLines("Sync.txt", new string[] { $"{DateTime.Now:yy_MM_dd_hh_mm_ss},{e.CurrentLinearDriftCoeff}" });            
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
        
        private void TimeTagsCollected(object sender, TimeTagsCollectedEventArgs e)
        {
            if(OverwriteChecked || _correlator==null)
            {
                //Configure Correlation Channels
                List<CorrelationGroup> corrconfig = new List<CorrelationGroup>
                {
                    new Histogram(new List<(byte cA, byte cB)>{ (102, 0) }, TimeWindow * 1000000000, (long)Resolution * 1000000)
                };

                _correlator = new Kurolator(corrconfig, TimeWindow * 1000000000);
            }

            TimeTags tt;
            if (!_EQKDServer.ServerTimeTagger.GetNextTimeTags(out tt)) return;

            _correlator.AddCorrelations(tt, tt, 0);
            

            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(_correlator[0].Histogram_X.Zip(_correlator[0].Histogram_Y, (X,Y) => new ObservablePoint(X/1000000000.0,Y))));

        }

        private void TimeTagsReceived(object sender, TimeTagsReceivedEventArgs e)
        {
            //for (int i = 0; i < _clientChannelViewModel.ChanDiag.Count; i++)
            //{
            //    _clientChannelViewModel.ChanDiag[i].CountRate = e.Countrate[i];
            //}
        }
               
    }
}