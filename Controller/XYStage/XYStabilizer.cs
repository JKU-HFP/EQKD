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
        /// Setpoint tolerance
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
        public int BufferStepTime { get; set; } = 1000;
        
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
        // E N U M E R A B L E S
        //--------------------------
        

        //--------------------------
        // P R I V A T E S
        //--------------------------
        private readonly ILinearStage _stageX;
        private readonly ILinearStage _stageY;
        private readonly Func<double> _getPV;

        private readonly System.Timers.Timer _coreTimer = new System.Timers.Timer();
        private readonly Queue<double> _PVbuffer = new Queue<double>();

        private int _currStep = 0;
        private double _Xtotal = 0;
        private double _Ytotal = 0;
  
        public XYStabilizer(ILinearStage stageX, ILinearStage stageY, Func<double> getPV)
        {
            this._stageX = stageX;
            this._stageY = stageY;
            this._getPV = getPV;

            _coreTimer.Elapsed += _coreTimer_Elapsed;
        }

        public void StartStabilize()
        {
            _coreTimer.Interval = BufferStepTime;
            _coreTimer.Start();
            StabilizationActive = true;
        }

        public void StopStabilize()
        {
            _coreTimer.Stop();
            StabilizationActive = false;
        }

        private void _coreTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (NumBuffer < 1) NumBuffer = 1;

            _PVbuffer.Enqueue(_getPV());
            while (_PVbuffer.Count > NumBuffer) _PVbuffer.Dequeue();

            ProcessValue = _PVbuffer.Average();

            PVBufferFilled = _PVbuffer.Count >= NumBuffer;

            //Control required?
            if (!PVBufferFilled) return;
            if (++_currStep < StepTimeMultiplier) return;
            _currStep = 0;

            if (SetpointReached) return;

            //Main Control sequence



        }
    }
}
