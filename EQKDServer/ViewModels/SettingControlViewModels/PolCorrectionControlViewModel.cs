using AsyncAwaitBestPractices;
using EQKDServer.Models;
using EQKDServer.Models.Messages;
using GalaSoft.MvvmLight.Messaging;
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using QKD_Library;
using System.Timers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TimeTagger_Library.Correlation;
using System.ComponentModel;
using System;

namespace EQKDServer.ViewModels.SettingControlViewModels
{
    public class PolCorrectionControlViewModel : ViewModelBase
    {
        //#############################
        //### P R I V A T E         ###
        //#############################

        private Timer _posTimer = new Timer(1000);

        //#############################
        //### P R O P E R T I E S   ###
        //#############################

        private int _packetSize = 200000;
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

        private ulong _keyGenTimebin = 1000;

        public ulong KeyGenTimebin
        {
            get { return _keyGenTimebin; }
            set { 
                _keyGenTimebin = value;
                OnPropertyChanged(nameof(KeyGenTimebin));
            }
        }


        private StateCorrection.Mode _correctionMode = StateCorrection.Mode.DownhillSimplex;

        public StateCorrection.Mode CorrectionMode
        {
            get { return _correctionMode; }
            set { 
                _correctionMode = value;
                OnPropertyChanged("CorrectionMode");
            }
        }

        public IEnumerable<StateCorrection.Mode> ModeValues
        {
            get
            {
                return Enum.GetValues(typeof(StateCorrection.Mode)).Cast<StateCorrection.Mode>();
            }
        }

        private int _iterations = 100;
        public int Iterations
        {
            get { return _iterations; }
            set {
                _iterations = value;
                OnPropertyChanged("Iterations");
            }
        }

        private double _bruteForceRange=5;
        public double BruteForceRange
        {
            get { return _bruteForceRange; }
            set {
                _bruteForceRange = value;
                OnPropertyChanged("BruteForceRange");
            }
        }




        private ObservableCollection<StagePosModel> _targetPos =
            new ObservableCollection<StagePosModel>(Enumerable.Range(0, 3).Select(i => new StagePosModel() { Value = 0 }).ToList());
        public ObservableCollection<StagePosModel> TargetPos {
            get { return _targetPos; }
            set
            {
                _targetPos = value;
                OnPropertyChanged("TargetPos");
            }
        }

        public ObservableCollection<StagePosModel> _currPos =
            new ObservableCollection<StagePosModel>(Enumerable.Range(0, 3).Select(i => new StagePosModel() { Value = 0 }).ToList());
        public ObservableCollection<StagePosModel> CurrPos
        {
            get { return _currPos; }
            set
            {
                _currPos = value;
                OnPropertyChanged("CurrPos");
            }
        }


        private double _stage_XPos;
        public double Stage_XPos
        {
            get { return _stage_XPos; }
            set {
                _stage_XPos = value;
                OnPropertyChanged("Stage_XPos");
            }
        }


        private double _stage_YPos;

        public double Stage_YPos
        {
            get { return _stage_YPos; }
            set {
                _stage_YPos = value;
                OnPropertyChanged("Stage_YPos");
            }
        }

        public double CountrateSetpoint { get; set; } = 53000;
        public double CountrateSetpointTolerance { get; set; } = 0.8;


        private int _autoStabilization = 0;

        public int AutoStabilization
        {
            get { return _autoStabilization; }
            set {
                _autoStabilization = value;
                OnPropertyChanged("AutoStabilization");
                _EQKDServer.Hardware.AutoStabilization = value==1;
                _EQKDServer.Hardware.XYStabilizer.Activated = value == 1;
            }
        }



        //Charts
        public SeriesCollection CorrelationCollection { get; set; }
        public SectionsCollection CorrelationSectionsCollection { get; set; }
        public VisualElementsCollection CorrelationVisualElementsCollection { get; set; } = new VisualElementsCollection();


        //Private fields
        private EQKDServerModel _EQKDServer;

        private ChartValues<ObservablePoint> _correlationChartValuesOrtho;
        private ChartValues<ObservablePoint> _correlationChartValuesColin;
        private LineSeries _correlationLineSeriesOrtho;
        private LineSeries _correlationLineSeriesColin;

        //Commands
        public RelayCommand<object> StartCorrectionCommand { get; private set; }

        public RelayCommand<object> StartKeyGenerationCommand { get; private set; }

        public RelayCommand<object> StartDensityMatrixCommand { get; private set; }
        public RelayCommand<object> CancelCommand { get; private set; }

        public RelayCommand<object> GoToPositionCommand { get; private set; }

        public RelayCommand<object> Stage_Yplus_Command { get; private set; }
        public RelayCommand<object> Stage_Yminus_Command { get; private set; }
        public RelayCommand<object> Stage_Xplus_Command { get; private set; }
        public RelayCommand<object> Stage_Xminus_Command { get; private set; }
        public RelayCommand<object> Stage_Optimize_Command { get; private set; }

        public RelayCommand<object> SetCountrateSP_Command { get; private set; }

