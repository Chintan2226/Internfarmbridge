using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace API.Services
{
    public class PdfService
    {
        private static readonly string GreenDark = "#1B5E20";
        private static readonly string GreenMid = "#2E7D32";
        private static readonly string GreenLight = "#E8F5E9";
        private static readonly string GreenAccent = "#43A047";
        private static readonly string TextDark = "#1A1A1A";
        private static readonly string TextLight = "#757575";
        private static readonly string BorderGray = "#E0E0E0";
        private static readonly string White = "#FFFFFF";
        private static readonly string BgPage = "#F9FBF9";

        public byte[] GenerateInspectionPdf(dynamic data)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.Background(Color.FromHex(BgPage));

                    // HEADER
                    page.Header().Column(header =>
                    {
                        header.Item().Background(Color.FromHex(GreenDark)).Padding(28).Row(row =>
                        {
                            // LOGO
                            row.ConstantItem(60).Height(60)
                                .AlignMiddle()
                                .Svg(File.ReadAllText(
                                    Path.Combine(Directory.GetCurrentDirectory(), "..", "MVC", "wwwroot", "Logo", "Logo_Without_Bg.svg")
                                ));

                            row.ConstantItem(10);

                            // BRAND TEXT
                            row.RelativeItem().Column(brand =>
                            {
                                brand.Item()
                                    .Text("FarmBridge")
                                    .FontSize(26).Bold()
                                    .FontColor(Color.FromHex(White));

                                brand.Item()
                                    .Text("Inspection Report")
                                    .FontSize(13)
                                    .FontColor(Color.FromHex(GreenAccent));
                            });

                            row.ConstantItem(200).Column(meta =>
                            {
                                meta.Item().AlignRight()
                                    .Text((string)(data.farmerName ?? "—"))
                                    .FontSize(13).Bold()
                                    .FontColor(Color.FromHex(White));

                                meta.Item().AlignRight()
                                    .Text($"Dated: {data.submittedAt:dd MMM yyyy}")
                                    .FontSize(10)
                                    .FontColor(Color.FromHex(GreenAccent));
                            });
                        });

                        header.Item().Background(Color.FromHex(GreenMid))
                            .PaddingHorizontal(28)
                            .PaddingVertical(8)
                            .Row(status =>
                            {
                                status.RelativeItem()
                                    .Text("✓ Inspection Completed & Accepted")
                                    .FontSize(10).Bold()
                                    .FontColor(Color.FromHex(White));

                                status.AutoItem()
                                    .Text("Field Officer Assessed")
                                    .FontSize(10)
                                    .FontColor(Color.FromHex(GreenLight));
                            });
                    });

                    // FOOTER
                    page.Footer().Background(Color.FromHex(GreenDark)).Padding(18).Row(footer =>
                    {
                        footer.RelativeItem().Column(left =>
                        {
                            left.Item()
                                .Text("FarmBridge — Connecting Farmers to Markets")
                                .FontSize(9)
                                .FontColor(Color.FromHex(GreenAccent));

                            left.Item()
                                .Text($"This is a system-generated report and requires no signature. Generated: {DateTime.Now:dd MMM yyyy, hh:mm tt}")
                                .FontSize(8)
                                .FontColor(Color.FromHex(GreenLight))
                                .Italic();
                        });
                    });

                    // BODY
                    page.Content().Padding(28).Column(body =>
                    {
                        // Farmer & Crop Details
                        body.Item().PaddingBottom(20).Column(sec =>
                        {
                            SectionHeader(sec, "Farmer & Crop Details");

                            sec.Item().Background(Color.FromHex(White))
                                .Border(1).BorderColor(Color.FromHex(BorderGray))
                                .CornerRadius(6)
                                .Padding(16)
                                .Grid(grid =>
                                {
                                    grid.Columns(2);
                                    grid.Spacing(12);

                                    InfoCell(grid, "Farmer Name", (string)(data.farmerName ?? "—"));
                                    InfoCell(grid, "Crop", (string)(data.cropName ?? "—"));
                                    InfoCell(grid, "Variety", (string)(data.variety ?? "—"));
                                    InfoCell(grid, "Grade", (string)(data.grade ?? "—"));
                                });
                        });

                        // Quantity Assessment
                        body.Item().PaddingBottom(20).Column(sec =>
                        {
                            SectionHeader(sec, "Quantity Assessment");

                            sec.Item().Background(Color.FromHex(White))
                                .Border(1).BorderColor(Color.FromHex(BorderGray))
                                .CornerRadius(6)
                                .Padding(16)
                                .Grid(grid =>
                                {
                                    grid.Columns(2);
                                    grid.Spacing(12);

                                    InfoCellHighlight(grid, "Accepted Qty", $"{data.acceptedQuantity} kg", GreenMid);
                                    InfoCellHighlight(grid, "Rejected Qty", $"{data.rejectedQuantity} kg", "#C62828");
                                    InfoCell(grid, "Moisture", $"{data.moisturePct}%");
                                    InfoCell(grid, "Foreign Matter", $"{data.foreignMatterPct}%");
                                });
                        });

                        // Quality Observations
                        body.Item().PaddingBottom(20).Column(sec =>
                        {
                            SectionHeader(sec, "Quality Observations");

                            sec.Item().Background(Color.FromHex(White))
                                .Border(1).BorderColor(Color.FromHex(BorderGray))
                                .CornerRadius(6)
                                .Padding(16)
                                .Column(obs =>
                                {
                                    LabelValue(obs, "Defects Noted", (string)(data.defectsNoted ?? "None"));
                                    obs.Item().PaddingTop(8);
                                    LabelValue(obs, "Inspector Remarks", (string)(data.remarks ?? "—"));
                                });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private void SectionHeader(ColumnDescriptor col, string title)
        {
            col.Item().PaddingBottom(8).Row(row =>
            {
                row.ConstantItem(4).Background(Color.FromHex(GreenMid)).CornerRadius(2);
                row.ConstantItem(8);
                row.RelativeItem()
                    .Text(title.ToUpper())
                    .FontSize(10).Bold()
                    .FontColor(Color.FromHex(GreenDark))
                    .LetterSpacing(0.05f);
            });
        }

        private void InfoCell(GridDescriptor grid, string label, string value)
        {
            grid.Item().Column(c =>
            {
                c.Item().Text(label)
                    .FontSize(9).FontColor(Color.FromHex(TextLight));
                c.Item().Text(value)
                    .FontSize(12).Bold().FontColor(Color.FromHex(TextDark));
            });
        }

        private void InfoCellHighlight(GridDescriptor grid, string label, string value, string color)
        {
            grid.Item().Column(c =>
            {
                c.Item().Text(label)
                    .FontSize(9).FontColor(Color.FromHex(TextLight));
                c.Item().Text(value)
                    .FontSize(12).Bold().FontColor(Color.FromHex(color));
            });
        }

        private void LabelValue(ColumnDescriptor col, string label, string value)
        {
            col.Item().Column(c =>
            {
                c.Item().Text(label)
                    .FontSize(9).FontColor(Color.FromHex(TextLight));
                c.Item().PaddingTop(2).Text(value)
                    .FontSize(11).FontColor(Color.FromHex(TextDark));
            });
        }
    }
}