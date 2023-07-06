using Authenticator.Core;
using SteamAuth;
using System.ComponentModel;
using System.Windows.Input;

namespace Authenticator
{
    public class ConfirmationItem : Confirmation, INotifyPropertyChanged
    {
        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Fields & Properties

        #region Display Properties

        public int Number { get; set; }
        public ulong DisplayID => ID;
        public ulong DisplayKey => Key;
        public ulong DisplayCreator => Creator;
        public EMobileConfirmationType DisplayType => ConfType;

        #endregion

        private ConfirmationStatus _status = ConfirmationStatus.Waiting;
        public ConfirmationStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                OnPropertyChanged("Status");
            }
        }

        #endregion

        #region Life Cycle

        public ConfirmationItem(ulong id, ulong key, EMobileConfirmationType type, ulong creator)
        {
            ID = id;
            Key = key;
            ConfType = type;
            Creator = creator;
        }

        public ConfirmationItem(Confirmation confirmation)
        {
            ID = confirmation.ID;
            Key = confirmation.Key;
            ConfType = confirmation.ConfType;
            Creator = confirmation.Creator;
        }

        #endregion

        #region Override Methods

        public override bool Equals(object obj)
        {
            if (obj is ConfirmationItem item)
            {
                return ID == item.ID && Key == item.Key && Creator == item.Creator;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hashCode = -2141251448;
            hashCode = hashCode * -1521134295 + DisplayID.GetHashCode();
            hashCode = hashCode * -1521134295 + DisplayKey.GetHashCode();
            hashCode = hashCode * -1521134295 + DisplayCreator.GetHashCode();

            return hashCode;
        }

        public override string ToString()
        {
            return $"Confirmation ID:{ID} Key:{Key} Creator:{Creator} Type:{ConfType}";
        }

        #endregion

        #region Private Methods

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
