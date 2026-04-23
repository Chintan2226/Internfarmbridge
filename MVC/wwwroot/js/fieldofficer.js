/* FarmBridge — Field Officer Layout JS */
/* Combined: fo_layout.js + fieldofficer.js */

const FO_NAV = [
    { icon: 'fi fi-rr-apps',           text: 'Dashboard',        url: '/FieldOfficer/Dashboard' },
    { icon: 'fi fi-rr-user',           text: 'Profile',          url: '/FieldOfficer/Profile' },
    { icon: 'fi fi-rr-clipboard-list', text: 'QC Request',       url: '/FieldOfficer/QCRequest' },
    { icon: 'fi fi-rr-settings-sliders', text: 'Quality Params', url: '/FieldOfficer/QualityParams' },
    { icon: 'fi fi-rr-check-circle',   text: 'Inspection History', url: '/FieldOfficer/InspectionHistory' },
    { icon: 'fi fi-rr-book-open-reader', text: 'Catalog',       url: '/FieldOfficer/Catalog' },
    { icon: 'fi fi-rr-credit-card',    text: 'Payment History',  url: '/FieldOfficer/PaymentHistory' }
];

let foNotifs = [], foCurrentTab = 'all';
window.API_BASE = window.API_BASE || 'http://localhost:5020/api/FieldOfficer';

function getCookieValue(name) {
    const prefix = `${name}=`;
    const cookie = document.cookie
        .split(';')
        .map(x => x.trim())
        .find(x => x.startsWith(prefix));
    return cookie ? decodeURIComponent(cookie.substring(prefix.length)) : '';
}

function getFoJwtToken() {
    return localStorage.getItem('fo_jwt')
        || sessionStorage.getItem('fo_jwt')
        || getCookieValue('authToken')
        || '';
}

function getFoAuthHeaders() {
    const token = getFoJwtToken();
    return token ? { Authorization: `Bearer ${token}` } : {};
}

function getFoJsonHeaders() {
    return { 'Content-Type': 'application/json', ...getFoAuthHeaders() };
}

function foApiUrl(path) {
    if (!path) return window.API_BASE;
    return path.startsWith('http') ? path : `${window.API_BASE}${path.startsWith('/') ? '' : '/'}${path}`;
}

function handleFoUnauthorized(xhrOrResponse) {
    const status = xhrOrResponse?.status;
    if (status !== 401) return false;

    Swal.fire({
        icon: 'warning',
        iconColor: '#fbbf24',
        title: 'Session Expired',
        text: 'Please log in again to continue.',
        confirmButtonText: 'Go to Login',
        background: 'linear-gradient(135deg, #1c1200 0%, #3d2800 100%)',
        color: '#f1f5f9',
        confirmButtonColor: '#d97706'
    }).then(() => {
        logout();
    });

    return true;
}

function foOpenAuthorizedWindow(path) {
    const token = getFoJwtToken();
    if (!token) {
        handleFoUnauthorized({ status: 401 });
        return;
    }

    const url = foApiUrl(path);
    fetch(url, {
        method: 'GET',
        headers: getFoAuthHeaders()
    })
        .then(response => {
            if (handleFoUnauthorized(response)) return null;
            if (!response.ok) {
                throw new Error(`Request failed with status ${response.status}`);
            }
            return response.blob();
        })
        .then(blob => {
            if (!blob) return;
            const objectUrl = URL.createObjectURL(blob);
            window.open(objectUrl, '_blank', 'noopener');
            setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
        })
        .catch(() => {
            showFoLoadFailedAlert('report');
        });
}

function foFetch(url, options = {}) {
    const mergedHeaders = { ...(options.headers || {}), ...getFoAuthHeaders() };
    return fetch(url, { ...options, headers: mergedHeaders }).then(response => {
        handleFoUnauthorized(response);
        return response;
    });
}

document.addEventListener('DOMContentLoaded', () => {
    if (window.jQuery) {
        $.ajaxSetup({
            beforeSend: function (xhr) {
                const token = getFoJwtToken();
                if (token) {
                    xhr.setRequestHeader('Authorization', `Bearer ${token}`);
                }
            }
        });
    }

    buildFoNav();
    buildFoDrawerNav();
    buildFoBottomNav();
    loadFoUserProfile();
    loadFoNotifs();
    setInterval(loadFoNotifs, 30000);

    document.addEventListener('click', e => {
        const notif = document.getElementById('foNotif');
        const bellBtn = document.getElementById('foBellBtn');
        if (notif?.classList.contains('open') && !notif.contains(e.target) && !bellBtn?.contains(e.target)) {
            notif.classList.remove('open');
        }
    });
});

