using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    [Serializable]
    public class ThreadUsageVM
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal? Length { get; set; }
        public int? NeedleNo { get; set; }
    }
}