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
using Stage_Library.Thorlabs;

namespace QKD_Library.Synchronization
{
    public class TaggerSync
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################

        public ulong ClockTimeBin { get; set; } = 1000;

        //Clock Synchronisation
        public bool GlobalOffsetDefined { get; private set; } = false;
        public ulong ClockSyncTimeWindow { get; set; } = 100000;
        /// <summary>
        /// Defined by: time ALICE - time BOB
        /// </summary>
        public long GlobalClockOffset { get; set; } = 0;

        public double LinearDriftCoefficient { get; set; } = 0;
        public double LinearDriftCoeff_Var { get; set; } = 0.001E-5;
        public int LinearDriftCoeff_NumVar { get; set; } = 2;

        public double STD_Tolerance { get; set; } = 1700;
        public long GroundlevelTimebin { get; set; } = 2000;
        public double GroundlevelTolerance { get; set; } = 0.1;
        public double PVal { get; set; } = 0;
        public ulong ExcitationPeriod { get; set; } = 12500; //200000000; 

        //Correlation Synchronization
        public ulong CorrTimeBin { get; set; } = 512;
        /// <summary>
        /// Offset by relative fiber distance of Alice and Bob
        /// </summary>
        public ulong CorrSyncTimeWindow { get; set; } = 100000;

        //#################################################
        //##  P R I V A T E S
        //#################################################

        private ITimeTagger _tagger1;
        private ITimeTagger _tagger2;

        private Action<string> _loggerCallback;
        private Func<string, string, int> _userprompt;
        private KPRM1EStage _shutterContr;

        private Kurolator _clockKurolator;

        private CorrSyncStatus _corrsyncStatus = CorrSyncStatus.SearchingCoarseRange;
        private long _coarseTimeOffset = 0;
        private int _numCoarseSearches = 0;


        private static byte oR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
        private static byte oD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;
        private List<(byte cA, byte cB)> _clockChanConfig = new List<(byte cA, byte cB)>
        {
            //Clear Basis
            (0,5),(0,6),(0,7),(0,8),
            (1,5),(1,6),(1,7),(1,8),
            (2,5),(2,6),(2,7),(2,8),
            (3,5),(3,6),(3,7),(3,8),

            //Obscured Basis
            (0,oR),(0,oD),(1,oR),(1,oD),(2,oR),(2,oD),(3,oR),(3,oD)

            //Funky generator
            //(1,5)
        };
        //List<(byte cA, byte cB)> _clockChanConfig = new List<(byte cA, byte cB)>
        //        {
        //            //Laser
        //            (2,7)

        //            //Funky generator
        //            //(0,1)
        //        };

        private List<(byte cA, byte cB)> _corrChanConfig = new List<(byte cA, byte cB)>
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

        public event EventHandler<OffsetFoundEventArgs> OffsetFound;
        private void OnOffsetFound(OffsetFoundEventArgs e)
        {
            OffsetFound?.Raise(this, e);
        }

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################

        public TaggerSync(ITimeTagger tagger1, ITimeTagger tagger2 = null, Action<string> loggerCallback=null, Func<string, string, int> userprompt=null, KPRM1EStage shutterContr=null)
        {
            _loggerCallback = loggerCallback;
            _userprompt = userprompt;
            _tagger1 = tagger1;
            _tagger2 = tagger2;
            _shutterContr = shutterContr;

            //Define tagger1 as SyncRate Source
            _tagger1.SyncRateChanged += (sender, e) => _tagger2.SyncRate = e.SyncRate;
        }

        //#################################################
        //##  M E T H O D S 
        //#################################################
        public TimeTags GetSingleTimeTags(int TaggerNr, int packetSize = 100000)
        {
            if (TaggerNr < 0 || TaggerNr > 1 || TaggerNr == 1 && _tagger2 == null)
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
            tagger.ClearTimeTagBuffer();

            return tt;
        }


