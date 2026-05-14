/**
 * Vendor Dashboard Logic
 * Handles KPI fetching, Chart.js initialization, and activity tracking.
 */

(function () {
    const API_URL = window.API_BASE_URL;
    let ordersChart, categoryChart;

    function showDashboardSkeletons() {
        const container = document.getElementById('dashboardContent');
        if (!container) return;
        container.innerHTML = `
            <div class="stats-grid">
                ${[1, 2, 3, 4].map(() => `
                    <div class="stat-card glass-morphism skeleton-active">
                        <div class="skeleton-icon"></div>
                        <div class="stat-info w-100">
                            <div class="skeleton-line w-40 mb-2 h-20"></div>
                            <div class="skeleton-line w-80 h-30"></div>
                        </div>
                    </div>
                `).join('')}
            </div>
            <div class="charts-row">
                <div class="chart-card glass-morphism skeleton-active">
                    <div class="skeleton-line w-30 mb-4 h-20"></div>
                    <div class="skeleton-chart"></div>
                </div>
                <div class="chart-card glass-morphism skeleton-active">
                    <div class="skeleton-line w-30 mb-4 h-20"></div>
                    <div class="skeleton-chart-round"></div>
                </div>
            </div>
            <div class="recent-card glass-morphism skeleton-active">
                <div class="skeleton-line w-20 mb-4 h-30"></div>
                <div class="skeleton-table"></div>
            </div>
        `;
    }

    async function loadDashboard() {
        showDashboardSkeletons();
        try {
            // Fetch all dashboard data in parallel
            const [kpiRes, trendsRes, categoryRes, recentRes] = await Promise.all([
                authorizedFetch(`${API_URL}/dashboard/kpi`),
                authorizedFetch(`${API_URL}/dashboard/monthly-trends`),
                authorizedFetch(`${API_URL}/dashboard/category-spending`),
                authorizedFetch(`${API_URL}/user/recent-orders`)
            ]);

            if (!kpiRes || !trendsRes || !categoryRes || !recentRes) {
                console.warn("One or more dashboard requests failed (likely 401)");
                return;
            }

            const [kpiData, trendsData, categoryData, recentData] = await Promise.all([
                kpiRes.json(),
                trendsRes.json(),
                categoryRes.json(),
                recentRes.json()
            ]);

            console.log("Dashboard Data Loaded:", { kpiData, trendsData, categoryData, recentData });

            // Map API response to UI state with correct property names (camelCase from ASP.NET Core)
            const dashboardState = {
                totalOrders: kpiData.data?.totalOrders || 0,
                totalSpent: kpiData.data?.totalSpent || 0,
                totalQuantity: kpiData.data?.totalQuantity || 0,
                pendingOrders: kpiData.data?.pendingOrders || 0,
                
                monthlyLabels: trendsData.data?.monthlyData?.map(d => d.month) || [],
                monthlySalesData: trendsData.data?.monthlyData?.map(d => d.amount) || [],
                
                categoryLabels: categoryData.data?.map(c => c.category) || [],
                categoryData: categoryData.data?.map(c => c.totalSpent) || [],
                categoryOrderCounts: categoryData.data?.map(c => c.orderCount) || [],
                
                recentOrders: recentData.data || []
            };

            setTimeout(() => displayDashboard(dashboardState), 600);
        } catch (error) {
            console.error('Dashboard fatal error:', error);
            const container = document.getElementById('dashboardContent');
            if (container) {
                container.innerHTML = `
                    <div class="glass-morphism p-5 text-center reveal visible" style="border: 1px solid rgba(220, 53, 69, 0.2);">
                        <div class="error-icon-wrap mb-4" style="width: 80px; height: 80px; background: #fff5f5; border-radius: 50%; display: flex; align-items:center; justify-content:center; margin: 0 auto;">
                             <i class="fi fi-rr-cloud-error text-danger" style="font-size: 40px;"></i>
                        </div>
                        <h3 class="mb-2" style="font-weight: 800;">Data Connection Failed</h3>
                        <p class="text-muted mb-4">We're having trouble fetching your business data. This could be due to a server issue or an expired session.</p>
                        <div class="d-flex justify-content-center gap-3">
                            <button class="btn btn-primary px-4 py-2" onclick="location.reload()" style="border-radius: 12px; background: var(--emerald-primary); border:none; font-weight: 700;">
                                <i class="fi fi-rr-refresh me-2"></i>Retry Sync
                            </button>
                            <button class="btn btn-outline-secondary px-4 py-2" onclick="window.location.href='/Vendor/Login'" style="border-radius: 12px; font-weight: 700;">
                                <i class="fi fi-rr-sign-in-alt me-2"></i>Re-login
                            </button>
                        </div>
                    </div>
                `;
            }
        }
    }

    function displayDashboard(data) {
        const container = document.getElementById('dashboardContent');
        if (!container) return;

        container.innerHTML = `
            <div class="stats-grid">
                <div class="stat-card glass-morphism reveal">
                    <div class="stat-icon-wrap emerald">
                        <i class="fi fi-rr-shopping-bag"></i>
                    </div>
                    <div class="stat-info">
                        <span class="stat-label" data-i18n="db_total_orders">Total Orders</span>
                        <h2 class="stat-value">${data.totalOrders}</h2>
                    </div>
                </div>

                <div class="stat-card glass-morphism reveal" style="transition-delay: 0.05s">
                    <div class="stat-icon-wrap gold">
                        <i class="fi fi-rr-coins"></i>
                    </div>
                    <div class="stat-info">
                        <span class="stat-label" data-i18n="db_total_spent">Total Spent</span>
                        <h2 class="stat-value">&#8377;${(data.totalSpent || 0).toLocaleString('en-IN', { maximumFractionDigits: 0 })}</h2>
                    </div>
                </div>

                <div class="stat-card glass-morphism reveal" style="transition-delay: 0.1s">
                    <div class="stat-icon-wrap blue">
                        <i class="fi fi-rr-box-open"></i>
                    </div>
                    <div class="stat-info">
                        <span class="stat-label" data-i18n="db_total_volume">Total Volume</span>
                        <h2 class="stat-value">${(data.totalQuantity || 0).toLocaleString('en-IN')} <small style="font-size: 13px; opacity: 0.7;">kg</small></h2>
                    </div>
                </div>

                <div class="stat-card glass-morphism reveal" style="transition-delay: 0.15s">
                    <div class="stat-icon-wrap orange">
                        <i class="fi fi-rr-time-fast"></i>
                    </div>
                    <div class="stat-info">
                        <span class="stat-label" data-i18n="db_pending">Pending</span>
                        <h2 class="stat-value">${data.pendingOrders}</h2>
                    </div>
                </div>
            </div>

            <div class="charts-row">
                <div class="chart-card glass-morphism reveal" style="transition-delay: 0.2s">
                    <div class="chart-header">
                        <h3 class="chart-title" data-i18n="db_purchase_activity">Purchase Activity</h3>
                        <span class="chart-period" data-i18n="db_current_year">Current Year</span>
                    </div>
                    <div class="chart-container">
                        <canvas id="ordersChart"></canvas>
                    </div>
                </div>
                <div class="chart-card glass-morphism reveal" style="transition-delay: 0.25s">
                    <div class="chart-header">
                        <h3 class="chart-title" data-i18n="db_category_spend">Category Spend</h3>
                    </div>
                    <div class="chart-container">
                        <canvas id="categoryChart"></canvas>
                    </div>
                </div>
            </div>

            <div class="recent-card glass-morphism reveal" style="transition-delay: 0.3s">
                <div class="recent-header">
                    <h3 class="section-title"><i class="fi fi-rr-receipt" style="color: var(--emerald-primary)"></i> <span data-i18n="db_recent_activity">Recent Activity</span></h3>
                    <a href="/Vendor/Orders" class="btn-link-premium"><span data-i18n="db_view_history">View Full History</span> <i class="fi fi-rr-arrow-right"></i></a>
                </div>
                <div class="premium-table-wrapper">
                    <table class="premium-table">
                        <thead>
                            <tr>
                                <th data-i18n="db_table_order">ORDER NUMBER</th>
                                <th data-i18n="db_table_date">DATE</th>
                                <th data-i18n="db_table_amount">TOTAL AMOUNT</th>
                                <th data-i18n="db_table_status">STATUS</th>
                                <th class="text-right" data-i18n="db_table_action">ACTION</th>
                            </tr>
                        </thead>
                        <tbody>
                            ${data.recentOrders.length > 0 ? 
                                data.recentOrders.map(order => {
                                    const status = (order.status || '').toLowerCase();
                                    const statusClass = (status === 'delivered' || status === 'completed') ? 'status-delivered' :
                                        (status === 'pending' || status === 'placed' || status === 'orderplaced') ? 'status-pending' : 
                                        (status === 'cancelled') ? 'status-cancelled' : 'status-processing';
                                    
                                    // Format Status Text (Space out CamelCase)
                                    let statusText = order.statusText || order.status;
                                    if (statusText === 'OrderPlaced') statusText = 'Order Placed';
                                    
                                    const date = order.placedAt ? new Date(order.placedAt).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }) : 'N/A';
                                    return `
                                        <tr>
                                            <td><span class="order-id-pill">${order.orderNumber}</span></td>
                                            <td><span class="date-text">${date}</span></td>
                                            <td><strong style="color: var(--emerald-primary); font-size: 15px;">&#8377;${(order.grandTotal || 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</strong></td>
                                            <td><span class="order-status-badge ${statusClass}">${statusText}</span></td>
                                            <td class="text-right">
                                                <button class="btn-icon-track" onclick="location.href='/Vendor/Orders'" title="View Details">
                                                    <i class="fi fi-rr-arrow-right"></i>
                                                </button>
                                            </td>
                                        </tr>
                                    `;
                                }).join('') : 
                                '<tr><td colspan="5" class="text-center py-5"><div class="text-muted"><i class="fi fi-rr-inbox mb-2 d-block" style="font-size: 32px"></i>No activity recorded yet</div></td></tr>'
                            }
                        </tbody>
                    </table>
                </div>
            </div>
        `;
        if (window.fbInit) fbInit();
        
        setTimeout(() => {
            $(".reveal").addClass("visible");
            initCharts(data);
        }, 50);
    }

    function initCharts(data) {
        const ctxOrders = document.getElementById('ordersChart')?.getContext('2d');
        if (!ctxOrders) return;

        const emeraldGradient = ctxOrders.createLinearGradient(0, 0, 0, 300);
        emeraldGradient.addColorStop(0, '#15803d');
        emeraldGradient.addColorStop(1, '#166534');

        const hasTrendData = data.monthlySalesData.length > 0 && data.monthlySalesData.some(v => v > 0);

        new Chart(ctxOrders, {
            type: 'bar',
            data: {
                labels: hasTrendData ? data.monthlyLabels : ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
                datasets: [{
                    label: (window.fbT ? fbT('nav_orders') : 'Orders'),
                    data: hasTrendData ? data.monthlySalesData : [0, 0, 0, 0, 0, 0],
                    backgroundColor: hasTrendData ? emeraldGradient : '#f1f5f9',
                    borderRadius: 6,
                    barThickness: 35,
                    maxBarThickness: 50
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { 
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#1e293b',
                        padding: 12,
                        cornerRadius: 8,
                        displayColors: false,
                        callbacks: {
                            label: (context) => ` ₹${context.parsed.y.toLocaleString('en-IN')}`
                        }
                    }
                },
                scales: {
                    y: { 
                        beginAtZero: true, 
                        grid: { color: 'rgba(0,0,0,0.03)', drawBorder: false },
                        ticks: { font: { family: "'Plus Jakarta Sans'", size: 10 }, color: '#94a3b8' }
                    },
                    x: { 
                        grid: { display: false },
                        ticks: { font: { family: "'Plus Jakarta Sans'", size: 10 }, color: '#94a3b8' }
                    }
                }
            }
        });

        const ctxCategory = document.getElementById('categoryChart')?.getContext('2d');
        if (!ctxCategory) return;

        const hasCategoryData = data.categoryData.length > 0 && data.categoryData.some(v => v > 0);
        const totalSum = data.categoryData.reduce((a, b) => a + b, 0);
        const chartColors = ['#15803d', '#d4af37', '#3b82f6', '#f59e0b', '#8b5cf6', '#ec4899', '#06b6d4'];

        new Chart(ctxCategory, {
            type: 'doughnut',
            data: {
                labels: hasCategoryData ? data.categoryLabels : [(window.fbT ? fbT('db_no_data') : 'No Data')],
                datasets: [{
                    data: hasCategoryData ? data.categoryData : [1],
                    backgroundColor: hasCategoryData ? chartColors : ['#f1f5f9'],
                    borderColor: 'white',
                    borderWidth: 3,
                    hoverOffset: 15
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '75%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { 
                            usePointStyle: true,
                            pointStyle: 'circle',
                            padding: 15,
                            font: { family: "'Plus Jakarta Sans'", size: 11, weight: '600' },
                            color: '#64748b'
                        }
                    },
                    tooltip: {
                        backgroundColor: '#1e293b',
                        padding: 12,
                        cornerRadius: 8,
                        callbacks: {
                            label: (context) => {
                                const val = context.parsed;
                                const percent = ((val / totalSum) * 100).toFixed(1);
                                const count = data.categoryOrderCounts[context.dataIndex] || 0;
                                return ` ₹${val.toLocaleString('en-IN')} (${percent}%) - ${count} orders`;
                            }
                        }
                    }
                }
            },
            plugins: [{
                id: 'centerText',
                beforeDraw: (chart) => {
                    if (!hasCategoryData) return;
                    const { ctx, width, height } = chart;
                    ctx.save();
                    ctx.font = 'bold 16px "Plus Jakarta Sans"';
                    ctx.fillStyle = '#1e293b';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(`₹${totalSum.toLocaleString('en-IN', { maximumFractionDigits: 0 })}`, width / 2, height / 2 - 5);
                    ctx.font = '500 11px "Plus Jakarta Sans"';
                    ctx.fillStyle = '#64748b';
                    ctx.fillText('Total Spent', width / 2, height / 2 + 15);
                    ctx.restore();
                }
            }]
        });
    }

    $(document).ready(() => {
        setTimeout(() => {
            $(".reveal").addClass("visible");
        }, 100);
        loadDashboard();
    });
})();
