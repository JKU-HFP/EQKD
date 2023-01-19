using SecQNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDServer.Models.Hardware
{
    public class Operations: Connections
    {
        public Operations(Action<string> loggerCallback, SecQNetServer secQNetServer): base(loggerCallback, secQNetServer)
        {

        }

        public void PolarizerControl(bool status)
        {
            switch (status)
            {
                case true:
                    PolarizerFlipper.Move(INSERTEDPOS);
                    break;
                case false:
                    PolarizerFlipper.Move(REMOVEDPOS);
                    break;
                default:
                    break;
            }
        }
        public void TriggerShutter(bool status)
        {
            switch (status)
            {
                case true:
                    ShutterFlipper.Move(INSERTEDPOS);
                    break;
                case false:
                    ShutterFlipper.Move(REMOVEDPOS);
                    break;
                default:
                    break;
            }
        }
        public Task MoveXYStage(int direction)
        {
            double step = 0.2E-3;

            return Task.Run(() =>
            {
                switch (direction)
                {
                    case 0:
                        YStage.Move_Relative(step);
                        break;
                    case 1:
                        YStage.Move_Relative(-step);
                        break;
                    case 2:
                        XStage.Move_Relative(step);
                        break;
                    case 3:
                        XStage.Move_Relative(-step);
                        break;
                }
            });
        }
        public Task XYStageOptimize()
        {
            return XYStabilizer.CorrectAsync();
        }


    }
}
