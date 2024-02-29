using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormDetection_OCR.Constants
{
    public class SharedSettings
    {
        private static SharedSettings instance;
        private SettingsModel loadedSettings;

        private SharedSettings()
        {
        }

        public static SharedSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SharedSettings();
                }
                return instance;
            }
        }

        public SettingsModel LoadedSettings
        {
            get { return loadedSettings; }
        }

        public void SetSettings(SettingsModel settings)
        {
            loadedSettings = settings;
        }

        public SettingsModel GetSettings()
        {
            return loadedSettings;
        }
    }
    public class SettingsModel
    {
        public SettingsModel()
        {
        }
       
        public string WatcherPath = "C:\\WatchedFolder";
        public double FormSimilarity = 5;
        public string apiUrl = "apiURL";
        public bool EnableFTP = false;
        public string FTPServerURL = "ftp://sample/";
        public string FTPUserName = "username";
        public string FTPPassword = "password";
        public string FTPWatchedDirectory = "\\Papers\\";
        public string FTPTempDirectory = "\\Temp\\";
    }
}