function buildFoNav() {
    const menu = document.getElementById('foNav');
    if (!menu) return;
    menu.innerHTML = '';
    FO_NAV.forEach(item => {
        const active = location.pathname.toLowerCase().startsWith(item.url.toLowerCase());
        menu.insertAdjacentHTML('beforeend', `
            <li><a href="${item.url}" class="fo-nav-link${active ? ' active' : ''}" title="${item.text}">
                <i class="${item.icon}"></i><span>${item.text}</span>
            </a></li>`);
    });
}

function buildFoDrawerNav() {
    const nav = document.getElementById('foDrawerNav');
    if (!nav) return;
    nav.innerHTML = '';
    FO_NAV.forEach(item => {
        const active = location.pathname.toLowerCase().startsWith(item.url.toLowerCase());
        nav.insertAdjacentHTML('beforeend', `
            <li><a href="${item.url}" class="fo-nav-link${active ? ' active' : ''}">
                <i class="${item.icon}"></i><span>${item.text}</span>
            </a></li>`);
    });
}

function buildFoBottomNav() {
    const nav = document.getElementById('foBottomNav');
    if (!nav) return;
    nav.innerHTML = '';
    FO_NAV.forEach(item => {
        const active = location.pathname.toLowerCase().startsWith(item.url.toLowerCase());
        nav.insertAdjacentHTML('beforeend', `
            <a href="${item.url}" class="fo-bottom-item${active ? ' active' : ''}" aria-label="${item.text}">
                <i class="${item.icon}"></i><span>${item.text}</span>
            </a>`);
    });
}

function loadFoUserProfile() {
    const avatarEl = document.getElementById('foAvatar');
    if (!avatarEl) return;
    
    const base = window.API_BASE;
    foFetch(base + `/profile/me`)
        .then(r => r.json())
        .then(res => {
            if (res?.success && res.data) {
                const user = res.data;
                const initials = (user.firstName?.[0] || 'F') + (user.lastName?.[0] || 'O');
                avatarEl.textContent = initials.toUpperCase();
                avatarEl.title = user.firstName + ' ' + user.lastName;
                if (user.profileImageUrl) {
                    avatarEl.style.backgroundImage = `url('${user.profileImageUrl}')`;
                    avatarEl.style.backgroundSize = 'cover';
                    avatarEl.style.backgroundPosition = 'center';
                    avatarEl.textContent = '';
                }
            }
        })
        .catch(() => { });
}
function logout() {

    // Remove JWT cookie
    document.cookie =
        "authToken=; path=/; expires=Thu, 01 Jan 1970 00:00:00 UTC; SameSite=Strict";

    // Clear storage (optional but recommended)
    localStorage.clear();
    sessionStorage.clear();

    // Redirect to login
    window.location.href = "/StaffAuth/Login";
}
let foDrawerOpen = false;
function toggleFoDrawer() {
    foDrawerOpen = !foDrawerOpen;
    document.getElementById('foDrawer')?.classList.toggle('open', foDrawerOpen);
    document.getElementById('foOverlay')?.classList.toggle('open', foDrawerOpen);
}

function toggleFoNotif(e) {
    if (e) e.stopPropagation();
    document.getElementById('foNotif')?.classList.toggle('open');
}

const FO_NOTIF_API_BASE = 'http://localhost:5020/api/Notification';

async function getFoUnreadCount() {
    try {
        const response = await foFetch(`${FO_NOTIF_API_BASE}/UnreadCount`);
        const data = await response.json();
        if (data.success) {
            updateFoBellBadge(data.count);
            return data.count;
        }
        return 0;
    } catch (error) {
        return 0;
    }
}

function updateFoBellBadge(count) {
    const dot = document.getElementById('foBellDot');
    if (dot) dot.hidden = count === 0;
    
    const badge = document.getElementById('foBellBadge');
    if (badge) {
        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : count;
            badge.style.display = 'flex';
        } else {
            badge.style.display = 'none';
        }
    }
    
    const uel = document.getElementById('foUnread');
    if (uel) uel.textContent = count;
    
    const ub = document.getElementById('foUnreadBadge');
    if (ub) ub.textContent = count;
}

