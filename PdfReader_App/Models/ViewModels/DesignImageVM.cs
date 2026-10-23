using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    public class DesignImageVM
    {
        public int DesignId { get; set; }
        public string DesignCode { get; set; }
        public string DesignName { get; set; }
        public byte[] ImageData { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public int? Colors { get; set; }
        public int? Stitches { get; set; }

        public bool HasImage => ImageData != null && ImageData.Length > 0;
    }

}