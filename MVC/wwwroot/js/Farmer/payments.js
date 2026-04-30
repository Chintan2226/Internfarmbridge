
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
                var isFullySettled = (x.paymentNumber === 2 || x.status === "settled");
                
                if (isFullySettled) {
                    totalEarned += amt;
                } else {
                    // advance_paid (30%)
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
                    
                    PaymentNumber: item.paymentNumber,
                    
                    ContractValue: item.contractValue,

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
                            "#:CropType#" +
                            "</div>",
                        attributes: { "data-i18n-header": "crop_request" }
                    },

                    {
                        field: "ContractValue",
                        title: "Contract Value",
                        width: 190,

                        template:
                            "<div class='amount-val'>₹#=kendo.toString(ContractValue, 'n0')#</div>" +
                            "<div class='amount-sub'><i class='fi fi-rr-bank'></i> #:PayMode#</div>",
                        attributes: { "data-i18n-header": "contract_value" }
                    },

                    {
                        title: "Payment Progress",
                        width: 400,
                        template: function (d) {
                            var contractAmt = d.ContractValue > 0 ? d.ContractValue : (d.Amount > 0 ? d.Amount / 0.3 : 0); // Fallback if db is missing contract value
                            var isFullySettled = (d.PaymentNumber === 2) || (d.Status === "settled");
                            
                            var progressPercent = isFullySettled ? 100 : 30;
                            
                            var thirtyAmt = Math.round(contractAmt * 0.3);
                            var seventyAmt = contractAmt - thirtyAmt;

                            var bracketText = isFullySettled 
                                ? "[Total: ₹" + kendo.toString(contractAmt, 'n0') + " | 30%: ₹" + kendo.toString(thirtyAmt, 'n0') + " | 70%: ₹" + kendo.toString(seventyAmt, 'n0') + "]"
                                : "[Total: ₹" + kendo.toString(contractAmt, 'n0') + " | 30%: ₹" + kendo.toString(thirtyAmt, 'n0') + " | 70%: Pending]";
                            
                            var barColor = isFullySettled ? "linear-gradient(135deg, #22c55e, #16a34a)" : "linear-gradient(135deg, #fbe69b, #d4af37)";
                            var textColor = isFullySettled ? "#16a34a" : "#d4af37";
                            var statusText = isFullySettled ? "100% Fully Settled" : "30% Advance Cleared";
                            var subText = isFullySettled ? "Completed" : "Pending Admin Approval";
                            var icon = isFullySettled ? "check-circle" : "time-fast";

                            return "<div style='display:flex; flex-direction:column; gap:10px; padding-right:15px; padding-top:5px;'>" +
                                      "<div style='display:flex; justify-content:space-between; align-items:flex-end;'>" +
                                          "<div style='display:flex; flex-direction:column; line-height:1.2;'>" +
                                              "<span style='font-size:10px; font-weight:800; color:#64748b; text-transform:uppercase; letter-spacing:0.5px;'>Payment Breakdown</span>" +
                                              "<span style='font-size:13px; font-weight:700; color:#0f172a; margin-top:2px; letter-spacing:0.3px;'>" + bracketText + "</span>" +
                                          "</div>" +
                                      "</div>" +
                                      "<div style='width:100%; background:rgba(22, 163, 74, 0.1); border-radius:10px; height:10px; overflow:hidden; box-shadow:inset 0 1px 3px rgba(0,0,0,0.06);'>" +
                                          "<div style='width:" + progressPercent + "%; background:" + barColor + "; height:100%; transition:width 1s cubic-bezier(0.4, 0, 0.2, 1); border-radius:10px;'></div>" +
                                      "</div>" +
                                      "<div style='display:flex; justify-content:space-between; font-size:12px; font-weight:700;'>" +
                                          "<span style='color:" + textColor + "; display:flex; align-items:center; gap:6px;'><i class='fi fi-rr-" + icon + "'></i>" + statusText + "</span>" +
                                          "<span style='color:#64748b;'>" + subText + "</span>" +
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