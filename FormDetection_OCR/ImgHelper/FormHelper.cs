
using Components.Helpers;
using FormDetection_OCR.Constants;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;
using static FormDetection_OCR.ImgHelper.FormHelper;

namespace FormDetection_OCR.ImgHelper
{
    public static class FormHelper
    {
        private static string tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
        private static string Language = "ara+eng";
        private static TesseractEngine _engine = new TesseractEngine(tessdataPath, Language, EngineMode.Default);
        public static async Task<string> DetectTextInZones(Bitmap processedImage, string templateImagePath)
        {
      

            // Print the selected template
            Console.WriteLine($"Selected template: {templateImagePath}");


            // Load the template zones from the JSON file
            string jsonFilePath = GetJsonFilePath(templateImagePath);
            var templateForm = LoadTemplateFromJson(jsonFilePath);

            // Create a JSON object to store the OCR results
            dynamic ocrResults = new JObject();
            using (var engine = new TesseractEngine(tessdataPath, Language, EngineMode.Default))
            {
                // Iterate over each template image
                foreach (var templateImage in templateForm.TemplateImages)
                {

                    foreach (var zone in templateImage.SerializableRect)
                    {
                        //int currentIndex = templateImage.SerializableRect.IndexOf(zone);
                        Bitmap croppedImage = null;
                        if (zone.ActualWidth > 0 && zone.ActualHeight > 0)
                        {
                            double resizeFactorX = zone.ActualWidth / processedImage.Width;
                            double resizeFactorY = zone.ActualHeight / processedImage.Height;
                            processedImage = ImageProcessingHelper.ResizeImage(processedImage, resizeFactorX, resizeFactorY);
                            croppedImage = ImageProcessingHelper.CropImage(processedImage, zone.X, zone.Y, zone.Width, zone.Height);
                            if (zone.WhiteList != null)
                            {
                                engine.SetVariable("tessedit_char_whitelist", zone.WhiteList);
                            }
                            else
                            {
                                engine.SetVariable("tessedit_char_whitelist", "");
                            }
                        }
                        string ocrResult = PerformOCR(croppedImage, engine);
                        // Print the OCR result
                        Console.WriteLine($"OCR Result for zone {zone.IndexingField}: {ocrResult}");

                        // Apply regular expressions or custom logic to extract the desired value from the OCR result
                        if (zone.Regex != null)
                        {
                            ocrResult = ExtractValueFromOCRResult(ocrResult, zone.Regex, zone.IndexingField);
                        }

                        ocrResults[zone.IndexingField] = ocrResult;
                    }
                }
                string json = ocrResults.ToString();

                return json;
            }
        
        }
        public static async Task<string> DetectTextInPage(Bitmap processedImage, string templateImagePath)
        {
            string jsonFilePath = GetJsonFilePath(templateImagePath);
            var templateForm = LoadTemplateFromJson(jsonFilePath);
            // Create an OCR engine
            using (var engine = new TesseractEngine(tessdataPath, Language, EngineMode.Default))
            {
                // Perform OCR on the entire processed image
                string ocrResult = PerformOCR(processedImage, engine);
                // Create a JSON object to store the OCR results
                dynamic ocrResults = new JObject();
                foreach (var templateImage in templateForm.TemplateImages)
                {


                    foreach (var field in templateImage.SerializableRect)
                    {
                        string pattern = field.IndexingField + @"(.*)";

                        Match match = Regex.Match(ocrResult, pattern);

                        if (match.Success)
                        {
                            string value = match.Groups[1].Value.Trim();

                            // Store the extracted value with the field name
                            ocrResults[field.Name] = value;
                        }

                    }
                }

                string json = ocrResults.ToString();
                return json;
            }
        }


     
        private static Bitmap PreprocessImage(Bitmap image)
        {
            // Apply image enhancement or noise reduction techniques to improve image quality
            // Example: perform histogram equalization
            Bitmap processedImage = ImageProcessingHelper.HistogramEqualization(image);

            return processedImage;
        }

