using Extensions_Library;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimeTagger_Library;
using TimeTagger_Library.Correlation;
using TimeTagger_Library.TimeTagger;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Distributions;

namespace QKD_Library
{
    public class Synchronization
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        public ulong TimeBin { get; set; } = 1000;

        //Clock Synchronisation

        public ulong ClockSyncTimeWindow { get; set; } = 100000;
        public long GlobalClockOffset { get; set; } = 0;

        public double LinearDriftCoefficient { get; set; } = 0;
        public double LinearDriftCoeff_Var { get; set; } = 0.001E-5;
        public int LinearDriftCoeff_NumVar { get; set; } = 2;
        
        public double STD_Tolerance { get; set; } = 4000;
        public double PVal { get; set; } = 0;
        public ulong ExcitationPeriod { get; set; } = 200000000; //12500;



        //Correlation Synchronization

        public byte CorrChan_Tagger1 { get; set; } = 2;
        public byte CorrChan_Tagger2 { get; set; } = 7;
        /// <summary>
        /// Offset by relative fiber distance of Alice and Bob
        /// </summary>
        public long FiberOffset { get; set; } = 0;
        public ulong CorrSyncTimeWindow { get; set; } = 1000000;
        public long CorrPeakOffset_Tolerance { get; set; } = 2000;

        //#################################################
        //##  P R I V A T E S
        //#################################################

        private ITimeTagger _tagger1;
        private ITimeTagger _tagger2;

        private Action<string> _loggerCallback;
        private Kurolator _kurolator;
        
        //#################################################
        //##  E V E N T S 
        //#################################################

        public event EventHandler<SyncClocksCompleteEventArgs> SyncClocksComplete;
        
        private void OnSyncClocksComplete(SyncClocksCompleteEventArgs e)
        {
            SyncClocksComplete?.Raise(this, e);
        }

        public event EventHandler<SyncCorrResults> SyncCorrComplete;

        private void OnSyncCorrComplete(SyncCorrCompleteEventArgs e)
        {
            SyncCorrComplete?.Raise(this, e);
        }

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################

        public Synchronization(ITimeTagger tagger1, ITimeTagger tagger2=null, Action<string> loggerCallback=null)
        {
            _loggerCallback = loggerCallback;
            _tagger1 = tagger1;
            _tagger2 = tagger2;
        }

        //#################################################
        //##  M E T H O D S 
        //#################################################
        public TimeTags GetSingleTimeTags(int TaggerNr, int packetSize=100000)
        {
            if(TaggerNr<0 || TaggerNr>1 || (TaggerNr==1 && _tagger2==null))
            {
                WriteLog("Invalid Time Tagger Number.");
                return null;
            }

            ITimeTagger tagger = TaggerNr == 0 ? _tagger1 : _tagger2;

            tagger.PacketSize = packetSize;

            tagger.ClearTimeTagBuffer();
            tagger.StartCollectingTimeTagsAsync();

            TimeTags tt = null;
            while (!tagger.GetNextTimeTags(out tt)) Thread.Sleep(10);

            tagger.StopCollectingTimeTags();

            return tt;
        }

        public SyncClockResults GetSyncedTimeTags(int packetSize=100000)
        {
            if(_tagger1 == null || _tagger2 == null)
            {
                WriteLog("One or both timetaggers not ready.");
                return null;
            }

            TimeTags ttA = null;
            TimeTags ttB = null;

            _tagger1.PacketSize = _tagger2.PacketSize = packetSize;

            //Collect Timetags
            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            _tagger1.StartCollectingTimeTagsAsync();
            _tagger2.StartCollectingTimeTagsAsync();

            WriteLog("Requesting timetags");

            while (!_tagger1.GetNextTimeTags(out ttA)) Thread.Sleep(10);
            while(!_tagger2.GetNextTimeTags(out ttB)) Thread.Sleep(10);

            _tagger1.StopCollectingTimeTags();
            _tagger2.StopCollectingTimeTags();
            
            SyncClockResults syncClockres = SyncClocksAsync(ttA, ttB).GetAwaiter().GetResult();

           return syncClockres;
                      
        }

