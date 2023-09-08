using OpenCvSharp;
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Components.Helpers
{
    public static class ImageProcessingHelper
    {
        public static async Task<Bitmap> DoPerspectiveTransformAsync(string imagePath)
        {
            return await Task.Run(() =>
            {
                using (Mat img = new Mat(imagePath))
                {
                    int hh = img.Height;
                    int ww = img.Width;

                    // read template
                    using (Mat template = img.Clone())
                    {
                        int ht = template.Height;
                        int wd = template.Width;

                        // Convert the image to grayscale
                        using (Mat grayImage = new Mat())
                        {
                            Cv2.CvtColor(img, grayImage, ColorConversionCodes.BGR2GRAY);

                            // do otsu threshold on gray image
                            Mat thresh = new Mat();
                            Cv2.Threshold(grayImage, thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                            // pad thresh with black to preserve corners when applying morphology
                            Mat pad = new Mat();
                            Cv2.CopyMakeBorder(thresh, pad, 20, 20, 20, 20, BorderTypes.Constant, Scalar.Black);

                            // apply morphology
                            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(15, 15));
                            Mat morph = new Mat();
                            Cv2.MorphologyEx(pad, morph, MorphTypes.Close, kernel);

                            // remove padding
                            OpenCvSharp.Rect roi = new OpenCvSharp.Rect(20, 20, ww, hh);
                            Mat croppedMorph = new Mat(morph, roi);

                            // get largest external contour
                            OpenCvSharp.Point[][] contours;
                            HierarchyIndex[] hierarchy;
                            Cv2.FindContours(croppedMorph, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                            var bigContour = FindLargestContour(contours);

                            // get perimeter and approximate a polygon
                            double peri = Cv2.ArcLength(bigContour, true);
                            Point2f[] corners = Cv2.ApproxPolyDP(bigContour, 0.04 * peri, true);

                            // draw polygon on input image from detected corners
                            Mat polygon = img.Clone();
                            OpenCvSharp.Point[] cornersInt = Array.ConvertAll(corners, point => new OpenCvSharp.Point((int)point.X, (int)point.Y));
                            Cv2.Polylines(polygon, new[] { cornersInt }, true, Scalar.Green, 2, LineTypes.AntiAlias);

                            // Calculate the angle between the first and second corners
                            double angle = GetAngle(corners[0], corners[1]);

                            Point2f[] oCorners;
                            if (angle >= -45 && angle < 45) // Horizontal
                            {
                                // Check if the image is mirrored vertically
                                bool isMirrored = corners[0].Y > corners[3].Y;
                                if (isMirrored)
                                {
                                    oCorners = new Point2f[] { new Point2f(wd, ht), new Point2f(wd, 0), new Point2f(0, 0), new Point2f(0, ht) };
                                }
                                else
                                {
                                    oCorners = new Point2f[] { new Point2f(wd, ht), new Point2f(0, ht), new Point2f(0, 0), new Point2f(wd, 0) };
                                }
                            }
                            else if (angle >= 45 && angle < 135) // Vertical
                            {
                                // Check if the image is mirrored vertically
                                bool isMirrored = corners[0].Y > corners[1].Y;

                                if (isMirrored)
                                {
                                    oCorners = new Point2f[] { new Point2f(wd, ht), new Point2f(wd, 0), new Point2f(0, 0), new Point2f(0, ht) };
                                }
                                else
                                {
                                    oCorners = new Point2f[] { new Point2f(0, 0), new Point2f(0, ht), new Point2f(wd, ht), new Point2f(wd, 0) };
                                }
                            }
                            else if (angle >= -135 && angle < -45) // Vertical (upside down)
                            {
                                // Check if the image is mirrored vertically (upside down)
                                bool isMirrored = corners[2].Y > corners[3].Y;

                                if (isMirrored)
                                {
                                    oCorners = new Point2f[] { new Point2f(0, ht), new Point2f(0, 0), new Point2f(wd, 0), new Point2f(wd, ht) };
                                }
                                else
                                {
                                    oCorners = new Point2f[] { new Point2f(wd, 0), new Point2f(wd, ht), new Point2f(0, ht), new Point2f(0, 0) };
                                }
                            }
                            else // Upside down
                            {
                                // Check if the image is mirrored vertically (upside down)
                                bool isMirrored = corners[0].Y > corners[3].Y;
                                if (isMirrored)
                                {
                                    oCorners = new Point2f[] { new Point2f(wd, ht), new Point2f(wd, 0), new Point2f(0, 0), new Point2f(0, ht) };
                                }
                                else
                                {
                                    oCorners = new Point2f[] { new Point2f(0, 0), new Point2f(wd, 0), new Point2f(wd, ht), new Point2f(0, ht) };
                                    oCorners = new Point2f[] { new Point2f(wd, 0), new Point2f(0, 0), new Point2f(0, ht), new Point2f(wd, ht) };
                                }

                            }
                            // get perspective transformation matrix
                            Mat M = Cv2.GetPerspectiveTransform(corners, oCorners);

                            // Check if the perspective transformation matrix is valid
                            if (M == null || M.Rows != 3 || M.Cols != 3 || M.Type() != MatType.CV_64FC1)
                            {
                                Console.WriteLine("Invalid perspective transformation matrix.");
                                return null;
                            }
                            Mat warped = new Mat();
                            try
                            {
                                Cv2.WarpPerspective(img, warped, M, new OpenCvSharp.Size(wd, ht), InterpolationFlags.Linear);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error during perspective transformation: " + ex.Message);
                                return null;
                            }

                            // Convert BGR to RGB for displaying the image in WPF
                            using (Mat rgbWarped = new Mat())
                            {
                                Cv2.CvtColor(warped, rgbWarped, ColorConversionCodes.BGR2RGB);

                                // Convert the warped Mat to a Bitmap
                                Bitmap warpedImage = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(rgbWarped);

                                // Clear resources by releasing Mat instances
                                grayImage.Dispose();
                                rgbWarped.Dispose();
                                warped.Dispose();

                                return warpedImage;
                            }
                        }
                    }
                }
            });
        }
        private static double GetAngle(Point2f point1, Point2f point2)
        {
            double dx = point2.X - point1.X;
            double dy = point2.Y - point1.Y;
            return Math.Atan2(dy, dx) * 180 / Math.PI;
        }
        private static Point2f[] FindLargestContour(OpenCvSharp.Point[][] contours)
        {
            double maxArea = 0;
            Point2f[] largestContour = null;
            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);
                if (area > maxArea)
                {
                    maxArea = area;
                    largestContour = Array.ConvertAll(contour, point => new Point2f(point.X, point.Y));
                }
            }
            return largestContour;
        }
        public static Bitmap ResizeImage(Bitmap image, double widthFactor, double heightFactor)
        {
            int newWidth = (int)(image.Width * widthFactor);
            int newHeight = (int)(image.Height * heightFactor);

            Bitmap resizedImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphics = Graphics.FromImage(resizedImage))
            {
                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return resizedImage;
        }
        public static Bitmap CropImage(Bitmap image, double x, double y, double width, double height)
        {
            Rectangle cropRect = new Rectangle((int)x, (int)y, (int)width, (int)height);
            Bitmap croppedImage = image.Clone(cropRect, image.PixelFormat);
            return croppedImage;
        }
        public static Bitmap HistogramEqualization(Bitmap image)
        {
            // Convert the image to grayscale
            Bitmap grayscaleImage = ConvertToGrayscale(image);

            // Calculate the histogram of the grayscale image
            int[] histogram = CalculateHistogram(grayscaleImage);

            // Calculate the cumulative distribution function (CDF)
            int[] cdf = CalculateCDF(histogram);

            // Calculate the transformation mapping
            int[] mapping = CalculateMapping(cdf, grayscaleImage.Width * grayscaleImage.Height);

            // Apply the transformation mapping to equalize the histogram
            Bitmap equalizedImage = ApplyMapping(grayscaleImage, mapping);

            return equalizedImage;
        }

        private static Bitmap ConvertToGrayscale(Bitmap image)
        {
            Bitmap grayscaleImage = new Bitmap(image.Width, image.Height);

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Color pixel = image.GetPixel(x, y);
                    int average = (pixel.R + pixel.G + pixel.B) / 3;
                    grayscaleImage.SetPixel(x, y, Color.FromArgb(average, average, average));
                }
            }

            return grayscaleImage;
        }

        private static int[] CalculateHistogram(Bitmap image)
        {
            int[] histogram = new int[256];

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Color pixel = image.GetPixel(x, y);
                    int intensity = pixel.R;
                    histogram[intensity]++;
                }
            }

            return histogram;
        }

        private static int[] CalculateCDF(int[] histogram)
        {
            int[] cdf = new int[256];
            cdf[0] = histogram[0];

            for (int i = 1; i < 256; i++)
            {
                cdf[i] = cdf[i - 1] + histogram[i];
            }

            return cdf;
        }

        private static int[] CalculateMapping(int[] cdf, int totalPixels)
        {
            int[] mapping = new int[256];

            for (int i = 0; i < 256; i++)
            {
                mapping[i] = (int)((double)cdf[i] / totalPixels * 255);
            }

            return mapping;
        }

        private static Bitmap ApplyMapping(Bitmap image, int[] mapping)
        {
            Bitmap equalizedImage = new Bitmap(image.Width, image.Height);

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Color pixel = image.GetPixel(x, y);
                    int intensity = pixel.R;
                    int equalizedIntensity = mapping[intensity];
                    equalizedImage.SetPixel(x, y, Color.FromArgb(equalizedIntensity, equalizedIntensity, equalizedIntensity));
                }
            }

            return equalizedImage;
        }
    }
}