function loadFoNotifs() {
    foFetch(`${FO_NOTIF_API_BASE}/GetNotifications`)
        .then(r => r.json())
        .then(res => { 
            if (res?.success && res.data) { 
                foNotifs = res.data || []; 
                renderFoNotifs(); 
                const unreadCount = foNotifs.filter(n => !n.isRead).length;
                updateFoBellBadge(unreadCount);
            } 
        })
        .catch(err => { console.log('Notifications load skipped'); });
}

function foTab(tab, btn) {
    foCurrentTab = tab;
    document.querySelectorAll('.fo-notif-tabs button').forEach(b => b.classList.remove('active'));
    if (btn) btn.classList.add('active');
    renderFoNotifs();
}

function renderFoNotifs() {
    const unreadCount = foNotifs.filter(n => !n.isRead).length;
    updateFoBellBadge(unreadCount);

    const filtered = foCurrentTab === 'unread' ? foNotifs.filter(n => !n.isRead) : foNotifs;
    const list = document.getElementById('foNotifList');
    if (!list) return;

    if (!filtered.length) {
        list.innerHTML = `<div style="padding:32px;text-align:center;font-size:13px;color:var(--fo-muted)">No notifications</div>`;
        return;
    }
    
    list.innerHTML = filtered.map(n => `
        <div class="fo-notif-item${n.isRead ? '' : ' unread'}" data-id="${n.id}">
            <div style="flex:1; display:flex; gap:10px; cursor:pointer;" onclick="handleFoNotificationClick(${n.id}, '${esc(n.redirectUrl || '')}')">
                <i class="fi fi-rr-bell" style="font-size:18px;color:#1a5c2a"></i>
                <div>
                    <div style="font-size:13px;font-weight:600;">${esc(n.title)}</div>
                    <div style="font-size:12px;color:var(--fo-muted);">${esc(n.message || '')}</div>
                    <div style="font-size:10px;color:var(--fo-muted); margin-top:4px;">${formatFoDate(n.createdAt)}</div>
                </div>
            </div>
            <div class="fo-notif-actions" style="padding:0; align-items:flex-start; gap:4px; display:flex;">
                ${!n.isRead ? `
                    <button onclick="event.stopPropagation(); foMarkNotificationAsRead(${n.id})" 
                            style="background:none; border:none; color:var(--fo-blue); cursor:pointer; font-size:14px;" title="Mark as read">
                        ✓
                    </button>
                ` : ''}
                <button onclick="event.stopPropagation(); foDeleteNotification(${n.id})" 
                        style="background:none; border:none; color:var(--fo-muted); cursor:pointer; font-size:14px;" title="Delete">
                    ✕
                </button>
            </div>
        </div>`).join('');
}

async function handleFoNotificationClick(notificationId, redirectUrl) {
    const notification = foNotifs.find(n => n.id == notificationId);
    if (notification && !notification.isRead) {
        await foMarkNotificationAsRead(notificationId);
    }
    if (redirectUrl && redirectUrl !== '#') {
        window.location.href = redirectUrl;
    }
}

async function foMarkNotificationAsRead(notificationId) {
    try {
        const response = await foFetch(`${FO_NOTIF_API_BASE}/MarkAsRead`, {
            method: 'POST',
            headers: getFoJsonHeaders(),
            body: JSON.stringify(notificationId)
        });
        const data = await response.json();
        if (data.success) {
            const notification = foNotifs.find(n => n.id == notificationId);
            if (notification) {
                notification.isRead = true;
                renderFoNotifs();
                const unreadCount = foNotifs.filter(n => !n.isRead).length;
                updateFoBellBadge(unreadCount);
            }
        }
    } catch (error) {
    }
}

async function foDeleteNotification(notificationId) {
    if (!confirm('Delete this notification?')) return;
    try {
        const response = await foFetch(`${FO_NOTIF_API_BASE}/DeleteNotification`, {
            method: 'POST',
            headers: getFoJsonHeaders(),
            body: JSON.stringify(notificationId)
        });
        const data = await response.json();
        if (data.success) {
            foNotifs = foNotifs.filter(n => n.id != notificationId);
            renderFoNotifs();
            const unreadCount = foNotifs.filter(n => !n.isRead).length;
            updateFoBellBadge(unreadCount);
            showFoToast('Notification deleted', 'success');
        }
    } catch (error) {
        showFoToast('Failed to delete notification', 'error');
    }
}

