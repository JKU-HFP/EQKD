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

        private long _packetTimeSpan;

        public long PacketTImeSpan
        {
            get { return _packetTimeSpan; }
            set 
            {
                _packetTimeSpan = value;
                OnPropertyChanged("PacketTImeSpan");
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

        public RelayCommand<object> CancelCommand { get; private set; }

        public RelayCommand<object> GoToPositionCommand { get; private set; }

        public PolCorrectionControlViewModel()
        {
            //Map RelayCommmands
            StartCorrectionCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.PacketSize = PacketSize;
                _EQKDServer.FiberCorrection.PacketTimeSpan = PacketTImeSpan;
                _EQKDServer.StartFiberCorrectionAsync().SafeFireAndForget();
            });
            StartKeyGenerationCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.PacketSize = PacketSize;
                _EQKDServer.PacketTImeSpan = PacketTImeSpan;
                 _EQKDServer.StartKeyGenerationAsync().SafeFireAndForget();
            });

            CancelCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.StopCorrection();
                _EQKDServer.StopKeyGeneration();
            });

            GoToPositionCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.GotoPosition(TargetPos.Select(t=>t.Value).ToList()).SafeFireAndForget();
            },
            (o) => !_EQKDServer?.FiberCorrection?.IsActive ?? false);

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


            //Set and start Position timer
            _posTimer.Elapsed += (sender, e) =>
            {
                CurrPos = new ObservableCollection<StagePosModel>(_EQKDServer?.FiberCorrection?.StagePositions.Select(pos => new StagePosModel { Value = pos }).ToList());
            };
            _posTimer.Start();
        }

        private void StateCorr_LossFunctionAquired(object sender, LossFunctionAquiredEventArgs e)
        {
            _correlationChartValuesOrtho.Clear();
            _correlationChartValuesOrtho.AddRange(new ChartValues<ObservablePoint>(e.Ortho_HistogramX.Zip(e.Ortho_HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            _correlationChartValuesColin.Clear();
            _correlationChartValuesColin.AddRange(new ChartValues<ObservablePoint>(e.Colin_HistogramX.Zip(e.Colin_HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            CorrelationSectionsCollection.Clear();
            CorrelationVisualElementsCollection.Clear();

            foreach (Peak peak in e.Ortho_Peaks)
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
    }
    
}