        public SyncClockResult TestClock(int packetSize=100000)
        {
            TimeTags ttA = null;
            TimeTags ttB = null;

            _tagger1.PacketSize = _tagger2.PacketSize = packetSize;

            //Refresh sync rate
            _tagger2.SyncRate = _tagger1.SyncRate;

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            _tagger1.StartCollectingTimeTagsAsync();
            _tagger2.StartCollectingTimeTagsAsync();

            while (!_tagger1.GetNextTimeTags(out ttA)) Thread.Sleep(10);
            while (!_tagger2.GetNextTimeTags(out ttB)) Thread.Sleep(10);

            SyncClockResult res = SyncClocks(ttA, ttB);
            return res;
        }


        private void TriggerShutter()
        {
            _shutterContr.SetOutput(true);
            Thread.Sleep(100);
            _shutterContr.SetOutput(false);
        }
        
        private long GetGlobalOffset()
        {
            TimeTags ttA = null;
            TimeTags ttB = null;

            int tmpPacketSize = _tagger1.PacketSize;
            _tagger1.PacketSize = _tagger2.PacketSize = 200000;

            while (!GlobalOffsetDefined)
            {
                if(_shutterContr==null)
                {
                    UserPrompt("Global Clock offset undefined. Block signal and release it fast.");
                }

                ResetTimeTaggers();

                if (_shutterContr!=null)
                {
                    TriggerShutter();
                    Thread.Sleep(2000);
                }

                _tagger1.StartCollectingTimeTagsAsync();
                _tagger2.StartCollectingTimeTagsAsync();

                if(_shutterContr!=null)
                {
                    Thread.Sleep(3000);
                    TriggerShutter();
                }

                while (!_tagger1.GetNextTimeTags(out ttA)) Thread.Sleep(10);
                while (!_tagger2.GetNextTimeTags(out ttB)) Thread.Sleep(10);

                SignalStartFinder serverStartFinder = new SignalStartFinder("Alice", _loggerCallback);
                SignalStartResult startresA = serverStartFinder.FindSignalStartTime(ttA);

                SignalStartFinder clientStartFinder = new SignalStartFinder("Bob", _loggerCallback);
                SignalStartResult startresB = clientStartFinder.FindSignalStartTime(ttB);

                if (startresA.Status < SignalStartStatus.SignalFittingFailed || startresB.Status < SignalStartStatus.SignalFittingFailed) continue;

                OnOffsetFound(new OffsetFoundEventArgs(startresA, startresB));

                if (startresA.Status == SignalStartStatus.SlopeOK && startresB.Status == SignalStartStatus.SlopeOK)
                {
                    GlobalClockOffset = startresA.GlobalStartTime - startresB.GlobalStartTime;
                    GlobalOffsetDefined = true;
                }
                         
            }

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();
            _tagger1.PacketSize = _tagger2.PacketSize = tmpPacketSize;

            return 0;
        }

        public TaggerSyncResults GetSyncedTimeTags(int packetSize = 100000)
        {
            if (_tagger1 == null || _tagger2 == null)
            {
                WriteLog("One or both timetaggers not ready.");
                return null;
            }

            TimeTags ttA = null;
            TimeTags ttB = null;

            _tagger1.PacketSize = _tagger2.PacketSize = packetSize;

            //Refresh sync rate
            _tagger2.SyncRate = _tagger1.SyncRate;


            _tagger1.StartCollectingTimeTagsAsync();
            _tagger2.StartCollectingTimeTagsAsync();

            //---------------------------------------------------
            //Is global offset defined? If not: Find starting time
            //---------------------------------------------------

            if (!GlobalOffsetDefined) GetGlobalOffset();
           
            //---------------------------------------------------
            // Request timetags and perform synchronisation
            //---------------------------------------------------

            _tagger1.ClearTimeTagBuffer();
            _tagger2.ClearTimeTagBuffer();

            while (!_tagger1.GetNextTimeTags(out ttA)) Thread.Sleep(10);
            while (!_tagger2.GetNextTimeTags(out ttB)) Thread.Sleep(10);

            //Check packet overlap and get new packet if not overlapping
            Kurolator.CorrResult overlapResult = Kurolator.CheckPacketOverlap(ttA, ttB, (long)ClockSyncTimeWindow, GlobalClockOffset);
            switch (overlapResult)
            {
                case Kurolator.CorrResult.PartnerAhead:
                    while (!_tagger1.GetNextTimeTags(out ttA)) Thread.Sleep(10);
                    break;
                case Kurolator.CorrResult.PartnerBehind:
                    while (!_tagger2.GetNextTimeTags(out ttB)) Thread.Sleep(10);
                    break;
            }


            TaggerSyncResults result = new TaggerSyncResults()
            {
                TimeTags_Alice = ttA,
                TimeTags_Bob = ttB,
            };
                     
            //Clock synchronization
            SyncClockResult syncClockRes = SyncClocks(ttA, ttB);
            if (!syncClockRes.IsClocksSync)             
            {
                WriteLog("Clock synchronization failed.");
                return result;
            }

            //Final synchronization by correlation finding
            SyncCorrResults corrSyncRes = SyncCorrelation(ttA, syncClockRes.CompTimeTags_Bob);
                        
            result.IsSync = corrSyncRes.IsCorrSync;

            //Correct TimeTags if Offset is defined
            if (GlobalOffsetDefined) result.CompTimeTags_Bob = new TimeTags(ttB.chan, ttB.time.Select(t => t + GlobalClockOffset).ToArray());

            return result;
        }