        private async Task<SyncClockResults> SyncClocksAsync(TimeTags ttAlice, TimeTags ttBob)
        {
            Stopwatch sw = new Stopwatch();

            SyncClockResults syncres = await Task<SyncClockResults>.Run(() =>
            {                           
                //Initialize
                WriteLog("Start synchronizing clocks");
                                
                sw.Start();

                //Get packet timespan
                var alice_first = ttAlice.time[0];
                var alice_last = ttAlice.time[ttAlice.time.Length - 1];
                var bob_first = ttBob.time[0];
                var bob_last = ttBob.time[ttBob.time.Length - 1];
                var alice_diff = alice_last - alice_first;
                var bob_diff = bob_last - bob_first;

                TimeSpan packettimespan = new TimeSpan(0, 0, 0, 0, (int)(Math.Min(alice_diff, bob_diff) * 1E-9));

                GlobalClockOffset = (ttAlice.time[0] - ttBob.time[0]);

                //----------------------------------------------------------------
                //Compensate Bobs tags for a variation of linear drift coefficients
                //-----------------------------------------------------------------

                long starttime = ttBob.time[0];

                int[] variation_steps = Generate.LinearRangeInt32(-LinearDriftCoeff_NumVar, LinearDriftCoeff_NumVar);
                List<double> linDriftCoefficients = variation_steps.Select(s => LinearDriftCoefficient + s*LinearDriftCoeff_Var).ToList();
                List<long[]> comp_times_list = linDriftCoefficients.Select((c) => ttBob.time.Select(t => (long)(t + (t - starttime) * c)).ToArray()).ToList();
                List<TimeTags> ttBob_comp_list = comp_times_list.Select( (ct) => new TimeTags(ttBob.chan, ct)).ToList();

                //------------------------------------------------------------------------------
                //Generate Histograms and Fittings for all Compensation Configurations
                //------------------------------------------------------------------------------
                List<DriftCompResult> driftCompResults = new List<DriftCompResult>() { };

                //Channel configuration
                byte oR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
                byte oD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;
                //List<(byte cA, byte cB)> chan_config = new List<(byte cA, byte cB)>
                //{
                //    //Clear Basis
                //    (0,5),(0,6),(0,7),(0,8),
                //    (1,5),(1,6),(1,7),(1,8),
                //    (2,5),(2,6),(2,7),(2,8),
                //    (3,5),(3,6),(3,7),(3,8),

                //    //Obscured Basis
                //    (0,oR),(0,oD),(1,oR),(1,oD),(2,oR),(2,oD),(3,oR),(3,oD)
                //};
                List<(byte cA, byte cB)> chan_config = new List<(byte cA, byte cB)>
                {
                    //Function generator
                    (0,1)
                };

                for (int drift_index = 0; drift_index<variation_steps.Length; drift_index++)
                {
                    DriftCompResult driftCompResult = new DriftCompResult(drift_index);

                    Histogram hist = new Histogram(chan_config, ClockSyncTimeWindow, (long)TimeBin);
                    _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, ClockSyncTimeWindow);
                    _kurolator.AddCorrelations(ttAlice, ttBob_comp_list[drift_index], GlobalClockOffset + FiberOffset);
                   
                    //----- Analyse peaks ----
                     
                    List<Peak> peaks = hist.GetPeaks(peakBinning: 2000);
                    
                    //Number of peaks plausible?
                    int numExpectedPeaks = (int)(2 * ClockSyncTimeWindow / ExcitationPeriod)+1;
                    if (peaks.Count < numExpectedPeaks || peaks.Count > numExpectedPeaks + 1)
                    {
                        //Return standard result
                                                         
                        driftCompResult.LinearDriftCoeff = linDriftCoefficients[drift_index];
                        driftCompResult.IsFitSuccessful = false;
                        driftCompResult.HistogramX = hist.Histogram_X;
                        driftCompResult.HistogramY = hist.Histogram_Y;
                        driftCompResult.Peaks = peaks;
                        driftCompResult.MiddlePeak = null;
                        driftCompResult.FittedMeanTime = 0;
                        driftCompResult.Sigma = (-1,-1);                     
                    }
                    else
                    {
                        //FITTING

                        //Get Middle Peak
                        Peak MiddlePeak = peaks.Where(p => Math.Abs(p.MeanTime) == peaks.Select(a => Math.Abs(a.MeanTime)).Min()).FirstOrDefault();

                        //Fit peaks
                        Vector<double> initial_guess = new DenseVector(new double[] { MiddlePeak.MeanTime, 30, 1000 });
                        Vector<double> lower_bound = new DenseVector(new double[] { (double)MiddlePeak.MeanTime - ExcitationPeriod / 2, 5, 1000 });
                        Vector<double> upper_bound = new DenseVector(new double[] { (double)MiddlePeak.MeanTime + ExcitationPeriod / 2, 1000000, ExcitationPeriod * 0.75 });

                        Vector<double> XVals = new DenseVector(hist.Histogram_X.Select(x => (double)x).ToArray());
                        Vector<double> YVals = new DenseVector(hist.Histogram_Y.Select(y => (double)y).ToArray());

                        Func<Vector<double>, Vector<double>, Vector<double>> obj_function = (Vector<double> p, Vector<double> x) =>
                        {
                        //Parameter Vector:
                        //0 ... height
                        //1 ... mean value
                        //2 ... std deviation

                        Vector<double> result_vector = new DenseVector(x.Count);

                            int[] x0_range = Generate.LinearRangeInt32(-numExpectedPeaks, numExpectedPeaks);
                            double[] x0_vals = x0_range.Select(xv => p[1] + xv * (double)ExcitationPeriod).ToArray();

                            for (int i = 0; i < x.Count; i++)
                            {
                                result_vector[i] = p[0] * x0_vals.Select(xv => Normal.PDF(xv, p[2], x[i])).Sum();
                            }

                            return result_vector;
                        };

                        IObjectiveModel objective_model = ObjectiveFunction.NonlinearModel(obj_function, XVals, YVals);

                        LevenbergMarquardtMinimizer solver = new LevenbergMarquardtMinimizer(maximumIterations: 1000);
                        NonlinearMinimizationResult minimization_result = solver.FindMinimum(objective_model, initial_guess, lowerBound: lower_bound, upperBound: upper_bound);

                        //Write results                                               
                        driftCompResult.LinearDriftCoeff = linDriftCoefficients[drift_index];
                        driftCompResult.IsFitSuccessful = minimization_result.ReasonForExit != ExitCondition.ExceedIterations;
                        driftCompResult.HistogramX = hist.Histogram_X;
                        driftCompResult.HistogramY = hist.Histogram_Y;
                        driftCompResult.Peaks = peaks;
                        driftCompResult.MiddlePeak = MiddlePeak;
                        if (driftCompResult.IsFitSuccessful)
                        {
                            driftCompResult.HistogramYFit = obj_function(minimization_result.MinimizingPoint, XVals).ToArray();
                            driftCompResult.FittedMeanTime = minimization_result.MinimizingPoint[1];
                            driftCompResult.Sigma = (minimization_result.MinimizingPoint[2], minimization_result.StandardErrors[2]);
                        }
                    }

                    driftCompResults.Add(driftCompResult);
                }

                sw.Stop();

                //------------------------------------------------------------------------------
                // Rate fitting results
                //------------------------------------------------------------------------------

                //No fitting found
                if (driftCompResults.Where( r => r.IsFitSuccessful==true).Count()<=0)
                {
                    DriftCompResult initDriftResult = driftCompResults[driftCompResults.Count / 2];

                    return new SyncClockResults()
                    {
                        PeaksFound = false,
                        IsClocksSync = false,
                        HistogramX = initDriftResult.HistogramX,
                        HistogramY = initDriftResult.HistogramY,
                        Peaks = initDriftResult.Peaks,
                        HistogramYFit = initDriftResult.HistogramYFit,
                        NewLinearDriftCoeff = initDriftResult.LinearDriftCoeff,
                        CompTimeTags_Bob = ttBob_comp_list[initDriftResult.Index],
                        MiddlePeak = initDriftResult.MiddlePeak,
                        ProcessingTime = sw.Elapsed,
                        TimeTags_Alice = ttAlice,
                        TimeTags_Bob = ttBob
                    };
                }

                //Find best fit
                double min_sigma = driftCompResults.Where(d => d.IsFitSuccessful).Select(d => d.Sigma.val).Min();
                DriftCompResult opt_driftResults = driftCompResults.Where(d => d.Sigma.val == min_sigma).FirstOrDefault();
                               
                //Write statistics
               
                WriteLog($"Sync cycle complete in {sw.Elapsed} | TimeSpan: {packettimespan}| Fitted FWHM: {opt_driftResults.Sigma.val:F2}({opt_driftResults.Sigma.err:F2})" +
                         $" | Pos: {opt_driftResults.MiddlePeak.MeanTime:F2} | Fitted pos: {opt_driftResults.FittedMeanTime:F2} | new DriftCoeff {opt_driftResults.LinearDriftCoeff}({LinearDriftCoefficient-opt_driftResults.LinearDriftCoeff})");

                //Define new Drift Coefficient
                LinearDriftCoefficient = opt_driftResults.LinearDriftCoeff;

                return new SyncClockResults()
                {
                    PeaksFound = true,
                    IsClocksSync = opt_driftResults.Sigma.val <= STD_Tolerance,
                    HistogramX = opt_driftResults.HistogramX,
                    HistogramY = opt_driftResults.HistogramY,
                    HistogramYFit = opt_driftResults.HistogramYFit,
                    Sigma = opt_driftResults.Sigma,
                    NewLinearDriftCoeff = opt_driftResults.LinearDriftCoeff,
                    Peaks = opt_driftResults.Peaks,
                    MiddlePeak = opt_driftResults.MiddlePeak,
                    CompTimeTags_Bob = ttBob_comp_list[opt_driftResults.Index],
                    ProcessingTime = sw.Elapsed,
                    TimeTags_Alice = ttAlice,
                    TimeTags_Bob = ttBob
                };

            });

