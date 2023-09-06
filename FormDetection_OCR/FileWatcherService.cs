using Components.Helpers;
using FormDetection_OCR.Constants;
using FormDetection_OCR.ImgHelper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormDetection_OCR
{
    public class FileWatcherService : BackgroundService
    {
        private string SettingsFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings");
        private const string SettingsFileName = "config.json";
        private readonly string _directoryPath;
        private readonly ILogger<FileWatcherService> _logger;
        private FileSystemWatcher _fileWatcher;
        private bool isQueued = false;
        private Queue<string> Queue = new Queue<string>();
        private string[] templateImagePaths;

        public FileWatcherService(ILogger<FileWatcherService> logger)
        {
            LoadSettings();
            templateImagePaths = FormHelper.LoadTemplateImagesFrom();
            _logger = logger;

            // Set the directory path to monitor
            _directoryPath = SharedSettings.Instance.LoadedSettings.WatcherPath;
        }
        private void LoadSettings()
        {
            var settingsFilePath = Path.Combine(SettingsFolderPath, SettingsFileName);
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    var settingsJson = File.ReadAllText(settingsFilePath);
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
                    File.WriteAllText(settingsFilePath, defaultSettingsJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating default settings file: {ex.Message}");
                }
            }

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FileWatcherService is starting.");

            _fileWatcher = new FileSystemWatcher(_directoryPath);
            _fileWatcher.IncludeSubdirectories = true;
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.EnableRaisingEvents = true;

            while (!stoppingToken.IsCancellationRequested)
            {
                // Perform background tasks here (if any)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            _logger.LogInformation("FileWatcherService is stopping.");
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            Queue.Enqueue(e.FullPath);

            // Start the upload process if it's not already in progress
            if (!isQueued)
            {
                ProcessUploadQueue();
            }


        }

        private async void ProcessUploadQueue()
        {
            // Check if upload is already in progress
            if (isQueued)
            {
                return;
            }

            // Set uploading flag to true
            isQueued = true;

            while (Queue.Count > 0)
            {
                string filePath = Queue.Dequeue();



                // Upload the file
                bool uploadStatus = await CreateJson(filePath);

                // Perform actions based on upload status
                if (uploadStatus)
                {

                }
                else
                {

                }
            }

            // Set uploading flag to false
            isQueued = false;
        }
        private async Task<bool> CreateJson(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
            {
                Console.WriteLine($"File not found: {sourceFilePath}");
                return false;
            }

            // Check if the file has a .json extension
            if (Path.GetExtension(sourceFilePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                return false; // Skip processing .json files
            }

            // Rest of the code to create the JSON file
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);

            int maxRetries = 3;
            int retryDelay = 1000; // 1 second delay between retries

            for (int retry = 1; retry <= maxRetries; retry++)
            {
                try
                {
                    // Create a Bitmap object from the source image file
                    using (var image = new Bitmap(sourceFilePath))
                    {
                        // Check if the image is valid
                        if (image.Width <= 0 || image.Height <= 0)
                        {
                            Console.WriteLine($"Invalid image: {sourceFilePath}");
                            return false;
                        }

                        //
                        Bitmap processedImage = await ImageProcessingHelper.DoPerspectiveTransformAsync(sourceFilePath);
                        // Preprocess the image to improve quality
                        //Bitmap processedImage = PreprocessImage(image);

                        // Load the detected template image
                        string templateImagePath = await FormHelper.DetectImageTemplates(processedImage, SharedSettings.Instance.LoadedSettings.FormSimilarity, templateImagePaths);
                        if (templateImagePath == null)
                        {
                            // No template match found
                            Console.WriteLine("No template match found.");
                        }
                        // Detect the text in zones and get the JSON string
                        string json = await FormHelper.DetectTextInZones(processedImage, templateImagePath);

                        dynamic ocrResults = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                        // Now you can work with the extracted results
                        if (string.IsNullOrEmpty(ocrResults["Reservation Number"].ToString()))
                        {
                            json = await FormHelper.DetectTextInPage(processedImage, templateImagePath);


                            ocrResults = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                        }
                        // Print the JSON array to the console
                        Console.WriteLine(json);

                        // Set the destination file path
                        string destinationFilePath = Path.Combine(Path.GetDirectoryName(sourceFilePath), $"{fileName}.json");

                        // Write the JSON file
                        Directory.CreateDirectory(_directoryPath);
                        await File.WriteAllTextAsync(destinationFilePath, json);

                        _logger.LogInformation($"JSON file created for {fileName}: {destinationFilePath}");

                        // Extract values from the JSON and post them to the API
                        // JObject jsonObject = JObject.Parse(json);
                        string claimNum = ocrResults["Reservation Number"].ToString(); //jsonObject.GetValue("Reservation Number:")?.ToString();
                        string claimDescription = ocrResults["Clinic"].ToString();//jsonObject.GetValue("Clinic")?.ToString();
                        string claimDate = ocrResults["Date"].ToString();//jsonObject.GetValue("Date:")?.ToString();
                        DateTime date = DateTime.ParseExact(claimDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        string formattedDate = date.ToString("yyyy-M-d");
                        string patientId = ocrResults["MRN"].ToString();//jsonObject.GetValue("MRN:")?.ToString();


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
                                return true;
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
                                return false;
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
                        // Delay before retrying
                        await Task.Delay(retryDelay);
                    }
                    else
                    {
                        _logger.LogError($"Error creating JSON file for {fileName}: {ex.Message}");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    _logger.LogError($"Error creating JSON file for {fileName}: {ex.Message}");
                    return false;
                }
            }

            return false;
        }



    }
}
