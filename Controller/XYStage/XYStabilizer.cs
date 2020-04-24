using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Stage_Library;

namespace Controller.XYStage
{
    public class XYStabilizer
    {
        //--------------------------
        // P R O P E R T I E S
        //--------------------------

        /// <summary>
        /// Target Setpoint
        /// </summary>
        public double SetPoint { get; set; }
        
        /// <summary>
        /// Absolute Setpoint tolerance
        /// </summary>
        public double SPTolerance { get; set; }

        public bool SetpointReached => Math.Abs(ProcessValue - SetPoint) < SPTolerance;

        /// <summary>
        /// Current Process Value
        /// </summary>
        public double ProcessValue { get; private set; }

        /// <summary>
        /// Size of averaging buffer
        /// </summary>
        public int NumBuffer { get; set; } = 5;

        /// <summary>
        /// Time between process variable (PV) readout in ms
        /// </summary>
        public int BufferStepTime { get; private set; } = 1000;

        /// <summary>
        /// Maximum number of steps searched
        /// </summary>
        public int MaxSteps { get; set; } = 100;

        /// <summary>
        /// Stepsize in m
        /// </summary>
        public double StepSize { get; set; } = 500E-9;
        
        /// <summary>
        /// Process variable buffer is filled -> Value is valid
        /// </summary>
        public bool PVBufferFilled { get; private set; }

        public bool StabilizationActive { get; private set; }

        /// <summary>
        /// Stage Step size in m
        /// </summary>
        public double XYStep { get; set; } = 200E-9;

        /// <summary>
        /// Do control steps every <var> timer cycles
        /// </summary>
        public double StepTimeMultiplier { get; set; } = 2;

        //--------------------------
        // P R I V A T E S
        //--------------------------
        private readonly ILinearStage _stageX;
        private readonly ILinearStage _stageY;
        private readonly Func<double> _getPV;
        private readonly Action<string> _loggerCallback;

        private readonly System.Timers.Timer _PVtimer = new System.Timers.Timer();
        private readonly Queue<double> _PVbuffer = new Queue<double>();
        private volatile bool _bufferRefreshed;
        private CancellationTokenSource _cts = new CancellationTokenSource();
 
        public XYStabilizer(ILinearStage stageX, ILinearStage stageY, Func<double> getPV, int bufferStepTime=1000, Action<string> loggerCallback=null)
        {
            this._stageX = stageX;
            this._stageY = stageY;
            this._getPV = getPV;
            this._loggerCallback = loggerCallback;

            BufferStepTime = bufferStepTime;

            _PVtimer.Elapsed += _pvTimer_Elapsed;
            _PVtimer.Interval = BufferStepTime;
            _PVtimer.Start();
        }

        
        public Task<StabilizerResult> CorrectAsync()
        {
            return Task.Run(() => Correct());
        }

        public StabilizerResult Correct()
        {
            StabilizationActive = true;
            _cts = new CancellationTokenSource();

            int currStep = 0;
            double startX = _stageX.Position;
            double startY = _stageY.Position;

            StabilizerResult result = new StabilizerResult();

            int ydist = (int)Math.Ceiling(Math.Sqrt(MaxSteps));

            WriteLog($"Stabilization started at X={startX:e3},Y={startY:e3}, Setpoint={SetPoint}");
            
            while(true)
            {
                if (_cts.IsCancellationRequested) break;
                if (!_bufferRefreshed || !PVBufferFilled) continue;
                if (!PVBufferFilled) continue;

                if (SetpointReached)
                {
                    WriteLog($"Setpoint of {SetPoint} reached.");
                    result.Success = true;
                    break;
                }

                if (currStep % StepTimeMultiplier !=0) continue;
                if (currStep>MaxSteps)
                {
                    WriteLog("Maximum Steps Exceeded. Cancelling stabilization.");
                    result.MaxStepsExceeded = true;
                    break;
                }

                //Main Control sequence
                var (x, y) = StepFunctions.AlternatingZigZagYX(currStep, ydist);
                double posX = startX + StepSize * x;
                double posY = startY + StepSize * y;
                WriteLog($"Current PV: {ProcessValue}. Moving to X={posX:e4}, Y={posY:e4}");
                _stageX.Move_Absolute(posX);
                _stageY.Move_Absolute(posY);


                currStep++;
                _bufferRefreshed = false;
            }

            StabilizationActive = false;
            return result;

        }

        public void Cancel()
        {
            _cts.Cancel();
        }

        private void _pvTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (NumBuffer < 1) NumBuffer = 1;

            _PVbuffer.Enqueue(_getPV());
            while (_PVbuffer.Count > NumBuffer) _PVbuffer.Dequeue();

            ProcessValue = _PVbuffer.Average();

            PVBufferFilled = _PVbuffer.Count >= NumBuffer;
            _bufferRefreshed = true;
        }

        private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("XYStabilization: "+message);
        }
    }
}
