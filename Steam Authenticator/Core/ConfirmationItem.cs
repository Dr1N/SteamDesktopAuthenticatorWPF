using Authenticator.Core;
using SteamAuth;
using System.ComponentModel;

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
        public ConfirmationType DisplayType => ConfType;

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

        public ConfirmationItem(ulong id, ulong key, int type, ulong creator)
            : base(id, key, type, creator) { }

        public ConfirmationItem(Confirmation confirmation)
            : base(confirmation.ID, confirmation.Key, confirmation.IntType, confirmation.Creator) { }

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
