using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Extensions_Library;
using MathNet.Numerics;
using Stage_Library;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;

namespace Entanglement_Library
{
    /// <summary>
    /// Entangled state correction with three rotation Plates and one Timetagger
    /// </summary>
    public class StateCorrection
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        /// <summary>
        /// Desired accuracy in degree
        /// </summary>
        public double Accurracy { get; set; } = 10;
        public double[] MinPos { get; private set; }

        /// <summary>
        /// Correlation configuration, corresponding to  HV, DA
        /// </summary>
        private List<(byte cA, byte cB)> CorrConfig = new List<(byte cA, byte cB)>
        {
            (1,6),(2,5)
        };

        /// <summary>
        /// Integration time in seconds
        /// </summary>
        public int IntegrationTime { get; set; } = 5;

        /// <summary>
        /// Coarse Clock Offset between TimeTaggers
        /// </summary>
        public long TaggerOffset { get; set; } = 0;

        /// <summary>
        /// Peak Integration Time Bin
        /// </summary>
        public ulong TimeBin { get; set; } = 1000;
        
        /// <summary>
        /// T = ln(d/e)/ln(n-1) (t n)^3
        /// ------------------------------
        /// T... Overall measurement time
        /// d... Initial Range
        /// e... Target accuracy
        /// n... Number of points per iteration
        /// t... Time for one integration (+movement)
        /// </summary>
        public double TotalTime
        {
            get => Math.Log(_initRange / Accurracy) / Math.Log(2) * Math.Pow(1.3*IntegrationTime * 2, 3);
        }
        public object StopWatch { get; private set; }

        //#################################################
        //##  P R I V A T E S
        //#################################################

        //Waveplates in order
        //0... QWP
        //1... HWP
        //2... QWP
        List<IRotationStage> _rotationStages;
        ITimeTagger _tagger;
        private Action<string> _loggerCallback;
        private CancellationTokenSource _cts;

        private double _initRange = 180;

        //#################################################
        //##  E V E N T
        //#################################################

        public event EventHandler<CostFunctionAquiredEventArgs> CostFunctionAquired;
        private void OnCostFunctionAquired(CostFunctionAquiredEventArgs e)
        {
            CostFunctionAquired?.Raise(this, e);  
        }

        public event EventHandler<OptimizationCompleteEventArgs> OptimizationComplete;
        private void OnOptimizationComplete(OptimizationCompleteEventArgs e)
        {
            OptimizationComplete?.Raise(this, e);
        }

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################
        public StateCorrection(ITimeTagger tagger, List<IRotationStage> rotationStages, Action<string> loggerCallback = null)
        {
            _tagger = tagger;
            _rotationStages = rotationStages;
            _loggerCallback = loggerCallback;
        }

        public async Task StartOptimizationAsync()
        {
            _cts = new CancellationTokenSource();

            WriteLog($"Starting state correction with target accuracy = {Accurracy}deg, {IntegrationTime}s integration time");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            bool result = await Task.Run(() => DoOptimize(_cts.Token));

            WriteLog($"State correction complete in {stopwatch.Elapsed}");
        }

        private bool DoOptimize(CancellationToken ct)
        {

            Stopwatch stopwatch = new Stopwatch();

            //Make finer grid at first take to avoid false extremal points
            int initNumPoints = 3;

            WriteLog("-------------------------------------");
            WriteLog($"Initial Optimization | n={initNumPoints} | range={_initRange}");

            stopwatch.Restart();

            MinPos = GetOptimumPositions(new double[] { 0, 0, 0 }, initNumPoints, _initRange, ct);

            stopwatch.Stop();

            WriteLog($"Iteration done in {stopwatch.Elapsed} | Positions: ({MinPos[0]},{MinPos[1]},{MinPos[2]})");

            //Bisect until Accuracy is reached
            double Range = _initRange / (initNumPoints-1);

            int iteration = 1;
              
            while(Range>=Accurracy)
            {
                int n = 3;

                WriteLog("-------------------------------------");
                WriteLog($"Iteration {iteration} | n={n} | range={Range}");

                stopwatch.Restart();

                MinPos = GetOptimumPositions(MinPos, n, Range, ct);
                if (ct.IsCancellationRequested) return false;
                Range = Range / 2;

                stopwatch.Stop();

                WriteLog($"Iteration done in {stopwatch.Elapsed} | Positions: ({MinPos[0]},{MinPos[1]},{MinPos[2]})");

                iteration++;
            }

            //Move stages to optimum position
            _rotationStages[0].Move_Absolute(MinPos[0]);
            _rotationStages[1].Move_Absolute(MinPos[1]);
            _rotationStages[2].Move_Absolute(MinPos[2]);

            return true;
        }

