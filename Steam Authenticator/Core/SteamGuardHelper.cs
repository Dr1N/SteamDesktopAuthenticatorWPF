using Newtonsoft.Json;
using SteamAuth;
using System;
using System.IO;

namespace Authenticator
{
    public class SteamGuardHelper
    {
        private SteamGuardAccount _currentSteamGuardAccount;
        public SteamGuardAccount CurrentSteamGuard
        {
            get
            {
                return _currentSteamGuardAccount;
            }
        }

        public string AccountFilePath { get; set; }

        public void Initialize(string filePath)
        {
            if (String.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                throw new ArgumentException($"Invalid account file path [{Path.GetDirectoryName(filePath)}]", nameof(filePath));
            }

            _currentSteamGuardAccount = CreateSteamGuardAccountFromFilePath(filePath);
            AccountFilePath = filePath;
        }

        private SteamGuardAccount CreateSteamGuardAccountFromFilePath(string filePath)
        {
            SteamGuardAccount result = null;
            if (!String.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                result = JsonConvert.DeserializeObject<SteamGuardAccount>(File.ReadAllText(filePath));
            }
   
            return result;
        }
    }
}
