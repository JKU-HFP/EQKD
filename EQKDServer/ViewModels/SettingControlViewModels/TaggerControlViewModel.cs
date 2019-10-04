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

        private List<ChartValues<ObservablePoint>> _correlationChartValues;
        private List<LineSeries> _correlationLineSeries;

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

        private double _linDriftCoeff_Variation = 0.001E-5;

        public double LinDriftCoeff_Variation
        {
            get { return _linDriftCoeff_Variation; }
            set
            {
                _linDriftCoeff_Variation = value;
                OnPropertyChanged("LinDriftCoeff_Variation");
            }
        }

        private int _linDriftCoeff_NumVar = 10;

        public int LinDriftCoeffNumVar
        {
            get { return _linDriftCoeff_NumVar; }
            set
            {
                _linDriftCoeff_NumVar = value;
                OnPropertyChanged("LinDriftCoeffNumVar");
            }
        }

        private long _fiberOffset;

        public long FiberOffset
        {
            get { return _fiberOffset; }
            set
            {
                _fiberOffset = value;
                OnPropertyChanged("FiberOffset");

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
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; }
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; } = new VisualElementsCollection();

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
                _EQKDServer.ServerConfigRead += _EQKDServer_ServerConfigRead;
                _EQKDServer.TaggerSynchronization.SyncClocksComplete += TaggerSynchronization_SyncClocksComplete;
                _EQKDServer.TaggerSynchronization.SyncCorrComplete += TaggerSynchronization_SyncCorrComplete;
                _EQKDServer.TaggerSynchronization.FindSignalStartComplete += TaggerSynchronization_FindSignalStartComplete;
            });

           

            //Initialize Chart elements
            CorrelationCollection = new SeriesCollection();
            CorrelationSectionsCollection = new SectionsCollection();

 
            _correlationChartValues = new List<ChartValues<ObservablePoint>>
            {
                new ChartValues<ObservablePoint>(),
                new ChartValues<ObservablePoint>()
            };

            _correlationLineSeries = new List<LineSeries>{
                new LineSeries()
                {
                    Title = "Rate",
                    Values = _correlationChartValues[0],
                    PointGeometrySize = 0.0,
                    LineSmoothness = 0.0
                },
                new LineSeries()
                {
                    Title = "Fitted Rate",
                    Values = _correlationChartValues[1],
                    PointGeometrySize = 0.0,
                    LineSmoothness = 0.0,
                    Fill = null
                }
            };

            CorrelationCollection.AddRange(_correlationLineSeries);
        }

        private void TaggerSynchronization_FindSignalStartComplete(object sender, FindSignalStartEventArgs e)
        {

            File.WriteAllLines("FindStartTimeTest.txt", e.ResultA.Times.Zip(e.ResultA.Rates, (x, y) => x.ToString() + "\t" + y.ToString()));

            int[] XIndices = Enumerable.Range(0, e.ResultA.Times.Length).ToArray();

            //Rates
            _correlationChartValues[0].Clear();
            _correlationChartValues[0].AddRange(new ChartValues<ObservablePoint>(e.ResultA.Times.Zip(e.ResultA.Rates, (X, Y) => new ObservablePoint(X/1E6, Y))));

            //Fitted rates
            _correlationChartValues[1].Clear();
            _correlationChartValues[1].AddRange(new ChartValues<ObservablePoint>(e.ResultA.FittingTimes.Zip(e.ResultA.FittedRates, (X, Y) => new ObservablePoint(X/1E6, Y))));

            CorrChartXMin = e.ResultA.StartTime/1E6 - 1000;
            CorrChartXMax = double.NaN;


            CorrelationSectionsCollection.Clear();

            var axisSection = new AxisSection
            {
                Value = e.ResultA.StartTime / 1000.0,
                SectionWidth = 0.1,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection(new[] { 4d })
            };
            CorrelationSectionsCollection.Add(axisSection);
        }

        private void TaggerSynchronization_SyncCorrComplete(object sender, SyncCorrCompleteEventArgs e)
        {

            CorrelationSectionsCollection.Clear();

            _correlationChartValues[0].Clear();
            _correlationChartValues[0].AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            if (e.SyncRes.Peaks != null)
            {
                foreach (Peak p in e.SyncRes.Peaks)
                {
                    var axisSection = new AxisSection
                    {
                        Value = p.MeanTime / 1000.0,
                        SectionWidth = 0.1,
                        Stroke = p.MeanTime==e.SyncRes.CorrPeakPos ? Brushes.Red : Brushes.Blue,
                        StrokeThickness = 1,
                        StrokeDashArray = new DoubleCollection(new[] { 4d })
                    };
                    CorrelationSectionsCollection.Add(axisSection);
                }
            }

            //Write new FiberOffset
            FiberOffset = e.SyncRes.NewFiberOffset;         
        }

        private void TaggerSynchronization_SyncClocksComplete(object sender, SyncClocksCompleteEventArgs e)
        {
            LinearDriftCoefficient = e.SyncRes.NewLinearDriftCoeff;
        }

        //##########################
        // E V E N T   H A N D L E R
        //##########################

        private void _EQKDServer_ServerConfigRead(object sender, ServerConfigReadEventArgs e)
        {
            LinearDriftCoefficient = e.StartConfig.LinearDriftCoefficient;
            LinDriftCoeffNumVar = e.StartConfig.LinearDriftCoeff_NumVar;
            LinDriftCoeff_Variation = e.StartConfig.LinearDriftCoeff_Var;
            TimeWindow = e.StartConfig.TimeWindow;
            Resolution = e.StartConfig.TimeBin;
            PacketSize = e.StartConfig.PacketSize;

            FiberOffset = e.StartConfig.FiberOffset;

        }
    

        private void Synchronize(object o)
        {
            _EQKDServer.PacketSize = PacketSize;

            _EQKDServer.TaggerSynchronization.ClockTimeBin = Resolution;
            _EQKDServer.TaggerSynchronization.ClockSyncTimeWindow = TimeWindow;
            _EQKDServer.TaggerSynchronization.LinearDriftCoefficient = LinearDriftCoefficient;
            _EQKDServer.TaggerSynchronization.LinearDriftCoeff_Var = LinDriftCoeff_Variation;
            _EQKDServer.TaggerSynchronization.LinearDriftCoeff_NumVar = LinDriftCoeffNumVar;

            _EQKDServer.TaggerSynchronization.FiberOffset = FiberOffset;

            _EQKDServer.StartSynchronizeAsync();
        }

        private bool CanSynchrononize(object o)
        {
            return (_EQKDServer != null &&
                     _EQKDServer.ServerTimeTagger.CanCollect &&
                     _EQKDServer.ClientTimeTagger.CanCollect);
        }
               
    }
}