using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using PdfReader_App.Models.ViewModels;

namespace PdfReader_App.Helpers
{
    public static class PdfParser
    {
        public static ParsedDesignVM ParsePdf(string pdfPath)
        {
            var model = new ParsedDesignVM();

            try
            {
                using (PdfReader reader = new PdfReader(pdfPath))
                {
                    string fullText = "";

                    // Extract text from all pages
                    for (int page = 1; page <= reader.NumberOfPages; page++)
                    {
                        fullText += PdfTextExtractor.GetTextFromPage(reader, page, new SimpleTextExtractionStrategy());
                    }

                    // Extract first image from PDF
                    model.ImageData = ExtractFirstImage(reader);

                    // Parse different sections
                    ParseDesignInfo(fullText, model);
                    ParseMachineInfo(fullText, model);
                    ParseStopSequence(fullText, model);
                    ParseSequins(fullText, model);
                    ParseThreadUsage(fullText, model);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"PDF parsing error: {ex.Message}");
            }

            return model;
        }

        // ================= MAIN METHOD =================
        public static byte[] ExtractFirstImage(PdfReader reader)
        {
            try
            {
                List<ImageInfo> allImages = new List<ImageInfo>();

                for (int pageNum = 1; pageNum <= reader.NumberOfPages; pageNum++)
                {
                    PdfDictionary page = reader.GetPageN(pageNum);
                    PdfDictionary resources = page.GetAsDict(PdfName.RESOURCES);
                    if (resources == null) continue;

                    PdfDictionary xobjects = resources.GetAsDict(PdfName.XOBJECT);
                    if (xobjects == null) continue;

                    int imageIndex = 0;

                    foreach (PdfName name in xobjects.Keys)
                    {
                        PdfObject obj = xobjects.Get(name);
                        if (!obj.IsIndirect()) continue;

                        PdfDictionary xobject = (PdfDictionary)PdfReader.GetPdfObject(obj);
                        if (!PdfName.IMAGE.Equals(xobject.Get(PdfName.SUBTYPE))) continue;

                        try
                        {
                            PdfImageObject pdfImage = new PdfImageObject((PRStream)xobject);
                            Image drawingImage = pdfImage.GetDrawingImage();
                            if (drawingImage == null) continue;

                            Bitmap bitmap = new Bitmap(drawingImage);
                            drawingImage.Dispose();

                            // ✅ REMOVE WHITE / TRANSPARENT GAP
                            bitmap = CropWhiteSpace(bitmap);

                            if (bitmap.Width > 100 && bitmap.Height > 100)
                            {
                                allImages.Add(new ImageInfo
                                {
                                    Image = bitmap,
                                    PageNumber = pageNum,
                                    Index = imageIndex
                                });
                                imageIndex++;
                            }
                            else
                            {
                                bitmap.Dispose();
                            }
                        }
                        catch
                        {
                            // ignore broken images
                        }
                    }
                }

                if (allImages.Count == 0)
                    return null;

                // ✅ Only one image
                if (allImages.Count == 1)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        allImages[0].Image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        allImages[0].Image.Dispose();
                        return ms.ToArray();
                    }
                }

                // ✅ Sort & stitch
                var images = allImages
                    .OrderBy(i => i.PageNumber)
                    .ThenBy(i => i.Index)
                    .Select(i => i.Image)
                    .ToList();

