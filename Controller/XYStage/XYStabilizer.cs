using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Stage;

namespace Controller.XYStage
{
    public class XYStabilizer
    {
        //--------------------------
        // P R O P E R T I E S
        //--------------------------

        public string Logfile { get; set; } = "XYStabilization.txt";

        /// <summary>
        /// Target Setpoint
        /// </summary>
        public double SetPoint { get; set; }

        /// <summary>
        /// Setpoint tolerance in fraction of Setpoint
        /// </summary>
        public double SPTolerance { get; set; } = 0.92;

        public bool IsBelowSPTolerance => ProcessValue < (SetPoint * SPTolerance);

        /// <summary>
        /// Tolerance before triggering Correction in fraction of Setpoint
        /// </summary>
        public double TriggerTolerance { get; set; } = 0.8;

        public bool IsBelowTriggerPoint => ProcessValue < (SetPoint * TriggerTolerance);

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
        /// Stepsize in mm
        /// </summary>
        public double StepSize { get; set; } = 5E-4;
        
        /// <summary>
        /// Process variable buffer is filled -> Value is valid
        /// </summary>
        public bool PVBufferFilled { get; private set; }

        public bool StabilizationActive { get; private set; }

        /// <summary>
        /// Do control steps every <var> timer cycles
        /// </summary>
        public double StepTimeMultiplier { get; set; } = 3;
        public bool Activated { get; set; } = false;

        //--------------------------
        // E V E N T S
        //--------------------------

        public event EventHandler<BufferTimerEventArgs> BufferTimerEllapsed;

        private void _onBufferTimerEllapsed(BufferTimerEventArgs e)
        {
            BufferTimerEllapsed?.Invoke(this, e);
        }

        public event EventHandler<StabilizerResult> StabilizationCompleted;

        private void _onStabilizationCompleted(StabilizerResult e)
        {
            StabilizationCompleted?.Invoke(this, e);
        }


        //--------------------------
        // P R I V A T E S
        //--------------------------
        private bool _writeLog => !string.IsNullOrEmpty(Logfile);

        private readonly ILinearStage _stageX;
        private readonly ILinearStage _stageY;
        private readonly Func<double> _getPV;
        private readonly Action<string> _loggerCallback;

        private readonly System.Timers.Timer _PVtimer = new System.Timers.Timer();
        private readonly Queue<double> _PVbuffer = new Queue<double>();
        private volatile bool _bufferRefreshed;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public XYStabilizer(ILinearStage stageX, ILinearStage stageY, Func<double> getPV, Action<string> loggerCallback = null, int bufferStepTime = 1000)
        {
            this._stageX = stageX;
            this._stageY = stageY;
            this._getPV = getPV;
            this._loggerCallback = loggerCallback;

            BufferStepTime = bufferStepTime;

            _PVtimer.Elapsed += _pvTimer_Elapsed;
            _PVtimer.Interval = BufferStepTime;
            _PVtimer.Start();

            if (_writeLog) File.WriteAllLines(Logfile, new string[] { "Time,success,pV,posX,posY"});
        }

        
        public Task<StabilizerResult> CorrectAsync()
        {
            return Task.Run(Correct);
        }

        public StabilizerResult Correct()
        {
            StabilizerResult result = new StabilizerResult();

            if (StabilizationActive)
            {
                WriteLog("Stabilization already active.");
                return result;
            }

            StabilizationActive = true;
            _cts = new CancellationTokenSource();

            int procStep = 0;
            int stageStep = 0;
            double startX = _stageX.Position;
            double startY = _stageY.Position;
            (int x, int y) coords = (0, 0);
            double step_x = 0;
            double step_y = 0;
            double posX = startX;
            double posY = startY;

            (int x, int y) max_coords = (0, 0);
            double max_PV = ProcessValue;
            bool newMaxFound = false;

            void returnToHome()
            {
                WriteLog($"Returning to start position X ={startX} Y={startY}");
                _stageX.Move_Absolute(startX);
                _stageY.Move_Absolute(startY);
            }     

            WriteLog("-------------------------------");
            WriteLog($"Stabilization started at X={startX:e6},Y={startY:e6}| Setpoint={SetPoint} | Tolerance={SPTolerance} | MaxSteps={MaxSteps} | Stepwidth={StepSize}");
            
            while(true)
            {
                if (_cts.IsCancellationRequested)
                {
                    WriteLog("Stabilization cancelled. Returning to start position");
                    returnToHome();
                    break;
                }

                if (!_bufferRefreshed || !PVBufferFilled) continue;

                if(ProcessValue>max_PV)
                {
                    newMaxFound = true;
                    max_coords = coords;
                    max_PV = ProcessValue;
                }

                if (!IsBelowSPTolerance)
                {
                    WriteLog($"Setpoint of {SetPoint} reached with PV={ProcessValue} at dX={step_x} dY={step_y}. Stabilization complete.");
                    if (_writeLog) File.AppendAllLines(Logfile, new string[] { $"{DateTime.Now.ToString("yyyy:MM:dd:HH:mm:ss")},1,{stageStep},{ProcessValue},{posX},{posY}" });
                    result.Success = true;
                    break;
                }


                _bufferRefreshed = false;
                procStep++;
                if (procStep % StepTimeMultiplier != 0) continue;
          
                if (stageStep>MaxSteps)
                {
                    WriteLog($"Maximum Steps of {MaxSteps} Exceeded. Cancelling stabilization.");
                    if(newMaxFound)
                    {
                        step_x = StepSize * max_coords.x;
                        step_y = StepSize * max_coords.y;
                        posX = startX + step_x;
                        posY = startY + step_y;
                        WriteLog($"Moving to new maximum of {max_PV} at Rel: X={step_x:e6} Y={step_y:e6}");
                        if (_writeLog) File.AppendAllLines(Logfile, new string[] { $"{DateTime.Now.ToString("yyyy:MM:dd:HH:mm:ss")},0,{stageStep},{max_PV},{posX},{posY}" });
                        _stageX.Move_Absolute(posX);
                        _stageY.Move_Absolute(posY);
                    }
                    else returnToHome();

                    result.MaxStepsExceeded = true;
                    break;
                }      

                //Main Control sequence
                coords = StepFunctions.AndreasSpiral(stageStep);
                step_x = StepSize * coords.x;
                step_y = StepSize * coords.y;
                posX = startX + step_x;
                posY = startY + step_y;
                WriteLog($"StepNr: {stageStep}/{MaxSteps} | PV: {ProcessValue} | Moving to Rel: X={step_x:e6} Y={step_y:e6}");
                _stageX.Move_Absolute(posX);
                _stageY.Move_Absolute(posY);

                _bufferRefreshed = false;

                stageStep++;
            }

            StabilizationActive = false;
            _onStabilizationCompleted(result);
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

            _onBufferTimerEllapsed(new BufferTimerEventArgs());
        }

        private void WriteLog(string message)
        {
            _loggerCallback?.Invoke("XYStabilization: "+message);    
        }
    }

    public class BufferTimerEventArgs: EventArgs
    {

    }
}
