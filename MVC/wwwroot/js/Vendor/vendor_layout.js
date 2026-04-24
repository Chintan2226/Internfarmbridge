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
    console.log("FINAL COUNT:", count);

    count = Number(count) || 0;

    // Red dot
    const dot = document.getElementById('fvBellDot');
    if (dot) dot.hidden = count === 0;

    // Drawer text
    const text = document.getElementById('fvUnreadCountText');
    if (text) text.textContent = count;

    // Tab badge
    const badge = document.getElementById('fvUnreadBadgeNum');
    if (badge) badge.textContent = count;

    // 🔥 Bell icon count badge (NEW)
    const bellCount = document.getElementById('fvBellCount');
    if (bellCount) {
        if (count > 0) {
            bellCount.style.display = 'inline-block';
            bellCount.textContent = count > 99 ? '99+' : count;
        } else {
            bellCount.style.display = 'none';
        }
    }
}

async function loadNotifications() {
    try {
        const res = await fvAuthFetch(`${NOTIF_API_BASE}/GetNotifications`);
        const data = await res.json();

        if (data?.success) {
            fvNotifs = data.data || [];
            renderNotifList();
        }
    } catch (err) {
        console.error("Load notifications error:", err);
    }
}


function toggleDrawer() {
    document.getElementById('fvSidebar')?.classList.toggle('open');
    document.getElementById('fvOverlay')?.classList.toggle('open');
}

function toggleNotifDrawer(e) {
    if (e) e.stopPropagation();
    document.getElementById('fvNotif')?.classList.toggle('open');
}

function fvTab(tab, btn) {
    currentFvTab = tab;
    document.querySelectorAll('.fv-notif-tabs button').forEach(b => b.classList.remove('active'));
    btn?.classList.add('active');
    renderNotifs();
}

function loadNotifs() {
    const base = 'http://localhost:5020/api/Notification';

    const token = getCookie('authToken');
    fetch(base + '/GetNotifications', {
        headers: { 'Authorization': `Bearer ${token}` }
    })
        .then(r => r.json())
        .then(res => {
            if (res?.success) {
                fvNotifs = res.data || [];
                renderNotifs();
            }
        })
        .catch(() => { });
}

function renderNotifs() {
    const unread = fvNotifs.filter(n => !n.isRead).length;

    const dot = document.getElementById('fvBellDot');
    if (dot) dot.hidden = unread === 0;

    const uel = document.getElementById('fvUnread');
    if (uel) uel.textContent = unread;

    const list = document.getElementById('fvNotifList');
    if (!list) return;

    const data = currentFvTab === 'unread'
        ? fvNotifs.filter(n => !n.isRead)
        : fvNotifs;

    if (!filtered.length) {
        list.innerHTML = `
            <div class="fv-notif-empty">
                <div class="empty-icon-wrap">
                    <i class="fi fi-rr-bell-ring"></i>
                </div>
                <p>No new notifications</p>
                <small>We'll notify you when something important happens.</small>
            </div>
        `;
        return;
    }

    list.innerHTML = filtered.map(n => {
        const timeStr = formatNotifTime(n.createdAt || n.CreatedAt);
        let iconClass = 'fi-rr-bell';
        let typeClass = 'type-info';

        const title = (n.title || '').toLowerCase();
        if (title.includes('order')) { iconClass = 'fi-rr-box-alt'; typeClass = 'type-order'; }
        else if (title.includes('payment') || title.includes('money')) { iconClass = 'fi-rr-usd-circle'; typeClass = 'type-payment'; }
        else if (title.includes('cart')) { iconClass = 'fi-rr-shopping-cart'; typeClass = 'type-cart'; }
        else if (title.includes('wishlist')) { iconClass = 'fi-rr-heart'; typeClass = 'type-wishlist'; }

        return `
            <div class="fv-notif-item ${n.isRead ? '' : 'unread'}" onclick="handleNotifClick('${n.id}', '${n.redirectUrl || '#'}')">
                <div class="notif-icon-wrap ${typeClass}">
                    <i class="fi ${iconClass}"></i>
                </div>
                <div class="notif-content">
                    <div class="notif-title">${esc(n.title)}</div>
                    <div class="notif-message">${esc(n.message)}</div>
                    <div class="notif-time">${timeStr}</div>
                </div>
                ${!n.isRead ? '<div class="notif-unread-dot"></div>' : ''}
            </div>
        `;
    }).join('');
}

function fvMarkAllRead() {
    fvNotifs.forEach(n => n.isRead = true);
    renderNotifs();
}

function fvClearAll() {
    fvNotifs = [];
    renderNotifs();
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

    const list = document.getElementById('cartItemsList');
    const footer = document.getElementById('cartFooter');
    const totalEl = document.getElementById('cartTotal');

    if (!list) return;

    if (!vendorCart.length) {
        list.innerHTML = `<p class="text-center text-muted py-5">Cart is empty</p>`;
        if (footer) footer.style.display = 'none';
        return;
    }

    let total = 0;

    list.innerHTML = vendorCart.map(item => {
        total += item.total;

        return `
        <div class="cart-item">
            <div>
                <strong>${esc(item.name)}</strong><br>
                ₹${item.price} × ${item.quantity}
            </div>
            <div>
                <button onclick="updateCartQuantity(${item.id},-1)">-</button>
                <button onclick="updateCartQuantity(${item.id},1)">+</button>
                <button onclick="removeFromCart(${item.id})">🗑</button>
            </div>
        </div>`;
    }).join('');

    if (footer) footer.style.display = 'block';
    if (totalEl) totalEl.innerText = '₹' + total;
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

    renderNotifList();
};

window.markSingleRead = markSingleRead;
window.deleteSingleNotif = deleteSingleNotif;
window.fvMarkAllRead = fvMarkAllRead;
window.fvClearAll = fvClearAll;