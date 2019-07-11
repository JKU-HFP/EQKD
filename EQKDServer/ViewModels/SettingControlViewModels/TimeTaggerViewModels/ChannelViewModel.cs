using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EQKDServer.Models;

namespace EQKDServer.ViewModels.SettingControlViewModels.TimeTaggerViewModels
{
    public class ChannelViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ChannelDiagnosis> ChanDiag { get; set; }

        public ChannelViewModel()
        {
            ChanDiag = new ObservableCollection<ChannelDiagnosis> { new ChannelDiagnosis(0), new ChannelDiagnosis(1), new ChannelDiagnosis(2), new ChannelDiagnosis(3) };           
        }

        //Events
        public event PropertyChangedEventHandler PropertyChanged;

        internal void OnPropertyChanged(string propname)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propname));
        }
    }

}
