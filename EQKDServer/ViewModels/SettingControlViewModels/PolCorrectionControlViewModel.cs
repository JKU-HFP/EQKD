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

        //Commands
        public RelayCommand<object> StartCorrectionCommand { get; private set; }
        public RelayCommand<object> CancelCommand { get; private set; }

        public PolCorrectionControlViewModel()
        {
            //Map RelayCommmands
            StartCorrectionCommand = new RelayCommand<object>((o) => _EQKDServer.StartFiberCorrectionAsync());

            CancelCommand = new RelayCommand<object>((o) =>
            {
               
            });

            //Handle Messages
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer = servermsg.EQKDServer;
            });
        }
    }
}
