using Authenticator.Core.Interfaces;

namespace Authenticator
{
    public class NLogHistory : IHistory
    {
        private static NLog.Logger History;

        public string HistoryPath { get; set; }

        public NLogHistory()
        {
            History = NLog.LogManager.GetLogger("history");
            HistoryPath = "history";
        }

        public void Write(string message)
        {
            History.Info(message);
        }
    }
}
