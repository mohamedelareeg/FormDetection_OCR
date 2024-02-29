using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FormDetection_OCR.Models
{
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
