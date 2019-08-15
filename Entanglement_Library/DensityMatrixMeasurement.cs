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
using System.Diagnostics;
using Extensions_Library;
using System.IO;

namespace Entanglement_Library
{
    public class DensityMatrixMeasurement
    {
        //#################################################
        //##  C O N S T A N T S 
        //#################################################

        //Standard-Basis sequence 36 elements
        public readonly List<double[]> StdBasis36 = new List<double[]>
        {
            // HWP_A, QWP_A, HWP_B, QWP_B

            //Hx
            new double[] {0,0,0,0}, //xH
            new double[] {0,0,45,0}, //xV
            new double[] {0,0,22.5,0}, //xD
            new double[] {0,0,-22.5,0}, //xA
            new double[] {0,0,0,45}, //xR
            new double[] {0,0,0,-45}, //xL

            //Vx
            new double[] {45,0,0,0}, //xH
            new double[] {45,0,45,0}, //xV
            new double[] {45,0,22.5,0}, //xD
            new double[] {45,0,-22.5,0}, //xA
            new double[] {45,0,0,45}, //xR
            new double[] {45,0,0,-45}, //xL

            //Dx
            new double[] {22.5,0,0,0}, //xH
            new double[] {22.5, 0,45,0}, //xV
            new double[] {22.5, 0,22.5,0}, //xD
            new double[] {22.5, 0,-22.5,0}, //xA
            new double[] {22.5, 0,0,45}, //xR
            new double[] {22.5, 0,0,-45}, //xL

            //Ax
            new double[] {-22.5,0,0,0}, //xH
            new double[] {-22.5, 0,45,0}, //xV
            new double[] {-22.5, 0,22.5,0}, //xD
            new double[] {-22.5, 0,-22.5,0}, //xA
            new double[] {-22.5, 0,0,45}, //xR
            new double[] {-22.5, 0,0,-45}, //xL

            //Rx
            new double[] {0,45,0,0}, //xH
            new double[] {0,45, 45,0}, //xV
            new double[] {0,45,22.5,0}, //xD
            new double[] {0,45,-22.5,0}, //xA
            new double[] {0,45,0,45}, //xR
            new double[] {0,45,0,-45}, //xL

            //Lx
            new double[] {0,-45,0,0}, //xH
            new double[] {0,-45, 45,0}, //xV
            new double[] {0,-45,22.5,0}, //xD
            new double[] {0,-45,-22.5,0}, //xA
            new double[] {0,-45,0,45}, //xR
            new double[] {0,-45,0,-45}, //xL
        };
        


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

        /// <summary>
        /// Folder for logging Density matrix Correction data. No saving if string is empty
        /// </summary>
        public string LogFolder { get; set; } = "DensityMatrix";

        //#################################################
        //##  P R I V A T E S 
        //#################################################
        private ITimeTagger _tagger;
        private IRotationStage _HWP_A;
        private IRotationStage _QWP_A;
        private IRotationStage _HWP_B;
        private IRotationStage _QWP_B;

        private CancellationTokenSource _cts;
        private List<DMBasis> _basisMeasurements;
        private List<double> _relMiddlePeakAreas;
        private Action<string> _loggerCallback;

        private string _logFolder = "";
        private string _currLogfile = "";
        private bool writeLog { get => !String.IsNullOrEmpty(_logFolder); }

        //#################################################
        //##  E V E N T S
        //#################################################
        public event EventHandler<BasisCompletedEventArgs> BasisCompleted;       
        private void OnBasisCompleted(BasisCompletedEventArgs e)
        {
            BasisCompleted?.Raise(this, e);
        }

