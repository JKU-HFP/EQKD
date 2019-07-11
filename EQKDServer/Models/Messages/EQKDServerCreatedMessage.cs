using SecQNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDServer.Models.Messages
{
    public class EQKDServerCreatedMessage
    {
        public readonly EQKDServerModel EQKDServer;

        public EQKDServerCreatedMessage(EQKDServerModel server)
        {
            EQKDServer = server;
        }
    }
}
