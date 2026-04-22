$(document).ready(function () {
    
    // Initialize Kendo Button Group for the timeframe pill toggle
    $("#timeframeToggle").kendoButtonGroup({
        items: [
            { text: "Week", selected: true },
            { text: "Month" },
            { text: "Year" }
        ],
        select: function(e) {
            // Logic to update the chart based on timeframe selected
            var timeframe = this.current().text();
            console.log("Selected Timeframe: " + timeframe);
            // Example: $("#revenueChart").data("kendoChart").dataSource.read({ span: timeframe });
        }
    });

    // Initialize Kendo Chart for Revenue Analytics
    $("#incomeChart").kendoChart({
        title: { visible: false },
        legend: { visible: false }, // Hide legend to save precious vertical/horizontal space
        chartArea: { background: "transparent", margin: { top: 10 } },
        seriesDefaults: { type: "area", style: "smooth", opacity: 0.2 }, // Use an elegant Area chart instead of Columns
        dataSource: {
            data: [] // Empty data array (Server will provide JSON)
        },
        series: [{
            name: "Actual Revenue (₹)",
            field: "actual",
            color: "#38a169", // Premium green
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
        noData: { template: "<div class='text-muted' style='padding: 20px 0; text-align: center;'>No revenue data available.</div>" },
        autoBind: false
    });

    // Payments Grid - Ready for API binding
    $("#paymentsGrid").kendoGrid({
        dataSource: {
            transport: {
                read: {
                    // url: window.API_BASE + "/payments/recent", // Uncomment when backend is ready
                    // dataType: "json"
                }
            },
            data: [], // Empty data initially
            pageSize: 5
        },
        pageable: true,
        autoBind: false, // Prevents loading until configured
        noRecords: { template: "<div class='text-muted' style='padding: 20px 0; text-align: center;'>No payment history found.</div>" },
        columns: [
            { field: "Date", title: "Date", width: "30%" },
            { field: "Crop", title: "Crop Sold", width: "40%" },
            { field: "Amount", title: "Amount", width: "30%", template: "<span style='font-weight:600; color:\\#2e7d32;'>₹#=kendo.toString(Amount, 'n0')#</span>" }
        ]
    });
});
