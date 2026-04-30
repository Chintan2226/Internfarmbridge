/* ===================== FarmBridge — Vendor Layout JS ===================== */

let fvNotifs = [];
let currentFvTab = 'all';
const NOTIF_API_BASE = 'http://localhost:5020/api/Notification';

/* ===================== AUTH ===================== */

function getAuthToken() {
    const cookieMatch = document.cookie.match(/authToken=([^;]+)/);
    if (cookieMatch && cookieMatch[1] && cookieMatch[1] !== 'undefined') {
        return cookieMatch[1];
    }
    return localStorage.getItem('authToken') ||
           sessionStorage.getItem('authToken') ||
           null;
}

async function fvAuthFetch(url, options = {}) {
    const token = getAuthToken();

    if (!token) {
        window.location.href = '/Vendor/Login';
        throw new Error("No auth token");
    }

    const response = await fetch(url, {
        ...options,
        headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
            ...(options.headers || {})
        }
    });

    if (response.status === 401) {
        window.location.href = '/Vendor/Login';
        throw new Error("Unauthorized");
    }

    return response;
}

/* ===================== NOTIFICATIONS ===================== */

async function getUnreadCount() {
    try {
        const res = await fvAuthFetch(`${NOTIF_API_BASE}/UnreadCount`);
        const data = await res.json();

        console.log("Unread API:", data);

        if (data?.success) {
            const count = Number(data.count ?? 0);
            updateBellBadge(count);
            return count;
        }

    } catch (err) {
        console.error("Unread count error:", err);
    }

    updateBellBadge(0);
    return 0;
}
function updateBellBadge(count) {
    count = Number(count) || 0;

    const text = document.getElementById('fvUnread');
    if (text) text.textContent = count;

    const badge = document.getElementById('fvUnreadBadge');
    if (badge) badge.textContent = count;

    const bellBadge = document.getElementById('fvBellBadge');
    if (bellBadge) {
        if (count > 0) {
            bellBadge.style.display = 'flex';
            bellBadge.textContent = count > 99 ? '99+' : count;
        } else {
            bellBadge.style.display = 'none';
        }
    }
}

async function loadNotifications() {
    try {
        const response = await fvAuthFetch(`${NOTIF_API_BASE}/GetNotifications`);
        const data = await response.json();
        if (data.success) {
            fvNotifs = data.data || [];
            renderNotifs();
            
            const unreadCount = fvNotifs.filter(n => !n.isRead).length;
            updateBellBadge(unreadCount);
        }
        return data;
    } catch (error) {
        console.error("Error fetching notifications:", error);
    }
}

async function markNotificationAsRead(notificationId) {
    try {
        const response = await fvAuthFetch(`${NOTIF_API_BASE}/MarkAsRead`, {
            method: 'POST',
            body: JSON.stringify(notificationId)
        });
        const data = await response.json();
        if (data.success) {
            const notification = fvNotifs.find(n => n.id == notificationId);
            if (notification) {
                notification.isRead = true;
                renderNotifs();
                
                const unreadCount = fvNotifs.filter(n => !n.isRead).length;
                updateBellBadge(unreadCount);
            }
            showFvToast('Notification marked as read', 'success');
        }
    } catch (error) {
        console.error("Error marking as read:", error);
        showFvToast('Failed to mark as read', 'error');
    }
}

async function deleteSingleNotif(id) {
    if (!confirm(fbT('notif_confirm_delete'))) return;

    try {
        const response = await fvAuthFetch(`${NOTIF_API_BASE}/DeleteNotification`, {
            method: 'POST',
            body: JSON.stringify(id)
        });
        const data = await response.json();
        if (data.success) {
            fvNotifs = fvNotifs.filter(n => n.id != id);
            renderNotifs();
            
            const unreadCount = fvNotifs.filter(n => !n.isRead).length;
            updateBellBadge(unreadCount);
            
            showFvToast('Notification deleted', 'success');
        }
    } catch (error) {
        console.error("Error deleting notification:", error);
        showFvToast('Failed to delete notification', 'error');
    }
}

function toggleDrawer() {
    document.getElementById('fvSidebar')?.classList.toggle('open');
    document.getElementById('fvOverlay')?.classList.toggle('open');
}

