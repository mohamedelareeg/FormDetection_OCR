using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Components.Helpers;
using FormDetection_OCR.Constants;
using FormDetection_OCR.ImgHelper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FormDetection_OCR
{
    public class FileWatcherService : BackgroundService
    {
        #region Fields

        private string SettingsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        private const string SettingsFileName = "config.json";
        private readonly string _directoryPath;
        private readonly ILogger<FileWatcherService> _logger;
        private FileSystemWatcher _fileWatcher;
        private bool isQueued = false;
        private Queue<string> Queue = new Queue<string>();
        private string[] templateImagePaths;

        #endregion

        #region Constructor

        public FileWatcherService(ILogger<FileWatcherService> logger)
        {
            LoadSettings();
            templateImagePaths = FormHelper.LoadTemplateImagesFrom();
            _logger = logger;
            _directoryPath = SharedSettings.Instance.LoadedSettings.WatcherPath;
        }

        #endregion

        #region Private Methods

        private void LoadSettings()
        {
            var settingsFilePath = Path.Combine(SettingsFolderPath, SettingsFileName);
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    var settingsJson = File.ReadAllText(settingsFilePath);
                    var settings = JsonConvert.DeserializeObject<SettingsModel>(settingsJson);
                    SharedSettings.Instance.SetSettings(settings);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading settings: {ex.Message}");
                }
            }
            else
            {
                var defaultSettings = new SettingsModel();
                SharedSettings.Instance.SetSettings(defaultSettings);
                try
                {
                    var defaultSettingsJson = JsonConvert.SerializeObject(defaultSettings);
                    File.WriteAllText(settingsFilePath, defaultSettingsJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating default settings file: {ex.Message}");
                }
            }
        }

        private async Task CreateJson(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
            {
                Console.WriteLine($"File not found: {sourceFilePath}");
                return;
            }

            if (Path.GetExtension(sourceFilePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            int maxRetries = 3;
            int retryDelay = 1000;

            for (int retry = 1; retry <= maxRetries; retry++)
            {
                try
                {
                    using (var image = new Bitmap(sourceFilePath))
                    {
                        if (image.Width <= 0 || image.Height <= 0)
                        {
                            Console.WriteLine($"Invalid image: {sourceFilePath}");
                            return;
                        }

                        Bitmap processedImage = await ImageProcessingHelper.DoPerspectiveTransformAsync(sourceFilePath);
                        string templateImagePath = await FormHelper.DetectImageTemplates(processedImage, SharedSettings.Instance.LoadedSettings.FormSimilarity, templateImagePaths);
                        if (templateImagePath == null)
                        {
                            Console.WriteLine("No template match found.");
                        }

                        string json = await FormHelper.DetectTextInZones(processedImage, templateImagePath);
                        dynamic ocrResults = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                        if (string.IsNullOrEmpty(ocrResults["Reservation Number"].ToString()))
                        {
                            json = await FormHelper.DetectTextInPage(processedImage, templateImagePath);
                            ocrResults = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                        }
                        Console.WriteLine(json);

                        string destinationFilePath = Path.Combine(Path.GetDirectoryName(sourceFilePath), $"{fileName}.json");
                        Directory.CreateDirectory(_directoryPath);
                        await File.WriteAllTextAsync(destinationFilePath, json);
                        _logger.LogInformation($"JSON file created for {fileName}: {destinationFilePath}");

                        string claimNum = ocrResults["Reservation Number"].ToString();
                        string claimDescription = ocrResults["Clinic"].ToString();
                        string claimDate = ocrResults["Date"].ToString();
                        DateTime date = DateTime.ParseExact(claimDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        string formattedDate = date.ToString("yyyy-M-d");
                        string patientId = ocrResults["MRN"].ToString();

                        var postData = new MultipartFormDataContent
                        {
                            { new StringContent(claimNum), "Claim_Num" },
                            { new StringContent(claimDescription), "Claim_Description" },
                            { new StringContent(formattedDate), "Claim_Date" },
                            { new StringContent(patientId), "Patient_ID" }
                        };

                        using (var client = new HttpClient())
                        {
                            var response = await client.PostAsync(SharedSettings.Instance.LoadedSettings.apiUrl, postData);

                            if (response.IsSuccessStatusCode)
                            {
                                Console.WriteLine("Data posted successfully to the API.");
                                if (response.Content != null)
                                {
                                    string responseContent = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine("Response Content: " + responseContent);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Error posting data to the API.");
                                Console.WriteLine("Response StatusCode: " + response.StatusCode);
                                if (response.Content != null)
                                {
                                    string responseContent = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine("Response Content: " + responseContent);
                                }
                            }
                        }
                    }
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Parameter is not valid for image: {sourceFilePath}");
                    _logger.LogWarning($"Parameter is not valid for image: {sourceFilePath}");

                    if (retry < maxRetries)
                    {
                        await Task.Delay(retryDelay);
                    }
                    else
                    {
                        _logger.LogError($"Error creating JSON file for {fileName}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    _logger.LogError($"Error creating JSON file for {fileName}: {ex.Message}");
                }
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            Queue.Enqueue(e.FullPath);
            if (!isQueued)
            {
                ProcessUploadQueue();
            }
        }

        private async void ProcessUploadQueue()
        {
            if (isQueued)
            {
                return;
            }

            isQueued = true;

            while (Queue.Count > 0)
            {
                string filePath = Queue.Dequeue();
                await CreateJson(filePath);
            }

            isQueued = false;
        }

        #endregion

        #region Overridden Methods

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FileWatcherService is starting.");

            _fileWatcher = new FileSystemWatcher(_directoryPath);
            _fileWatcher.IncludeSubdirectories = true;
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.EnableRaisingEvents = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            _logger.LogInformation("FileWatcherService is stopping.");
        }

        #endregion
    }
}
