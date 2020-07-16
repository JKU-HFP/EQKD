using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using EQKDServer.Models;
using TimeTagger_Library.TimeTagger;

namespace EQKDServer.ViewModels.SettingControlViewModels.TimeTaggerViewModels
{
    public class ChannelViewModel : INotifyPropertyChanged
    {
        private ITimeTagger _timetagger;
        private DispatcherTimer _refreshTimer;

        public ObservableCollection<ChannelDiagnosis> ChanDiag { get; set; }

        public ChannelViewModel() : this(null) { }

        public ChannelViewModel(ITimeTagger timetagger)
        {
            if (timetagger == null) return;

            _timetagger = timetagger;
            ChanDiag = new ObservableCollection<ChannelDiagnosis> ( Enumerable.Range(0,_timetagger.NumChannels).Select(i => new ChannelDiagnosis(i)) );
            ChanDiag.Add(new ChannelDiagnosis(-1)); //Sum of all

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Tick += OnRefreshTimerClick;
            _refreshTimer.Interval = new TimeSpan(0,0,1);
            _refreshTimer.IsEnabled = true;
       
        }

        private void OnRefreshTimerClick(object sender, EventArgs e)
        {
            List<int> tagger_Countrate = _timetagger.GetCountrate();

            if (tagger_Countrate.Count < ChanDiag.Count) return;

            for (int i = 0; i < ChanDiag.Count-1; i++) ChanDiag[i].CountRate = tagger_Countrate[i];
            ChanDiag.Last().CountRate = tagger_Countrate.Sum();
        }

        //Events
        public event PropertyChangedEventHandler PropertyChanged;


        internal void OnPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }
    }

}
