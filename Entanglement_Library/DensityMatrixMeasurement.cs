using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library.TimeTagger;
using TimeTagger_Library.Correlation;
using Stage_Library;
using System.Threading;
using TimeTagger_Library;

namespace Entanglement_Library
{
    public class DensityMatrixMeasurement
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        /// <summary>
        /// Integration time per Basis in seconds
        /// </summary>
        public int IntegrationTime { get; set; } = 5;
        public uint ChannelA { get; set; } = 0;
        public uint ChannelB { get; set; } = 1;
        public long OffsetChanB { get; set; } = 0;

        //#################################################
        //##  P R I V A T E S 
        //#################################################
        private ITimeTagger _tagger;
        private IRotationStage _HWP_A;
        private IRotationStage _QWP_A;
        private IRotationStage _HWP_B;
        private IRotationStage _QWP_B;

        private CancellationTokenSource _cts;
        private List<Basis> _basisMeasurements;
        private List<Histogram> _basisHistograms;
        private Kurolator correlator;
        private List<double> _relMiddlePeakAreas;
        private Action<string> _loggerCallback;

        //#################################################
        //##  E V E N T S
        //#################################################
        public event EventHandler<BasisCompletedEventArgs> BasisCompleted;       
        private void OnBasisCompleted(BasisCompletedEventArgs e)
        {
            BasisCompleted?.Invoke(this, e);
        }

        public event EventHandler<DensityMatrixCompletedEventArgs> DensityMatrixCompleted;
        private void OnDensityMatrixCompleted(DensityMatrixCompletedEventArgs e)
        {
            DensityMatrixCompleted?.Invoke(this, e);
        }


        //#################################################
        //##  C O N S T R U C T O R
        //#################################################
        public DensityMatrixMeasurement(ITimeTagger tagger, IRotationStage HWP_A, IRotationStage QWP_A, IRotationStage HWP_B, IRotationStage QWP_B, Action<string> loggerCallback)
        {
            _tagger = tagger;

            _HWP_A = HWP_A;
            _QWP_A = QWP_A;
            _HWP_B = HWP_B;
            _QWP_B = QWP_B;

            _loggerCallback = loggerCallback;
        }     
        
        public async Task MeasurePeakAreasAsync(List<double[]> basisConfigs)
        {
            if(!basisConfigs.TrueForAll(p => p.Count() == 4))
            {
                WriteLog("Wrong Basis format.");
                return;
            }

            //Create Basis elements
            _basisMeasurements = new List<Basis>();
            foreach (var basisConfig in basisConfigs)
            {
                _basisMeasurements.Add(new Basis(basisConfig, ChannelA, ChannelB, 100000));
            }

            _cts = new CancellationTokenSource();

            //Measure Histograms
            bool result = await Task.Run(() => DoMeasureHistograms(_cts.Token));

            //Calculate relative Peak areas from Histograms
            _relMiddlePeakAreas = new List<double>();
            foreach(var basis in _basisMeasurements)
            {
                IEnumerable<Peak> e_MiddlePeak = basis.Peaks.Where(p => Math.Abs(p.MeanTime) == basis.Peaks.Select(a => Math.Abs(a.MeanTime)).Min());
                IEnumerable<Peak> e_SidePeaks = basis.Peaks.Except(e_MiddlePeak);

                var A_Middle = e_MiddlePeak.Select(p => p.Area).Average();
                var A_Side_Average = e_SidePeaks.Select(p => p.Area).Average();

                _relMiddlePeakAreas.Add(A_Middle / A_Side_Average);
            }

            //Report
            OnDensityMatrixCompleted(new DensityMatrixCompletedEventArgs(_relMiddlePeakAreas));
        }
        
        public void CancelMeasurement()
        {
            _cts.Cancel();
        }

        private bool DoMeasureHistograms(CancellationToken ct)
        {
            _relMiddlePeakAreas = new List<double>();

            //Initialize photon buffer
            _tagger.StopCollectingTimeTags();
            _tagger.ClearTimeTagBuffer();

            //measure
            foreach (var basis in _basisMeasurements)
            {
                if (ct.IsCancellationRequested) return false;

                //Asynchronously Rotate stages to position
                Task hwpA_Task = Task.Run( () => _HWP_A.Move_Absolute(basis.BasisConfig[0]) );
                Task qwpA_Task = Task.Run( () =>_QWP_A.Move_Absolute(basis.BasisConfig[1]) );
                Task hwpB_Task = Task.Run(() =>_HWP_B.Move_Absolute(basis.BasisConfig[0]) );
                Task qwpB_Task = Task.Run(() => _QWP_B.Move_Absolute(basis.BasisConfig[1]) );

                Task.WhenAll(hwpA_Task, qwpA_Task, hwpB_Task, qwpB_Task).GetAwaiter().GetResult();

                //Start collecting timetags
                _tagger.StartCollectingTimeTagsAsync();

                //Wait integration time
                for(int i=0; i<IntegrationTime; i++)
                {
                    if (ct.IsCancellationRequested) return false;
                    Thread.Sleep(1000);
                }

                //Stop collecting timetags
                _tagger.StopCollectingTimeTags();

                //Create Histogram
                List<TimeTags> tt = _tagger.GetAllTimeTags();
                basis.CreateHistogram(tt,OffsetChanB);

                //Report
                OnBasisCompleted(new BasisCompletedEventArgs(basis.CrossCorrHistogram.Histogram_X, basis.CrossCorrHistogram.Histogram_Y));
            }

            return true;
        }
        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke("Density Matrix Measurement: "+msg);
        }

    }

    public class BasisCompletedEventArgs
    {
        public long[] HistogramX { get; private set; }
        public long[] HistogramY { get; private set; }

        public BasisCompletedEventArgs(long[] histX, long[] histY)
        {
            HistogramX = histX;
            HistogramY = histY;
        }

    }

    public class DensityMatrixCompletedEventArgs
    {
        public List<double> RelPeakAreas { get; private set; }

        public DensityMatrixCompletedEventArgs(List<double> relPeakAreas)
        {
            RelPeakAreas = relPeakAreas;
        }
    }


}
