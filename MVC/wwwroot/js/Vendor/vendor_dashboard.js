// ============ Shared Helpers ============
function getCookie(name) {
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);
    if (parts.length === 2) return parts.pop().split(';').shift();
    return null;
}

async function authorizedFetch(url, options = {}) {
    const token = getCookie("authToken");
    options.headers = {
        ...options.headers,
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
    };

    const response = await fetch(url, options);
    if (response.status === 401) {
        window.location.href = "/Vendor/Login";
        return null;
    }
    return response;
}

// Global exposure
window.getCookie = getCookie;
window.authorizedFetch = authorizedFetch;
window.API_BASE_URL = window.API_BASE_URL || 'http://localhost:5020/api/Vendor';

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
        vendorCart.push({ id: id, name: name, price: price, quantity: 1, total: price, image: imageIcon || 'fi-rr-seedling' });
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
        $('#cartItemsList').html('<div class="text-center text-muted py-5"><i class="fi fi-rr-shopping-cart-add mb-3 d-block" style="font-size: 40px; opacity: 0.3;"></i><p>Your cart is empty</p></div>');
        $('#cartFooter').hide();
    } else {
        let html = '';
        vendorCart.forEach(item => {
            let stockWarning = '';
            if (item.quantity > (item.availableStock || 0)) {
                stockWarning = `<small class="text-danger">⚠️ Only ${item.availableStock || 0}kg available!</small>`;
            }
            
            html += `<div class="cart-item glass-morphism mb-2" style="border-radius: 12px; padding: 12px;">
                <div class="cart-item-info">
                    <h6 style="margin: 0; font-weight: 700;">${item.name}</h6>
                    <small class="text-muted">₹${item.price.toLocaleString('en-IN')} / kg</small>
                    ${stockWarning}
                </div>
                <div class="cart-item-price" style="font-weight: 800; color: var(--emerald-primary);">₹${item.total.toLocaleString('en-IN')}</div>
                <div class="d-flex align-items-center gap-2 mt-2">
                    <div class="quantity-control d-flex align-items-center bg-light rounded-pill px-2">
                        <button class="btn btn-sm p-0" onclick="updateCartQuantity(${item.id}, -1)"><i class="fi fi-rr-minus-small"></i></button>
                        <span class="mx-2 fw-bold" style="min-width: 20px; text-align: center;">${item.quantity}</span>
                        <button class="btn btn-sm p-0" onclick="updateCartQuantity(${item.id}, 1)"><i class="fi fi-rr-plus-small"></i></button>
                    </div>
                    <button class="btn btn-sm text-danger ms-auto" onclick="removeFromCart(${item.id})"><i class="fi fi-rr-trash"></i></button>
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
        wishlist.push({ id: id, name: name, price: price, image: imageIcon || 'fi-rr-seedling' });
        showToast('Added to wishlist', 'success');
    }
    saveWishlist();
    if (window.location.pathname.includes('Wishlist')) loadWishlistPage();
};

// ============ Notification Functions ============
let notifications = [
    { id: 1, icon: 'fi-rr-envelope-dollar', title: 'Payment Received', msg: '₹22,000 credited for Wheat order', time: '2 hrs ago', read: false },
    { id: 2, icon: 'fi-rr-microscope', title: 'Quality Check Done', msg: 'Cotton batch graded A', time: '5 hrs ago', read: false },
    { id: 3, icon: 'fi-rr-box-open', title: 'New Order Update', msg: 'Order #ORD002 is In Transit', time: '1 day ago', read: true }
];

function renderNotifications() {
    let unread = notifications.filter(n => !n.read).length;
    $('#notifDot').css('display', unread > 0 ? 'block' : 'none');
    let html = '';
    notifications.forEach(n => {
        html += `<div class="notif-item ${!n.read ? 'unread' : ''}" onclick="markAsRead(${n.id})">
            <div class="notif-icon-wrap ${!n.read ? 'active' : ''}"><i class="fi ${n.icon}"></i></div>
            <div class="notif-content">
                <div class="notif-text"><strong>${n.title}</strong><br>${n.msg}</div>
                <div class="notif-time">${n.time}</div>
            </div>
        </div>`;
    });
    $('#notifList').html(html);
}

function markAsRead(id) { let n = notifications.find(x => x.id == id); if(n && !n.read) { n.read = true; renderNotifications(); } }
function markAllRead() { notifications.forEach(n => n.read = true); renderNotifications(); showToast('All notifications marked as read', 'info'); }
function toggleNotifs(e) { $('#notifDrawer').toggleClass('open'); renderNotifications(); }

// ============ Premium Toast ============
function showToast(msg, type = 'success') {
    if (window.fbNotify) {
        fbNotify({ message: msg, icon: type, type: 'toast' });
    } else {
        console.log("Toast:", msg);
    }
}
window.showToast = showToast;

function logout() { 
    Swal.fire({
        title: 'Ready to leave?',
        text: "You'll need to login again to access your dashboard.",
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#15803d',
        cancelButtonColor: '#64748b',
        confirmButtonText: 'Yes, Logout',
        borderRadius: '16px'
    }).then((result) => {
        if (result.isConfirmed) {
            localStorage.removeItem('vendor_cart');
            window.location.href = '/';
        }
    });
}

$(document).ready(function() {
    renderCart();
    renderNotifications();
});

// ============ Skeleton Loader Helpers ============
window.showCardSkeleton = function(containerId, cardCount = 4) {
    if (typeof window.FBSkeleton === 'object') {
        FBSkeleton.show(containerId, cardCount, 'product');
    }
};

window.showGridSkeleton = function(containerId, rowCount = 5) {
    if (typeof window.FBSkeleton === 'object') {
        FBSkeleton.show(containerId, rowCount, 'list');
    }
};