                return StitchImagesVertically(images);
            }
            catch
            {
                return null;
            }
        }

        // ================= IMAGE INFO CLASS =================
        private class ImageInfo
        {
            public Bitmap Image { get; set; }
            public int PageNumber { get; set; }
            public int Index { get; set; }
        }



        private static Bitmap CropWhiteSpace(Bitmap bmp)
        {
            int top = bmp.Height;
            int bottom = 0;
            bool found = false;

            // 🔹 TOP crop
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 10 && (c.R < 245 || c.G < 245 || c.B < 245))
                    {
                        top = y;
                        found = true;
                        goto BottomScan;
                    }
                }
            }

        BottomScan:
            // 🔹 BOTTOM crop
            for (int y = bmp.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 10 && (c.R < 245 || c.G < 245 || c.B < 245))
                    {
                        bottom = y;
                        goto Done;
                    }
                }
            }

        Done:
            if (!found || bottom <= top)
                return bmp;

            Rectangle crop = Rectangle.FromLTRB(
                0,
                top,
                bmp.Width,
                bottom + 1
            );

            return bmp.Clone(crop, bmp.PixelFormat);
        }


        // ================= STITCH IMAGES (SMART – GAP FREE) =================
        // ================= FINAL SAFE STITCH (TOP 3 IMAGES) =================
        private static byte[] StitchImagesVertically(List<Bitmap> images)
        {
            if (images == null || images.Count == 0)
                return null;

            // 🔥 STEP 1: Remove text / mostly white images
            var filtered = images
                .Where(img => GetContentRatio(img) > 0.08) // 🔥 magic line
                .ToList();

            if (filtered.Count == 0)
                filtered = images;

            // 🔥 STEP 2: Take main design parts
            var mainImages = filtered
                .OrderByDescending(i => i.Height)
                .Take(3)
                .ToList();

            int maxWidth = mainImages.Max(i => i.Width);
            int totalHeight = mainImages.Sum(i => i.Height);

            using (Bitmap finalImage = new Bitmap(maxWidth, totalHeight))
            using (Graphics g = Graphics.FromImage(finalImage))
            {
                g.Clear(Color.White);

                int y = 0;
                foreach (var img in mainImages)
                {
                    int x = (maxWidth - img.Width) / 2;
                    g.DrawImage(img, x, y);
                    y += img.Height;
                    img.Dispose();
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    finalImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }


        private static double GetContentRatio(Bitmap bmp)
        {
            int content = 0;
            int total = bmp.Width * bmp.Height;

            for (int y = 0; y < bmp.Height; y += 4)
            {
                for (int x = 0; x < bmp.Width; x += 4)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (c.A > 10 && (c.R < 245 || c.G < 245 || c.B < 245))
                        content++;
                }
            }

            return (double)content / (total / 16);
        }




        private static void ParseDesignInfo(string text, ParsedDesignVM model)
        {
            try
            {
                // Design Code/Name (first occurrence after "Design:")
                var designMatch = Regex.Match(text, @"Design:\s*\n\s*(.+?)(?:\n|Title:)", RegexOptions.Singleline);
                if (designMatch.Success)
                {
                    string designLine = designMatch.Groups[1].Value.Trim();

                    // Extract code (first number) and name (rest after first space)
                    var parts = designLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        model.DesignCode = parts[0]; // First part is code (2505365)
                        if (parts.Length > 1)
                        {
                            model.DesignName = parts[1]; // Rest is name (F 93 Pallu 2505365)
                        }
                        else
                        {
                            model.DesignName = parts[0]; // If no space, use same as code
                        }
                    }
                }

                // Stitches
                var stitchesMatch = Regex.Match(text, @"Stitches:\s*([\d,]+)");
                if (stitchesMatch.Success)
                {
                    model.Stitches = int.Parse(stitchesMatch.Groups[1].Value.Replace(",", ""));
                }

                // Height
                var heightMatch = Regex.Match(text, @"Height:\s*([\d.]+)\s*mm");
                if (heightMatch.Success)
                {
                    model.Height = decimal.Parse(heightMatch.Groups[1].Value);
                }

                // Width
                var widthMatch = Regex.Match(text, @"Width:\s*([\d.]+)\s*mm");
                if (widthMatch.Success)
                {
                    model.Width = decimal.Parse(widthMatch.Groups[1].Value);
                }

                // Colors
                var colorsMatch = Regex.Match(text, @"Colors:\s*(\d+)");
                if (colorsMatch.Success)
                {
                    model.Colors = int.Parse(colorsMatch.Groups[1].Value);
                }

                // Colorway
                var colorwayMatch = Regex.Match(text, @"Colorway:\s*(.+?)(?:\n|Zoom:)");
                if (colorwayMatch.Success)
                {
                    model.Colorway = colorwayMatch.Groups[1].Value.Trim();
                }

                // Zoom
                var zoomMatch = Regex.Match(text, @"Zoom:\s*([\d.]+)");
                if (zoomMatch.Success)
                {
                    model.Zoom = decimal.Parse(zoomMatch.Groups[1].Value);
                }

                // Design Last Saved
                var savedMatch = Regex.Match(text, @"Design last saved\s*:\s*(.+?)(?:\s+Date printed|$)");
                if (savedMatch.Success)
                {
                    DateTime savedDate;
                    if (DateTime.TryParse(savedMatch.Groups[1].Value.Trim(), out savedDate))
                    {
                        model.DesignLastSaved = savedDate;
                    }
                }

                // Date Printed
                var printedMatch = Regex.Match(text, @"Date printed:\s*(.+?)(?:\s+Page|\n|$)");
                if (printedMatch.Success)
                {
                    DateTime printedDate;
                    if (DateTime.TryParse(printedMatch.Groups[1].Value.Trim(), out printedDate))
                    {
                        model.DatePrinted = printedDate;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue parsing
                System.Diagnostics.Debug.WriteLine($"Design Info Parse Error: {ex.Message}");
            }
        }

        private static void ParseMachineInfo(string text, ParsedDesignVM model)
        {
            try
            {
                var machineInfo = new MachineInfoVM();

                // Machine Format
                var formatMatch = Regex.Match(text, @"Machine format:\s*(.+?)(?:\n|$)");
                if (formatMatch.Success)
                    machineInfo.MachineFormat = formatMatch.Groups[1].Value.Trim();

                // Color Changes
                var colorChangesMatch = Regex.Match(text, @"Color changes:\s*(\d+)");
                if (colorChangesMatch.Success)
                    machineInfo.ColorChanges = int.Parse(colorChangesMatch.Groups[1].Value);

                // Stops
                var stopsMatch = Regex.Match(text, @"Stops:\s*(\d+)");
                if (stopsMatch.Success)
                    machineInfo.Stops = int.Parse(stopsMatch.Groups[1].Value);

                // Trims
                var trimsMatch = Regex.Match(text, @"Trims:\s*(\d+)");
                if (trimsMatch.Success)
                    machineInfo.Trims = int.Parse(trimsMatch.Groups[1].Value);

                // Appliqués
                var appliquesMatch = Regex.Match(text, @"Appliqués:\s*(\d+)");
                if (appliquesMatch.Success)
                    machineInfo.Appliques = int.Parse(appliquesMatch.Groups[1].Value);

                // Left
                var leftMatch = Regex.Match(text, @"Left:\s*([\d.]+)\s*mm");
                if (leftMatch.Success)
                    machineInfo.Left = decimal.Parse(leftMatch.Groups[1].Value);

                // Right
                var rightMatch = Regex.Match(text, @"Right:\s*([\d.]+)\s*mm");
                if (rightMatch.Success)
                    machineInfo.Right = decimal.Parse(rightMatch.Groups[1].Value);

                // Up
                var upMatch = Regex.Match(text, @"Up:\s*([\d.]+)\s*mm");
                if (upMatch.Success)
                    machineInfo.Up = decimal.Parse(upMatch.Groups[1].Value);

                // Down
                var downMatch = Regex.Match(text, @"Down:\s*([\d.]+)\s*mm");
                if (downMatch.Success)
                    machineInfo.Down = decimal.Parse(downMatch.Groups[1].Value);

                // EndX
                var endXMatch = Regex.Match(text, @"EndX:\s*([\d.]+)\s*mm");
                if (endXMatch.Success)
                    machineInfo.EndX = decimal.Parse(endXMatch.Groups[1].Value);

                // EndY
                var endYMatch = Regex.Match(text, @"EndY:\s*([\d.]+)\s*mm");
                if (endYMatch.Success)
                    machineInfo.EndY = decimal.Parse(endYMatch.Groups[1].Value);

                // Area
                var areaMatch = Regex.Match(text, @"Area\s*([\d.]+)\s*mm²");
                if (areaMatch.Success)
                    machineInfo.Area = decimal.Parse(areaMatch.Groups[1].Value);

                // Max Stitch
                var maxStitchMatch = Regex.Match(text, @"Max stitch:\s*([\d.]+)\s*mm");
                if (maxStitchMatch.Success)
                    machineInfo.MaxStitch = decimal.Parse(maxStitchMatch.Groups[1].Value);

                // Min Stitch
                var minStitchMatch = Regex.Match(text, @"Min stitch:\s*([\d.]+)\s*mm");
                if (minStitchMatch.Success)
                    machineInfo.MinStitch = decimal.Parse(minStitchMatch.Groups[1].Value);

                // Max Jump
                var maxJumpMatch = Regex.Match(text, @"Max jump:\s*([\d.]+)\s*mm");
                if (maxJumpMatch.Success)
                    machineInfo.MaxJump = decimal.Parse(maxJumpMatch.Groups[1].Value);

                // Total Thread
                var totalThreadMatch = Regex.Match(text, @"Total thread:\s*([\d.]+)m");
                if (totalThreadMatch.Success)
                    machineInfo.TotalThread = decimal.Parse(totalThreadMatch.Groups[1].Value);

                // Total Bobbin
                var totalBobbinMatch = Regex.Match(text, @"Total bobbin:\s*([\d.]+)m");
                if (totalBobbinMatch.Success)
                    machineInfo.TotalBobbin = decimal.Parse(totalBobbinMatch.Groups[1].Value);

                model.MachineInfo = machineInfo;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Machine Info Parse Error: {ex.Message}");
            }
        }

        private static void ParseStopSequence(string text, ParsedDesignVM model)
        {
            try
            {
                model.StopSequences = new List<StopSequenceVM>();

                // Find Stop Sequence section
                var stopSectionMatch = Regex.Match(text, @"Stop Sequence:(.+?)(?:Sequins:|Thread usage:|$)", RegexOptions.Singleline);
                if (!stopSectionMatch.Success) return;

                string stopSection = stopSectionMatch.Groups[1].Value;

                // Parse each line with pattern: number. needle stitches code/name (optional sequin info)
                var lineMatches = Regex.Matches(stopSection, @"(\d+)\.\s+(\d+)\s+([\d,]+)\s+(.+?)(?:\n|$)", RegexOptions.Multiline);

                foreach (Match match in lineMatches)
                {
                    try
                    {
                        var stop = new StopSequenceVM();

                        stop.SequenceNo = int.Parse(match.Groups[1].Value);
                        stop.NeedleNo = int.Parse(match.Groups[2].Value);
                        stop.StitchCount = int.Parse(match.Groups[3].Value.Replace(",", ""));

                        string fullLine = match.Groups[4].Value.Trim();

                        // Remove sequin info in parentheses at end first
                        var sequinInfoMatch = Regex.Match(fullLine, @"(.+?)\s*\((\d+)\)\s*$");
                        if (sequinInfoMatch.Success)
                        {
                            fullLine = sequinInfoMatch.Groups[1].Value.Trim();
                            stop.SequinInfo = $"({sequinInfoMatch.Groups[2].Value})";
                        }

                        // Now fullLine contains: "5071 GRT SEQ LIGHT GOLD KATORI" or "804 TILLA" or "SILVER TILLA FAKE"
                        // Extract color code (complete code with text) and name

                        // Pattern 1: Code starts with numbers (e.g., "5071 GRT SEQ LIGHT GOLD KATORI")
                        // Pattern 2: Code is just text (e.g., "SILVER TILLA FAKE")

                        var parts = fullLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length > 0)
                        {
                            // Check if line has multiple uppercase words - could be code + name
                            // Example: "5071 GRT SEQ LIGHT GOLD KATORI"
                            // Code should be everything before a clear name pattern

                            // Simple approach: Everything is the color code unless there's a clear separator
                            // Based on your PDF, codes are like: "5071 GRT", "804 TILLA", "SILVER TILLA FAKE"
                            // Names appear separately after sequin info

                            // For now, take the entire line as ColorCode since names are usually in Chart/Element columns
                            stop.ColorCode = fullLine;

                            // ColorName is typically empty or in a separate column in the PDF
                            // If you need to split it differently, provide the exact pattern
                        }

                        model.StopSequences.Add(stop);
                    }
                    catch
                    {
                        continue; // Skip problematic lines
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop Sequence Parse Error: {ex.Message}");
            }
        }

        private static void ParseSequins(string text, ParsedDesignVM model)
        {
            try
            {
                model.Sequins = new List<SequinVM>();

                // Find Sequins section
                var sequinSectionMatch = Regex.Match(text, @"Sequins:(.+?)(?:Thread usage:|$)", RegexOptions.Singleline);
                if (!sequinSectionMatch.Success) return;

                string sequinSection = sequinSectionMatch.Groups[1].Value;

                // Parse sequin lines: number. size count name?
                // Pattern matches: "1. 3.0 1762" or "1. 3.0 1762 Some Name"
                var lineMatches = Regex.Matches(sequinSection, @"\d+\.\s+([\d.]+)\s+(\d+)\s*(.+)?", RegexOptions.Multiline);

                foreach (Match match in lineMatches)
                {
                    try
                    {
                        var sequin = new SequinVM();

                        // Size is complete value (e.g., 3.0)
                        sequin.Size = decimal.Parse(match.Groups[1].Value);

                        // Count
                        sequin.Count = int.Parse(match.Groups[2].Value);

                        // Name (optional)
                        if (!string.IsNullOrWhiteSpace(match.Groups[3].Value))
                            sequin.Name = match.Groups[3].Value.Trim();

                        model.Sequins.Add(sequin);
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sequins Parse Error: {ex.Message}");
            }
        }

        private static void ParseThreadUsage(string text, ParsedDesignVM model)
        {
            try
            {
                model.ThreadUsages = new List<ThreadUsageVM>();

                // Find Thread usage section
                var threadSectionMatch = Regex.Match(text, @"Thread usage:[^\n]*\n(.+?)(?:Production Worksheet|$)", RegexOptions.Singleline);
                if (!threadSectionMatch.Success) return;

                string threadSection = threadSectionMatch.Groups[1].Value;

                // ✅ SAME PATTERN AS STOP SEQUENCE: needle. EVERYTHING_UNTIL length
                // Parse each line with pattern: number. full_text lengthm
                var lineMatches = Regex.Matches(threadSection, @"(\d+)\.\s+(.+?)\s+([\d.]+)m", RegexOptions.Multiline);

                foreach (Match match in lineMatches)
                {
                    try
                    {
                        var thread = new ThreadUsageVM();

                        thread.NeedleNo = int.Parse(match.Groups[1].Value);

                        // ✅ GET FULL LINE (just like Stop Sequence does)
                        string fullLine = match.Groups[2].Value.Trim();

                        // ✅ ENTIRE LINE AS CODE (same as Stop Sequence ColorCode logic)
                        thread.Code = fullLine;

                        // ✅ NAME EMPTY (same as Stop Sequence ColorName logic)
                        // Name is typically in a separate column in the PDF or not present
                        thread.Name = "";

                        thread.Length = decimal.Parse(match.Groups[3].Value);

                        model.ThreadUsages.Add(thread);
                    }
                    catch
                    {
                        continue; // Skip problematic lines
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thread Usage Parse Error: {ex.Message}");
            }
        }
    }
}