
/* ===================== NOTIFICATIONS ===================== */

let fvNotifs = [], currentFvTab = 'all';

document.addEventListener('DOMContentLoaded', () => {
    loadNotifs();
    setInterval(loadNotifs, 30000);

    document.addEventListener('click', e => {
        const notif = document.getElementById('fvNotif');
        const bellBtn = document.getElementById('fvBellBtn');

        if (notif?.classList.contains('open') &&
            !notif.contains(e.target) &&
            !bellBtn?.contains(e.target)) {
            notif.classList.remove('open');
        }
    });

    renderCart(); // init cart
});


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

    const filtered = currentFvTab === 'unread'
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

function formatNotifTime(dateStr) {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diff = Math.floor((now - date) / 1000);

    if (diff < 60) return 'Just now';
    if (diff < 3600) return Math.floor(diff / 60) + 'm ago';
    if (diff < 86400) return Math.floor(diff / 3600) + 'h ago';
    return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });
}

function handleNotifClick(id, url) {
    const notif = fvNotifs.find(n => n.id === id);
    if (notif) notif.isRead = true;
    renderNotifs();
    if (url && url !== '#') location.href = url;
}

function esc(t) {
    if (!t) return '';
    const d = document.createElement('div');
    d.textContent = t;
    return d.innerHTML;
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

function esc(str) {
    return String(str || '')
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}