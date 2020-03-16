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
    class PolCorrectionControlViewModel : ViewModelBase
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

        private List<double> _targetPos = new List<double>(new double[] { 1, 2, 3 });
        public List<double> TargetPos {
            get { return _targetPos; }
            set
            {
                _targetPos = value;
                OnPropertyChanged("TargetPos");
            }
        }

        public ObservableCollection<double> _currPos = new ObservableCollection<double>(new double[] { 0, 0, 0 });
        public ObservableCollection<double> CurrPos
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

        private ChartValues<ObservablePoint> _correlationChartValues;
        private LineSeries _correlationLineSeries;

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
                _EQKDServer.StartFiberCorrectionAsync().SafeFireAndForget();
            });
            StartKeyGenerationCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.PacketSize = PacketSize;
                 _EQKDServer.StartKeyGeneration().SafeFireAndForget();
            });

            CancelCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.StopCorrection();
                _EQKDServer.StopKeyGeneration();
            });

            GoToPositionCommand = new RelayCommand<object>((o) =>
            {
                _EQKDServer.FiberCorrection.GotoPosition(TargetPos.ToList()).SafeFireAndForget();
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

            _correlationChartValues = new ChartValues<ObservablePoint> { };
            _correlationLineSeries = new LineSeries()
            {
                Title = "Polarization correlations",
                PointGeometrySize = 0,
                LineSmoothness = 0.0,
                Values = _correlationChartValues

            };
            CorrelationCollection.Add(_correlationLineSeries);

            //Set and start Position timer
            _posTimer.Elapsed += (sender, e) =>
            {
                CurrPos = new ObservableCollection<double>(_EQKDServer?.FiberCorrection?.StagePositions);
            };
            _posTimer.Start();
        }

        private void StateCorr_LossFunctionAquired(object sender, LossFunctionAquiredEventArgs e)
        {
            _correlationChartValues.Clear();
            _correlationChartValues.AddRange(new ChartValues<ObservablePoint>(e.HistogramX.Zip(e.HistogramY, (X, Y) => new ObservablePoint(X / 1000.0, Y))));

            CorrelationSectionsCollection.Clear();
            CorrelationVisualElementsCollection.Clear();

            foreach (Peak peak in e.Peaks)
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
