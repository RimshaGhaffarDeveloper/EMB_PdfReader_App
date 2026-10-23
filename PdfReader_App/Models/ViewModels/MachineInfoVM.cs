using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    [Serializable]
    public class MachineInfoVM
    {
        public string MachineFormat { get; set; }
        public int? ColorChanges { get; set; }
        public int? Stops { get; set; }
        public int? Trims { get; set; }
        public int? Appliques { get; set; }          // add this
        public decimal? Left { get; set; }
        public decimal? Right { get; set; }
        public decimal? Up { get; set; }
        public decimal? Down { get; set; }
        public decimal? EndX { get; set; }           // add this
        public decimal? EndY { get; set; }           // add this
        public decimal? Area { get; set; }           // add this
        public decimal? MaxStitch { get; set; }      // add this
        public decimal? MinStitch { get; set; }      // add this
        public decimal? MaxJump { get; set; }        // add this
        public decimal? TotalThread { get; set; }    // add this
        public decimal? TotalBobbin { get; set; }    // add this
    }

}