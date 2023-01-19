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
using System.IO;
using System.Windows.Media;
using QKD_Library.Synchronization;
using AsyncAwaitBestPractices;

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

        private long _packetTimeSpan = 2000000000000;

        public long PacketTImeSpan
        {
            get { return _packetTimeSpan; }
            set
            {
                _packetTimeSpan = value;
                OnPropertyChanged("PacketTImeSpan");
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

        private double _linDriftCoeff_Variation = 1E-11;

        public double LinDriftCoeff_Variation
        {
            get { return _linDriftCoeff_Variation; }
            set
            {
                _linDriftCoeff_Variation = value;
                OnPropertyChanged("LinDriftCoeff_Variation");
            }
        }

        private int _linDriftCoeff_NumVar = 0;

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

        private double _corrChartXMin = double.NaN;
        public double CorrChartXMin
        {
            get { return _corrChartXMin; }
            set
            {
                _corrChartXMin = value;
                OnPropertyChanged("CorrChartXMin");
            }
        }

        private double _corrChartXMax = double.NaN;
        public double CorrChartXMax
        {
            get { return _corrChartXMax; }
            set
            {
                _corrChartXMax = value;
                OnPropertyChanged("CorrChartXMax");
            }
        }

        private double _synFreqOffset;

        public double SyncFreqOffset
        {
            get { return _synFreqOffset; }
            set
            {
                _synFreqOffset = value;
                OnPropertyChanged("SyncFreqOffset");
            }
        }

        private int _startFinderThreshold = 30000;

        public int StartFinderThreshold
        {
            get { return _startFinderThreshold; }
            set 
            { 
                _startFinderThreshold = value;
                OnPropertyChanged("StartFinderThreshold");
            }
        }

        private double _corrSignificance = 0.8;
        public double CorrSignificance
        {
            get { return _corrSignificance; }
            set {
                _corrSignificance = value;
                OnPropertyChanged(nameof(CorrSignificance));
            }
        }



        #endregion

        //Charts
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationXSectionsCollection { get; set; }
        public SectionsCollection CorrelationYSectionsCollection { get; set; }
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; } = new VisualElementsCollection();

        //Commands
        public RelayCommand<object> StartSyncCommand { get; private set; }
        public RelayCommand<object> CancelCommand { get; private set; }
        public RelayCommand<object> TestClockCommand { get; private set; }

        //###########################
        // C O N S T R U C T O R
        //###########################
        public TaggerControlViewModel()
        {
            //Map RelayCommmands
            StartSyncCommand = new RelayCommand<object>(Synchronize, CanSynchrononize);

            CancelCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.ResetTaggers();
            });

            TestClockCommand = new RelayCommand<object>((o) =>
                {
                    SetSyncParameters();
                    _EQKDServer.TestClock().SafeFireAndForget();
                });

            //Handle Messages
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer= servermsg.EQKDServer;
                //_EQKDServer.DensMeas.BasisCompleted += BasisComplete;

                //Register Events
                _EQKDServer.ServerConfigRead += _EQKDServer_ServerConfigRead;
                _EQKDServer.AliceBobSync.SyncClocksComplete += TaggerSynchronization_SyncClocksComplete;
                _EQKDServer.AliceBobSync.SyncCorrComplete += TaggerSynchronization_SyncCorrComplete;
                _EQKDServer.AliceBobSync.OffsetFound += AliceBobSync_FindSignalStartComplete;
            });

           

            //Initialize Chart elements
            CorrelationCollection = new SeriesCollection();
            CorrelationXSectionsCollection = new SectionsCollection();
            CorrelationYSectionsCollection = new SectionsCollection();

 
            _correlationChartValues = new List<ChartValues<ObservablePoint>>
            {
                new ChartValues<ObservablePoint>(),
                new ChartValues<ObservablePoint>(),
                new ChartValues<ObservablePoint>(),
                new ChartValues<ObservablePoint>()
            };

            _correlationLineSeries = new List<LineSeries>{
                new LineSeries()
                {
                    Title = "Rate Alice",
                    Values = _correlationChartValues[0],
                    PointGeometrySize = 1.0,
                    LineSmoothness = 0.0,
                    Stroke = Brushes.Blue
                },
                new LineSeries()
                {
                    Title = "Fitted Rate Alice",
                    Values = _correlationChartValues[1],
                    PointGeometrySize = 0.0,
                    LineSmoothness = 0.0,
                    Fill=Brushes.Transparent,
                    Stroke=Brushes.Red
                },
                new LineSeries()
                {
                    Title = "Rate Bob",
                    Values = _correlationChartValues[2],
                    PointGeometrySize = 1.0,
                    LineSmoothness = 0.0,
                    Stroke = Brushes.Green
                },
                new LineSeries()
                {
                    Title = "Fitted Bob Alice",
                    Values = _correlationChartValues[3],
                    PointGeometrySize = 0.0,
                    LineSmoothness = 0.0,
                    Fill=Brushes.Transparent,
                    Stroke=Brushes.Red
                }
            };

            CorrelationCollection.AddRange(_correlationLineSeries);
        }

        private void AliceBobSync_FindSignalStartComplete(object sender, OffsetFoundEventArgs e)
        {

            if(e.ResultA.Rates!=null) File.WriteAllLines("FindStartTime_Alice.txt", e.ResultA.Times.Zip(e.ResultA.Rates, (x, y) => x.ToString() + "\t" + y.ToString()));
            if (e.ResultB.Rates != null) File.WriteAllLines("FindStartTime_Bob.txt", e.ResultB.Times.Zip(e.ResultB.Rates, (x, y) => x.ToString() + "\t" + y.ToString()));

            int[] XIndices = Enumerable.Range(0, e.ResultA.Times.Length).ToArray();

            CorrelationYSectionsCollection?.Clear();

            var thresholdAxisSection = new AxisSection
            {
                Value = e.ResultA.Threshold,
                SectionWidth = 0.1,
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection(new[] { 4d })
            };
            CorrelationYSectionsCollection?.Add(thresholdAxisSection);
        

            //ALICE

            //Rates
            _correlationChartValues[0]?.Clear();
            if (e.ResultA.Status > SignalStartStatus.ThresholdNotFound)
                _correlationChartValues[0]?.AddRange(new ChartValues<ObservablePoint>(e.ResultA.Times.Zip(e.ResultA.Rates, (X, Y) => new ObservablePoint(X/1E6, Y))));

            //Fitted rates
            _correlationChartValues[1]?.Clear();
            if(e.ResultA.Status > SignalStartStatus.SignalFittingFailed)
            _correlationChartValues[1]?.AddRange(new ChartValues<ObservablePoint>(e.ResultA.FittingTimes.Zip(e.ResultA.FittedRates, (X, Y) => new ObservablePoint(X/1E6, Y))));


            CorrelationXSectionsCollection?.Clear();

            if(e.ResultA.StartTime!=0)
            {
                var aliceStartAxisSection = new AxisSection
                {
                    Value = e.ResultA.StartTime / 1E6,
                    SectionWidth = 0.1,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection(new[] { 4d })
                };
                CorrelationXSectionsCollection?.Add(aliceStartAxisSection);
            }

            //BOB

            double bob_timeoffset = e.ResultA.StartTime != 0 && e.ResultB.StartTime != 0 ? e.ResultA.StartTime - e.ResultB.StartTime : 0;

            //Rates
            _correlationChartValues[2]?.Clear();
            if (e.ResultB.Status > SignalStartStatus.ThresholdNotFound)
                _correlationChartValues[2]?.AddRange(new ChartValues<ObservablePoint>(e.ResultB.Times.Zip(e.ResultB.Rates, (X, Y) => new ObservablePoint((X+bob_timeoffset) / 1E6, Y))));

            //Fitted rates
            _correlationChartValues[3]?.Clear();
            if (e.ResultA.Status > SignalStartStatus.SignalFittingFailed)
                _correlationChartValues[3]?.AddRange(new ChartValues<ObservablePoint>(e.ResultB.FittingTimes.Zip(e.ResultB.FittedRates, (X, Y) => new ObservablePoint((X+bob_timeoffset) / 1E6, Y))));


            CorrChartXMin = e.ResultA.StartTime/1E6 - 4000;
            CorrChartXMax = e.ResultA.StartTime/1E6 + 4000;

            if (e.ResultB.StartTime!=0)
            {
                var bobStartAxisSection = new AxisSection
                {
                    Value = e.ResultB.StartTime / 1E6,
                    SectionWidth = 0.1,
                    Stroke = Brushes.Green,
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection(new[] { 4d })
                };
                CorrelationXSectionsCollection?.Add(bobStartAxisSection); 
            }

        }

        private void TaggerSynchronization_SyncCorrComplete(object sender, SyncCorrCompleteEventArgs e)
        {
            _correlationChartValues?.ForEach(cv => cv.Clear());
            _correlationChartValues[0]?.AddRange(new ChartValues<ObservablePoint>(e.SyncRes.HistogramX.Zip(e.SyncRes.HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            
            //FIND ERROR!!!

            //CorrelationXSectionsCollection.Clear();  
            //var axisSection = new AxisSection
            //{
            //    Value = e.SyncRes.CorrPeakPos / 1000.0,
            //    SectionWidth = 0.1,
            //    Stroke = Brushes.Red,
            //    StrokeThickness = 1,
            //    StrokeDashArray = new DoubleCollection(new[] { 4d })
            //};
            //CorrelationXSectionsCollection.Add(axisSection);

            CorrChartXMin = double.NaN;
            CorrChartXMax = double.NaN;
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
    

        private void SetSyncParameters()
        {
            _EQKDServer.PacketSize = PacketSize;
            _EQKDServer.PacketTImeSpan = PacketTImeSpan;
            
            _EQKDServer.AliceBobSync.ClockTimeBin = Resolution;
            _EQKDServer.AliceBobSync.ClockSyncTimeWindow = TimeWindow;
            _EQKDServer.AliceBobSync.LinearDriftCoefficient = LinearDriftCoefficient;
            _EQKDServer.AliceBobSync.LinearDriftCoeff_Var = LinDriftCoeff_Variation;
            _EQKDServer.AliceBobSync.LinearDriftCoeff_NumVar = LinDriftCoeffNumVar;

            _EQKDServer.AliceBobSync.SyncFreqOffset = SyncFreqOffset;

            _EQKDServer.AliceBobSync.StartFinderThreshold = StartFinderThreshold;
            _EQKDServer.AliceBobSync.CorrSignificance = CorrSignificance;
        }
        private void Synchronize(object o)
        {
            SetSyncParameters();
            _EQKDServer.StartSynchronizeAsync().SafeFireAndForget();
        }

        private bool CanSynchrononize(object o)
        {
            return false;
            return (_EQKDServer != null &&
                     _EQKDServer.Hardware.ServerTimeTagger.CanCollect &&
                     _EQKDServer.Hardware.ClientTimeTagger.CanCollect);
        }
               
    }
}