        public event EventHandler<DensityMatrixCompletedEventArgs> DensityMatrixCompleted;
        private void OnDensityMatrixCompleted(DensityMatrixCompletedEventArgs e)
        {
            DensityMatrixCompleted?.Raise(this, e);
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


        //#################################################
        //##  M E T H O D S
        //#################################################
        public async Task MeasurePeakAreasAsync(List<double[]> basisConfigsIn = null)
        {
            //Use standard basis if no other given
            List<double[]> basisConfigs = basisConfigsIn ?? StdBasis36;
            
            if(!basisConfigs.TrueForAll(p => p.Count() == 4))
            {
                WriteLog("Wrong Basis format.");
                return;
            }

            //Set Log folder
            if (!String.IsNullOrEmpty(LogFolder))
            {
                _logFolder = Directory.CreateDirectory(LogFolder + "_" + DateTime.Now.ToString("HH_mm_ss")).FullName;
            }

            //Create Basis elements
            _basisMeasurements = new List<DMBasis>();
            foreach (var basisConfig in basisConfigs)
            {
                _basisMeasurements.Add(new DMBasis(basisConfig, ChannelA, ChannelB, 100000));
            }

            _cts = new CancellationTokenSource();

            //Measure Histograms
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            WriteLog("Start measuring histograms with " + basisConfigs.Count.ToString() + " basis");
            bool result = await Task.Run(() => DoMeasureHistograms(_cts.Token));

            //Calculate relative Peak areas from Histograms
            WriteLog("Calculating relative peak areas");
            _relMiddlePeakAreas = new List<double>();
            foreach(var basis in _basisMeasurements)
            {
                _relMiddlePeakAreas.Add(Histogram.GetRelativeMiddlePeakArea(basis.Peaks).val);
            }

            //Report
            stopwatch.Stop();    
            OnDensityMatrixCompleted(new DensityMatrixCompletedEventArgs(_relMiddlePeakAreas));
            WriteLog($"Recording density matrix complete in {stopwatch.Elapsed}.");
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

            Stopwatch stopwatch = new Stopwatch();
            int index = 1;

            //measure
            foreach (var basis in _basisMeasurements)
            {
                if (ct.IsCancellationRequested) return false;

                WriteLog("Collecting coincidences in configuration Nr." + index + ": " + basis.BasisConfig[0] + "," + basis.BasisConfig[1] + "," + basis.BasisConfig[2] + "," + basis.BasisConfig[3]);
                stopwatch.Restart();

                //Asynchronously Rotate stages to position
                Task hwpA_Task = Task.Run( () => {
                    _HWP_A.Move_Absolute(basis.BasisConfig[0]);
                    });

                Task qwpA_Task = Task.Run( () => {
                    _QWP_A.Move_Absolute(basis.BasisConfig[1]);
                    });

                Task hwpB_Task = Task.Run(() => {
                    _HWP_B.Move_Absolute(basis.BasisConfig[2]);
                    });

                Task qwpB_Task = Task.Run(() => {
                    _QWP_B.Move_Absolute(basis.BasisConfig[3]);
                    });

                //Wait for all stages to arrive at destination
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
                stopwatch.Stop();             
                OnBasisCompleted(new BasisCompletedEventArgs(basis.CrossCorrHistogram.Histogram_X, basis.CrossCorrHistogram.Histogram_Y, basis.Peaks));

                if(writeLog)
                {
                    _currLogfile = Path.Combine(_logFolder, $"Histogram_Basis_{index:D2}.txt");
                    File.WriteAllLines(_currLogfile, basis.CrossCorrHistogram.Histogram_X.Zip(basis.CrossCorrHistogram.Histogram_Y, (x, y) => x.ToString() + "\t" + y.ToString()).ToArray());
                }

                WriteLog($"Basis {index} completed in {stopwatch.Elapsed}");
                index++;
            }

            return true;
        }
        private void WriteLog(string msg, bool doLog=false)
        {
            _loggerCallback?.Invoke("Density Matrix Measurement: "+msg);
            if (doLog && !String.IsNullOrEmpty(_currLogfile)) File.AppendAllLines(_currLogfile, new string[] { msg });
        }

    }


    //#################################################
    //##  E V E N T   A R G U M E N T S
    //#################################################

    public class BasisCompletedEventArgs : EventArgs
    {
        public long[] HistogramX { get; private set; }
        public long[] HistogramY { get; private set; }
        public List<Peak> Peaks { get; private set; }

        public BasisCompletedEventArgs(long[] histX, long[] histY, List<Peak> peaks)
        {
            HistogramX = histX;
            HistogramY = histY;
            Peaks = peaks;
        }

    }

    public class DensityMatrixCompletedEventArgs : EventArgs
    {
        public List<double> RelPeakAreas { get; private set; }

        public DensityMatrixCompletedEventArgs(List<double> relPeakAreas)
        {
            RelPeakAreas = relPeakAreas;
        }
    }


}