        private static string ExtractValueFromOCRResult(string ocrResult, string regexPattern, string indexingField)
        {
            string value = "";

            if (regexPattern != "")
            {
                Match match = Regex.Match(ocrResult, regexPattern);
                if (match.Success)
                {
                    value = match.Groups[1].Value;
                }
                else
                {
                    value = ExtractFieldValue(ocrResult, indexingField);
                }
            }
            else
            {
                value = ExtractFieldValue(ocrResult, indexingField);
            }

            return value;
        }

        public static async Task<string> DetectImageTemplates(Bitmap image, double formSimilarity, string[] templateImagePaths)
        {
           

            string detectedTemplatePath = null;
            double maxMatchPercentage = 0.0;

            // Convert the Bitmap object to a Mat object
            Mat matImage = ImgHelper.ConvertBitmapToMatOpenCV(image);

            // Convert the Mat to grayscale
            Mat targetImageGray = new Mat();
            Cv2.CvtColor(matImage, targetImageGray, ColorConversionCodes.BGR2GRAY);

            // Create feature detectors and descriptors
            var sift = SIFT.Create();

            // Set the threshold for matching
            const float matchThreshold = 0.7f; // Adjust the threshold as needed

            // Compute the keypoints and descriptors for the target image
            KeyPoint[] targetKeyPoints;
            Mat targetDescriptors = new Mat();
            sift.DetectAndCompute(targetImageGray, null, out targetKeyPoints, targetDescriptors);

            // Process each template image
            foreach (var templateImagePath in templateImagePaths)
            {
                // Load the template image and convert it to grayscale
                Mat templateImage = Cv2.ImRead(templateImagePath, ImreadModes.Grayscale);

                // Compute the keypoints and descriptors for the template image
                KeyPoint[] templateKeyPoints;
                Mat templateDescriptors = new Mat();
                sift.DetectAndCompute(templateImage, null, out templateKeyPoints, templateDescriptors);

                // Match the features between the template and target images
                var matcher = new FlannBasedMatcher();
                var matches = matcher.KnnMatch(templateDescriptors, targetDescriptors, 2);

                // Filter the matches based on the distance ratio
                var filteredMatches = matches.Where(m => m[0].Distance < matchThreshold * m[1].Distance).ToList();

                // Calculate the match percentage
                double matchPercentage = (double)filteredMatches.Count / templateDescriptors.Rows * 100;

                // Check if the current match is more identical than the previous matches
                if (matchPercentage > maxMatchPercentage)
                {
                    maxMatchPercentage = matchPercentage;
                    detectedTemplatePath = templateImagePath;
                }
            }

            // Check if the best match exceeds the threshold
            if (maxMatchPercentage >= formSimilarity)
            {
                return detectedTemplatePath;
            }
            else
            {
                return null; // No match above the threshold
            }
        }


