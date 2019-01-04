namespace Authenticator.Core.Interfaces
{
    public interface IHistory
    {
        string HistoryPath { get; set; }
        void Write(string message);
    }
}
