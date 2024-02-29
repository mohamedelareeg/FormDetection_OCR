# FormDetection_OCR

## Overview
FormDetection_OCR is a console application developed to streamline the process of automating image processing, form detection, and text extraction using OCR (Optical Character Recognition). The application operates as a Windows service, running silently in the background to monitor designated directories for new images. Upon detecting new images, the application automatically processes them, corrects any skew, compares them with template images to identify forms, and extracts relevant text using Tesseract OCR. Finally, it sends the extracted data to a specified endpoint for further processing.

## Features
- **Automated Image Processing**: Automatically corrects image skew and applies image enhancements to improve OCR accuracy.
- **Template Matching and Form Detection**: Compares incoming images with a set of template images to identify specific forms or document layouts.
- **Text Extraction with OCR**: Utilizes Tesseract OCR to extract text from images, even in cases of handwritten or poor-quality text.
- **Data Transmission**: Sends extracted data, such as claim numbers, descriptions, dates, and patient IDs, to a designated endpoint using HTTP POST requests.
- **Multiple Watcher Modes**: Supports watching directories on the local device, shared network folders, and FTP servers for new images.
- **Windows Service**: Runs continuously in the background as a Windows service, ensuring seamless and unobtrusive operation.

## Installation
1. **Clone the Repository**: Clone the FormDetection_OCR repository from GitHub to your local machine.
2. **Install Dependencies**: Ensure that all required dependencies, including .NET Core and any third-party libraries, are installed.
3. **Configure Settings**: Customize the application settings in the `config.json` file, specifying directories to monitor, FTP server details, and API endpoints for data transmission.
4. **Build the Project**: Build the project using Visual Studio or the .NET CLI.

## Usage
1. **Configuration**: Update the `config.json` file with appropriate settings, including input/output directories, FTP server credentials, and API endpoints.
2. **Execution**: Run the application either from Visual Studio or by executing the compiled binary.
3. **Image Processing**: Place images to be processed into the designated input directory, shared network folder, or FTP server.
4. **Automatic Processing**: The application will automatically detect new images, process them, extract text, and transmit the extracted data to the specified API endpoint.

## Folder Watcher Modes
FormDetection_OCR supports three modes of folder watching:

### Local Device Folder Watching
- Specify a local directory path in the `config.json` file.
- The application will monitor this directory for new images.

### Shared Network Folder Watching
- Specify a shared network folder path in the `config.json` file.
- Provide appropriate network credentials if required.
- The application will monitor the shared folder for new images.

### FTP Folder Watching
- Specify FTP server details (host, port, username, password, remote directory) in the `config.json` file.
- The application will connect to the FTP server and monitor the specified remote directory for new images.
## Form Detection with SIFT or ORB Algorithm
To detect the form within an incoming image, the application employs either the Scale-Invariant Feature Transform (SIFT) or Oriented FAST and Rotated BRIEF (ORB) algorithm. These algorithms are robust to variations in scale, rotation, and illumination, making them suitable for template matching in images.

### SIFT Algorithm
The SIFT algorithm detects and describes keypoint features in an image, which are invariant to scale and rotation. It then matches these keypoints between the template image and the incoming image to determine the similarity between them. Here's an outline of the process:

1. Convert both the template and incoming images to grayscale.
2. Detect keypoints and compute descriptors using the SIFT algorithm for both images.
3. Match keypoints between the template and incoming images using a feature matcher.
4. Apply a threshold to filter out good matches based on their distances.
5. Calculate a match percentage based on the number of filtered matches.
6. If the match percentage exceeds a predefined threshold, the template image is considered a match.

### ORB Algorithm
The ORB algorithm is a fast alternative to SIFT, particularly suitable for real-time applications. It detects keypoints using an efficient feature detection method and computes binary descriptors to describe these keypoints. The matching process is similar to that of SIFT, but ORB operates at a lower computational cost. Here's how it works:

1. Convert both the template and incoming images to grayscale.
2. Detect keypoints and compute descriptors using the ORB algorithm for both images.
3. Match keypoints between the template and incoming images using a feature matcher.
4. Apply a threshold to filter out good matches based on their distances.
5. Calculate a match percentage based on the number of filtered matches.
6. If the match percentage exceeds a predefined threshold, the template image is considered a match.

By comparing the match percentages obtained from SIFT and ORB, the application selects the best-matching template image for further processing.

```csharp
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

public static async Task<string> DetectImageTemplatesORB(Bitmap image, double FormSimilarity, string[] templateImagePaths)
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

```
- This section explains the process of form detection using the SIFT and ORB algorithms. It provides an overview of each algorithm's steps and includes C# code snippets demonstrating how to implement form detection using SIFT and ORB in your application. Adjust the code as necessary to fit your specific requirements and use cases.