function renderNotifs() {
    const list = document.getElementById('fvNotifList');
    if (!list) return;

    const filtered = currentFvTab === 'unread'
        ? fvNotifs.filter(n => !n.isRead)
        : fvNotifs;

    const unreadCount = fvNotifs.filter(n => !n.isRead).length;
    const unreadCountEl = document.getElementById('fvUnreadCountText');
    if (unreadCountEl) unreadCountEl.textContent = unreadCount;

    const unreadBadge = document.getElementById('fvUnreadBadgeNum');
    if (unreadBadge) unreadBadge.textContent = unreadCount;

    if (!filtered.length) {
        list.innerHTML = `<div class="fv-notif-empty">🔔 ${window.fbT ? fbT('notif_no_new') : 'No new notifications'}</div>`;
        return;
    }

    list.innerHTML = filtered.map(n => {
        const timeStr = formatDate(n.createdAt || n.CreatedAt);
        let iconClass = 'fi-rr-bell';
        
        const title = (n.title || '').toLowerCase();
        if (title.includes('order')) iconClass = 'fi-rr-box-alt';
        else if (title.includes('payment') || title.includes('money')) iconClass = 'fi-rr-usd-circle';
        else if (title.includes('cart')) iconClass = 'fi-rr-shopping-cart';
        else if (title.includes('wishlist')) iconClass = 'fi-rr-heart';

        return `
        <div class="fv-notif-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
            <div class="fv-notif-content" onclick="handleNotifClick(${n.id}, '${esc(n.redirectUrl || '#')}')">
                <div class="fv-notif-icon">
                    <i class="fi ${iconClass}" style="font-size:18px;color:var(--fv-primary)"></i>
                </div>
                <div style="flex:1">
                    <div class="notif-title">${esc(n.title)}</div>
                    <div class="notif-message" style="white-space:normal;">${esc(n.message)}</div>
                    <div class="notif-time">${timeStr}</div>
                </div>
            </div>
            <div class="fv-notif-actions-btns">
                ${!n.isRead ? `
                    <button onclick="event.stopPropagation(); markNotificationAsRead(${n.id})" 
                            class="fv-notif-btn" title="Mark as read">
                        ✓
                    </button>
                ` : ''}
                <button onclick="event.stopPropagation(); deleteNotification(${n.id})" 
                        class="fv-notif-btn" title="Delete">
                    ✕
                </button>
            </div>
        </div>
        `;
    }).join('');
}

async function fvMarkAllRead() {
    try {
        const response = await fvAuthFetch(`${NOTIF_API_BASE}/MarkAllRead`, {
            method: 'POST',
            body: JSON.stringify({})
        });

        fvNotifs.forEach(n => n.isRead = true);
        renderNotifs();
        getUnreadCount();

        showFvToast(fbT('notif_marked_read'));

    } catch {
        showFvToast('Update failed', 'error');
    }
}

async function fvClearAll() {
    if (!confirm(fbT('notif_confirm_clear'))) return;

    try {
        const response = await fvAuthFetch(`${NOTIF_API_BASE}/ClearAllNotifications`, {
            method: 'POST'
        });
        const data = await response.json();
        if (data.success) {
            fvNotifs = [];
            renderNotifs();
            updateBellBadge(0);
            showFvToast('All notifications cleared', 'success');
        }
    } catch (error) {
        showFvToast('Failed to clear notifications', 'error');
    }
}



/* ===================== TOAST ===================== */

window.showFvToast = function (message, type = 'success') {
    const container = document.getElementById('fvToasts');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `fv-toast ${type}`;

    toast.innerHTML = `
        <span>${esc(message)}</span>
        <button onclick="this.parentElement.remove()">✕</button>
    `;

    container.appendChild(toast);

    setTimeout(() => {
        toast.remove();
    }, 3000);
};


/* ===================== CART ===================== */

// let vendorCart = JSON.parse(localStorage.getItem('vendor_cart') || '[]');

function saveCart() {
    localStorage.setItem('vendor_cart', JSON.stringify(vendorCart));
}

function loadCart() {
    vendorCart = JSON.parse(localStorage.getItem('vendor_cart') || '[]');
}

window.addToCart = function (id, name, price) {
    loadCart();

    let item = vendorCart.find(x => x.id == id);

    if (item) {
        item.quantity++;
        item.total = item.quantity * item.price;
    } else {
        vendorCart.push({
            id,
            name,
            price,
            quantity: 1,
            total: price
        });
    }

    saveCart();
    renderCart();
    showFvToast(`${name} added to cart`);
};

window.removeFromCart = function (id) {
    loadCart();
    vendorCart = vendorCart.filter(x => x.id != id);
    saveCart();
    renderCart();
    showFvToast('Item removed');
};

window.updateCartQuantity = function (id, change) {
    loadCart();

    let item = vendorCart.find(x => x.id == id);
    if (!item) return;

    item.quantity += change;

    if (item.quantity <= 0) {
        vendorCart = vendorCart.filter(x => x.id != id);
    } else {
        item.total = item.quantity * item.price;
    }

    saveCart();
    renderCart();
};

function renderCart() {
    loadCart();
    const cartBadge = document.getElementById('headerCartCount');
    if (cartBadge) cartBadge.innerText = vendorCart.length;
}

function toggleCart() {
    document.getElementById('cartSidebar')?.classList.toggle('active');
}

function proceedToCheckout() {
    if (vendorCart.length > 0) {
        window.location.href = '/Vendor/Checkout';
    } else {
        showFvToast('Cart is empty', 'error');
    }
} 


