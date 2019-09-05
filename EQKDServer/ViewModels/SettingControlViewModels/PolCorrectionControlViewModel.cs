using EQKDServer.Models;
using EQKDServer.Models.Messages;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EQKDServer.ViewModels.SettingControlViewModels
{
    class PolCorrectionControlViewModel : ViewModelBase
    {
        //Private fields
        private EQKDServerModel _EQKDServer;

        public PolCorrectionControlViewModel()
        {
            //Handle Messages
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer = servermsg.EQKDServer;
            });
        }
    }
}
