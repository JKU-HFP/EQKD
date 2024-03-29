﻿using EQKDServer.Models;
using EQKDServer.Models.Messages;
using GalaSoft.MvvmLight.Messaging;
using SecQNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TimeTagger_Library;

namespace EQKDServer.ViewModels.SettingControlViewModels
{
        
    public class NetworkControlViewModel : ViewModelBase
    {
        //Private fields
        private EQKDServerModel _EQKDServer;

        //Properties
        private string _serverIPAddress;
        public string ServerIPAddress
        {
            get { return _serverIPAddress; }
            set
            {
                _serverIPAddress = value;
                OnPropertyChanged("ServerIPAddress");
            }
        }

        private int _port;
        public int Port
        {
            get { return _port; }
            set
            {
                _port = value;
                OnPropertyChanged("Port");
            }
        }

        //Commands
        public RelayCommand<object> StartListeningToNetworkCommand { get; private set; }

        //Contructor
        public NetworkControlViewModel()
        {
            Messenger.Default.Register<EQKDServerCreatedMessage>(this, (servermsg) =>
            {
                _EQKDServer = servermsg.EQKDServer;
            });

            //Route Commands
            StartListeningToNetworkCommand = new RelayCommand<object>(StartListeningToNetwork);

            Port = 4242;
        }      

        private void StartListeningToNetwork(object o)
        {
            //_EQKDServer.densMeas.MeasurePeakAreasAsync();

            _EQKDServer.SecQNetServer.ConnectAsync(SecQNetServer.GetIP4Address(), Port);
            ServerIPAddress = _EQKDServer.SecQNetServer.ServerIP.ToString();
        }
             
    }
}