## Template Zone Extraction
After comparing the incoming image with template images, the application reads the template zones from a JSON file. Each template image has associated zones defined as follows:

```json
{
  "Index": 1,
  "ImageFileName": "template_image1.bmp",
  "SerializableRect": [
    {
      "X": 100,
      "Y": 50,
      "Width": 200,
      "Height": 100,
      "ActualWidth": 300,
      "ActualHeight": 150,
      "Name": "Claim_Num",
      "IndexingField": "Reservation Number",
      "Regex": "\\d+",
      "Type": "Textbox",
      "WhiteList": ""
    },
    ...
  ]
}
```

- `X`, `Y`: Coordinates of the zone, representing the top-left corner of the zone rectangle.
- `Width`, `Height`: Dimensions of the zone rectangle.
- `ActualWidth`, `ActualHeight`: Actual dimensions of the zone in the original image. This can be different from `Width` and `Height` if the image has been resized or scaled.
- `Name`: A descriptive name for the zone, usually indicating the type of information it contains or represents.
- `IndexingField`: The field to which the zone corresponds. This is typically used to map the extracted text to specific data fields.
- `Regex`: A regular expression pattern for extracting text from the zone. This is optional and is used when the text format within the zone follows a specific pattern.
- `Type`: The type of the zone, such as Textbox, Checkbox, or Signature. This helps in determining how the extracted text should be processed or interpreted.
- `WhiteList`: A whitelist of characters that the OCR engine should consider when performing text recognition within the zone. This is useful for restricting recognition to specific characters or symbols.
- `IsDuplicated`: A flag indicating whether the zone is duplicated. This is useful when dealing with multiple instances of the same type of zone within a template image.
- `IndexingField`: Field to which the zone corresponds.
- `Regex`: Regular expression pattern for extracting text from the zone (optional).
- `Type`: Type of the zone (e.g., Textbox, Checkbox).
- `WhiteList`: Characters to whitelist during OCR (optional).

## Text Extraction and Data Transmission
Once the application has identified the template image that matches the incoming image, it proceeds to extract text from each zone defined in the template. The process involves:

1. **Zone Detection**: Identifying the zones defined in the template image and locating them within the incoming image based on their coordinates.

2. **Text Extraction**: Using OCR (Optical Character Recognition) to extract text from each identified zone. The application applies any specified regex pattern or whitelist to refine the extracted text if necessary.

3. **Data Formatting**: Formatting the extracted text based on the expected format of each field. For example, converting dates to a standardized format or parsing numerical values.

4. **Data Transmission**: Sending the extracted data to the specified API endpoint for further processing. The data is typically transmitted as a JSON payload or form data, depending on the requirements of the endpoint.

## Sample Code
```csharp
// Extracting text from zones and formatting data for transmission

// Extract data from OCR results
string claimNum = ocrResults["Reservation Number"].ToString();
string claimDescription = ocrResults["Clinic"].ToString();
string claimDate = ocrResults["Date"].ToString();

// Parse date using CultureInfo.InvariantCulture to ensure consistent formatting
DateTime date = DateTime.ParseExact(claimDate, "dd/MM/yyyy", CultureInfo.InvariantCulture);
string formattedDate = date.ToString("yyyy-M-d");

string patientId = ocrResults["MRN"].ToString();

// Prepare data for transmission
var postData = new MultipartFormDataContent
{
    { new StringContent(claimNum), "Claim_Num" },
    { new StringContent(claimDescription), "Claim_Description" },
    { new StringContent(formattedDate), "Claim_Date" },
    { new StringContent(patientId), "Patient_ID" }
};

// Send data to API endpoint
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
```

## Deployment
1. **Production Environment**: Deploy the application to a production environment, ensuring that it runs continuously as a Windows service.
2. **Monitoring**: Implement monitoring and logging mechanisms to track application performance and identify any issues promptly.
3. **Scaling**: Consider scaling the application to handle increased workloads by distributing processing tasks across multiple instances or machines if necessary.

## Contributing
1. **Bug Reports and Feature Requests**: Submit bug reports or feature requests via GitHub Issues, providing detailed descriptions and, if applicable, proposed solutions.
2. **Code Contributions**: Fork the repository, implement changes or new features, and submit pull requests for review. Follow coding standards and guidelines outlined in the project documentation.

## License
This project is licensed under the [MIT License](LICENSE). See the LICENSE file for details.


