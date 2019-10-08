using Extensions_Library;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;

namespace QKD_Library.Synchronization
{
    public class SignalStartFinder
    {
        //#################################################
        //##  P R O P E R T I E S
        //#################################################
        public int AveragingFIFOSize { get; set; } = 30;
        public int RateThreshold { get; set; } = 30000;
        /// <summary>
        /// Minimum slope
        /// eg. 30.000E-8 -> 30000 cps per 100 micro second
        /// </summary>
        public double SlopeTolerance { get; set; } = 30000E-8;

        //#################################################
        //##  P R I V A T E S
        //#################################################

        private string _iD = "";
        private Action<string> _loggerCallback;

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################

        public SignalStartFinder(string ID="", Action<string> loggercallback = null)
        {
            _iD = ID;
            _loggerCallback = loggercallback;
        }

        //#################################################
        //##  M E T H O D S
        //#################################################

        public static double Rate(IEnumerable<long> buffer)
        {
            double rate = 1E12 * buffer.Count() / (buffer.Last() - buffer.First());
            return rate;
        }

        public SignalStartResult FindSignalStartTime(TimeTags tt)
        {
            int threshold_index = 0;
            bool threshold_found = false;

            SignalStartResult result = new SignalStartResult()
            {
                ID = _iD,
                Threshold = RateThreshold
            };

            Queue<long> FIFO = new Queue<long>();
            List<double> rates = new List<double>();

            long[] times = tt.time;

            //Get rates and find threshold
            for (int i = 0; i < times.Length; i++)
            {
                //Fill FIFO
                FIFO.Enqueue(times[i]);

                //Is FIFO filled?
                if (FIFO.Count < AveragingFIFOSize)
                {
                    rates.Add(0);
                    continue;
                }

                //Calculate rates
                rates.Add(Rate(FIFO));

                FIFO.Dequeue();

                //Is threshold exeeded?
                if (rates[i] > RateThreshold && threshold_found == false)
                {
                    threshold_index = i;
                    threshold_found = true;
                }
            }

            //No threshold found?
            if (!threshold_found)
            {
                result.Status = SignalStartStatus.ThresholdNotFound;
                WriteLog($"Threshold of {RateThreshold} not exeeded.");
                return result;
            }

            //Is signal above threshold in the beginning? 
            if (threshold_index < 2 * AveragingFIFOSize)
            {
                result.Status = SignalStartStatus.InitialSignalTooHigh;
                WriteLog($"Inital signal higher than Threshold ({RateThreshold}). Block beam!");
                return result;
            }

            //Crop part around threshold and store start time
            int min_index = Math.Max(threshold_index - 10 * AveragingFIFOSize, 0);
            long crop_starttime = times[min_index];

            int cropped_threshold_index = threshold_index - min_index;
            long[] cropped_times = times.Skip(min_index).Take(20 * AveragingFIFOSize).Select(t => t - crop_starttime).ToArray();
            double[] cropped_rates = rates.Skip(min_index).Take(20 * AveragingFIFOSize).ToArray();

            result.Times = cropped_times;
            result.Rates = cropped_rates;

            //-------------------------------
            //      F I T   S L O P E
            //-------------------------------

            //Select some points around threshold
            int linRegrRange = 10;
            double[] linRegrTimes = cropped_times.Skip(cropped_threshold_index - linRegrRange/2).Take(linRegrRange).Select(v => (double)v).ToArray();
            double[] linRegrVals = cropped_rates.Skip(cropped_threshold_index - linRegrRange / 2).Take(linRegrRange).ToArray();

            result.FittingTimes = linRegrTimes;

            (double d, double k) linRegression = MathNet.Numerics.Fit.Line(linRegrTimes, linRegrVals).ToValueTuple();

            double[] func_values = linRegrTimes.Select(t => linRegression.k * t + linRegression.d).ToArray();

            result.Slope = linRegression.k;
            result.FittedRates = func_values;

            if (linRegression.k < 0)
            {
                result.Status = SignalStartStatus.SignalFittingFailed;
                WriteLog("Failed to fit slope.");
                return result;
            }

            //-------------------------------
            //     R A T E   R E S U L T S
            //-------------------------------

            result.StartTime = (RateThreshold-linRegression.d)/linRegression.k;
            result.GlobalStartTime = (long)Math.Round( crop_starttime + result.StartTime);

            result.Status = result.Slope > SlopeTolerance ? SignalStartStatus.SlopeOK : SignalStartStatus.SlopeTooLow;

            WriteLog($"Global Start Time: {result.GlobalStartTime} | Slope: {result.Slope*1E8:F2}/100μs | Min: {SlopeTolerance*1E8}/100μs | {(result.Status==SignalStartStatus.SlopeOK ? "Slope OK":"Slope TOO LOW")}");
            
            return result;
        }

        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke($"SignalStartFinder {_iD}: {msg}");
        }

    }


}
