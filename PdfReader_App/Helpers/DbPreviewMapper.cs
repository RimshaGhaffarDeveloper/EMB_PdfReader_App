using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using PdfReader_App.Models;
using PdfReader_App.Models.ViewModels;

namespace PdfReader_App.Helpers
{
    public class DbPreviewMapper
    {
        public static ParsedDesignVM MapToPreviewVM(int designId)
        {
            using (var db = new PdfReader_DbEntities())
            {
                var design = db.Designs.First(d => d.DesignId == designId);

                ParsedDesignVM model = new ParsedDesignVM
                {
                    DesignId = design.DesignId,
                    DesignCode = design.DesignCode,
                    DesignName = design.DesignName,
                    Stitches = design.Stitches,
                    Height = design.HeightMM,
                    Width = design.WidthMM,
                    Colors = design.Colors,
                    Colorway = design.Colorway,
                    Zoom = design.Zoom,
                    DesignLastSaved = design.DesignLastSaved,
                    DatePrinted = design.DatePrinted,
                    IsSaved = true
                };

                // ================= MACHINE INFO =================
                var machine = db.MachineInfoes
                                .FirstOrDefault(m => m.DesignId == designId);

                if (machine != null)
                {
                    model.MachineInfo.MachineFormat = machine.MachineFormat;
                    model.MachineInfo.ColorChanges = machine.ColorChanges;
                    model.MachineInfo.Stops = machine.Stops;
                    model.MachineInfo.Trims = machine.Trims;
                    model.MachineInfo.Left = machine.LeftMM;
                    model.MachineInfo.Right = machine.RightMM;
                    model.MachineInfo.Up = machine.UpMM;
                    model.MachineInfo.Down = machine.DownMM;
                }

                // ================= STOP SEQUENCE =================
                model.StopSequences = db.StopSequences
                    .Where(s => s.DesignId == designId)
                    .Select(s => new StopSequenceVM
                    {
                        SequenceNo = s.SequenceNo?? 0,
                        NeedleNo = s.NeedleNo,
                        StitchCount = s.StitchCount,
                        ColorCode = s.ColorCode,
                        ColorName = s.ColorName,
                        ElementName = s.ElementName,
                        SequinInfo = s.SequinInfo
                    }).ToList();

                // ================= SEQUINS =================
                model.Sequins = db.Sequins
                    .Where(s => s.DesignId == designId)
                    .Select(s => new SequinVM
                    {
                        Size = s.SizeMM,
                        Count = s.Count,
                        Name = s.Name
                    }).ToList();

                // ================= THREAD USAGE =================
                model.ThreadUsages = db.ThreadUsages
                    .Where(t => t.DesignId == designId)
                    .Select(t => new ThreadUsageVM
                    {
                        Code = t.ColorCode,
                        Name = t.ColorName,
                        Length = t.ThreadLengthM
                    }).ToList();

                return model;
            }
        }
    }
}