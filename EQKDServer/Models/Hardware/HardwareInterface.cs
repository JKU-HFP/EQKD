using SecQNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDServer.Models.Hardware
{
    public class HardwareInterface: Operations
    {
        public HardwareInterface(Action<string> loggerCallback, SecQNetServer secQNetServer) : base(loggerCallback, secQNetServer)
        {

        }
    }
}
