/**
 * Farmer Payments — payments.js
 * JWT Bearer token is read from cookie: "authToken"
 * Multi-farmer support enabled
 */

$(document).ready(function () {

    /* ──────────────────────────────────────────────
       AUTH HELPERS
    ────────────────────────────────────────────── */

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function authHeaders() {

        var token = getToken();

        if (!token) {
            redirectToLogin();
            return {};
        }

        return {
            "Authorization": "Bearer " + token
        };
    }

    function handleUnauthorized(xhr) {

        if (!xhr || xhr.status === 401 || xhr.status === 403 || !getToken()) {
            redirectToLogin();
            return true;
        }

        return false;
    }

    function redirectToLogin() {
        window.location.href = "/Farmer/Login";
    }

    function showLoadError(section) {
        console.error("Failed to load:", section);
    }

    /* Abort if token missing */

    if (!getToken()) {
        redirectToLogin();
        return;
    }

    console.log("Farmer ID:", window.FARMER_ID);


    /* ──────────────────────────────────────────────
       PAYMENTS GRID
    ────────────────────────────────────────────── */

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/payments`,

        type: "GET",

        headers: authHeaders(),

        success: function (response) {

            /* Lifetime earnings KPI */

            var total = 0;

            (response.data || []).forEach(function (x) {
                total += x.amount;
            });

            $("#kpiLifetimeEarned").text(
                "₹" + total.toLocaleString("en-IN")
            );


            /* Map rows */

            var rows = (response.data || []).map(function (item) {

                return {

                    OrderRef: item.utrReference || "Pending",

                    CropType: item.cropName,

                    Amount: item.amount,

                    PayMode: item.paymentMode || "Bank Transfer",

                    Status: item.status
                };

            });


            $("#paymentsGrid").kendoGrid({

                toolbar: ["search"],

                dataSource: {
                    data: rows,
                    pageSize: 10
                },

                pageable: true,

                sortable: true,

                columns: [

                    {
                        field: "OrderRef",
                        title: "UTR Ref",
                        width: 145,
                        template:
                            "<span class='order-chip'>#:OrderRef#</span>"
                    },

                    {
                        field: "CropType",
                        title: "Crop Listing",
                        width: 210,
                        template:
                            "<div style='font-weight:700; color:rgb(21,128,61); font-size:13px;'>#:CropType#</div>"
                    },

                    {
                        field: "Amount",
                        title: "Agreement Value",
                        width: 160,

                        template:
                            "<div>" +
                            "<strong style='font-size:16px; color:rgb(15,23,42); font-weight:800;'>" +
                            "₹#=kendo.toString(Amount, 'n0')#" +
                            "</strong>" +
                            "<div style='font-size:11px; color:rgb(100,116,139); font-weight:600;'>" +
                            "via #:PayMode#" +
                            "</div>" +
                            "</div>"
                    },

                    {
                        title: "Payment Progress",
                        width: 380,
                        template: function (d) {
                            var amt = d.Amount || 0;
                            var advance = Math.round(amt * 0.3);
                            var finalPay = amt - advance;
                            
                            var isSuccess = d.Status === "success";
                            
                            // Visual classes for steps based on status
                            var line1Class = "active";
                            var step2Class = "active";
                            var line2Class = isSuccess ? "settled" : "";
                            var step3Class = isSuccess ? "settled" : "";
                            
                            var color1 = "#059669";
                            var color2 = "#059669";
                            var color3 = isSuccess ? "#0d9488" : "#94a3b8";

                            return "<div style='padding: 5px 0;'>" +
                                       "<div class='pay-tracker' style='padding: 0 10px;'>" +
                                           "<div class='tracker-step initiated'></div>" +
                                           "<div class='tracker-line " + line1Class + "'></div>" +
                                           "<div class='tracker-step " + step2Class + "'></div>" +
                                           "<div class='tracker-line " + line2Class + "'></div>" +
                                           "<div class='tracker-step " + step3Class + "'></div>" +
                                       "</div>" +
                                       "<div class='tracker-label' style='margin-top: 8px;'>" +
                                           "<div style='text-align: left; color: " + color1 + "; width:33%;'>" +
                                              "<span style='display:block; font-size:9px; text-transform:uppercase; opacity:0.8;'>Initialized</span>" +
                                              "<span style='font-size:11px;'>₹" + kendo.toString(amt, 'n0') + "</span>" +
                                           "</div>" +
                                           "<div style='text-align: center; color: " + color2 + "; width:33%;'>" +
                                              "<span style='display:block; font-size:9px; text-transform:uppercase; opacity:0.8;'>30% Advance</span>" +
                                              "<span style='font-size:11px;'>₹" + kendo.toString(advance, 'n0') + "</span>" +
                                           "</div>" +
                                           "<div style='text-align: right; color: " + color3 + "; width:33%;'>" +
                                              "<span style='display:block; font-size:9px; text-transform:uppercase; opacity:0.8;'>70% Balance</span>" +
                                              "<span style='font-size:11px;'>₹" + kendo.toString(finalPay, 'n0') + "</span>" +
                                           "</div>" +
                                       "</div>" +
                                   "</div>";
                        }
                    }

                ]

            });

        },

        error: function (xhr) {

            if (handleUnauthorized(xhr)) return;

            showLoadError("payments");


            /* Empty grid fallback */

            $("#paymentsGrid").kendoGrid({

                dataSource: {
                    data: [],
                    pageSize: 10
                },

                pageable: true,

                sortable: true,

                columns: [

                    { field: "OrderRef", title: "UTR Ref", width: 145 },

                    { field: "CropType", title: "Crop Listing", width: 210 },

                    { field: "Amount", title: "Agreement Value", width: 160 },

                    { title: "Payment Progress", width: 380 }

                ]

            });

        }

    });

});