            OnSyncClocksComplete(new SyncClocksCompleteEventArgs(syncres));

            return syncres;
        }

        public async Task<SyncCorrResults> SyncCorrelationAsync(TimeTags ttAlice, TimeTags ttBob)
        {

            SyncCorrResults syncRes = await Task<SyncCorrResults>.Run( () => 
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();

                Histogram hist = new Histogram(new List<(byte cA, byte cB)> { (CorrChan_Tagger1, CorrChan_Tagger2) }, CorrSyncTimeWindow, (long)TimeBin);
                _kurolator = new Kurolator(new List<CorrelationGroup> { hist }, CorrSyncTimeWindow);

                _kurolator.AddCorrelations(ttAlice, ttBob, GlobalClockOffset + FiberOffset);

                //Find correlated peak
                List<Peak> peaks = hist.GetPeaks(peakBinning:2000);
                double av_area = peaks.Select(p => p.Area).Average();
                double av_area_err = Math.Sqrt(av_area);

                //Find peak most outside outside the average
                Peak CorrPeak = peaks.Where(p => Math.Abs(p.Area-av_area) == peaks.Select(a => Math.Abs(a.Area-av_area)).Min()).FirstOrDefault();

                //Is peak area statistically significant?
                bool isCorrSync = false;
                bool corrPeakFound = false;

                TimeTags comptimetags_Bob = null;

                if (Math.Abs(CorrPeak.Area - av_area) > 2*av_area_err)
                {
                    corrPeakFound = true;
                    if (Math.Abs(CorrPeak.MeanTime) < CorrPeakOffset_Tolerance) isCorrSync = true;

                    FiberOffset += CorrPeak.MeanTime;

                    //Correct Bobs tags
                    comptimetags_Bob = new TimeTags(ttBob.chan, ttBob.time.Select(t => t - (GlobalClockOffset + FiberOffset)).ToArray());
                }

                sw.Stop();

                return new SyncCorrResults()
                {
                    HistogramX = hist.Histogram_X,
                    HistogramY = hist.Histogram_Y,
                    CorrPeakPos = CorrPeak.MeanTime,
                    CorrPeakFound = corrPeakFound,
                    IsCorrSync = isCorrSync,
                    CompTimeTags_Bob = comptimetags_Bob,
                    ProcessingTime = sw.Elapsed
                };
            });

            OnSyncCorrComplete(new SyncCorrCompleteEventArgs(syncRes));

            return syncRes;
        }

        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke("Sync: " + msg);
        }
    }

    public class SyncClocksCompleteEventArgs : EventArgs
    {
        public SyncClockResults SyncRes { get; private set;} 
        public SyncClocksCompleteEventArgs(SyncClockResults syncRes)
        {
            SyncRes = syncRes;
        }
    }
    
    public class SyncCorrCompleteEventArgs : EventArgs
    {
        public SyncCorrResults SyncRes { get; private set; }
        public SyncCorrCompleteEventArgs(SyncCorrResults syncRes)
        {
            SyncRes = syncRes;
        }
    }

}
