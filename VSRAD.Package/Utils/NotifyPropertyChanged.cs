using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VSRAD.Package.Utils
{
    public abstract class DefaultNotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /* https://stackoverflow.com/a/1316417 */
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null, bool raiseIfEqual = false, bool ignoreNull = false)
        {
            if (!raiseIfEqual && EqualityComparer<T>.Default.Equals(field, value))
                return false;
            if (ignoreNull && value == null)
                return false;

            field = value;
            RaisePropertyChanged(propertyName);

            return true;
        }

        protected void RaisePropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
