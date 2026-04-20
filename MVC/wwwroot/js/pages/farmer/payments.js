$(document).ready(function () {
    $("#paymentsGrid").kendoGrid({
        toolbar: ["search"],
        pdf: {
            allPages: true,
            avoidLinks: true,
            paperSize: "A4",
            margin: { top: "2cm", left: "1cm", right: "1cm", bottom: "1cm" },
            landscape: true,
            repeatHeaders: true,
            template: $("#page-template").html(),
            scale: 0.8,
            fileName: "Farmer_Payment_History.pdf"
        },
        dataSource: {
            // transport: { read: { url: window.API_BASE + "/payments", dataType: "json" } },
            pageSize: 10
        },
        pageable: true,
        sortable: true,
        autoBind: false,
        noRecords: { template: "No payments recorded yet." },
        columns: [
            {
                field: "OrderRef",
                title: "Order ID",
                width: 145,
                template: function (d) {
                    return "<span class='order-chip'>" + d.OrderRef + "</span>";
                }
            },
            {
                field: "CropType",
                title: "Crop Listing",
                width: 210,
                template: function (d) {
                    return "<div style='font-weight:700; color:rgb(21,128,61); font-size:13px; line-height:1.5;'>" + d.CropType + "</div>";
                }
            },
            {
                field: "Amount",
                title: "Agreement Value",
                width: 160,
                template: function (d) {
                    var mode = d.PayMode || "Bank Transfer";
                    var amount = kendo.toString(d.Amount, "n0");
                    return "<div><strong style='font-size:16px; color:rgb(15,23,42); font-weight:800;'>\u20b9" + amount + "</strong>"
                        + "<div style='font-size:11px; color:rgb(100,116,139); margin-top:3px; font-weight:600;'>via " + mode + "</div></div>";
                }
            },
            {
                title: "Live Payment Progress",
                width: 360,
                template: function (d) {
                    var adv = Math.round(d.Amount * 0.3);   // 30% post-QC advance
                    var rem = Math.round(d.Amount * 0.7);   // 70% final settlement after sale
                    var fmt = function (n) { return "\u20b9" + n.toLocaleString("en-IN"); };

                    var l1 = d.Pay1Status === "Paid" ? "active" : "";
                    var s2 = d.Pay1Status === "Paid" ? "active" : "";
                    var l2 = d.Pay2Status === "Paid" ? "settled" : "";
                    var s3 = d.Pay2Status === "Paid" ? "settled" : "";

                    var c1 = "#6ee7b7";
                    var c2 = d.Pay1Status === "Paid" ? "#10b981" : "#94a3b8";
                    var c3 = d.Pay2Status === "Paid" ? "#0d9488" : "#94a3b8";

                    var badge;
                    if (d.Pay2Status === "Paid") {
                        badge = "<span style='background:#fff;color:#0d9488;border:1.5px solid #0d9488;padding:2px 10px;border-radius:20px;font-size:10px;font-weight:700;letter-spacing:0.02em;'>&#10003; Fully Settled</span>";
                    } else if (d.Pay1Status === "Paid") {
                        badge = "<span style='background:#fff;color:#059669;border:1.5px solid #10b981;padding:2px 10px;border-radius:20px;font-size:10px;font-weight:700;letter-spacing:0.02em;'>&#9654; 30% Advance Cleared &bull; " + fmt(rem) + " pending</span>";
                    } else {
                        badge = "<span style='background:#fff;color:#64748b;border:1.5px solid #cbd5e1;padding:2px 10px;border-radius:20px;font-size:10px;font-weight:600;letter-spacing:0.02em;'>&#9679; Awaiting First Clearance</span>";
                    }

                    return "<div style='padding:6px 8px;'>"
                        + "<div class='pay-tracker'>"
                        + "<div class='tracker-step initiated' title='Initiated'></div>"
                        + "<div class='tracker-line " + l1 + "'></div>"
                        + "<div class='tracker-step " + s2 + "' title='70% Advance'></div>"
                        + "<div class='tracker-line " + l2 + "'></div>"
                        + "<div class='tracker-step " + s3 + "' title='Fully Settled'></div>"
                        + "</div>"
                        + "<div class='tracker-label'>"
                        + "<span style='color:" + c1 + ";'>Initiated</span>"
                        + "<span style='color:" + c2 + ";'>30% (" + fmt(adv) + ")</span>"
                        + "<span style='color:" + c3 + ";'>70% Final (" + fmt(rem) + ")</span>"
                        + "</div>"
                        + "<div style='margin-top:6px;'>" + badge + "</div>"
                        + "</div>";
                }
            },
            {
                title: "Invoice",
                width: 100,
                template: function () {
                    return "<button class='invoice-btn'><i class='fi fi-rr-file-pdf'></i> PDF</button>";
                }
            }
        ]
    });
});

function exportPaymentsStatement() {
    $("#paymentsGrid").data("kendoGrid").saveAsPDF();
}
