using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VSRAD.Package.Utils
{
    public sealed class HostItem : DefaultNotifyPropertyChanged
    {
        private string _host = "";
        public string Host { get => _host; set => SetField(ref _host, value); }

        private string _alias = "";
        public string Alias { get => _alias; set => SetField(ref _alias, value); }

        public bool UsedInActiveProfile { get; }

        public HostItem(string host, bool usedInActiveProfile, string alias = "")
        {
            Alias = alias;
            Host = host;
            UsedInActiveProfile = usedInActiveProfile;
        }
    }
}
