/**
 * Vendor Orders Logic
 * Handles order history fetching, filtering, cancellation, and reordering.
 */

(function () {
    const API_URL = 'http://localhost:5020/api/Vendor';
    let notification;
    let allOrdersData = [];

    // --- HELPER FUNCTIONS ---
    function getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    async function loadOrders() {
        showOrderSkeletons();
        const token = getCookie('authToken');
        try {
            const res = await fetch(`${API_URL}/orders`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (res.status === 401) {
                window.location.href = "/Vendor/Login";
                return;
            }

            const result = await res.json();
            allOrdersData = result.data || [];
            displayOrders(allOrdersData);
        } catch (err) {
            $("#ordersContainer").html('<p style="text-align:center;">Failed to load order history.</p>');
        }
    }

    function showOrderSkeletons() {
        if (typeof window.FBSkeleton === 'object') {
            window.FBSkeleton.show("#ordersContainer", 6, 'order');
        }
    }

    function filterOrders() {
        const searchTerm = $("#orderSearch").val().toLowerCase();
        const statusFilter = $("#statusFilter").val().toLowerCase();

        const filtered = allOrdersData.filter(order => {
            const orderIdStr = (order.orderNumber || order.orderId || '').toString().toLowerCase();
            const matchesSearch = orderIdStr.includes(searchTerm);
            const status = (order.status || '').toLowerCase();
            const matchesStatus = statusFilter === 'all' || 
                                 (statusFilter === 'confirmed' && (status === 'placed' || status === 'admin_confirmed')) ||
                                 (statusFilter === 'dispatched' && (status === 'dispatched' || status === 'in_transit')) ||
                                 status === statusFilter;
            return matchesSearch && matchesStatus;
        });
        displayOrders(filtered);
    }

    function displayOrders(orders) {
        const container = $("#ordersContainer");
        if (!container.length) return;
        try {
            if (!orders || !Array.isArray(orders) || orders.length === 0) {
                container.html(`
                    <div class="orders-empty-state glass-morphism">
                        <div class="empty-icon-wrap">
                            <i class="fi fi-rr-search-alt"></i>
                        </div>
                        <h3 data-i18n="ord_empty_title">No matching orders found</h3>
                        <p class="text-muted" data-i18n="ord_empty_desc">Try adjusting your filters or search term to find what you're looking for.</p>
                        <a href="/Vendor/Catalog" class="btn-browse-catalog">
                            <i class="fi fi-rr-shop"></i> <span data-i18n="nav_crop_catalog">Browse Catalog</span>
                        </a>
                    </div>
                `);
                return;
            }

            let html = '';
            orders.forEach((order, index) => {
                try {
                    const status = (order.status || '').toLowerCase();
                    let statusClass = (status === 'delivered') ? 'status-delivered' :
                        (status === 'dispatched' || status === 'in_transit') ? 'status-transit' :
                            (status === 'cancelled') ? 'status-cancelled' : 
                            (status === 'placed' || status === 'admin_confirmed') ? 'status-confirmed' : 'status-placed';

                    const canCancel = (status === 'placed' || status === 'admin_confirmed');
                    
                    let orderDateStr = 'N/A';
                    const rawDate = order.placedAt || order.PlacedAt || order.orderDate;
                    if (rawDate) {
                        const d = new Date(rawDate);
                        if (!isNaN(d.getTime())) {
                            orderDateStr = d.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
                        }
                    }

                    let rawTotal = order.grandTotal;
                    if (typeof rawTotal === 'string') {
                        rawTotal = parseFloat(rawTotal.replace(/,/g, ''));
                    }
                    const totalNum = Number(rawTotal) || 0;

                    const formattedTotal = new Intl.NumberFormat('en-IN', {
                        style: 'currency',
                        currency: 'INR',
                        minimumFractionDigits: 2,
                        maximumFractionDigits: 2
                    }).format(totalNum);
                    
                    html += `
                        <div class="order-card-premium glass-morphism animate-in" style="animation-delay: ${index * 0.05}s">
                            <div class="order-card-header">
                                <div class="order-id-badge">
                                    <span class="label" data-i18n="ord_ref">PURCHASE REF</span>
                                    <span class="id">#${order.orderNumber || order.orderId}</span>
                                </div>
                                <span class="order-status-badge ${statusClass}">
                                    <span class="status-dot"></span>
                                    ${(window.fbT ? window.fbT(order.statusText || order.status) : (order.statusText || order.status))}
                                </span>
                            </div>
                            
                            <div class="order-card-body">
                                <div class="order-meta-row">
                                    <div class="meta-item">
                                        <div class="meta-icon"><i class="fi fi-rr-calendar"></i></div>
                                        <div class="meta-content">
                                            <small data-i18n="ord_date">Order Date</small>
                                            <strong>${orderDateStr}</strong>
                                        </div>
                                    </div>
                                    <div class="meta-item">
                                        <div class="meta-icon"><i class="fi fi-rr-box-open"></i></div>
                                        <div class="meta-content">
                                            <small data-i18n="ord_pkg">Package</small>
                                            <strong>${order.itemCount || 0} Items</strong>
                                        </div>
                                    </div>
                                </div>
                                
                                <div class="order-price-card">
                                    <div class="price-details">
                                        <span class="price-label" data-i18n="ord_total">Grand Total</span>
                                        <span class="price-value">${formattedTotal}</span>
                                    </div>
                                </div>
                            </div>

                            <div class="order-card-footer">
                                ${status !== 'cancelled' ? `
                                    <button class="btn-track-order" onclick="location.href='/Vendor/OrderTracking?id=${order.orderId}'">
                                        <span data-i18n="ord_track">Track Your Order</span>
                                        <i class="fi fi-rr-arrow-right"></i>
                                    </button>
                                ` : `
                                    <button class="btn-view-details" onclick="location.href='/Vendor/OrderTracking?id=${order.orderId}'">
                                        <span data-i18n="ord_details">View Order Details</span>
                                        <i class="fi fi-rr-eye"></i>
                                    </button>
                                `}
                                
                                <button class="btn-reorder" onclick="window.repeatOrder('${order.orderId}')" title="Reorder Items">
                                    <i class="fi fi-rr-rotate-right"></i>
                                    <span>Reorder</span>
                                </button>
                                
                                ${canCancel ? `
                                <button class="btn-cancel-order" onclick="window.openCancelModal('${order.orderId}')" title="Cancel Order">
                                    <i class="fi fi-rr-cross-small notranslate"></i>
                                </button>` : ''}
                            </div>
                        </div>
                    `;
                } catch (innerErr) {
                    console.error("Error processing single order:", innerErr, order);
                }
            });
            container.html(html);
            if (window.fbInit) window.fbInit();
        } catch (err) {
            console.error("Critical display error:", err);
            container.html('<div class="p-4 text-center"><h4>Oops! Something went wrong</h4><p>We encountered an error while displaying your orders. Please try refreshing the page.</p></div>');
        }
    }

    let cancelOrderId = null;
    window.openCancelModal = function (id) { 
        cancelOrderId = id; 
        $("#cancelModal").addClass("active");
    };

    window.closeCancelModal = function () { 
        $("#cancelModal").removeClass("active");
        $("#cancelReason").val('');
    };

    window.confirmCancel = async function () {
        const reason = $("#cancelReason").val() || "Cancelled by vendor";
        const token = getCookie('authToken');
        try {
            const res = await fetch(`${API_URL}/orders/cancel`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                body: JSON.stringify({ orderId: cancelOrderId, reason: reason })
            });
            const result = await res.json();
            if (window.fbNotify) {
                window.fbNotify({ 
                    message: result.message, 
                    icon: result.success ? "success" : "error", 
                    type: 'toast' 
                });
            }
            window.closeCancelModal();
            loadOrders();
        } catch (e) {
            if (window.fbNotify) window.fbNotify({ message: "System Error", icon: "error", type: 'toast' });
        }
    };

    window.repeatOrder = async function (orderId) {
        if (typeof Swal === 'undefined') {
            console.error("SweetAlert2 not found");
            return;
        }
        const confirmed = await Swal.fire({
            title: "Are you sure you want to reorder these items?",
            text: "A new order will be placed instantly.",
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#10b981',
            cancelButtonColor: '#d33',
            confirmButtonText: 'Yes, Reorder'
        }).then(r => r.isConfirmed);
        if (!confirmed) return;
        
        const token = getCookie('authToken');
        try {
            const res = await fetch(`${API_URL}/orders/repeat`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                body: JSON.stringify(orderId.toString())
            });
            const result = await res.json();
            if (notification) {
                notification.show({ message: result.message }, result.success ? "success" : "error");
            } else if (window.fbNotify) {
                window.fbNotify({ message: result.message, icon: result.success ? "success" : "error", type: 'toast' });
            }
            if (result.success) loadOrders();
        } catch (e) {
            if (notification) notification.show({ message: "System Error" }, "error");
        }
    };

    // --- INIT ---
    $(document).ready(function () {
        const popupNotif = $("#popupNotification");
        if (popupNotif.length && typeof popupNotif.kendoNotification === 'function') {
            notification = popupNotif.kendoNotification({
                position: { pinned: true, top: 30, right: 30 },
                autoHideAfter: 3000,
                templates: [
                    { type: "success", template: "<div><i class='fi fi-rr-check me-2'></i> #= message #</div>" },
                    { type: "error", template: "<div><i class='fi fi-rr-exclamation me-2'></i> #= message #</div>" }
                ]
            }).data("kendoNotification");
        }

        setTimeout(() => {
            $(".reveal").addClass("visible");
        }, 100);

        loadOrders();
    });

    window.filterOrders = filterOrders;

})();
