using SteamAuth;
using System;

namespace Authenticator.Core
{
    #region Login Events

    public class AuthLoginEventArgs : EventArgs
    {
        public bool Result { get; private set; }

        public AuthLoginEventArgs(bool result)
        {
            Result = result;
        }
    }

    public class AuthLinkerEventArgs : EventArgs
    {
        public AuthenticatorLinker.LinkResult Result { get; private set; }

        public AuthLinkerEventArgs(AuthenticatorLinker.LinkResult result)
        {
            Result = result;
        }
    }

    public class AuthFinalizeEventArgs : EventArgs
    {
        public AuthenticatorLinker.FinalizeResult Result { get; private set; }

        public AuthFinalizeEventArgs(AuthenticatorLinker.FinalizeResult result)
        {
            Result = result;
        }
    }

    #endregion

    #region Authenticator Events

    public class AuthenticatorStateChangedEventArgs : EventArgs
    {
        public AuthenticatorState State { get; private set; }

        public AuthenticatorStateChangedEventArgs(AuthenticatorState state)
        {
            State = state;
        }
    }

    #endregion

    #region Confirmations Events

    public class AuthenticatorConfirmationsEventArgs : EventArgs
    {
        public ConfirmationActionResult Action { get; private set; }
        public int Count { get; private set; }

        public AuthenticatorConfirmationsEventArgs(ConfirmationActionResult action, int count)
        {
            Action = action;
            Count = count;
        }
    }

    public class AuthenticatorConfirmationEventArgs : EventArgs
    {
        public ConfirmationActionResult Action { get; private set; }
        public ConfirmationItem Item { get; private set; }

        public AuthenticatorConfirmationEventArgs(ConfirmationActionResult action, ConfirmationItem item)
        {
            Action = action;
            Item = item;
        }
    }

    #endregion
}
