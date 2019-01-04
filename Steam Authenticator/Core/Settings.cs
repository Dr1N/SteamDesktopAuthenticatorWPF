using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace Authenticator
{
    class Settings
    {
        [JsonIgnore]
        public static string DefaultPath = "settings.json";

        public string AccountFilePath;
        public string Password;
        public bool IsUpdate;
        public bool IsConfirm;
        public int Timeout;

        public void Save(string path = "")
        {
            string settingStr = JsonConvert.SerializeObject(this, Formatting.Indented);
            string currentPath = String.IsNullOrEmpty(path) == false ? path : DefaultPath;
            File.WriteAllText(currentPath, settingStr, Encoding.UTF8);
        }

        public static Settings Load(string path = "")
        {
            Settings result = null;
            try
            {
                string currentPath = String.IsNullOrEmpty(path) == false ? path : DefaultPath;
                if (File.Exists(currentPath) == true)
                {
                    result = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(currentPath));
                }
            }
            catch (Exception)
            {
                result = null;
            }

            return result;
        }

        public static void Clear(string path = "")
        {
            string currentPath = String.IsNullOrEmpty(path) == false ? path : DefaultPath;
            if (File.Exists(currentPath) == true && currentPath.EndsWith(".json"))
            {
                File.Delete(currentPath);
            }
        }
    }
}
