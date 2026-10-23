using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using PdfReader_App.Helpers;
using PdfReader_App.Models;
using PdfReader_App.Models.ViewModels;



namespace PdfReader_App.Controllers
{
    public class DesignController : Controller
    {
        PdfReader_DbEntities db = new PdfReader_DbEntities();

        // =========================
        // INDEX (Design List + Upload)
        // =========================
        public ActionResult Index()
        {
            var designs = db.Designs
        .OrderByDescending(d => d.CreatedOn)
        .Select(d => new DesignListItemVM  // ✅ YE CHANGE KIYA
        {
            Id = d.DesignId,
            DesignCode = d.DesignCode,
            DesignName = d.DesignName,
            WidthMM = d.WidthMM,
            HeightMM = d.HeightMM,
            Colors = d.Colors,
            Stitches = d.Stitches,
            CreatedOn = d.CreatedOn
        })
        .ToList();

            return View(designs);
        }
        // =========================
        [HttpPost]
        public ActionResult UploadPdf(HttpPostedFileBase pdfFile)
        {
            if (pdfFile == null || pdfFile.ContentLength == 0)
            {
                TempData["Error"] = "Please select a PDF file.";
                return RedirectToAction("Index");
            }

            // Validate PDF extension
            if (!pdfFile.FileName.ToLower().EndsWith(".pdf"))
            {
                TempData["Error"] = "Only PDF files are allowed.";
                return RedirectToAction("Index");
            }

            try
            {
                string uploadDir = Server.MapPath("~/Content/Uploads/");

                if (!Directory.Exists(uploadDir))
                {
                    Directory.CreateDirectory(uploadDir);
                }

                string fileName = System.IO.Path.GetFileName(pdfFile.FileName);
                string path = System.IO.Path.Combine(uploadDir, fileName);

                // Save file
                pdfFile.SaveAs(path);

                // Parse PDF
                ParsedDesignVM model = PdfParser.ParsePdf(path);

                // Store parsed model in Session for saving later
                Session["ParsedModel"] = model;

                return View("Preview", model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error processing PDF: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // =========================
        // PREVIEW (from database)
        // =========================
        public ActionResult Preview(int? designId)
        {
            if (!designId.HasValue)
            {
                TempData["Error"] = "Invalid design ID.";
                return RedirectToAction("Index");
            }

            try
            {
                var design = db.Designs
                    .Include("MachineInfoes")
                    .Include("StopSequences")
                    .Include("Sequins")
                    .Include("ThreadUsages")
                    .FirstOrDefault(d => d.DesignId == designId.Value);

                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                // Map to ViewModel
                var model = new ParsedDesignVM
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
                    ImageData = design.DesignImage, // Load image
                    IsSaved = true
                };

                // Machine Info
                var machineInfo = design.MachineInfoes.FirstOrDefault();
                if (machineInfo != null)
                {
                    model.MachineInfo = new MachineInfoVM
                    {
                        MachineFormat = machineInfo.MachineFormat,
                        ColorChanges = machineInfo.ColorChanges,
                        Stops = machineInfo.Stops,
                        Trims = machineInfo.Trims,
                        Appliques = machineInfo.Appliques,
                        Left = machineInfo.LeftMM,
                        Right = machineInfo.RightMM,
                        Up = machineInfo.UpMM,
                        Down = machineInfo.DownMM,
                        EndX = machineInfo.EndXMM,
                        EndY = machineInfo.EndYMM,
                        Area = machineInfo.AreaMM2,
                        MaxStitch = machineInfo.MaxStitchMM,
                        MinStitch = machineInfo.MinStitchMM,
                        MaxJump = machineInfo.MaxJumpMM,
                        TotalThread = machineInfo.TotalThreadM,
                        TotalBobbin = machineInfo.TotalBobbinM
                    };
                }

                // Stop Sequences
                model.StopSequences = design.StopSequences
                    .OrderBy(s => s.SequenceNo)
                    .Select(s => new StopSequenceVM
                    {
                        SequenceNo = s.SequenceNo ?? 0,
                        NeedleNo = s.NeedleNo,
                        StitchCount = s.StitchCount,
                        ColorCode = s.ColorCode,
                        ColorName = s.ColorName,
                        ElementName = s.ElementName,
                        SequinInfo = s.SequinInfo
                    })
                    .ToList();

                // Sequins
                model.Sequins = design.Sequins
                    .OrderBy(s => s.SequenceNo)
                    .Select(s => new SequinVM
                    {
                        Shape = s.Shape,
                        Size = s.SizeMM,
                        Count = s.Count,
                        Name = s.Name
                    })
                    .ToList();

                // Thread Usage
                model.ThreadUsages = design.ThreadUsages
                    .OrderBy(t => t.NeedleNo)
                    .Select(t => new ThreadUsageVM
                    {
                        NeedleNo = t.NeedleNo,
                        Code = t.ColorCode,
                        Name = t.ColorName,
                        Length = t.ThreadLengthM
                    })
                    .ToList();

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading design: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // =========================
        // SAVE PARSED DESIGN TO DATABASE
        // =========================
        [HttpPost]
        public ActionResult SaveParsedDesign()
        {
            // Retrieve model from Session
            var model = Session["ParsedModel"] as ParsedDesignVM;

            if (model == null)
            {
                TempData["Error"] = "No data to save. Please upload PDF again.";
                return RedirectToAction("Index");
            }

            try
            {
                // Create Design entity
                var design = new Design
                {
                    DesignCode = model.DesignCode,
                    DesignName = model.DesignName,
                    Stitches = model.Stitches,
                    HeightMM = model.Height,
                    WidthMM = model.Width,
                    Colors = model.Colors,
                    Colorway = model.Colorway,
                    Zoom = model.Zoom,
                    DesignLastSaved = model.DesignLastSaved,
                    DatePrinted = model.DatePrinted,
                    DesignImage = model.ImageData, // Save image
                    CreatedOn = DateTime.Now
                };

                db.Designs.Add(design);
                db.SaveChanges(); // Save to get DesignId

                // Save Machine Info
                if (model.MachineInfo != null)
                {
                    var machineInfo = new MachineInfo
                    {
                        DesignId = design.DesignId,
                        MachineFormat = model.MachineInfo.MachineFormat,
                        ColorChanges = model.MachineInfo.ColorChanges,
                        Stops = model.MachineInfo.Stops,
                        Trims = model.MachineInfo.Trims,
                        Appliques = model.MachineInfo.Appliques,
                        LeftMM = model.MachineInfo.Left,
                        RightMM = model.MachineInfo.Right,
                        UpMM = model.MachineInfo.Up,
                        DownMM = model.MachineInfo.Down,
                        EndXMM = model.MachineInfo.EndX,
                        EndYMM = model.MachineInfo.EndY,
                        AreaMM2 = model.MachineInfo.Area,
                        MaxStitchMM = model.MachineInfo.MaxStitch,
                        MinStitchMM = model.MachineInfo.MinStitch,
                        MaxJumpMM = model.MachineInfo.MaxJump,
                        TotalThreadM = model.MachineInfo.TotalThread,
                        TotalBobbinM = model.MachineInfo.TotalBobbin
                    };
                    db.MachineInfoes.Add(machineInfo);
                }

                // Save Stop Sequences
                if (model.StopSequences != null && model.StopSequences.Any())
                {
                    foreach (var stop in model.StopSequences)
                    {
                        var stopSequence = new StopSequence
                        {
                            DesignId = design.DesignId,
                            SequenceNo = stop.SequenceNo,
                            NeedleNo = stop.NeedleNo,
                            StitchCount = stop.StitchCount,
                            ColorCode = stop.ColorCode,
                            ColorName = stop.ColorName,
                            ElementName = stop.ElementName,
                            SequinInfo = stop.SequinInfo
                        };
                        db.StopSequences.Add(stopSequence);
                    }
                }

                // Save Sequins
                if (model.Sequins != null && model.Sequins.Any())
                {
                    int seqNo = 1;
                    foreach (var seq in model.Sequins)
                    {
                        var sequin = new Sequin
                        {
                            DesignId = design.DesignId,
                            SequenceNo = seqNo++,
                            Shape = seq.Shape,
                            SizeMM = seq.Size,
                            Count = seq.Count,
                            Name = seq.Name
                        };
                        db.Sequins.Add(sequin);
                    }
                }

                // Save Thread Usage
                if (model.ThreadUsages != null && model.ThreadUsages.Any())
                {
                    foreach (var thread in model.ThreadUsages)
                    {
                        var threadUsage = new ThreadUsage
                        {
                            DesignId = design.DesignId,
                            NeedleNo = thread.NeedleNo,
                            ColorCode = thread.Code,
                            ColorName = thread.Name,
                            ThreadLengthM = thread.Length
                        };
                        db.ThreadUsages.Add(threadUsage);
                    }
                }

                db.SaveChanges();

                // Clear session after successful save
                Session.Remove("ParsedModel");

                TempData["Success"] = $"Design '{design.DesignName}' saved successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error saving design: {ex.Message}";
                return RedirectToAction("Index");
            }
        }


        // =========================
        // DELETE DESIGN
        // =========================

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        // =========================
        // GET: View Design
        // =========================
        public ActionResult View(int id)
        {
            var dbModel = DbPreviewMapper.MapToPreviewVM(id);
            dbModel.IsSaved = true;
            return View("Preview", dbModel);
        }

        // =========================
        // DELETE DESIGN
        // =========================
        /* public ActionResult Delete(int id)
         {
             var design = db.Designs.Find(id);
             if (design != null)
             {
                 // Delete related child tables
                 db.MachineInfoes.RemoveRange(db.MachineInfoes.Where(m => m.DesignId == id));
                 db.StopSequences.RemoveRange(db.StopSequences.Where(s => s.DesignId == id));
                 db.Sequins.RemoveRange(db.Sequins.Where(s => s.DesignId == id));
                 db.ThreadUsages.RemoveRange(db.ThreadUsages.Where(t => t.DesignId == id));

                 db.Designs.Remove(design);
                 db.SaveChanges();
             }
             return RedirectToAction("Index");
         }*/



        // ================= MACHINE INFO VIEW =================
        public ActionResult MachineInfoView(int designId)
        {
            try
            {
                var design = db.Designs
                    .Include("MachineInfoes")
                    .FirstOrDefault(d => d.DesignId == designId);

                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                var machineInfo = design.MachineInfoes.FirstOrDefault();

                var model = new MachineInfoVM();
                if (machineInfo != null)
                {
                    model.MachineFormat = machineInfo.MachineFormat;
                    model.ColorChanges = machineInfo.ColorChanges;
                    model.Stops = machineInfo.Stops;
                    model.Trims = machineInfo.Trims;
                    model.Appliques = machineInfo.Appliques;
                    model.Left = machineInfo.LeftMM;
                    model.Right = machineInfo.RightMM;
                    model.Up = machineInfo.UpMM;
                    model.Down = machineInfo.DownMM;
                    model.EndX = machineInfo.EndXMM;
                    model.EndY = machineInfo.EndYMM;
                    model.Area = machineInfo.AreaMM2;
                    model.MaxStitch = machineInfo.MaxStitchMM;
                    model.MinStitch = machineInfo.MinStitchMM;
                    model.MaxJump = machineInfo.MaxJumpMM;
                    model.TotalThread = machineInfo.TotalThreadM;
                    model.TotalBobbin = machineInfo.TotalBobbinM;
                }

                ViewBag.DesignId = designId;
                ViewBag.DesignCode = design.DesignCode;
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ================= STOP SEQUENCE VIEW =================
        public ActionResult StopSequenceView(int designId)
        {
            try
            {
                var design = db.Designs
                    .Include("StopSequences")
                    .FirstOrDefault(d => d.DesignId == designId);

                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                var stopSequences = design.StopSequences
                    .OrderBy(s => s.SequenceNo)
                    .Select(s => new StopSequenceVM
                    {
                        SequenceNo = s.SequenceNo ?? 0,
                        NeedleNo = s.NeedleNo,
                        StitchCount = s.StitchCount,
                        ColorCode = s.ColorCode,
                        ColorName = s.ColorName,
                        ElementName = s.ElementName,
                        SequinInfo = s.SequinInfo
                    })
                    .ToList();

                ViewBag.DesignId = designId;
                ViewBag.DesignCode = design.DesignCode;
                return View(stopSequences);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ================= SEQUINS VIEW =================
        public ActionResult SequinView(int designId)
        {
            try
            {
                var design = db.Designs
                    .Include("Sequins")
                    .FirstOrDefault(d => d.DesignId == designId);

                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                var sequins = design.Sequins
                    .OrderBy(s => s.SequenceNo)
                    .Select(s => new SequinVM
                    {
                        Shape = s.Shape,
                        Size = s.SizeMM,
                        Count = s.Count,
                        Name = s.Name
                    })
                    .ToList();

                ViewBag.DesignId = designId;
                ViewBag.DesignCode = design.DesignCode;
                return View(sequins);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ================= THREAD USAGE VIEW =================
        public ActionResult ThreadUsageView(int designId)
        {
            try
            {
                var design = db.Designs
                    .Include("ThreadUsages")
                    .FirstOrDefault(d => d.DesignId == designId);

                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                var threadUsages = design.ThreadUsages
                    .OrderBy(t => t.NeedleNo)
                    .Select(t => new ThreadUsageVM
                    {
                        NeedleNo = t.NeedleNo,
                        Code = t.ColorCode,
                        Name = t.ColorName,
                        Length = t.ThreadLengthM
                    })
                    .ToList();

                ViewBag.DesignId = designId;
                ViewBag.DesignCode = design.DesignCode;
                return View(threadUsages);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ================= DESIGN DETAILS VIEW =================
        public ActionResult DesignDetailsView(int designId)
        {
            try
            {
                var design = db.Designs.FirstOrDefault(d => d.DesignId == designId);

                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                var model = new ParsedDesignVM
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

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
        [HttpGet]
        public ActionResult Delete(int id)
        {
            using (var db = new PdfReader_DbEntities())
            {
                // Pehle related data delete karein
                var machine = db.MachineInfoes.FirstOrDefault(m => m.DesignId == id);
                if (machine != null)
                    db.MachineInfoes.Remove(machine);

                var stopSequences = db.StopSequences.Where(s => s.DesignId == id).ToList();
                foreach (var s in stopSequences)
                    db.StopSequences.Remove(s);

                var sequins = db.Sequins.Where(s => s.DesignId == id).ToList();
                foreach (var s in sequins)
                    db.Sequins.Remove(s);

                var threadUsages = db.ThreadUsages.Where(t => t.DesignId == id).ToList();
                foreach (var t in threadUsages)
                    db.ThreadUsages.Remove(t);

                // Finally Design record delete karein
                var design = db.Designs.FirstOrDefault(d => d.DesignId == id);
                if (design != null)
                    db.Designs.Remove(design);

                db.SaveChanges();
            }

            TempData["Success"] = "Design deleted successfully!";
            return RedirectToAction("Index");
        }

        // =========================
        // VIEW DESIGN IMAGE
        // =========================
        public ActionResult ViewDesignImage(int designId)
        {
            try
            {
                var design = db.Designs.Find(designId);
                if (design == null)
                {
                    TempData["Error"] = "Design not found.";
                    return RedirectToAction("Index");
                }

                var model = new DesignImageVM
                {
                    DesignId = design.DesignId,
                    DesignCode = design.DesignCode,
                    DesignName = design.DesignName,
                    ImageData = design.DesignImage,
                    Width = design.WidthMM,
                    Height = design.HeightMM,
                    Colors = design.Colors,
                    Stitches = design.Stitches
                };

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading image: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // =========================
        // GET IMAGE (For direct image display)
        // =========================
        public ActionResult GetDesignImage(int designId)
        {
            try
            {
                var design = db.Designs.Find(designId);
                if (design != null && design.DesignImage != null)
                {
                    return File(design.DesignImage, "image/png");
                }
                return null;
            }
            catch
            {
                return null;
            }
        }


    }
}