        public PolCorrectionControlViewModel()
        {
            //Map RelayCommmands
            StartCorrectionCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.PacketTImeSpan = PacketTImeSpan;

                _EQKDServer.FiberCorrection.PacketSize = PacketSize;
                _EQKDServer.FiberCorrection.PacketTimeSpan = PacketTImeSpan;

                _EQKDServer.FiberCorrection.MinPos = TargetPos.Select(t => t.Value).ToArray();
                _EQKDServer.FiberCorrection.MaxIterations = Iterations;
                _EQKDServer.FiberCorrection.OptimizationMode = CorrectionMode;

                _EQKDServer.FiberCorrection.InitRange = BruteForceRange;
                _EQKDServer.FiberCorrection.Accurracy_BruteForce = BruteForceRange / Iterations;

                _EQKDServer.StartFiberCorrectionAsync().SafeFireAndForget();
            });
            StartKeyGenerationCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.PacketSize = PacketSize;
                _EQKDServer.PacketTImeSpan = PacketTImeSpan;
                _EQKDServer.Key_TimeBin = KeyGenTimebin;
                _EQKDServer.StartKeyGenerationAsync().SafeFireAndForget();
            });

            StartDensityMatrixCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.PacketSize = PacketSize;
                _EQKDServer.PacketTImeSpan = PacketTImeSpan;
                _EQKDServer.StartDensityMatrixAsync().SafeFireAndForget();
            });

            CancelCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.StopCorrection();
                _EQKDServer.Cancel();
            });

            GoToPositionCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.GotoPosition(TargetPos.Select(t=>t.Value).ToList()).SafeFireAndForget();
            },
            (o) => !_EQKDServer?.FiberCorrection?.IsActive ?? false);


            Stage_Yplus_Command = new RelayCommand<object>((o) => _EQKDServer.Hardware.MoveXYStage(0));
            Stage_Yminus_Command = new RelayCommand<object>((o) => _EQKDServer.Hardware.MoveXYStage(1));
            Stage_Xplus_Command = new RelayCommand<object>((o) => _EQKDServer.Hardware.MoveXYStage(2));
            Stage_Xminus_Command = new RelayCommand<object>((o) => _EQKDServer.Hardware.MoveXYStage(3));
            Stage_Optimize_Command = new RelayCommand<object>((o) => _EQKDServer.Hardware.XYStageOptimize());

            SetCountrateSP_Command = new RelayCommand<object>((o) =>
            {
                _EQKDServer.Hardware.XYStabilizer.SetPoint = CountrateSetpoint;
                _EQKDServer.Hardware.XYStabilizer.TriggerTolerance = CountrateSetpointTolerance;
            });

            //Handle Messages
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer = servermsg.EQKDServer;

                //Register Events
                _EQKDServer.FiberCorrection.LossFunctionAquired += StateCorr_LossFunctionAquired;
            });

            //Initialize Chart elements
            CorrelationCollection = new SeriesCollection();
            CorrelationSectionsCollection = new SectionsCollection();

            _correlationChartValuesOrtho = new ChartValues<ObservablePoint> { };
            _correlationLineSeriesOrtho = new LineSeries()
            {
                Title = "Correlations Orthogonal",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _correlationChartValuesOrtho
            };
            CorrelationCollection.Add(_correlationLineSeriesOrtho);

            _correlationChartValuesColin = new ChartValues<ObservablePoint> { };
            _correlationLineSeriesColin = new LineSeries()
            {
                Title = "Correlation Colinear",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _correlationChartValuesColin
            };
            CorrelationCollection.Add(_correlationLineSeriesColin);

            try
            {
                //Set and start Position timer
                _posTimer.Elapsed += (sender, e) =>
                {
                    CurrPos = new ObservableCollection<StagePosModel>();//(_EQKDServer?.FiberCorrection?.StagePositions.Select(pos => new StagePosModel { Value = 0 }).ToList());

                    Stage_XPos = _EQKDServer?.Hardware.XStage?.Position ?? double.NaN;
                    Stage_YPos = _EQKDServer?.Hardware.YStage?.Position ?? double.NaN;
                };
                _posTimer.Start();
            }
            catch { }
        }

        private void StateCorr_LossFunctionAquired(object sender, LossFunctionAquiredEventArgs e)
        {
            _correlationChartValuesOrtho?.Clear();

            if(e.Ortho_HistogramX!=null) _correlationChartValuesOrtho?.AddRange(new ChartValues<ObservablePoint>(e.Ortho_HistogramX.Zip(e.Ortho_HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            _correlationChartValuesColin?.Clear();
            if(e.Colin_HistogramX!=null) _correlationChartValuesColin?.AddRange(new ChartValues<ObservablePoint>(e.Colin_HistogramX.Zip(e.Colin_HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            CorrelationSectionsCollection?.Clear();
            CorrelationVisualElementsCollection?.Clear();

            var peaks = e.Ortho_Peaks ?? e.Colin_Peaks;
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
                CorrelationSectionsCollection?.Add(axisSection);
            }
        }
    }
    
}
