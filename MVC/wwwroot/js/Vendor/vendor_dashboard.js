// ============ Cart Functions ============
let vendorCart = JSON.parse(localStorage.getItem('vendor_cart') || '[]');

function saveCart() { localStorage.setItem('vendor_cart', JSON.stringify(vendorCart)); }
function loadCart() { vendorCart = JSON.parse(localStorage.getItem('vendor_cart') || '[]'); }

window.addToCart = function(id, name, price, imageIcon) {
    loadCart();
    let existing = vendorCart.find(item => item.id == id);
    if (existing) {
        existing.quantity++;
        existing.total = existing.quantity * existing.price;
    } else {
        vendorCart.push({ id: id, name: name, price: price, quantity: 1, total: price, image: imageIcon || 'fa-seedling' });
    }
    saveCart();
    renderCart();
    showToast(`${name} added to cart!`, 'success');
};

window.removeFromCart = function(id) {
    loadCart();
    vendorCart = vendorCart.filter(item => item.id != id);
    saveCart();
    renderCart();
    showToast('Item removed from cart', 'info');
};

window.updateCartQuantity = function(id, change) {
    loadCart();
    let item = vendorCart.find(i => i.id == id);
    if (item) {
        item.quantity += change;
        if (item.quantity <= 0) {
            vendorCart = vendorCart.filter(i => i.id != id);
        } else {
            item.total = item.quantity * item.price;
        }
        saveCart();
        renderCart();
    }
};

function renderCart() {
    loadCart();
    let count = vendorCart.reduce((sum, item) => sum + item.quantity, 0);
    let total = vendorCart.reduce((sum, item) => sum + item.total, 0);
    $('.cart-count').text(count);

    if (vendorCart.length === 0) {
        $('#cartItemsList').html('<p class="text-center text-muted py-5">Cart is empty</p>');
        $('#cartFooter').hide();
    } else {
        let html = '';
        vendorCart.forEach(item => {
            let stockWarning = '';
            if (item.quantity > (item.availableStock || 0)) {
                stockWarning = `<small class="text-danger">⚠️ Only ${item.availableStock || 0}kg available!</small>`;
            }
            
            html += `<div class="cart-item">
                <div class="cart-item-info">
                    <h6>${item.name}</h6>
                    <small>Qty: ${item.quantity} × ₹${item.price.toLocaleString('en-IN')}</small>
                    ${stockWarning}
                </div>
                <div class="cart-item-price">₹${item.total.toLocaleString('en-IN')}</div>
                <div>
                    <button class="btn btn-sm btn-outline-secondary" onclick="updateCartQuantity(${item.id}, -1)">-</button>
                    <button class="btn btn-sm btn-outline-secondary" onclick="updateCartQuantity(${item.id}, 1)">+</button>
                    <button class="btn btn-sm btn-link text-danger" onclick="removeFromCart(${item.id})"><i class="fas fa-trash"></i></button>
                </div>
            </div>`;
        });
        $('#cartItemsList').html(html);
        $('#cartTotal').text('₹' + total.toLocaleString('en-IN'));
        $('#cartFooter').show();
    }
}

function toggleCart() { $('#cartSidebar').toggleClass('open'); }
function proceedToCheckout() { if(vendorCart.length > 0) window.location.href = '/Vendor/Checkout'; else showToast('Cart is empty!', 'warning'); }

// ============ Wishlist Functions ============
let wishlist = JSON.parse(localStorage.getItem('vendor_wishlist') || '[]');
function saveWishlist() { localStorage.setItem('vendor_wishlist', JSON.stringify(wishlist)); }

window.toggleWishlist = function(id, name, price, imageIcon) {
    let existing = wishlist.find(item => item.id == id);
    if (existing) {
        wishlist = wishlist.filter(item => item.id != id);
        showToast('Removed from wishlist', 'info');
    } else {
        wishlist.push({ id: id, name: name, price: price, image: imageIcon || 'fa-seedling' });
        showToast('Added to wishlist', 'success');
    }
    saveWishlist();
    if (window.location.pathname.includes('Wishlist')) loadWishlistPage();
};

