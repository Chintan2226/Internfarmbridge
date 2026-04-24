/**
 * Farmer Dashboard — dashboard.js
 */

$(document).ready(function () {

    /* ──────────────────────────────────────────────
       WELCOME MODAL LOGIC 
    ────────────────────────────────────────────── */
    function getCookie(name) {
        var match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
        if (match) return match[2];
        return null;
    }

    function eraseCookie(name) {
        document.cookie = name + '=; Max-Age=-99999999; path=/';
    }

    if (getCookie("ShowWelcomeGuide") === "true") {
        $('#farmerWelcomeModal').addClass('show');
        $('body').css('overflow', 'hidden'); 
        eraseCookie("ShowWelcomeGuide"); 
    }


    /* ──────────────────────────────────────────────
       1. INITIALIZE UI CONTROLS
    ────────────────────────────────────────────── */
    
    // Initialize Kendo Button Group for the timeframe pill toggle
    $("#timeframeToggle").kendoButtonGroup({
        items: [
            { text: "Week", selected: true },
            { text: "Month" },
            { text: "Year" }
        ],
        select: function(e) {
            var timeframe = this.current().text();
            console.log("Selected Timeframe: " + timeframe);
            // Example: $("#revenueChart").data("kendoChart").dataSource.read({ span: timeframe });
        }
    });

    /* ──────────────────────────────────────────────
       2. REVENUE CHART
    ────────────────────────────────────────────── */
    
    // Setup Chart Options (Empty Data initially)
    $("#incomeChart").kendoChart({
        title: { visible: false },
        legend: { visible: false }, 
        chartArea: { background: "transparent", margin: { top: 10 } },
        seriesDefaults: { type: "area", style: "smooth", opacity: 0.2 }, 
        dataSource: { data: [] },
        series: [{
            name: "Actual Revenue (₹)",
            field: "actual",
            color: "#38a169", 
            line: { width: 3 }
        }],
        valueAxis: {
            labels: { format: "₹{0}", step: 2, color: "#718096", font: "11px var(--font-body)" },
            line: { visible: false },
            majorGridLines: { color: "rgba(0,0,0,0.04)" }
        },
        categoryAxis: {
            field: "month",
            majorGridLines: { visible: false },
            labels: { color: "#718096", font: "11px var(--font-body)", padding: { top: 6 } },
            line: { color: "rgba(0,0,0,0.08)" }
        },
        tooltip: { visible: true, format: "₹{0}", template: "#= series.name #: ₹#= kendo.toString(value, 'n0') #" },
        noData: { template: "<div class='text-muted' style='padding: 20px 0; text-align: center;'>No revenue data available.</div>" }
    });

    /* ──────────────────────────────────────────────
       3. RECENT PAYMENTS GRID (WITH SKELETON)
    ────────────────────────────────────────────── */
    
    // 👉 STEP 1: Show the skeleton animation first
    if (typeof window.showGridSkeleton === 'function') {
        window.showGridSkeleton("#paymentsGrid", 3);
    }

    // 👉 STEP 2: Fetch the data
    // Note: Replace this setTimeout with your actual $.ajax call when backend is ready!
    setTimeout(function() {
        
        // Mock data to simulate API response
        var mockPayments = [
            { Date: "12 Apr 2026", Crop: "Wheat Grade A", Amount: 24500 },
            { Date: "08 Apr 2026", Crop: "Cotton Extra Long", Amount: 56000 },
            { Date: "01 Apr 2026", Crop: "Soybean", Amount: 18200 }
        ];

        // 👉 STEP 3: Load real data into Kendo Grid (This overwrites the skeleton)
        $("#paymentsGrid").kendoGrid({
            dataSource: {
                data: mockPayments, // Use your real data variable here
                pageSize: 5
            },
            pageable: true,
            sortable: true,
            columns: [
                { field: "Date", title: "Date", width: "30%" },
                { field: "Crop", title: "Crop Sold", width: "40%" },
                { field: "Amount", title: "Amount", width: "30%", template: "<span style='font-weight:600; color:\\#2e7d32;'>₹#=kendo.toString(Amount, 'n0')#</span>" }
            ]
        });

    }, 1500); // 1.5 second delay to show off the skeleton animation!

});