using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Tesseract;

namespace FormDetection_OCR.ImgHelper
{
    public static class ImgHelper
    {
        public static Bitmap LoadBitmap(string filePath)
        {
            try
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        using (var image = Image.FromStream(memoryStream))
                        {
                            return new Bitmap(image);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading bitmap from file: {filePath}\n{ex.Message}");
            }
        }



        public static Bitmap CropImage(Bitmap image, int x, int y, int width, int height)
        {
            Rectangle cropRect = new Rectangle(x, y, width, height);
            Bitmap croppedImage = image.Clone(cropRect, image.PixelFormat);
            return croppedImage;
        }
        public static OpenCvSharp.Mat ConvertBitmapToMatOpenCV(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap), "Bitmap object cannot be null");
            }

            // Convert the Bitmap to a System.Drawing.Image
            System.Drawing.Image image = (System.Drawing.Image)bitmap;

            // Create a new OpenCvSharp.Mat object
            OpenCvSharp.Mat mat = new OpenCvSharp.Mat();

            // Convert the System.Drawing.Image to a byte array
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                byte[] imageBytes = stream.ToArray();

                // Load the byte array into the OpenCvSharp.Mat object
                mat = Cv2.ImDecode(imageBytes, OpenCvSharp.ImreadModes.Color);
            }

            return mat;
        }

        public static Pix BitmapToPix(Bitmap bitmap)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Save the Bitmap to a MemoryStream in BMP format
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);

                // Rewind the MemoryStream
                ms.Seek(0, SeekOrigin.Begin);

                // Load the MemoryStream into a Pix object
                return Pix.LoadFromMemory(ms.GetBuffer());
            }
        }


    }
}