        public static async Task<string> DetectImageTemplatesORB(Bitmap image, double FormSimilarity , string[] templateImagePaths)
        {
            

            string detectedTemplatePath = null;
            double maxMatchPercentage = 0.0;

            // Convert the Bitmap object to a Mat object
            OpenCvSharp.Mat matImage = ImgHelper.ConvertBitmapToMatOpenCV(image);

            // Convert the Mat to grayscale
            OpenCvSharp.Mat targetImageGray = new OpenCvSharp.Mat();
            Cv2.CvtColor(matImage, targetImageGray, ColorConversionCodes.BGR2GRAY);

            // Create an ORB feature detector and descriptor
            var orb = OpenCvSharp.ORB.Create();

            // Set the threshold for matching
            const int matchThreshold = 20; // Adjust the threshold as needed

            // Process each template image
            foreach (var templateImagePath in templateImagePaths)
            {
                // Load the template image and convert it to grayscale
                OpenCvSharp.Mat templateImage = Cv2.ImRead(templateImagePath, OpenCvSharp.ImreadModes.Grayscale);

                // Detect and compute the ORB features for the template image
                KeyPoint[] templateKeyPoints;
                OpenCvSharp.Mat templateDescriptors = new OpenCvSharp.Mat();
                orb.DetectAndCompute(templateImage, null, out templateKeyPoints, templateDescriptors);

                // Detect and compute the ORB features for the target image
                KeyPoint[] targetKeyPoints;
                OpenCvSharp.Mat targetDescriptors = new OpenCvSharp.Mat();
                orb.DetectAndCompute(targetImageGray, null, out targetKeyPoints, targetDescriptors);

                // Match the features between the template and target images
                var matcher = new OpenCvSharp.BFMatcher(NormTypes.Hamming);
                var matches = matcher.Match(templateDescriptors, targetDescriptors);

                // Filter the matches based on the distance and threshold
                var filteredMatches = matches.Where(m => m.Distance <= matchThreshold).ToList();

                // Calculate the match percentage
                double matchPercentage = (double)filteredMatches.Count / templateDescriptors.Rows * 100;

                // Check if the current match is more identical
                if (matchPercentage > maxMatchPercentage)
                {
                    maxMatchPercentage = matchPercentage;
                    detectedTemplatePath = templateImagePath;
                }
            }



            // Check if the best match exceeds the threshold
            if (maxMatchPercentage >= FormSimilarity)
            {
                return detectedTemplatePath;
            }
            else
            {
                return null; // No match above the threshold
            }
        }

        public static string[] LoadTemplateImagesFrom()
        {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "forms");
            string[] templateFiles = Directory.GetFiles(folderPath, "*.bmp");
            return templateFiles;
        }
        private static string ExtractFieldValue(string text, string keyword)
        {
            // Create a regular expression pattern to match the keyword and the value
            string pattern = $@"{keyword}\s*([\w\s]*)";

            // Use regular expression matching to extract the field value
            System.Text.RegularExpressions.Match match = Regex.Match(text, pattern);
            if (match.Success)
            {
                // Get the captured group that corresponds to the field value
                Group valueGroup = match.Groups[1];
                string value = valueGroup.Value.Trim();

                if (string.IsNullOrEmpty(value))
                {
                    // Get the index of the match
                    int matchIndex = match.Index;

                    // Find the position of the match in the text
                    int lineStart = text.LastIndexOf('\n', matchIndex) + 1;
                    int lineEnd = text.IndexOf('\n', matchIndex);

                    // Get the text in the same column below the match
                    string columnText = text.Substring(lineEnd);

                    // Remove leading and trailing whitespaces from the column text
                    columnText = columnText.Trim();

                    // Return the column text as the field value
                    return columnText;
                }

                // Return the captured field value
                return value;
            }

            // Return an empty string if the keyword and value are not found
            return string.Empty;
        }
        
        private static Form LoadTemplateFromJson(string jsonFilePath)
        {
            string json = File.ReadAllText(jsonFilePath);
            var templateData = JsonConvert.DeserializeObject<Form>(json);
            return templateData;
        }
        private static string GetJsonFilePath(string templateImagePath)
        {
            string jsonFileName = Path.GetFileNameWithoutExtension(templateImagePath) + ".json";
            string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "forms", jsonFileName);
            return jsonFilePath;
        }
        private static string PerformOCR(Bitmap image, TesseractEngine engine)
        {
            using (var page = engine.Process(ImgHelper.BitmapToPix(image)))
            {
                return page.GetText().Trim();
            }
        }

        public class Zone
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double ActualWidth { get; set; }
            public double ActualHeight { get; set; }
            public string ImageFileName { get; set; }
            public string Name { get; set; }
            public string IndexingField { get; set; }
            public string Regex { get; set; }

            public string Type { get; set; }
            public string WhiteList { get; set; }

            public bool IsDuplicated { get; set; } = false;
        }
        public class Form
        {
            public int Count { get; set; }
            public bool IsDuplicated { get; set; } = false;
            public List<TemplateImage> TemplateImages { get; set; }
        }

        public class TemplateImage
        {
            public int Index { get; set; }
            public string ImageFileName { get; set; }
            public List<Zone> SerializableRect { get; set; }

        }
    }
}
