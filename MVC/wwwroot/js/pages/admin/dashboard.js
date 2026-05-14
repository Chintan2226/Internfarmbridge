/**
 * Admin Dashboard Logic
 * Handles KPIs, Charts, Catalog Grid, and Universal Search.
 */

(function ($) {
    $.ajaxSetup({ cache: false });

    const API_URLS = {
        kpi:     '/Admin/GetDashboardKpi',
        revenue: '/Admin/GetRevenueChart',
        orders:  '/Admin/GetOrderVolumeChart',
        catalog: '/Admin/GetCropsCatalog'
    };

    let dashboardState = {
        revPeriod:   'weekly',
        orderPeriod: 'weekly',
        refreshRate: 10000,
        revChartInit:   false,
        orderChartInit: false,
        catalogGridInit:   false 
    };

    // --- INITIALIZATION ---
    $(window).on("load", startDashboard);

    function startDashboard() {
        refreshAllData();
        setInterval(refreshAllData, dashboardState.refreshRate);
        
        // Bind search events
        $('#globalSearchInput').on('keyup', function(e) {
            clearTimeout(window.searchTimeout);
            if (e.key === 'Enter') {
                performGlobalSearch();
            } else {
                window.searchTimeout = setTimeout(performGlobalSearch, 500);
            }
        });
        
        $('#globalSearchBtn').on('click', performGlobalSearch);
        
        $('#closeSearchResults').on('click', function() {
            $('#globalSearchResults').hide();
            $('#globalSearchInput').val('');
        });
    }

    async function refreshAllData() {
        const ts = Date.now();
        try {
            await Promise.all([
                loadKPIs(ts),
                loadRevenueChart(ts),
                loadOrderChart(ts),
                loadCatalogGrid(ts) 
            ]);
        } catch(e) {
            console.error("Dashboard refresh error:", e);
        }
    }

    // --- KPI LOGIC ---
    function loadKPIs(ts) {
        const kpiStrip = $('.kpi-strip');
        const revenueEl = $('#kpiRevenue');
        
        if (kpiStrip.length > 0 && revenueEl.text() === '₹0') {
             if (window.FBSkeleton) FBSkeleton.show('.kpi-strip', 5, 'kpi');
        }

        return $.getJSON(`${API_URLS.kpi}?_=${ts}`, res => {
            if (!res.success) return;
            
            if ($('.kpi-strip .fbs-card').length > 0) {
                restoreKpiHtml();
            }

            animateValue("kpiRevenue", "₹ " + Number(res.data.todayRevenue).toLocaleString('en-IN'));
            animateValue("kpiFarmers", res.data.totalFarmers);
            animateValue("kpiVendors", res.data.totalVendors);
            animateValue("kpiFOs",     res.data.activeFOs);
            animateValue("kpiOrders",  res.data.todayNewOrders);
        });
    }

    function restoreKpiHtml() {
        const html = `
            <div class="kpi-card kpi-accent">
                <div class="kpi-top"><div class="kpi-lbl">Total Revenue</div><div class="kpi-icon">💰</div></div>
                <div class="kpi-val" id="kpiRevenue">₹0</div>
                <div class="kpi-sub">Today's earnings</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-top"><div class="kpi-lbl">Farmers</div><div class="kpi-icon">🌾</div></div>
                <div class="kpi-val" id="kpiFarmers">0</div>
                <div class="kpi-sub">Registered</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-top"><div class="kpi-lbl">Vendors</div><div class="kpi-icon">🏪</div></div>
                <div class="kpi-val" id="kpiVendors">0</div>
                <div class="kpi-sub">Active</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-top"><div class="kpi-lbl">Field Officers</div><div class="kpi-icon">👷</div></div>
                <div class="kpi-val" id="kpiFOs">0</div>
                <div class="kpi-sub">On ground</div>
            </div>
            <div class="kpi-card">
                <div class="kpi-top"><div class="kpi-lbl">Today's Orders</div><div class="kpi-icon">📦</div></div>
                <div class="kpi-val" id="kpiOrders">0</div>
                <div class="kpi-sub">New today</div>
            </div>`;
        $('.kpi-strip').html(html);
    }

    function animateValue(id, newVal) {
        const el = document.getElementById(id);
        if (!el) return;
        el.style.opacity = "0";
        el.style.transform = "translateY(6px)";
        setTimeout(() => {
            el.textContent = newVal;
            el.style.transition = "opacity .3s, transform .3s";
            el.style.opacity = "1";
            el.style.transform = "translateY(0)";
        }, 120);
    }

    // --- REVENUE CHART ---
    function loadRevenueChart(ts) {
        if (!dashboardState.revChartInit) {
            if (window.FBSkeleton) FBSkeleton.show('#revenueChart', 1, 'chart');
        }
        return $.getJSON(`${API_URLS.revenue}?period=${dashboardState.revPeriod}&_=${ts}`, res => {
            if (!res.success) return;

            let data = res.data || [];
            if (data.length === 1) {
                data = [{ label: '—', value: 0 }, ...data];
            }

            if (dashboardState.revChartInit) {
                const chart = $("#revenueChart").data("kendoChart");
                if (chart) {
                    chart.destroy();
                    $("#revenueChart").empty();
                    dashboardState.revChartInit = false;
                }
            }

            dashboardState.revChartInit = true;
            $("#revenueChart").kendoChart({
                dataSource: { data: data },
                series: [{
                    type: "area",
                    field: "value",
                    categoryField: "label",
                    color: "#d4af37",
                    opacity: 0.25,
                    line: { color: "#d4af37", width: 3.5 },
                    markers: { visible: true, size: 7, background: "#d4af37", border: { color: "#fff", width: 2.5 } }
                }],
                categoryAxis: {
                    labels: { font: "11px Inter, monospace", color: "#64748b" },
                    majorGridLines: { visible: false },
                    line: { visible: false }
                },
                valueAxis: {
                    labels: { format: "₹{0}", font: "11px Inter, monospace", color: "#64748b" },
                    majorGridLines: { color: "rgba(212,175,55,.15)", dashType: "dash" },
                    line: { visible: false }
                },
                tooltip: {
                    visible: true,
                    background: "#060a07",
                    color: "#ffffff",
                    font: "12.5px Inter, monospace",
                    template: "₹ #= kendo.toString(value,'n0') #",
                    border: { width: 0 },
                    padding: { top: 8, bottom: 8, left: 14, right: 14 }
                },
                chartArea: { height: 260, background: "transparent" },
                plotArea: { margin: { top: 15, bottom: 0, left: 0, right: 0 } },
                legend: { visible: false },
                transitions: true
            });
        });
    }

    window.changeRevPeriod = function(p, btn) {
        dashboardState.revPeriod = p;
        dashboardState.revChartInit = false;
        $("#revSwitcher .btn-pd").removeClass("active");
        $(btn).addClass("active");
        $("#revenueChart").empty();
        loadRevenueChart(Date.now());
    };

    // --- ORDER CHART ---
    function loadOrderChart(ts) {
        if (!dashboardState.orderChartInit) {
            if (window.FBSkeleton) FBSkeleton.show('#orderVolumeChart', 1, 'chart');
        }
        return $.getJSON(`${API_URLS.orders}?period=${dashboardState.orderPeriod}&_=${ts}`, res => {
            if (!res.success) return;
            if (dashboardState.orderChartInit) {
                const chart = $("#orderVolumeChart").data("kendoChart");
                if (chart) { chart.setDataSource(new kendo.data.DataSource({ data: res.data })); }
            } else {
                dashboardState.orderChartInit = true;
                $("#orderVolumeChart").kendoChart({
                    dataSource: { data: res.data },
                    series: [{
                        type: "column",
                        field: "value",
                        categoryField: "label",
                        color: "#16a34a",
                        border: { width: 0, radius: 8 },
                        gap: 0.5
                    }],
                    categoryAxis: {
                        labels: { font: "11px Inter, monospace", color: "#64748b" },
                        majorGridLines: { visible: false },
                        line: { visible: false }
                    },
                    valueAxis: {
                        labels: { font: "11px Inter, monospace", color: "#64748b" },
                        majorGridLines: { color: "rgba(22,163,74,.1)", dashType: "dash" },
                        line: { visible: false }
                    },
                    tooltip: {
                        visible: true,
                        background: "#060a07",
                        color: "#ffffff",
                        font: "12.5px Inter, monospace",
                        template: "#= value # orders",
                        border: { width: 0 },
                        padding: { top: 8, bottom: 8, left: 14, right: 14 }
                    },
                    chartArea: { height: 260, background: "transparent" },
                    plotArea: { margin: { top: 15, bottom: 0, left: 0, right: 0 } },
                    legend: { visible: false },
                    transitions: true
                });
            }
        });
    }

    window.changeOrderPeriod = function(p, btn) {
        dashboardState.orderPeriod = p;
        dashboardState.orderChartInit = false;
        $("#orderSwitcher .btn-pd").removeClass("active");
        $(btn).addClass("active");
        $("#orderVolumeChart").empty();
        loadOrderChart(Date.now());
    };

    // --- CATALOG GRID ---
    function loadCatalogGrid(ts) {
        if (!dashboardState.catalogGridInit) {
            if (typeof window.showGridSkeleton === 'function') {
                window.showGridSkeleton('#activeCatalogGrid', 6);
            }
        }
        return new Promise((resolve) => {
            setTimeout(() => {
                $.getJSON(`${API_URLS.catalog}?_=${ts}`, res => {
                    if (!res || res.success === false || !Array.isArray(res.data)) {
                        const msg = (res && res.message) ? res.message : "Catalog data is unavailable.";
                        $("#activeCatalogGrid").html(
                            `<div style='padding:16px;color:#991b1b;font-weight:600;'>${msg}</div>`
                        );
                        resolve();
                        return;
                    }
                    
                    if (dashboardState.catalogGridInit) {
                        const grid = $("#activeCatalogGrid").data("kendoGrid");
                        if (grid) { grid.dataSource.data(res.data); }
                        resolve();
                        return;
                    }
                    
                    dashboardState.catalogGridInit = true;
                    $("#activeCatalogGrid").empty();

                    $("#activeCatalogGrid").kendoGrid({
                        dataSource: { 
                            data: res.data, 
                            pageSize: 5,
                            schema: {
                                model: {
                                    fields: {
                                        createdAt: { type: "date" },
                                        askingPrice: { type: "number" },
                                        quantityAvailable: { type: "number" }
                                    }
                                }
                            }
                        },
                        scrollable: false, 
                        sortable: true,
                        pageable: { buttonCount: 3, info: false, input: false, previousNext: true },
                        columns: [
                            { 
                                title: "Product", 
                                width: "22%",
                                template: "<div style='display:flex; align-items:center;'><div style='width:36px; height:36px; border-radius:8px; background:\\#EAF5EC; color:\\#1B7A3E; display:flex; align-items:center; justify-content:center; font-size:18px; margin-right:12px;'>🌾</div><div><div style='font-weight:800; color:\\#14532D;'>#: productName #</div><div style='font-size:11px; color:\\#6B7280; font-weight:600;'>Grade #: grade #</div></div></div>"
                            },
                            { 
                                title: "Storage", 
                                width: "14%",
                                template: "<span style='font-weight:600;'>#: storageTemp #°C</span><br><span style='font-size:11px; color:\\#6B7280;'>#: humidity #% RH</span>"
                            },
                            { 
                                title: "AI Prediction", 
                                width: "20%",
                                template: "<span style='font-weight:800; color:\\#14532D;'>#: predictedDaysLeft # Days Left</span><br><span style='font-size:11px; color:\\#6B7280;'>Stored for #: daysInStorage # days</span>"
                            },
                            { 
                                title: "Status", 
                                width: "12%",
                                template: "# if (status === 'Optimal') { #<span style='background:\\#EAF5EC; color:\\#1B7A3E; padding:4px 10px; border-radius:20px; font-size:11px; font-weight:800; display:inline-block;'>#: status #</span># } else if (status === 'Low Stock' || status === 'Discount Now') { #<span style='background:\\#FEF2F2; color:\\#991B1B; padding:4px 10px; border-radius:20px; font-size:11px; font-weight:800; display:inline-block;'>#: status #</span># } else { #<span style='background:\\#F3F4F6; color:\\#4B5563; padding:4px 10px; border-radius:20px; font-size:11px; font-weight:800; display:inline-block;'>#: status #</span># } #"
                            },
                            { 
                                title: "Listed", 
                                width: "12%",
                                template: "<span style='color:\\#6B7280; font-weight:500;'>#= kendo.toString(kendo.parseDate(createdAt), 'dd MMM yy') #</span>"
                            },
                            {
                                title: "Action",
                                width: "20%",
                                template: "# if (status === 'Discount Now' || status === 'Low Stock') { #<button type='button' style='background:\\#991B1B; color:white; padding:6px 16px; border-radius:6px; border:none; font-weight:bold; font-size: 11px; cursor:pointer; min-width: 110px; transition: 0.2s;' onclick='applyDiscount(event, \"#: productName #\")'>Apply Discount</button># } else { #<button type='button' style='background:\\#F3F4F6; color:\\#4B5563; padding:6px 16px; border-radius:6px; border:1px solid \\#E5E7EB; font-weight:bold; font-size: 11px; cursor:pointer; min-width: 110px; transition: 0.2s;' onclick='manageCrop(event, \"#: productName #\")'>Manage</button># } #"
                            }
                        ]
                    });
                    
                    resolve();
                });

            }, 1500); 
        });
    }

    window.applyDiscount = function(e, cropName) {
        if (e) e.preventDefault();
        if (typeof Swal === 'undefined') return;

        Swal.fire({
            title: 'Apply Discount?',
            text: `Initiate automated discount protocol for ${cropName}? This will instantly notify all active vendors.`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#991B1B', 
            cancelButtonColor: '#6B7280',
            confirmButtonText: 'Yes, apply discount!'
        }).then((result) => {
            if (result.isConfirmed) {
                const grid = $("#activeCatalogGrid").data("kendoGrid");
                if (grid) {
                    const data = grid.dataSource.data();
                    const item = data.find(x => x.productName === cropName);
                    if (item) {
                        item.set("status", "Optimal");
                    }
                }
                
                $.post('/Admin/ApplyDiscount', { cropName: cropName });

                Swal.fire({ title: 'Discount Applied!', text: `Vendors have been notified about the ${cropName} discount.`, icon: 'success', confirmButtonColor: '#1B7A3E' });
            }
        });
    };

    window.manageCrop = function(e, cropName) {
        if (e) e.preventDefault();
        window.location.href = '/Admin/Catalog';
    };

    // --- SEARCH LOGIC ---
    function performGlobalSearch() {
        var query = $('#globalSearchInput').val().trim();
        if (query.length < 2) {
            $('#globalSearchResults').hide();
            return;
        }
        
        window.currentQuery = query;
        $('#globalSearchResults').show();
        if (typeof window.showGridSkeleton === 'function') {
            window.showGridSkeleton('#searchResultsList', 3);
        }
        
        $.ajax({
            url: '/Admin/UniversalSearch',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ query: query, page: 1, pageSize: 30 }),
            success: function(response) {
                if (response.success && response.data && response.data.length > 0) {
                    var html = '';
                    $.each(response.data, function(i, item) {
                        var badgeClass = '';
                        var badgeIcon = '';
                        switch(item.type) {
                            case 'catalog': badgeClass = 'catalog'; badgeIcon = '📦'; break;
                            case 'warehouse': badgeClass = 'warehouse'; badgeIcon = '🏭'; break;
                            case 'user': badgeClass = 'user'; badgeIcon = '👤'; break;
                            case 'order': badgeClass = 'order'; badgeIcon = '📋'; break;
                            default: badgeClass = 'catalog'; badgeIcon = '📦';
                        }
                        
                        html += '<a href="' + item.url + '" class="result-item">';
                        html += '<div class="result-badge ' + badgeClass + '">' + badgeIcon + '</div>';
                        html += '<div class="result-content">';
                        html += '<div class="result-title">' + escapeHtml(item.title) + '</div>';
                        if (item.subtitle) {
                            html += '<div class="result-subtitle">' + escapeHtml(item.subtitle) + '</div>';
                        }
                        html += '</div>';
                        html += '<span class="result-status ' + (item.status === 'Active' ? 'active' : 'inactive') + '">' + (item.status || 'Active') + '</span>';
                        html += '</a>';
                    });
                    html += '<div style="padding: 12px 20px; background: #f8fafc; text-align: center; font-size: 12px; color: #64748b;">';
                    html += 'Found ' + response.data.length + ' results for "' + escapeHtml(window.currentQuery) + '"';
                    html += '</div>';
                    $('#searchResultsList').html(html);
                } else {
                    $('#searchResultsList').html('<div class="loading-state"><i class="fas fa-search fa-2x" style="color: #cbd5e1;"></i><p style="margin-top: 12px;">No results found for "' + escapeHtml(window.currentQuery) + '"</p></div>');
                }
            },
            error: function() {
                $('#searchResultsList').html('<div class="loading-state"><i class="fas fa-exclamation-triangle fa-2x" style="color: #ef4444;"></i><p style="margin-top: 12px;">Search failed. Please try again.</p></div>');
            }
        });
    }

    function escapeHtml(str) {
        if (!str) return '';
        return str.replace(/[&<>]/g, function(m) {
            if (m === '&') return '&amp;';
            if (m === '<') return '&lt;';
            if (m === '>') return '&gt;';
            return m;
        });
    }

})(jQuery);
