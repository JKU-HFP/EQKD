using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using EQKDServer.Models;

namespace EQKDServer.ViewModels.SettingControlViewModels.TimeTaggerViewModels
{
    public class ChannelViewModel : INotifyPropertyChanged
    {
        private EQKDServerModel _EQKDServer;
        private DispatcherTimer _refreshTimer;

        public ObservableCollection<ChannelDiagnosis> ChanDiag { get; set; }

        public ChannelViewModel() : this(null) { }

        public ChannelViewModel(EQKDServerModel eqkdserver)
        {
            _EQKDServer = eqkdserver;
            ChanDiag = new ObservableCollection<ChannelDiagnosis> { new ChannelDiagnosis(0), new ChannelDiagnosis(1), new ChannelDiagnosis(2), new ChannelDiagnosis(3) };      
            
            if(_EQKDServer!=null && _EQKDServer.ServerTimeTagger!=null)
            {
                _refreshTimer = new DispatcherTimer();
                _refreshTimer.Tick += OnRefreshTimerClick;
                _refreshTimer.Interval = new TimeSpan(0,0,1);
                _refreshTimer.IsEnabled = true;
            }
        }

        private void OnRefreshTimerClick(object sender, EventArgs e)
        {
            List<int> tagger_Countrate = _EQKDServer.ServerTimeTagger.GetCountrate();

            if (tagger_Countrate.Count < ChanDiag.Count) return;

            for (int i = 0; i < ChanDiag.Count; i++) ChanDiag[i].CountRate = tagger_Countrate[i];
        }

        //Events
        public event PropertyChangedEventHandler PropertyChanged;


        internal void OnPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }
    }

}
