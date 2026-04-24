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

    $.ajax({
        url: `${window.API_BASE}/${window.FARMER_ID}/dashboard`,
        type: "GET",
        headers: authHeaders(),

        success: function (res) {

            if (res.success) {

                var d = res.data;

                $("#kpiEarnings").text(
                    "₹" + (d.kpis.totalPaymentsReceived || 0)
                        .toLocaleString("en-IN")
                );

                $("#kpiCrops").text(
                    d.kpis.totalCropsListed || 0
                );

                $("#kpiPendingPay").text(
                    "₹" + (
                        d.kpis.pendingPaymentsAmount ||
                        d.kpis.pendingQcRequests ||
                        0
                    ).toLocaleString("en-IN")
                );

                $("#kpiQC").text(
                    d.kpis.confirmedQcSlots || 0
                );
            }
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            showLoadError("dashboard KPIs");
        }
    });


    /* ──────────────────────────────────────────────
       2. INCOME HISTORY CHART
    ────────────────────────────────────────────── */

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/income-chart`,
        type: "GET",
        headers: authHeaders(),

        success: function (res) {

            $("#incomeChart").kendoChart({

                title: { visible: false },
                legend: { visible: false },

                chartArea: {
                    background: "transparent",
                    margin: { top: 10 }
                },

                seriesDefaults: {
                    type: "column",
                    style: "smooth",
                    opacity: 0.2
                },

                dataSource: {
                    data: res.data || []
                },

                series: [{
                    name: "Actual Revenue (₹)",
                    field: "amount",
                    color: "#38a169",
                    line: { width: 3 }
                }],

                valueAxis: {
                    labels: {
                        format: "₹{0}",
                        color: "#718096"
                    },
                    line: { visible: false },
                    majorGridLines: {
                        color: "rgba(0,0,0,0.04)"
                    }
                },

                categoryAxis: {
                    field: "month",
                    majorGridLines: { visible: false },
                    labels: { color: "#718096" },
                    line: { color: "rgba(0,0,0,0.08)" }
                },

                tooltip: {
                    visible: true,
                    template:
                        "#= series.name #: ₹#= kendo.toString(value, 'n0') #"
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


            $("#paymentsGrid").kendoGrid({

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
