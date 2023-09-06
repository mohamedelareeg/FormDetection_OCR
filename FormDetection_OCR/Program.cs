using Components.Helpers;
using FormDetection_OCR;
using FormDetection_OCR.Constants;
using FormDetection_OCR.ImgHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using static System.Net.WebRequestMethods;

class Program
{
    private static string SettingsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
    private static string SettingsFileName = "config.json";
    static async Task Main(string[] args)
    {
        LoadSettings();
        await CreateHostBuilder(args).Build().RunAsync();
    }
    private static void LoadSettings()
    {
        var settingsFilePath = Path.Combine(SettingsFolderPath, SettingsFileName);
        if (System.IO.File.Exists(settingsFilePath))
        {
            try
            {
                var settingsJson = System.IO.File.ReadAllText(settingsFilePath);
                var settings = JsonConvert.DeserializeObject<SettingsModel>(settingsJson);

                // Store the loaded settings in a shared location accessible to other windows or components
                SharedSettings.Instance.SetSettings(settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }
        else
        {

            // Create a new settings file with default model values
            var defaultSettings = new SettingsModel();
            SharedSettings.Instance.SetSettings(defaultSettings);
            try
            {
                var defaultSettingsJson = JsonConvert.SerializeObject(defaultSettings);
                System.IO.File.WriteAllText(settingsFilePath, defaultSettingsJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default settings file: {ex.Message}");
            }
        }

    }
    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                if (SharedSettings.Instance.LoadedSettings.EnableFTP)
                {
                    services.AddHostedService<FTPWatcherService>();
                }
                else
                {
                    services.AddHostedService<FileWatcherService>();
                }
            })
            .UseWindowsService() // Use Windows service instead of console lifetime
            .ConfigureLogging((hostContext, logging) =>
            {
                logging.ClearProviders();
                logging.AddEventLog(settings =>
                {
                    settings.SourceName = "FolderWatcher";
                    settings.LogName = "Application";
                });
            });
}


