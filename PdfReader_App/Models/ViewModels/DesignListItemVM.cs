using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    public class DesignListItemVM
    {
        public int Id { get; set; }
        public string DesignCode { get; set; }
        public string DesignName { get; set; }
        public decimal? WidthMM { get; set; }
        public decimal? HeightMM { get; set; }
        public int? Colors { get; set; }
        public int? Stitches { get; set; }
        public DateTime? CreatedOn { get; set; }
    }
}