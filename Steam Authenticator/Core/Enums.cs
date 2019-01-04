namespace Authenticator.Core
{
    public enum ConfirmationAction
    {
        Accept,
        Decline,
    }

    public enum LoginShowReason
    {
        ActivateAuthenticator,
        RefreshSession,
    }

    public enum ConfirmationStatus
    {
        Unknow,
        Processing,
        Waiting,
        Accepted,
        Declined,
    }

    public enum AuthenticatorState
    {
        Ready,
        Error,
        Wait,
        ConfimationUpdating,
        ConfimationUpdated,
        ConfimationError,
        ConfirmationProcessing,
        ConfirmationProcessed,
        SessionRefreshing,
        SessionRefreshed,
        Relogin,
        ReloginSuccess,
        ReloginError,
    }

    public enum ConfirmationActionResult
    {
        None,
        Fetched,
        Added,
        Accept,
        Decline,
        Error,
    }
}
