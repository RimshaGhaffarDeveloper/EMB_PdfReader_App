using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PdfReader_App.Models.ViewModels
{
    
    public class ParsedDesignVM
    {
    // ================= DESIGN(Designs table) =================
        public int? DesignId { get; set; }   // after save
        public string DesignCode { get; set; }
        public string DesignName { get; set; }
        public int? Stitches { get; set; }
        public decimal? Height { get; set; }
        public decimal? Width { get; set; }
        public int? Colors { get; set; }
        public string Colorway { get; set; }
        public decimal? Zoom { get; set; }
        public DateTime? DesignLastSaved { get; set; }
        public DateTime? DatePrinted { get; set; }

        // ================= MACHINE INFO =================
        public MachineInfoVM MachineInfo { get; set; }

        // ================= STOP SEQUENCE =================
        public List<StopSequenceVM> StopSequences { get; set; }

        // ================= SEQUINS =================
        public List<SequinVM> Sequins { get; set; }

        // ================= THREAD USAGE =================
        public List<ThreadUsageVM> ThreadUsages { get; set; }

        // ================= UI CONTROL =================
        public bool IsSaved { get; set; }
        public byte[] ImageData { get; set; }

        public ParsedDesignVM()
        {
            StopSequences = new List<StopSequenceVM>();
            Sequins = new List<SequinVM>();
            ThreadUsages = new List<ThreadUsageVM>();
            MachineInfo = new MachineInfoVM();
        }
    }
}