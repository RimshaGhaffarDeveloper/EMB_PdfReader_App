using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    [Serializable]
    public class StopSequenceVM
    {
        public int SequenceNo { get; set; }
        public int? NeedleNo { get; set; }
        public int? StitchCount { get; set; }
        public string ColorCode { get; set; }
        public string ColorName { get; set; }
        public string ElementName { get; set; }
        public string SequinInfo { get; set; }
    }
}