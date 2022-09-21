using Controller.XYStage;
using Stage.NewPort;
using Stage.PI;
using Stage.Thorlabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library.TimeTagger;

namespace EQKDServer.Models.Hardware
{
    public class Catalogue
    {
        //-----------------------------------
        //---- C O N S T A N T S 
        //-----------------------------------

        protected const uint REMOVEDPOS  = 2;
        protected const uint INSERTEDPOS = 1;

        private bool EXTERNAL_CLOCK = false;

        //Time Tagger
        public HydraHarp Hydra { get; protected set; }
        public NetworkTagger nwtagger { get; protected set; }
        public ITimeTagger ServerTimeTagger { get; set; }
        public ITimeTagger ClientTimeTagger { get; set; }
        //Stabilization
        public XYStabilizer XYStabilizer { get; protected set; }
        public bool AutoStabilization { get; set; }
        //Rotation Stages
        public List<SMC100Stage> _smcStages;
        public SMC100Controller _smcController { get; protected set; }
        public SMC100Stage _HWP_A { get; protected set; }
        public KPRM1EStage _QWP_A { get; protected set; }
        public SMC100Stage _HWP_B { get; protected set; }
        public KPRM1EStage _HWP_C { get; protected set; }
        public KPRM1EStage _QWP_B { get; protected set; }
        public KPRM1EStage _QWP_C { get; protected set; }
        public KPRM1EStage _QWP_D { get; protected set; }
        //Linear stages
        public MFF101Flipper PolarizerFlipper { get; protected set; }
        public MFF101Flipper ShutterFlipper { get; protected set; }
        public PI_C843_Controller XY_Controller { get; protected set; }
        public PI_C843_Stage XStage { get; protected set; }
        public PI_C843_Stage YStage { get; protected set; }
    }
}

