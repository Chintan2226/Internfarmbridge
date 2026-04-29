/**
 * Farmer Dashboard — dashboard.js
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
        console.error("Failed to load: " + section);
    }

    /* Abort immediately if no token found */
    if (!getToken()) {
        redirectToLogin();
        return;
    }

    console.log("Farmer ID:", window.FARMER_ID);

    /* ──────────────────────────────────────────────
       1. KPI CARDS
    ────────────────────────────────────────────── */

    /* KPIs start in skeleton state via HTML class fb-skeleton-text-inline */

    $.ajax({
        url: `${window.API_BASE}/${window.FARMER_ID}/dashboard`,
        type: "GET",
        headers: authHeaders(),

        success: function (res) {

            if (res.success) {

                var d = res.data;

                $("#kpiEarnings")
                    .removeClass("fb-skeleton-text-inline")
                    .text("₹" + (d.kpis.totalPaymentsReceived || 0).toLocaleString("en-IN"));

                $("#kpiCrops")
                    .removeClass("fb-skeleton-text-inline")
                    .text(d.kpis.totalCropsListed || 0);

                $("#kpiPendingPay")
                    .removeClass("fb-skeleton-text-inline")
                    .text("₹" + (d.kpis.pendingPaymentsAmount || d.kpis.pendingQcRequests || 0).toLocaleString("en-IN"));

                $("#kpiQC")
                    .removeClass("fb-skeleton-text-inline")
                    .text(d.kpis.confirmedQcSlots || 0);
            }
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            // Clear skeletons on error too
            $(".kpi-val").removeClass("fb-skeleton-text-inline").text("--");
            showLoadError("dashboard KPIs");
        }
    });


    /* ──────────────────────────────────────────────
       2. INCOME HISTORY CHART
    ────────────────────────────────────────────── */

    FBSkeleton.show("#chartSkeleton", 1, 'chart');

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/income-chart`,
        type: "GET",
        headers: authHeaders(),

        success: function (res) {
            FBSkeleton.hide("#chartSkeleton");
            $("#incomeChart").show().kendoChart({

                title: { visible: false },
                legend: { visible: false },

                chartArea: {
                    background: "transparent",
                    margin: { top: 0, left: 0, right: 0, bottom: 0 }
                },

                plotArea: {
                    background: "transparent",
                    border: { width: 0 }
                },

                seriesDefaults: {
                    type: "column",
                    border: { width: 0 },
                    overlay: { gradient: "none" }
                },

                dataSource: {
                    data: res.data || []
                },

                series: [{
                    name: "Revenue (₹)",
                    field: "amount",
                    color: "#10b981",
                    highlight: {
                        visible: true
                    }
                }],

                valueAxis: {
                    labels: {
                        format: "₹{0:N0}",
                        color: "#5a7257",
                        font: "12px Inter, sans-serif"
                    },
                    line: { visible: false },
                    majorGridLines: {
                        color: "rgba(0,0,0,0.06)",
                        dashType: "dash"
                    }
                },

                categoryAxis: {
                    field: "month",
                    majorGridLines: { visible: false },
                    labels: {
                        color: "#5a7257",
                        font: "12px Inter, sans-serif"
                    },
                    line: { color: "rgba(0,0,0,0.08)" }
                },

                tooltip: {
                    visible: true,
                    background: "#0a2108",
                    color: "#fff",
                    border: { width: 0 },
                    template: "₹#= kendo.toString(value, 'n0') #"
                }

            });
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            showLoadError("income chart");
        }
    });


    /* ──────────────────────────────────────────────
       3. RECENT PAYMENTS GRID
    ────────────────────────────────────────────── */

    /* Section */

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/payments`,
        type: "GET",
        headers: authHeaders(),

        success: function (response) {

            console.log("Payments Response:", response);

            var rows = (response.data || []).map(function (item) {

                return {
                    Date: new Date().toLocaleDateString("en-IN"), // fallback date

                    Crop: item.cropName || "Crop Payment",

                    Amount: item.amount || 0
                };
            });

        $("#gridSkeleton").empty().hide();
        $("#paymentsGridSkeleton").empty().hide();
        $("#paymentsGrid").show().kendoGrid({

                dataSource: {
                    data: rows,
                    pageSize: 5
                },

                pageable: true,
                sortable: true,

                columns: [

                    {
                        field: "Date",
                        title: "Date",
                        width: "30%"
                    },

                    {
                        field: "Crop",
                        title: "Crop Sold",
                        width: "40%"
                    },

                    {
                        field: "Amount",
                        title: "Amount",
                        width: "30%",
                        template: function (dataItem) {
                            return "<span style='font-weight:600;color:#2e7d32;'>₹" +
                                kendo.toString(dataItem.Amount, "n0") +
                                "</span>";
                        }
                    }

                ]

            });

        },

        error: function (xhr) {

            if (handleUnauthorized(xhr)) return;

            showLoadError("recent payments");
        }

    });

});