/* ===================== WISHLIST ===================== */

// let wishlist = JSON.parse(localStorage.getItem('vendor_wishlist') || '[]');

function saveWishlist() {
    localStorage.setItem('vendor_wishlist', JSON.stringify(wishlist));
}

window.toggleWishlist = function (id, name, price) {
    let item = wishlist.find(x => x.id == id);

    if (item) {
        wishlist = wishlist.filter(x => x.id != id);
        showFvToast('Removed from wishlist');
    } else {
        wishlist.push({ id, name, price });
        showFvToast('Added to wishlist');
    }

    saveWishlist();
};


/* ===================== DASHBOARD ===================== */

window.loadDashboardStats = async function () {
    try {
        const res = await fetch(`${window.API_BASE_URL}/user/stats`);
        const result = await res.json();

        if (result.success && result.data) {
            const s = result.data;

            document.getElementById('totalOrders').innerText = s.totalOrders || 0;
            document.getElementById('totalSpent').innerText = '₹' + (s.totalSpent || 0);
            document.getElementById('totalQuantity').innerText = (s.totalQuantity || 0) + ' kg';
            document.getElementById('pendingDeliveries').innerText = s.pendingOrders || 0;
        }
    } catch (err) {
        console.error(err);
    }
};


/* ===================== HELPERS ===================== */

function formatDate(ds) {
    if (!ds) return '';

    const d = new Date(ds);
    const diff = Math.floor((new Date() - d) / 60000);

    if (diff < 1) return 'Just now';
    if (diff < 60) return `${diff}m ago`;
    if (diff < 1440) return `${Math.floor(diff / 60)}h ago`;

    return d.toLocaleDateString();
}

function esc(str) {
    return String(str || '')
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

function showFvToast(msg, type = 'success') {
    const container = document.getElementById('fvToasts');
    if (!container) return;

    const t = document.createElement('div');
    t.className = `fv-toast ${type}`;
    t.innerHTML = `${esc(msg)} <button onclick="this.parentElement.remove()">✕</button>`;

    container.appendChild(t);
    setTimeout(() => t.remove(), 4000);
}

/* ===================== GLOBAL HELPERS ===================== */

function syncGlobalBadges() {
    // Sync cart badge from localStorage (cart is locally tracked)
    const cart = JSON.parse(localStorage.getItem('vendor_cart') || '[]');
    const cartEl = document.getElementById('headerCartCount');
    if (cartEl) cartEl.innerText = cart.length;

    // NOTE: Wishlist count is NOT synced here.
    // Wishlist is API-based (stored in DB), not localStorage.
    // The wishlistCount badge is managed by updateWishlistCount()
    // in Catalog.cshtml after the API call to /wishlist completes.
}
window.syncGlobalBadges = syncGlobalBadges;

async function handleNotifClick(id, url) {
    await markNotificationAsRead(id);
    if (url && url !== '#') {
        window.location.href = url;
    }
}

async function deleteNotification(id) {
    if (!confirm('Delete this notification?')) return;
    try {
        await fvAuthFetch(`${NOTIF_API_BASE}/DeleteNotification`, {
            method: 'POST',
            body: JSON.stringify(id)
        });
        fvNotifs = fvNotifs.filter(n => n.id != id);
        renderNotifs();
        const unreadCount = fvNotifs.filter(n => !n.isRead).length;
        updateBellBadge(unreadCount);
        showFvToast('Notification deleted', 'success');
    } catch (error) {
        console.error('Error deleting notification:', error);
        showFvToast('Failed to delete notification', 'error');
    }
}

/* ===================== INIT ===================== */

document.addEventListener('DOMContentLoaded', () => {
    getUnreadCount();
    loadNotifications();

    setInterval(() => {
        getUnreadCount();
        loadNotifications();
    }, 30000);

    document.addEventListener('click', e => {
        const drawer = document.getElementById('fvNotif');
        const btn = document.getElementById('fvBellBtn');

        if (drawer?.classList.contains('open') &&
            !drawer.contains(e.target) &&
            !btn?.contains(e.target)) {
            drawer.classList.remove('open');
        }
    });
});

/* ===================== GLOBAL ===================== */

window.toggleNotifDrawer = function (e) {
    e?.stopPropagation();

    const d = document.getElementById('fvNotif');
    d?.classList.toggle('open');

    if (d?.classList.contains('open')) {
        loadNotifications();
    }
};

window.fvTab = function (tab, btn) {
    currentFvTab = tab;

    document.querySelectorAll('.fv-notif-tabs button')
        .forEach(b => b.classList.remove('active'));

    btn?.classList.add('active');

    renderNotifs();
};

window.fvMarkAllRead = fvMarkAllRead;
window.fvClearAll = fvClearAll;
window.markNotificationAsRead = markNotificationAsRead;
window.deleteNotification = deleteNotification;
window.handleNotifClick = handleNotifClick;