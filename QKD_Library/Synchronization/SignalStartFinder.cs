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
        public int RateThreshold { get; set; } = 50000;
        public double SlopeTolerance { get; set; } = 200;

        //#################################################
        //##  P R I V A T E S
        //#################################################

        private Action<string> _loggerCallback;

        //#################################################
        //##  C O N S T R U C T O R
        //#################################################

        public SignalStartFinder(Action<string> loggercallback = null)
        {
            _loggerCallback = loggercallback;
        }

        public static double Rate(IEnumerable<long> buffer)
        {
            double rate = 1E12 * buffer.Count() / (buffer.Last() - buffer.First());
            return rate;
        }

        public SignalStartResult FindSignalStartTime(TimeTags tt)
        {
            int threshold_index = 0;
            bool threshold_found = false;

            SignalStartResult result = new SignalStartResult();

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

            result.Threshold = RateThreshold;

            //No threshold found?
            if (!threshold_found)
            {
                result.Status = SignalStartStatus.ThresholdNotFound;
            }

            //Is signal above threshold in the beginning? 
            if (threshold_index < 2 * AveragingFIFOSize)
            {
                result.Status = SignalStartStatus.InitialSignalTooHigh;
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

            //-----------  1. Fit rate by polynomial -------------

            int polynomial_order = 10;

            Vector<double> initial_guess = new DenseVector(Enumerable.Repeat(1.0, polynomial_order).ToArray());

            //Select some points around threshold
            double[] linRegrTimes = cropped_times.Skip(cropped_threshold_index - 5).Take(5).Select(v => (double)v).ToArray();
            double[] linRegrVals = cropped_rates.Skip(cropped_threshold_index - 5).Take(5).ToArray();

            result.FittingTimes = linRegrTimes;

            Vector<double> XVals = new DenseVector(linRegrTimes);
            Vector<double> YVals = new DenseVector(linRegrVals);

            //FIX LINEAR REGRESSION!!!
            //!!!!!!!!!!!!!!!!!!!!!!!!!!

            Func<Vector<double>, Vector<double>, Vector<double>> obj_function = (p, x) =>
            {
                //p[0]... Slope
                //p[1]... Interceptor

                IEnumerable<double> results = x.Select(val => p[0] * val + p[1]);
                Vector<double> result_vector = new DenseVector(results.ToArray());

                return result_vector;
            };

            IObjectiveModel objective_model = ObjectiveFunction.NonlinearModel(obj_function, XVals, YVals);
            LevenbergMarquardtMinimizer solver = new LevenbergMarquardtMinimizer(maximumIterations: 100);

            NonlinearMinimizationResult minimization_result = solver.FindMinimum(objective_model, initial_guess);
            double[] func_values = obj_function(minimization_result.MinimizingPoint, XVals).ToArray();

            result.FittedRates = func_values;

            if (minimization_result.ReasonForExit != ExitCondition.Converged && minimization_result.ReasonForExit != ExitCondition.RelativePoints)
            {
                result.Status = SignalStartStatus.SignalFittingFailed;
                return result;
            }

            //-------------------------------
            //     R A T E   R E S U L T S
            //-------------------------------

            result.StartTime = cropped_times[cropped_threshold_index];
            result.GlobalStartTime = times[threshold_index];

            result.Slope = minimization_result.MinimizingPoint[0];
            WriteLog($"Slope: {result.Slope}");

            result.Status = result.Slope > SlopeTolerance ? SignalStartStatus.SlopeOK : SignalStartStatus.SlopeTooLow;
            return result;
        }

        private void WriteLog(string msg)
        {
            _loggerCallback?.Invoke("SignalStartFinder: " + msg);
        }

    }


}