function foMarkAllRead() {
    foNotifs.forEach(n => n.isRead = true);
    renderFoNotifs();
    updateFoBellBadge(0);
    foFetch(`${FO_NOTIF_API_BASE}/MarkAllRead`, { method: 'POST', headers: getFoJsonHeaders(), body: JSON.stringify({}) })
        .then(r => r.json())
        .then(res => { if(res.success) showFoToast('All notifications marked as read', 'success'); })
        .catch(() => { });
}

function foClearAll() {
    if (!confirm('Clear all notifications? This action cannot be undone.')) return;
    foNotifs = [];
    renderFoNotifs();
    updateFoBellBadge(0);
    foFetch(`${FO_NOTIF_API_BASE}/ClearAllNotifications`, { method: 'POST' })
        .then(r => r.json())
        .then(res => { if(res.success) showFoToast('All notifications cleared', 'success'); })
        .catch(() => { });
}

function formatFoDate(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} min ago`;
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
    return date.toLocaleDateString();
}

window.foMarkAllRead = foMarkAllRead;
window.foClearAll = foClearAll;
window.foMarkNotificationAsRead = foMarkNotificationAsRead;
window.foDeleteNotification = foDeleteNotification;
window.foTab = foTab;

window.showFoLoadFailedAlert = function (entityName) {
    Swal.fire({
        icon: 'error',
        iconColor: '#f87171',
        title: 'Load Failed',
        text: `Failed to load ${entityName || 'data'}. Please try again.`,
        confirmButtonText: 'OK',
        background: 'linear-gradient(135deg, #2d0a0a 0%, #450a0a 100%)',
        color: '#f1f5f9',
        confirmButtonColor: '#dc2626'
    });
};

/* ─────────────────────────────────────────────────────────────────────────
   FarmBridge SweetAlert2 Helpers — Field Officer portal (same scheme as Farmer)
   foSuccess / foError / foWarning / foAlert / foConfirm
───────────────────────────────────────────────────────────────────────── */
const _foBase = {
    color: '#f1f5f9',
    customClass: { popup: 'fb-swal-popup', title: 'fb-swal-title' }
};
window.foSuccess = (title, text = '') => Swal.fire({
    ..._foBase, icon: 'success', iconColor: '#34d399', title, text,
    background: 'linear-gradient(135deg, #052e16 0%, #064e3b 100%)',
    confirmButtonColor: '#059669', timer: 3000, timerProgressBar: true, showConfirmButton: false
});
window.foError = (title, text = '') => Swal.fire({
    ..._foBase, icon: 'error', iconColor: '#f87171', title, text,
    background: 'linear-gradient(135deg, #2d0a0a 0%, #450a0a 100%)',
    confirmButtonColor: '#dc2626'
});
window.foWarning = (title, text = '') => Swal.fire({
    ..._foBase, icon: 'warning', iconColor: '#fbbf24', title, text,
    background: 'linear-gradient(135deg, #1c1200 0%, #3d2800 100%)',
    confirmButtonColor: '#d97706'
});
window.foAlert = (message, title = 'FarmBridge') => Swal.fire({
    ..._foBase, icon: 'info', iconColor: '#60a5fa', title, text: message,
    background: 'linear-gradient(135deg, #0b1526 0%, #0f2746 100%)',
    confirmButtonColor: '#2563eb'
});
window.foConfirm = (title, text = '', confirmLabel = 'Yes, Confirm') =>
    Swal.fire({
        ..._foBase, icon: 'question', iconColor: '#a78bfa', title, text,
        background: 'linear-gradient(135deg, #120d26 0%, #1e1346 100%)',
        showCancelButton: true, confirmButtonText: confirmLabel, cancelButtonText: 'Cancel',
        confirmButtonColor: '#7c3aed', cancelButtonColor: '#374151'
    }).then(r => r.isConfirmed);

/* Toast utility */
window.showFoToast = function (message, type = 'success') {
    const container = document.getElementById('foToasts');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `fo-toast ${type}`;
    toast.innerHTML = `<span style="flex:1;font-size:13px;">${esc(message)}</span>
        <button onclick="this.closest('.fo-toast').remove()" style="background:none;border:none;cursor:pointer">✕</button>`;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 3500);
};

function esc(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

window.getFoJwtToken = getFoJwtToken;
window.getFoAuthHeaders = getFoAuthHeaders;
window.getFoJsonHeaders = getFoJsonHeaders;
window.foApiUrl = foApiUrl;
window.handleFoUnauthorized = handleFoUnauthorized;
window.foOpenAuthorizedWindow = foOpenAuthorizedWindow;
window.foFetch = foFetch;
