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
       
        public string WatcherPath = "\\\\10.10.10.190\\kodak";
        public double FormSimilarity = 5;
        public string apiUrl = "http://10.10.10.60:771/api/EClaims/PostClaim";
        public bool EnableFTP = false;
        public string FTPServerURL = "ftp://ftp-michael-george.alwaysdata.net/";
        public string FTPUserName = "michael-george";
        public string FTPPassword = "P@ssw0rd123$%^";
        public string FTPWatchedDirectory = "\\Papers\\";
        public string FTPTempDirectory = "\\Temp\\";
    }
}
