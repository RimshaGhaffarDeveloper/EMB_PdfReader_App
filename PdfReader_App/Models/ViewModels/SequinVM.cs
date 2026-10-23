using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    [Serializable]
    public class SequinVM
    {
        public decimal? Size { get; set; }
        public int? Count { get; set; }
        public string Name { get; set; }
        public string Shape {  get; set; }
    }
}