        public void ResetTimeTaggers()
        {
            _tagger1.StopCollectingTimeTags();
            _tagger2?.StopCollectingTimeTags();

            _tagger1.ClearTimeTagBuffer();
            _tagger2?.ClearTimeTagBuffer();

            GlobalOffsetDefined = false;
            _corrsyncStatus = CorrSyncStatus.SearchingCoarseRange;
        }

        private SyncClockResult SyncClocks(TimeTags ttAlice, TimeTags ttBob)
        {
            Stopwatch sw = new Stopwatch();

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


            //GlobalClockOffset = alice_first - bob_first;

            //----------------------------------------------------------------
            //Compensate Bobs tags for a variation of linear drift coefficients
            //-----------------------------------------------------------------

            int[] variation_steps = Generate.LinearRangeInt32(-LinearDriftCoeff_NumVar, LinearDriftCoeff_NumVar);
            List<double> linDriftCoefficients = variation_steps.Select(s => LinearDriftCoefficient + s * LinearDriftCoeff_Var).ToList();
            List<long[]> comp_times_list = linDriftCoefficients.Select((c) => ttBob.time.Select(t => (long)(t + (t - bob_first) * c)).ToArray()).ToList();
            List<TimeTags> ttBob_comp_list = comp_times_list.Select((ct) => new TimeTags(ttBob.chan, ct)).ToList();

            //------------------------------------------------------------------------------
            //Generate Histograms and Fittings for all Compensation Configurations
            //------------------------------------------------------------------------------
            List<DriftCompResult> driftCompResults = new List<DriftCompResult>() { };

            //Channel configuration
            byte oR = SecQNet.SecQNetPackets.TimeTagPacket.RectBasisCodedChan;
            byte oD = SecQNet.SecQNetPackets.TimeTagPacket.DiagbasisCodedChan;

            for (int drift_index = 0; drift_index < variation_steps.Length; drift_index++)
            {
                DriftCompResult driftCompResult = new DriftCompResult(drift_index);

                Histogram hist = new Histogram(_clockChanConfig, ClockSyncTimeWindow, (long)ClockTimeBin);
                _clockKurolator = new Kurolator(new List<CorrelationGroup> { hist }, ClockSyncTimeWindow);
                _clockKurolator.AddCorrelations(ttAlice, ttBob_comp_list[drift_index], GlobalClockOffset);

                //----- Analyse peaks ----

                List<Peak> peaks = hist.GetPeaks(peakBinning: 2000);

                //Number of peaks plausible?
                int numExpectedPeaks = (int)(2 * ClockSyncTimeWindow / ExcitationPeriod) + 1;
                if (peaks.Count < numExpectedPeaks - 1 || peaks.Count > numExpectedPeaks + 2)
                {
                    //Return standard result

                    driftCompResult.LinearDriftCoeff = linDriftCoefficients[drift_index];
                    driftCompResult.IsFitSuccessful = false;
                    driftCompResult.HistogramX = hist.Histogram_X;
                    driftCompResult.HistogramY = hist.Histogram_Y;
                    driftCompResult.Peaks = peaks;
                    driftCompResult.MiddlePeak = null;
                    driftCompResult.FittedMeanTime = 0;
                    driftCompResult.Sigma = (-1, -1);
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

                        int[] x0_range = Generate.LinearRangeInt32(-numExpectedPeaks / 2, numExpectedPeaks / 2);
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

                    foreach (var peak in peaks.Except(new List<Peak> { MiddlePeak }).Skip(1).Take(peaks.Count - 3))
                    {
                        int sign = peak.MeanTime < MiddlePeak.MeanTime ? 1 : -1;

                        long sum_coinc = 0;
                        int start_ind = peak.MeanIndex + sign * (int)((long)ExcitationPeriod / (2 * hist.Hist_Resolution));
                        long start_time = hist.Histogram_X[start_ind];
                        //Search backwards
                        int ind = start_ind;
                        while (hist.Histogram_X[ind] >= start_time - GroundlevelTimebin && ind > 0)
                        {
                            sum_coinc += hist.Histogram_Y[ind];
                            ind--;
                        }
                        //Search foward
                        ind = start_ind + 1;
                        while (hist.Histogram_X[ind] <= start_time + GroundlevelTimebin && ind < hist.Histogram_X.Length - 2)
                        {
                            sum_coinc += hist.Histogram_Y[ind];
                            ind++;
                        }
                        BetweenCoinc += sum_coinc;
                    }

                    //Write results                                               
                    driftCompResult.LinearDriftCoeff = linDriftCoefficients[drift_index];

                    driftCompResult.IsFitSuccessful = minimization_result.ReasonForExit ==
                    ExitCondition.Converged || minimization_result.ReasonForExit == ExitCondition.RelativePoints;
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

            DriftCompResult initDriftResult = driftCompResults[driftCompResults.Count / 2];

            SyncClockResult result = new SyncClockResult()
            {
                PeaksFound = false,
                IsClocksSync = false,
                HistogramX = initDriftResult.HistogramX,
                HistogramY = initDriftResult.HistogramY,
                Peaks = initDriftResult.Peaks,
                HistogramYFit = initDriftResult.HistogramYFit,
                GroundLevel = (initDriftResult.GroundLevel, 0),
                NewLinearDriftCoeff = initDriftResult.LinearDriftCoeff,
                CompTimeTags_Bob = ttBob_comp_list[initDriftResult.Index],
                MiddlePeak = initDriftResult.MiddlePeak,
                ProcessingTime = sw.Elapsed,
                TimeTags_Alice = ttAlice,
                TimeTags_Bob = ttBob
            };

            //No fitting found
            if (driftCompResults.Where(r => r.IsFitSuccessful == true).Count() <= 0)
            {

                WriteLog($"Clock Sync failed in {sw.Elapsed} | TimeSpan: {packettimespan} |DriftCoeff {LinearDriftCoefficient}");
                return result;
            }

            //FIND BEST FIT
            double min_groundlevel = driftCompResults.Where(d => d.IsFitSuccessful).Select(d => d.GroundLevel).Min();
            DriftCompResult opt_driftResults = driftCompResults.Where(d => d.GroundLevel == min_groundlevel).FirstOrDefault();

            //Write statistics

            WriteLog($"Clock sync cycle complete in {sw.Elapsed} | TimeSpan: {packettimespan}| Fitted FWHM: {opt_driftResults.Sigma.val:F2}({opt_driftResults.Sigma.err:F2})" +
                        $" | Pos: {opt_driftResults.MiddlePeak.MeanTime:F2} | Fitted pos: {opt_driftResults.FittedMeanTime:F2} | new DriftCoeff {opt_driftResults.LinearDriftCoeff}({LinearDriftCoefficient - opt_driftResults.LinearDriftCoeff})");

            //Define new Drift Coefficient

            double MaxGroundLevel = GroundlevelTolerance * opt_driftResults.MiddlePeak.Area * opt_driftResults.Peaks.Count;
            bool clockInSync = opt_driftResults.Sigma.val <= STD_Tolerance && min_groundlevel < MaxGroundLevel;
            if (clockInSync) LinearDriftCoefficient = opt_driftResults.LinearDriftCoeff;


            result.PeaksFound = true;
            result.IsClocksSync = clockInSync;
            result.HistogramX = opt_driftResults.HistogramX;
            result.HistogramY = opt_driftResults.HistogramY;
            result.HistogramYFit = opt_driftResults.HistogramYFit;
            result.NumIterations = opt_driftResults.NumIterations;
            result.GroundLevel = (opt_driftResults.GroundLevel, MaxGroundLevel);
            result.Sigma = opt_driftResults.Sigma;
            result.NewLinearDriftCoeff = LinearDriftCoefficient;
            result.Peaks = opt_driftResults.Peaks;
            result.MiddlePeak = opt_driftResults.MiddlePeak;
            result.CompTimeTags_Bob = ttBob_comp_list[opt_driftResults.Index];

            OnSyncClocksComplete(new SyncClocksCompleteEventArgs(result));

            return result;
        }

        private SyncCorrResults SyncCorrelation(TimeTags ttAlice, TimeTags ttBob)
        {
            SyncCorrResults results = new SyncCorrResults();

            double coarseCorrelationSignificance = 0.4;
            int maxNumCoarseSearches = 12;

            ulong FineTimeWindow = 10 * ExcitationPeriod;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // MAIN STATE MACHINE
            while (true)
            {
              
                switch (_corrsyncStatus)
                {
                    //-----------------------------------------
                    // Find coarse time by large search radius
                    //-----------------------------------------

                    case CorrSyncStatus.SearchingCoarseRange:


                        while (_numCoarseSearches <= maxNumCoarseSearches && _corrsyncStatus == CorrSyncStatus.SearchingCoarseRange)
                        {
                            SyncCorrResults coarseResults = new SyncCorrResults();

                            //Find coarse correlation by antibunching at all channels
                            ulong coarseTimewindow = 10000 * ExcitationPeriod;
                            Histogram _corrHist = new Histogram(_clockChanConfig, coarseTimewindow, (long)ExcitationPeriod);

                            Kurolator _corrKurolator = new Kurolator(new List<CorrelationGroup> { _corrHist }, coarseTimewindow);
                            _corrKurolator.AddCorrelations(ttAlice, ttBob, GlobalClockOffset + _coarseTimeOffset);

                            coarseResults.HistogramX = _corrHist.Histogram_X.ToList();
                            coarseResults.HistogramY = _corrHist.Histogram_Y.ToList();

                     
                            //Remove first and last point
                            IEnumerable<long> croppedHistogramY = coarseResults.HistogramY.Skip(1).Take(coarseResults.HistogramY.Count - 2);
                            long minPoint = croppedHistogramY.Min();
                            long maxPoint = croppedHistogramY.Max();
                            double average = croppedHistogramY.Average();

                            bool minPointsignificant = minPoint < (1-coarseCorrelationSignificance) * average;
                            bool maxPointsignificant = maxPoint > (1+coarseCorrelationSignificance) * average;                       

                            //Is Extremum significant?
                            if (maxPointsignificant)
                            {
                                //int ind = minPointsignificant ?
                                //    coarseResults.HistogramY.FindIndex(y => y == minPoint) :
                                //    coarseResults.HistogramY.FindIndex(y => y == maxPoint);
                                int ind = coarseResults.HistogramY.FindIndex(y => y == maxPoint);

                                coarseResults.CorrPeakPos = coarseResults.HistogramX[ind];
                                
                                GlobalClockOffset += _coarseTimeOffset - coarseResults.CorrPeakPos; //IS SIGN CORRECT?

                                _coarseTimeOffset = 0;
                                _numCoarseSearches = 0;
                                _corrsyncStatus = CorrSyncStatus.SearchingCorrPeak;

                                WriteLog($"Coarse correlations found at {coarseResults.CorrPeakPos} with a peak ration of {maxPoint/average:F2} after {sw.Elapsed}");
                            }
                            //If not: Change search range
                            else
                            {
                                //alternately vary search window                      
                                _coarseTimeOffset = (_numCoarseSearches % 2 == 0 ? 1 : -1) * (_numCoarseSearches / 2 + 1) * (long)coarseTimewindow;
                                _numCoarseSearches++;

                                WriteLog($"No coarse correlation found, shifting search window by {_coarseTimeOffset / 1E6:F2} μs");
                            }

                            coarseResults.Status = _corrsyncStatus;
                            //OnSyncCorrComplete(new SyncCorrCompleteEventArgs(coarseResults));

                            //DEBUG DELAY
                            Thread.Sleep(1000);

                            //No correlation found
                            if (_numCoarseSearches > maxNumCoarseSearches)
                            {
                                _corrsyncStatus = CorrSyncStatus.NoCorrelationFound;
                                results = coarseResults;

                                WriteLog($"No coarse correlations found after {sw.Elapsed}. Synchronization FAILED");
                            }
                        }
                        break;

                    //-----------------------------------------
                    // Find correlated peak
                    //-----------------------------------------
                    case CorrSyncStatus.SearchingCorrPeak:

                        SyncCorrResults res = new SyncCorrResults();
   
                        Histogram fineCorrHist = new Histogram(_clockChanConfig, FineTimeWindow, 512);

                        Kurolator fineCorrKurolator = new Kurolator(new List<CorrelationGroup> { fineCorrHist }, FineTimeWindow);
                        fineCorrKurolator.AddCorrelations(ttAlice, ttBob, GlobalClockOffset);

                        res.HistogramX = fineCorrHist.Histogram_X.ToList();
                        res.HistogramY = fineCorrHist.Histogram_Y.ToList();

                        List<Peak> peaks = fineCorrHist.GetPeaks();
                        res.Peaks = peaks;

                        //Find maximum distance between the peaks
                        List<long> peakDists = new List<long>();
                        for (int i=0; i<peaks.Count-1;i++)
                        {
                            peakDists.Add(peaks[i + 1].MeanTime - peaks[i].MeanTime);
                        }

                        List<long> LongDistances = peakDists.Where(d => d > 1.5 * ExcitationPeriod).ToList();
                        
                        //Multiple peaks missed --> error
                        if(LongDistances.Count>=2)
                        {
                            _corrsyncStatus = CorrSyncStatus.NoCorrelationFound;
                            WriteLog($"No correlated peak found after {sw.Elapsed}");
                        }
                        //One distinct strong antibunching found
                        else if(LongDistances.Count()==1)
                        {
                            int ind = peakDists.FindIndex(d => d == LongDistances.First());
                            res.CorrPeakPos = peaks[ind].MeanTime + (long)ExcitationPeriod / 2;

                            GlobalClockOffset -= res.CorrPeakPos; //IS SIGN CORRECT?

                            _corrsyncStatus = CorrSyncStatus.TrackingPeak;
                            WriteLog($"Strong antibunching found at {res.CorrPeakPos} after {sw.Elapsed}");
                        }
                        //Search for bunching/antibunching
                        else
                        {
                            double average_area = peaks.Select(p => p.Area).Average();
                            List<double> ratios = peaks.Select(p => Math.Abs((p.Area/average_area))).ToList();
                            double max_ratio = ratios.Max();
                            int ind_max = ratios.FindIndex(v => v == max_ratio);

                            //Is deviation significant?
                            if(max_ratio > 1.4)
                            {
                                res.CorrPeakPos = peaks[ind_max].MeanTime;

                                GlobalClockOffset -= res.CorrPeakPos; //IS SIGN CORRECT?

                                res.IsCorrSync = true;
                                _corrsyncStatus = CorrSyncStatus.TrackingPeak;
                                WriteLog($"Correlated peak found at {res.CorrPeakPos} with a ratio of {max_ratio:F2} after {sw.Elapsed}");
                            }
                            else
                            {
                                //Error, stop searching
                                _corrsyncStatus = CorrSyncStatus.NoCorrelationFound;
                                WriteLog($"No correlated peak found after {sw.Elapsed}");
                            }
                        }

                        OnSyncCorrComplete(new SyncCorrCompleteEventArgs(res));
                        results = res;

                        //DEBUG DELAY!!!
                        Thread.Sleep(1000);

                        break;

                    //-----------------------------------------
                    // Track middlepeak
                    //-----------------------------------------
                    case CorrSyncStatus.TrackingPeak:

                        SyncCorrResults trackingRes = new SyncCorrResults();

                        Histogram trackingHist = new Histogram(_clockChanConfig, FineTimeWindow, 512);

                        Kurolator trackingKurolator = new Kurolator(new List<CorrelationGroup> { trackingHist }, FineTimeWindow);
                        trackingKurolator.AddCorrelations(ttAlice, ttBob, GlobalClockOffset);

                        trackingRes.HistogramX = trackingHist.Histogram_X.ToList();
                        trackingRes.HistogramY = trackingHist.Histogram_Y.ToList();

                        List<Peak> trackedPeaks = trackingHist.GetPeaks();
                        trackingRes.Peaks = trackedPeaks;

                        //Track peak closest to zero
                        long mindist = trackedPeaks.Select(p => Math.Abs(p.MeanTime)).Min();
                       
                        Peak MiddlePeak = trackedPeaks.Where(p => Math.Abs(p.MeanTime) == mindist).FirstOrDefault();

                        //Is middlepeak around zero? --> DONE
                        if( ((double) MiddlePeak.MeanTime).AlmostEqual(0,ExcitationPeriod/10) )
                        {
                            trackingRes.CorrPeakPos = MiddlePeak.MeanTime;
                            trackingRes.IsCorrSync = true;

                            WriteLog($"Peak tracked at {trackingRes.CorrPeakPos} after {sw.Elapsed}");
                        }
                        //If not, is it strong antibunching?
                        else
                        {
                            //Get neighbour peak around zero
                            int middlepeakindex = trackedPeaks.FindIndex(p => p == MiddlePeak);

                            Peak neighbourPeak = null;
                            if (MiddlePeak.MeanTime < 0 && middlepeakindex < trackedPeaks.Count - 1) neighbourPeak = trackedPeaks[middlepeakindex + 1];
                            else if (MiddlePeak.MeanTime >= 0 && middlepeakindex > 0) neighbourPeak = trackedPeaks[middlepeakindex - 1];
                            else //Impossible error state
                            {                           
                                _corrsyncStatus = CorrSyncStatus.SearchingCorrPeak;
                                WriteLog($"Peak tracking failed after {sw.Elapsed} (non-plausible peak distances)");
                                break;
                            }

                            //Is the mean value in the middle and the distance is about 2 excitation cycles?
                            long mean_value = (MiddlePeak.MeanTime + neighbourPeak.MeanTime) / 2;
                            long distance = Math.Abs(MiddlePeak.MeanTime - neighbourPeak.MeanTime);

                            if ( mean_value < (long)ExcitationPeriod/10 && distance > 1.5*ExcitationPeriod )
                            {
                                trackingRes.CorrPeakPos = mean_value;
                                trackingRes.IsCorrSync = true;
                                WriteLog($"Peak tracked at {trackingRes.CorrPeakPos} after {sw.Elapsed}");
                            }
                            //If not, go back to finding correlation
                            else
                            {
                                _corrsyncStatus = CorrSyncStatus.SearchingCorrPeak;
                                WriteLog($"Peak tracking failed after {sw.Elapsed}");
                            }

                        }

                        GlobalClockOffset -= trackingRes.CorrPeakPos; //IS SIGN CORRECT?
                        OnSyncCorrComplete(new SyncCorrCompleteEventArgs(trackingRes));

                        results = trackingRes;

                        break;
                }

                if (_corrsyncStatus == CorrSyncStatus.NoCorrelationFound ||
                   _corrsyncStatus == CorrSyncStatus.TrackingPeak)
                    break;
            }

            sw.Stop();

            return results;
        }

        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke("Sync: " + msg);
        }

        private bool UserPrompt(string msg)
        {
            int result= _userprompt?.Invoke(msg, "Tagger Synchronization") ?? 2;
            return result == 1; 
        }
    }

    //##################################
    // E V E N T   A R G S
    //##################################
    public class SyncClocksCompleteEventArgs : EventArgs
    {
        public SyncClockResult SyncRes { get; private set; }
        public SyncClocksCompleteEventArgs(SyncClockResult syncRes)
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

    public class OffsetFoundEventArgs : EventArgs
    {
        public SignalStartResult ResultA { get; private set; }
        public SignalStartResult ResultB { get; private set; }

        public OffsetFoundEventArgs(SignalStartResult resultA, SignalStartResult resultB)
        {
            ResultA = resultA;
            ResultB = resultB;
        }
    }

}