        public void StopSynchronization()
        {
            _cts.Cancel();
        }

        private double[] GetOptimumPositions(double[] StartPos, int num_points, double range, CancellationToken ct)
        {
            double[] opt_pos = new double[] { 0, 0, 0 };

            if(_rotationStages.Count != 3)
            {
                WriteLog("Error: Number of rotation stages has to be 3");
                return opt_pos;
            }

            //Create position sets for 3 Lambda plates
            List<double[]> positions = new List<double[]>
            {
                Generate.LinearSpaced(num_points, StartPos[0] - range / 2, StartPos[0] + range / 2),
                Generate.LinearSpaced(num_points, StartPos[1] - range / 2, StartPos[1] + range / 2),
                Generate.LinearSpaced(num_points, StartPos[2] - range / 2, StartPos[2] + range / 2),
            };

            double cost, cost_min = 1.0;
            int iteration = 1;
            int totalIterations = (int) Math.Pow(num_points, 3);

            (int i0, int i1, int i2) min_indices = (0, 0, 0);
            
            //Main loop
            for (int i0=0; i0<positions[0].Length; i0++)
            {
                for (int i1 = 0; i1 < positions[0].Length; i1++)
                {
                    for (int i2 = 0; i2 < positions[0].Length; i2++)
                    {

                        if (ct.IsCancellationRequested) return opt_pos;

                        //Position rotation stages
                        double p0 = positions[0][i0];
                        double p1 = positions[1][i1];
                        double p2 = positions[2][i2];

                        Task taskpos1 = Task.Run(() => _rotationStages[0].Move_Absolute(p0));
                        Task taskpos2 = Task.Run(() => _rotationStages[1].Move_Absolute(p1));
                        Task taskpos3 = Task.Run(() => _rotationStages[2].Move_Absolute(p2));

                        Task.WhenAll(taskpos1, taskpos2, taskpos3).GetAwaiter().GetResult();

                        //Register costfunction value
                        cost = GetCostFunction(ct);

                        if (cost < cost_min)
                        {
                            min_indices = (i0, i1, i2);
                            cost_min = cost;
                        }
                                       
                        WriteLog($"Position Nr.{iteration}/{totalIterations} :({p0:F2},{p1:F2},{p2:F2}): {cost:F3}");

                        iteration++;
                    }
                }                  
            }

            opt_pos = new double[] { positions[0][min_indices.i0], positions[1][min_indices.i1], positions[2][min_indices.i2] };
            return opt_pos;
        }
        
        /// <summary>
        /// Returns relative middle peak area of combined histogram
        /// </summary>
        /// <returns></returns>
        private double GetCostFunction(CancellationToken ct)
        {
            ulong timewindow = 100000;
            Histogram hist = new Histogram(CorrConfig, timewindow);
            Kurolator corr = new Kurolator(new List<CorrelationGroup> { hist }, 100000);

            //Collect timetags
            _tagger.ClearTimeTagBuffer();        
            _tagger.StartCollectingTimeTagsAsync();
          
            for(int i=0; i<IntegrationTime; i++)
            {
                if (ct.IsCancellationRequested) return 1.0;
                Thread.Sleep(1000);
            }

            _tagger.StopCollectingTimeTags();       

            List<TimeTags> tts = _tagger.GetAllTimeTags();
          
            foreach(TimeTags tt in tts ) corr.AddCorrelations(tt,tt, TaggerOffset);

            double cost = Histogram.GetRelativeMiddlePeakArea(hist.GetPeaks(6250, 0.1, true, TimeBin));

            OnCostFunctionAquired(new CostFunctionAquiredEventArgs(hist.Histogram_X, hist.Histogram_Y,cost));

            return cost;
        }

        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke("State correction: " + msg);
        }
    }

    public class CostFunctionAquiredEventArgs : EventArgs
    {
        public long[] HistogramX { get; private set; }
        public long[] HistogramY { get; private set; }
        public double Cost { get; private set; }

        public CostFunctionAquiredEventArgs(long[] histX, long[] histY, double cost)
        {
            HistogramX = histX;
            HistogramY = histY;
            Cost = cost;
        }
    }
    
    public class OptimizationCompleteEventArgs : EventArgs
    {

    }
}