// ============ Dashboard Functions ============
async function loadDashboardStats() {
    try {
        const response = await fetch(`${API_BASE_URL}/user/stats`);
        const result = await response.json();
        
        if (result.success && result.data) {
            const stats = result.data;
            
            document.getElementById('totalOrders').innerText = stats.totalOrders || 0;
            document.getElementById('totalSpent').innerText = '₹' + (stats.totalSpent || 0).toLocaleString('en-IN');
            document.getElementById('totalQuantity').innerText = (stats.totalQuantity || 0) + ' kg';
            document.getElementById('pendingOrders').innerText = stats.pendingOrders || 0;
        }
    } catch (error) {
        console.error('Error loading dashboard stats:', error);
    }
}

// 👉 NEW: Example function of how to use the Card Skeleton on the Catalog Page!
window.loadVendorCatalog = async function(containerId) {
    // Show 8 shimmering cards before fetching
    if (typeof window.showCardSkeleton === 'function') {
        window.showCardSkeleton(containerId, 8); 
    }
    
    try {
        // Replace with your actual catalog fetch endpoint
        const response = await fetch(`${API_BASE_URL}/catalog`); 
        const result = await response.json();
        
        // Build your actual HTML and overwrite the skeleton
        let catalogHtml = '';
        // result.data.forEach(...) 
        
        $(containerId).html(catalogHtml);
        
    } catch(err) {
        $(containerId).html('<p>Failed to load catalog.</p>');
    }
}

// ============ Notification Functions ============
let notifications = [
    { id: 1, icon: '💰', title: 'Payment Received', msg: '₹22,000 credited for Wheat order', time: '2 hrs ago', read: false },
    { id: 2, icon: '🔬', title: 'Quality Check Done', msg: 'Cotton batch graded A', time: '5 hrs ago', read: false },
    { id: 3, icon: '📦', title: 'New Order Update', msg: 'Order #ORD002 is In Transit', time: '1 day ago', read: true }
];

function renderNotifications() {
    let unread = notifications.filter(n => !n.read).length;
    $('#notifDot').css('display', unread > 0 ? 'block' : 'none');
    let html = '';
    notifications.forEach(n => {
        html += `<div class="notif-item ${!n.read ? 'unread' : ''}" onclick="markAsRead(${n.id})">
            <div class="notif-dot"></div>
            <div><div class="notif-text"><strong>${n.title}</strong><br>${n.msg}</div><div class="notif-time">${n.time}</div></div>
        </div>`;
    });
    $('#notifList').html(html);
}

function markAsRead(id) { let n = notifications.find(x => x.id == id); if(n && !n.read) { n.read = true; renderNotifications(); } }
function markAllRead() { notifications.forEach(n => n.read = true); renderNotifications(); showToast('All notifications marked as read', 'info'); }
function toggleNotifs(e) { $('#notifDrawer').toggleClass('open'); renderNotifications(); }

// ============ Toast ============
function showToast(msg, type) {
    let icon = type === 'success' ? '✅' : type === 'error' ? '❌' : 'ℹ️';
    let toast = $(`<div class="fb-toast ${type}"><span>${icon}</span><span style="flex:1;">${msg}</span><button onclick="this.parentElement.remove()" style="background:none;border:none;cursor:pointer;">&times;</button></div>`);
    $('#fbToastContainer').append(toast);
    setTimeout(() => { toast.fadeOut(400, function() { $(this).remove(); }); }, 3000);
}
window.showToast = showToast;

function logout() { if(confirm('Logout?')) { localStorage.removeItem('vendor_cart'); window.location.href = '/'; } }

async function updateWishlistCount() {
    try {
        const response = await fetch(`${API_BASE_URL}/wishlist`);
        const result = await response.json();
        if (result.success && result.data) {
            const count = result.data.length;
            const wishlistCountSpan = document.getElementById('wishlistCount');
            if (wishlistCountSpan) wishlistCountSpan.innerText = count;
        }
    } catch (error) {
        console.error('Error updating wishlist count:', error);
    }
}

