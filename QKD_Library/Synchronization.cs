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

        public ulong ClockTimeBin { get; set; } = 1000;

        //Clock Synchronisation

        public ulong ClockSyncTimeWindow { get; set; } = 100000;
        public long GlobalClockOffset { get; set; } = 0;

        public double LinearDriftCoefficient { get; set; } = 0;
        public double LinearDriftCoeff_Var { get; set; } = 0.001E-5;
        public int LinearDriftCoeff_NumVar { get; set; } = 2;
        
        public double STD_Tolerance { get; set; } = 1700;
        public long GroundlevelTimebin { get; set; } = 2000;
        public long GroundlevelTolerance { get; set; } = 10;
        public double PVal { get; set; } = 0;
        public ulong ExcitationPeriod { get; set; } = 12500; //200000000; 



        //Correlation Synchronization

        public ulong CorrTimeBin { get; set; } = 512;
        public long CoarseFiberOffset { get; set; } = 0;
        /// <summary>
        /// Offset by relative fiber distance of Alice and Bob
        /// </summary>
        public long FiberOffset { get; set; } = 0;
        public ulong CorrSyncTimeWindow { get; set; } = 100000;
        public long CorrPeakOffset_Tolerance { get; set; } = 2000;

        //#################################################
        //##  P R I V A T E S
        //#################################################

        private ITimeTagger _tagger1;
        private ITimeTagger _tagger2;

        private Action<string> _loggerCallback;

        private Kurolator _clockKurolator;
        private Kurolator _corrKurolator;
        private Histogram _corrHist;

        private bool _firstSyncDone = false;
        private long _bobLastStopTime = 0;

        List<(byte cA, byte cB)> _clockChanConfig = new List<(byte cA, byte cB)>
        {
            //Clear Basis
            //(0,5),(0,6),(0,7),(0,8),
            //(1,5),(1,6),(1,7),(1,8),
            //(2,5),(2,6),(2,7),(2,8),
            //(3,5),(3,6),(3,7),(3,8),

           // Obscured Basis
            //(0,oR),(0,oD),(1,oR),(1,oD),(2,oR),(2,oD),(3,oR),(3,oD)

            //Funky generator
            (0,7)
        };
        //List<(byte cA, byte cB)> _clockChanConfig = new List<(byte cA, byte cB)>
        //        {
        //            //Laser
        //            (2,7)

        //            //Funky generator
        //            //(0,1)
        //        };

        List<(byte cA, byte cB)> _corrChanConfig = new List<(byte cA, byte cB)>
        {
            (2,7)
        };

        //#################################################
        //##  E V E N T S 
        //#################################################

        public event EventHandler<SyncClocksCompleteEventArgs> SyncClocksComplete;
        
        private void OnSyncClocksComplete(SyncClocksCompleteEventArgs e)
        {
            SyncClocksComplete?.Raise(this, e);
        }

        public event EventHandler<SyncCorrCompleteEventArgs> SyncCorrComplete;

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

            //Refresh sync rate
            _tagger2.SyncRate = _tagger1.SyncRate;

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

        public void Reset()
        {
            _tagger1.StopCollectingTimeTags();
            _tagger2.StopCollectingTimeTags();

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            _firstSyncDone = false;
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
                
              
                 GlobalClockOffset = (alice_first - bob_first);

                //----------------------------------------------------------------
                //Compensate Bobs tags for a variation of linear drift coefficients
                //-----------------------------------------------------------------

                int[] variation_steps = Generate.LinearRangeInt32(-LinearDriftCoeff_NumVar, LinearDriftCoeff_NumVar);
                List<double> linDriftCoefficients = variation_steps.Select(s => LinearDriftCoefficient + s*LinearDriftCoeff_Var).ToList();
                List<long[]> comp_times_list = linDriftCoefficients.Select((c) => ttBob.time.Select(t => (long)(t + (t - bob_first) * c)).ToArray()).ToList();
                List<TimeTags> ttBob_comp_list = comp_times_list.Select( (ct) => new TimeTags(ttBob.chan, ct)).ToList();

                //------------------------------------------------------------------------------
                //Generate Histograms and Fittings for all Compensation Configurations
                //------------------------------------------------------------------------------
                List<DriftCompResult> driftCompResults = new List<DriftCompResult>() { };

                //Channel configuration
                byte oR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
                byte oD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;

                for (int drift_index = 0; drift_index<variation_steps.Length; drift_index++)
                {
                    DriftCompResult driftCompResult = new DriftCompResult(drift_index);

                    Histogram hist = new Histogram(_clockChanConfig, ClockSyncTimeWindow, (long)ClockTimeBin);
                    _clockKurolator = new Kurolator(new List<CorrelationGroup> { hist }, ClockSyncTimeWindow);
                    _clockKurolator.AddCorrelations(ttAlice, ttBob_comp_list[drift_index], GlobalClockOffset + CoarseFiberOffset);
                   
                    //----- Analyse peaks ----
                     
                    List<Peak> peaks = hist.GetPeaks(peakBinning: 2000);
                    
                    //Number of peaks plausible?
                    int numExpectedPeaks = (int)(2 * ClockSyncTimeWindow / ExcitationPeriod)+1;
                    if (peaks.Count < numExpectedPeaks-1 || peaks.Count > numExpectedPeaks + 1)
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
                        Vector<double> initial_guess = new DenseVector(new double[] { MiddlePeak.MeanTime, 30, 500, 0 });
                        Vector<double> lower_bound = new DenseVector(new double[] { (double)MiddlePeak.MeanTime - ExcitationPeriod / 2, 5, 200, 0 });
                        Vector<double> upper_bound = new DenseVector(new double[] { (double)MiddlePeak.MeanTime + ExcitationPeriod / 2, 50000000, ExcitationPeriod * 0.75, 100 });

                        Vector<double> XVals = new DenseVector(hist.Histogram_X.Select(x => (double)x).ToArray());
                        Vector<double> YVals = new DenseVector(hist.Histogram_Y.Select(y => (double)y).ToArray());

                        Func<Vector<double>, Vector<double>, Vector<double>> obj_function = (Vector<double> p, Vector<double> x) =>
                        {
                            //Parameter Vector:
                            //0 ... mean value
                            //1 ... height
                            //2 ... std deviation
                            //3 ... ground level

                            Vector<double> result_vector = new DenseVector(x.Count);

                            int[] x0_range = Generate.LinearRangeInt32(-numExpectedPeaks/2, numExpectedPeaks/2);
                            double[] x0_vals = x0_range.Select(xv => p[0] + xv * (double)ExcitationPeriod).ToArray();

                            for (int i = 0; i < x.Count; i++)
                            {
                                result_vector[i] = p[3] + x0_vals.Select(xv => p[1] * Normal.PDF(xv, p[2], x[i])).Sum();
                            }

                            return result_vector;
                        };

                        IObjectiveModel objective_model = ObjectiveFunction.NonlinearModel(obj_function, XVals, YVals);

                        LevenbergMarquardtMinimizer solver = new LevenbergMarquardtMinimizer(maximumIterations: 100);
                        NonlinearMinimizationResult minimization_result = solver.FindMinimum(objective_model, initial_guess, lowerBound: lower_bound, upperBound: upper_bound);

                        //CHECK CONCIDENCES IN BETWEEN PEAKS
                        long BetweenCoinc = 0;
                        
                        foreach(var peak in peaks.Except(new List<Peak> { MiddlePeak }).Skip(1).Take(peaks.Count-3))
                        {
                            int sign = peak.MeanTime < MiddlePeak.MeanTime ? 1 : -1;

                            long sum_coinc = 0;
                            int start_ind = peak.MeanIndex + sign * (int)((long)ExcitationPeriod / (2 * hist.Hist_Resolution));
                            long start_time = hist.Histogram_X[start_ind];
                            //Search backwards
                            int ind = start_ind;
                            while (hist.Histogram_X[ind]>= start_time - (long)GroundlevelTimebin && ind > 0)
                            {                               
                                sum_coinc += hist.Histogram_Y[ind];
                                ind--;            
                            }
                            //Search foward
                            ind = start_ind+1;
                            while (hist.Histogram_X[ind] <= start_time + (long)GroundlevelTimebin && ind < hist.Histogram_X.Length - 2)
                            {
                                sum_coinc += hist.Histogram_Y[ind];
                                ind++;
                            }
                            BetweenCoinc += sum_coinc;
                        }

                        //Write results                                               
                        driftCompResult.LinearDriftCoeff = linDriftCoefficients[drift_index];

                        driftCompResult.IsFitSuccessful = (minimization_result.ReasonForExit ==
                        ExitCondition.Converged || minimization_result.ReasonForExit == ExitCondition.RelativePoints);
                        driftCompResult.NumIterations = minimization_result.Iterations;
                        driftCompResult.GroundLevel = BetweenCoinc;// minimization_result.MinimizingPoint[3];
                        driftCompResult.HistogramX = hist.Histogram_X;
                        driftCompResult.HistogramY = hist.Histogram_Y;
                        driftCompResult.Peaks = peaks;
                        driftCompResult.MiddlePeak = MiddlePeak;
                        //if (driftCompResult.IsFitSuccessful)
                        if (driftCompResult.IsFitSuccessful)
                        {
                            driftCompResult.HistogramYFit = obj_function(minimization_result.MinimizingPoint, XVals).ToArray();
                            driftCompResult.FittedMeanTime = minimization_result.MinimizingPoint[0];
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
                    
                    WriteLog($"Sync failed in {sw.Elapsed} | TimeSpan: {packettimespan} |DriftCoeff {LinearDriftCoefficient}");

                    DriftCompResult initDriftResult = driftCompResults[driftCompResults.Count / 2];

                    return new SyncClockResults()
                    {
                        PeaksFound = false,
                        IsClocksSync = false,
                        HistogramX = initDriftResult.HistogramX,
                        HistogramY = initDriftResult.HistogramY,
                        Peaks = initDriftResult.Peaks,
                        HistogramYFit = initDriftResult.HistogramYFit,
                        GroundLevel = (initDriftResult.GroundLevel,0),
                        NewLinearDriftCoeff = initDriftResult.LinearDriftCoeff,
                        CompTimeTags_Bob = ttBob_comp_list[initDriftResult.Index],
                        MiddlePeak = initDriftResult.MiddlePeak,
                        ProcessingTime = sw.Elapsed,
                        TimeTags_Alice = ttAlice,
                        TimeTags_Bob = ttBob
                    };
                }

                //Find best fit
                //double min_sigma = driftCompResults.Where(d => d.IsFitSuccessful && d.Sigma.val>0).Select(d => d.Sigma.val).Min();
                //DriftCompResult opt_driftResults = driftCompResults.Where(d => d.Sigma.val == min_sigma).FirstOrDefault();

                double min_groundlevel = driftCompResults.Where(d => d.IsFitSuccessful).Select(d => d.GroundLevel).Min();
                DriftCompResult opt_driftResults = driftCompResults.Where(d => d.GroundLevel == min_groundlevel).FirstOrDefault();

                //Write statistics

                WriteLog($"Sync cycle complete in {sw.Elapsed} | TimeSpan: {packettimespan}| Fitted FWHM: {opt_driftResults.Sigma.val:F2}({opt_driftResults.Sigma.err:F2})" +
                         $" | Pos: {opt_driftResults.MiddlePeak.MeanTime:F2} | Fitted pos: {opt_driftResults.FittedMeanTime:F2} | new DriftCoeff {opt_driftResults.LinearDriftCoeff}({LinearDriftCoefficient-opt_driftResults.LinearDriftCoeff})");

                //Define new Drift Coefficient

                long MaxGroundLevel = GroundlevelTolerance * opt_driftResults.Peaks.Count;
                bool clockInSync = opt_driftResults.Sigma.val <= STD_Tolerance && min_groundlevel < MaxGroundLevel;
                if(clockInSync) LinearDriftCoefficient = opt_driftResults.LinearDriftCoeff;

                return new SyncClockResults()
                {
                    PeaksFound = true,
                    IsClocksSync = clockInSync,
                    HistogramX = opt_driftResults.HistogramX,
                    HistogramY = opt_driftResults.HistogramY,
                    HistogramYFit = opt_driftResults.HistogramYFit,
                    NumIterations = opt_driftResults.NumIterations,
                    GroundLevel = (opt_driftResults.GroundLevel , MaxGroundLevel),
                    Sigma = opt_driftResults.Sigma,
                    NewLinearDriftCoeff = LinearDriftCoefficient,
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
            if(_corrKurolator==null)
            {
                _corrHist = new Histogram(_corrChanConfig, CorrSyncTimeWindow, (long)CorrTimeBin);
                _corrKurolator = new Kurolator(new List<CorrelationGroup> { _corrHist }, CorrSyncTimeWindow);
            }            

            SyncCorrResults syncRes = await Task<SyncCorrResults>.Run( () => 
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
             
                _corrKurolator.AddCorrelations(ttAlice, ttBob, GlobalClockOffset + FiberOffset);

                //Find correlated peak
                List<Peak> peaks = _corrHist.GetPeaks(peakBinning:2000);
                double av_area = peaks.Select(p => p.Area).Average();
                double av_area_err = Math.Sqrt(av_area);

                //Find peak most outside the average
                double max_deviation = peaks.Select(a => Math.Abs(a.Area - av_area)).Max();
                Peak CorrPeak = peaks.Where(p => Math.Abs(p.Area-av_area) == max_deviation).FirstOrDefault();

                //Is peak area statistically significant?
                bool isCorrSync = false;
                bool corrPeakFound = false;

                TimeTags comptimetags_Bob = null;

                if (Math.Abs(CorrPeak.Area - av_area) > 2*av_area_err)
                {
                    corrPeakFound = true;
                    if (Math.Abs(CorrPeak.MeanTime) < CorrPeakOffset_Tolerance) isCorrSync = true;

                    //FiberOffset += CorrPeak.MeanTime;

                    //Correct Bobs tags
                    comptimetags_Bob = new TimeTags(ttBob.chan, ttBob.time.Select(t => t - (GlobalClockOffset + FiberOffset)).ToArray());
                }

                sw.Stop();

                return new SyncCorrResults()
                {
                    HistogramX = _corrHist.Histogram_X,
                    HistogramY = _corrHist.Histogram_Y,
                    Peaks = peaks,
                    CorrPeakPos = CorrPeak.MeanTime,
                    CorrPeakFound = corrPeakFound,
                    NewFiberOffset = FiberOffset,
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
