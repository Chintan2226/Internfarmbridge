
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

    FBSkeleton.showInline(".kpi-val");
    FBSkeleton.show("#gridSkeleton", 5, 'list');


    /* ──────────────────────────────────────────────
       PAYMENTS GRID
    ────────────────────────────────────────────── */

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/payments`,

        type: "GET",

        headers: authHeaders(),

        success: function (response) {

            /* Lifetime earnings & Pending KPI */

            var totalEarned = 0;
            var totalPending = 0;

            (response.data || []).forEach(function (x) {
                var amt = x.amount || 0;
                if (x.status === "success") {
                    totalEarned += amt;
                } else {
                    // advance_paid
                    totalEarned += Math.round(amt * 0.3);
                    totalPending += (amt - Math.round(amt * 0.3));
                }
            });

            $("#kpiLifetimeEarned").removeClass("fb-skeleton-text-inline").text(
                "₹" + totalEarned.toLocaleString("en-IN")
            );
            
            $("#kpiPendingPay").removeClass("fb-skeleton-text-inline").text(
                "₹" + totalPending.toLocaleString("en-IN")
            );

            /* Map rows */

            var rows = (response.data || []).map(function (item) {

                return {

                    OrderRef: item.utrReference || "Pending",

                    CropType: item.cropName,

                    ImageUrl: item.imageUrl || "/images/placeholder.png",

                    Amount: item.amount,

                    PayMode: item.paymentMode ? item.paymentMode.split('_').map(function (word) { return word.charAt(0).toUpperCase() + word.slice(1); }).join(' ') : "Bank Transfer",

                    Status: item.status
                };

            });


            FBSkeleton.hide("#gridSkeleton");
            $("#paymentsGrid").show().kendoGrid({

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
                        title: "Reference",
                        width: 240,
                        template: "<div class='order-chip'><i class='fi fi-rr-hashtag' style='color:\\#94a3b8;'></i>#:OrderRef#</div>",
                        attributes: { "data-i18n-header": "reference" }
                    },

                    {
                        field: "CropType",
                        title: "Crop Request",
                        width: 180,
                        template:
                            "<div class='crop-name'>" +
                            "<div class='crop-image-wrapper'><img src='#:ImageUrl#' class='crop-image' onerror=\"this.src='/images/placeholder.png'\" /></div>" +
                            "#:CropType#" +
                            "</div>",
                        attributes: { "data-i18n-header": "crop_request" }
                    },

                    {
                        field: "Amount",
                        title: "Contract Value",
                        width: 190,

                        template:
                            "<div class='amount-val'>₹#=kendo.toString(Amount, 'n0')#</div>" +
                            "<div class='amount-sub'><i class='fi fi-rr-bank'></i> #:PayMode#</div>",
                        attributes: { "data-i18n-header": "contract_value" }
                    },

                    {
                        title: "Payment Timeline",
                        width: 440,
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

                            var color1 = "#047857"; // Deep Emerald
                            var color2 = "#d97706"; // Rich Gold
                            var color3 = isSuccess ? "#047857" : "#94a3b8";

                            return "<div class='tracker-container'>" +
                                "<div class='pay-tracker'>" +
                                "<div class='tracker-step initiated'></div>" +
                                "<div class='tracker-line " + line1Class + "'></div>" +
                                "<div class='tracker-step " + step2Class + "'></div>" +
                                "<div class='tracker-line " + line2Class + "'></div>" +
                                "<div class='tracker-step " + step3Class + "'></div>" +
                                "</div>" +
                                "<div class='tracker-label'>" +
                                "<div class='t-lbl-col' style='color: " + color1 + ";'>" +
                                "<span class='t-stage'>Initiated</span>" +
                                "<span class='t-amt'>₹" + kendo.toString(amt, 'n0') + "</span>" +
                                "</div>" +
                                "<div class='t-lbl-col' style='color: " + color2 + ";'>" +
                                "<span class='t-stage'>30% Advance</span>" +
                                "<span class='t-amt'>₹" + kendo.toString(advance, 'n0') + "</span>" +
                                "</div>" +
                                "<div class='t-lbl-col' style='color: " + color3 + ";'>" +
                                "<span class='t-stage'>70% Balance</span>" +
                                "<span class='t-amt'>₹" + kendo.toString(finalPay, 'n0') + "</span>" +
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
            $("#gridSkeleton").hide();
            $("#paymentsGrid").show().kendoGrid({

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