$(document).ready(function() {
    let vendorNav = [
        { icon: 'fa-tachometer-alt', text: 'Dashboard', url: '/Vendor/Dashboard' },
        { icon: 'fa-store', text: 'Catalog', url: '/Vendor/Catalog' },
        { icon: 'fa-shopping-cart', text: 'Cart / Checkout', url: '/Vendor/Checkout' },
        { icon: 'fa-heart', text: 'Wishlist', url: '/Vendor/Wishlist' },
        { icon: 'fa-truck', text: 'My Orders', url: '/Vendor/Orders' },
        { icon: 'fa-credit-card', text: 'Payments', url: '/Vendor/Payments' },
        { icon: 'fa-user', text: 'Profile', url: '/Vendor/Profile' }
    ];
    let currentPath = window.location.pathname.toLowerCase();
    let activeItem = vendorNav.find(n => currentPath.includes(n.url.toLowerCase()));
    
    let drawerHtml = `<div style="width:260px;height:100%;display:flex;flex-direction:column;">
        <div class="sidebar-header"><div class="sidebar-logo"><div class="sidebar-logo-icon">🌾</div><span>FarmBridge</span></div></div>
        <div class="sidebar-user"><div class="sidebar-avatar">V</div><div><div style="font-weight:600;color:#fff;">Vendor</div><div style="color:var(--green-pale);font-size:12px;">Vendor Account</div></div></div>
        <div class="nav-section-title">MENU</div><ul style="flex:1;padding:0;list-style:none;">`;
    vendorNav.forEach(item => {
        let isActive = currentPath.includes(item.url.toLowerCase());
        drawerHtml += `<li data-role="drawer-item" data-url="${item.url}" class="k-item ${isActive ? 'k-state-selected' : ''}">
            <span class="k-item-text"><i class="fa ${item.icon}"></i><span class="ms-2">${item.text}</span></span></li>`;
    });
    drawerHtml += `</ul><div class="sidebar-footer"><a href="#" class="logout-btn" onclick="logout(); return false;"><i class="fa fa-sign-out-alt"></i><span>Logout</span></a></div></div>`;

    $("#appbar").kendoAppBar({
        items: [
            { template: '<button id="menu-toggle" class="topbar-btn"><lord-icon src="https://cdn.lordicon.com/izqdfqdl.json" trigger="hover" style="width:28px;height:28px;"></lord-icon></button>', type: "contentItem" },
            { template: `<div class="d-none d-sm-flex align-items-center ms-3"><span class="topbar-title" id="page-title">${activeItem ? activeItem.text : 'Dashboard'}</span></div>`, type: "contentItem" },
            { width: 0, type: "spacer" },
            { template: '<button class="topbar-btn me-2 d-none d-sm-flex"><i class="fa fa-search"></i></button>', type: "contentItem" },
            { template: '<button id="notif-toggle-btn" class="topbar-btn me-3" onclick="toggleNotifs(event)"><i class="fa fa-bell"></i><span class="notif-badge" id="notifDot"></span></button>', type: "contentItem" },
            { template: '<div class="topbar-avatar" onclick="window.location.href=\'/Vendor/Profile\'">V</div>', type: "contentItem" }
        ]
    });

    $("#drawer").kendoDrawer({ template: drawerHtml, mode: "overlay", position: "left", minHeight: "calc(100vh - 64px)", swipeToOpen: true,
        show: function() { $("body").addClass("no-scroll"); },
        hide: function() { $("body").removeClass("no-scroll"); },
        itemClick: function(e) {
            let url = e.item.data("url");
            if (url) window.location.href = url;
            if (window.innerWidth <= 768) this.hide();
        }
    });

    $(document).on("click", "#menu-toggle", function() {
        let d = $("#drawer").data("kendoDrawer");
        if (d) d.visible ? d.hide() : d.show();
    });

    renderCart();
    renderNotifications();
    
    if (window.location.pathname.includes('/Vendor/Dashboard')) {
        loadDashboardStats();
    }
});