using Components.Helpers;
using FluentFTP;
using FormDetection_OCR.Constants;
using FormDetection_OCR.ImgHelper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FormDetection_OCR
{
    public class FTPWatcherService : BackgroundService
    {

        private string SaveFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fetched");
        private string Temppath = SharedSettings.Instance.LoadedSettings.FTPTempDirectory;
        private readonly string _directoryPath;
        private readonly ILogger<FTPWatcherService> _logger;
        private FileSystemWatcher _fileWatcher;
        private bool isQueued = false;
        private Queue<string> Queue = new Queue<string>();
        private string[] templateImagePaths;

        private readonly string _ftpServerUri;
        private readonly string _ftpUsername;
        private readonly string _ftpPassword;
        private readonly string _ftpDirectory;
        public FTPWatcherService(ILogger<FTPWatcherService> logger)
        {
          
            templateImagePaths = FormHelper.LoadTemplateImagesFrom();
            _logger = logger;

            _ftpServerUri = SharedSettings.Instance.LoadedSettings.FTPServerURL;
            _ftpUsername = SharedSettings.Instance.LoadedSettings.FTPUserName;
            _ftpPassword = SharedSettings.Instance.LoadedSettings.FTPPassword;
            _ftpDirectory = SharedSettings.Instance.LoadedSettings.FTPWatchedDirectory;
            // Set the directory path to monitor
            _directoryPath = SharedSettings.Instance.LoadedSettings.WatcherPath;
        }
      
        private List<string> downloadedFiles = new List<string>();
        private Queue<string> queue = new Queue<string>();
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FtpFileWatcherService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // List the files in the FTP directory
                List<string> fileNames = ListAllFtpFilesAndFolders(_ftpDirectory);

                foreach (string fileName in fileNames)
                {
                  
                        // Download the file from FTP to the local directory
                        string localFilePath = Path.Combine(SaveFolderPath, fileName);
                        bool success = DownloadFileFromFtp(fileName, localFilePath);

                        if (success)
                        {
                            DeleteFileFromFtp(fileName);
                            // Enqueue the file path for processing
                            Queue.Enqueue(localFilePath);
                            // Process the file
                            await ProcessNextFileAsync();

                    }
                        // Process files from the queue
                        while (queue.Count > 0)
                        {
                            string filePathToProcess = queue.Dequeue();
                            await CreateJson(filePathToProcess);
                        }



                }
                // Delay for a specific interval before checking again
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }

            _logger.LogInformation("FtpFileWatcherService is stopping.");
        }
        private bool DeleteFileFromFtp(string fileName)
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(new Uri($"{_ftpServerUri}/{_ftpDirectory}/{fileName}"));
                request.Credentials = new NetworkCredential(_ftpUsername, _ftpPassword);
                request.Method = WebRequestMethods.Ftp.DeleteFile;

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                {
                    // Check the response for success
                    if (response.StatusCode == FtpStatusCode.FileActionOK)
                    {
                        return true;
                    }
                    else
                    {
                        _logger.LogError($"Error deleting file {fileName} from FTP: {response.StatusDescription}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting file {fileName} from FTP: {ex.Message}");
                return false;
            }
        }
        private List<string> ListAllFtpFilesAndFolders(string directory)
        {
            List<string> filesAndFolders = new List<string>();

            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"{_ftpServerUri}/{directory}");
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_ftpUsername, _ftpPassword);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string[] entries = reader.ReadToEnd().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string entry in entries)
                    {
                        // Add the entry (file or folder) to the list
                        filesAndFolders.Add(entry);

                        // If the entry is a directory, recursively list its contents
                        if (IsDirectory(entry))
                        {
                            List<string> subEntries = ListAllFtpFilesAndFolders(Path.Combine(directory, entry));
                            filesAndFolders.AddRange(subEntries);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing FTP files and folders: {ex.Message}");
            }

            return filesAndFolders;
        }

        private bool IsDirectory(string entry)
        {
            // You may need to adjust this check based on the format of your FTP server listings
            return !string.IsNullOrWhiteSpace(entry) && !entry.Contains(".");
        }

        private string[] ListFtpFiles()
        {
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create($"{_ftpServerUri}/{_ftpDirectory}");
                request.Method = WebRequestMethods.Ftp.ListDirectory;
                request.Credentials = new NetworkCredential(_ftpUsername, _ftpPassword);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd().Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error listing FTP files: {ex.Message}");
                return new string[0];
            }
        }

        private bool DownloadFileFromFtp(string fileName, string localFilePath)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(_ftpUsername, _ftpPassword);
                    string ftpFilePath = $"{_ftpServerUri}/{_ftpDirectory}/{fileName}";

                    // Ensure the local directory path exists, creating subfolders if necessary
                    string localDirectory = Path.GetDirectoryName(localFilePath);
                    if (!Directory.Exists(localDirectory))
                    {
                        Directory.CreateDirectory(localDirectory);
                    }

                    client.DownloadFile(ftpFilePath, localFilePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error downloading file {fileName} from FTP: {ex.Message}");
                return false;
            }
        }
        private readonly SemaphoreSlim _processingSemaphore = new SemaphoreSlim(1);

        private async Task ProcessNextFileAsync()
        {
            if (Queue.Count == 0)
            {
                return; // No files in the queue
            }

            // Wait for the semaphore, ensuring only one file is processed at a time
            await _processingSemaphore.WaitAsync();

            try
            {
                string filePath = Queue.Peek(); // Peek at the next file without dequeuing
                bool success = await CreateJson(filePath);

                if (success)
                {
                    // Remove the file from the queue if processing is successful
                    Queue.Dequeue();
                }
                else
                {
                    // Handle any error or retry logic here
                }
            }
            finally
            {
                // Release the semaphore so the next file can be processed
                _processingSemaphore.Release();
            }
        }

        private async Task<bool> CreateJson(string sourceFilePath)
        {

            if (!downloadedFiles.Contains(sourceFilePath))
            {
                downloadedFiles.Add(sourceFilePath);
            }
            else
            {
                return false;
            }

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

            int maxRetries = 1;
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

                        string folderPath = Path.GetDirectoryName(sourceFilePath);

                        // Set the destination file path for the JSON file
                        string destinationJsonFilePath = Path.Combine(folderPath, $"{fileName}.json");

                        await File.WriteAllTextAsync(destinationJsonFilePath, json);

                        _logger.LogInformation($"JSON file created for {fileName}: {destinationJsonFilePath}");

                        string parentFolderName = Path.GetFileName(Path.GetDirectoryName(sourceFilePath));

                        // Create a folder with the same name as the parent folder on the FTP server
                        string destinationFtpFolder = $"{Temppath}/{parentFolderName}";
                        CreateFolderOnFTP(destinationFtpFolder);

                        // Upload the JSON file to the FTP server within the created folder
                        string destinationFtpPath = $"{destinationFtpFolder}/{fileName}.json";
                        UploadFileToFTP(destinationJsonFilePath, destinationFtpPath);

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
                                return true;
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
        private void CreateFolderOnFTP(string folderPath)
        {
            using (FtpClient ftpClient = new FtpClient(_ftpServerUri, new NetworkCredential(_ftpUsername, _ftpPassword)))
            {
                try
                {
                    ftpClient.Connect();

                    // Create the folder on the FTP server
                    ftpClient.CreateDirectory(folderPath);

                    _logger.LogInformation($"Created folder on FTP: {folderPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating folder on FTP: {ex.Message}");
                }
                finally
                {
                    ftpClient.Disconnect();
                }
            }
        }

        private void UploadFileToFTP(string localFilePath, string destinationFtpPath)
        {
            using (FtpClient ftpClient = new FtpClient(_ftpServerUri, new NetworkCredential(_ftpUsername, _ftpPassword)))
            {
                try
                {
                    ftpClient.Connect();

                    // Upload the file to the FTP server
                    ftpClient.UploadFile(localFilePath, destinationFtpPath, FtpRemoteExists.Overwrite);

                    _logger.LogInformation($"Uploaded file to FTP: {destinationFtpPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error uploading file to FTP: {ex.Message}");
                }
                finally
                {
                    ftpClient.Disconnect();
                }
            }
        }
    }
}
