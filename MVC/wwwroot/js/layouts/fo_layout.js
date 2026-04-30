/* FarmBridge — Field Officer Layout JS */

const FO_NAV = [
    { icon: 'fi fi-rr-apps',         i18n: 'nav_dashboard',   text: 'Dashboard',          url: '/FieldOfficer/Dashboard' },
    { icon: 'fi fi-rr-user',         i18n: 'nav_profile',     text: 'Profile',             url: '/FieldOfficer/Profile' },
    { icon: 'fi fi-rr-clipboard-list', i18n: 'nav_qc_requests', text: 'QC Requests',       url: '/FieldOfficer/QCRequest' },
    { icon: 'fi fi-rr-settings-sliders', i18n: 'nav_quality_params', text: 'Quality Params', url: '/FieldOfficer/QualityParams' },
    { icon: 'fi fi-rr-check-circle',   i18n: 'nav_history',     text: 'Inspection History',  url: '/FieldOfficer/InspectionHistory' },
    { icon: 'fi fi-rr-book-open-reader', i18n: 'nav_catalog', text: 'Catalog',       url: '/FieldOfficer/Catalog' },
    { icon: 'fi fi-rr-credit-card',  i18n: 'nav_payments',    text: 'Payment History',     url: '/FieldOfficer/PaymentHistory' }
];

let foNotifs = [], foCurrentTab = 'all';
const FO_NOTIF_API_BASE = 'http://localhost:5020/api/Notification';

function getFoAuthToken() {
    const cookieMatch = document.cookie.match(/authToken=([^;]+)/);
    if (cookieMatch && cookieMatch[1] && cookieMatch[1] !== 'undefined' && cookieMatch[1] !== 'null') {
        return cookieMatch[1];
    }
    const localToken = localStorage.getItem('authToken');
    if (localToken && localToken !== 'undefined' && localToken !== 'null') return localToken;
    const sessionToken = sessionStorage.getItem('authToken');
    if (sessionToken && sessionToken !== 'undefined' && sessionToken !== 'null') return sessionToken;
    return null;
}

async function foAuthFetch(url, options = {}) {
    const token = getFoAuthToken();
    if (!token) {
        window.location.href = '/StaffAuth/Login';
        throw new Error("No auth token");
    }
    const headers = {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        ...options.headers
    };
    const response = await fetch(url, { ...options, headers });
    if (response.status === 401) {
        window.location.href = '/StaffAuth/Login';
        throw new Error("Unauthorized");
    }
    return response;
}

async function getFoUnreadCount() {
    try {
        const response = await foAuthFetch(`${FO_NOTIF_API_BASE}/UnreadCount`);
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

async function getFoNotifications() {
    try {
        const response = await foAuthFetch(`${FO_NOTIF_API_BASE}/GetNotifications`);
        const data = await response.json();
        if (data.success) {
            foNotifs = data.data || [];
            renderFoNotifs();
            const unreadCount = foNotifs.filter(n => !n.isRead).length;
            updateFoBellBadge(unreadCount);
        }
        return data;
    } catch (error) {
        return { success: false, data: [] };
    }
}

async function foMarkNotificationAsRead(notificationId) {
    try {
        const response = await foAuthFetch(`${FO_NOTIF_API_BASE}/MarkAsRead`, {
            method: 'POST',
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
    const confirmed = await Swal.fire({
        title: 'Delete this notification?',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#10b981',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Yes'
    }).then(r => r.isConfirmed);
    if (!confirmed) return;
    try {
        const response = await foAuthFetch(`${FO_NOTIF_API_BASE}/DeleteNotification`, {
            method: 'POST',
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

async function foMarkAllRead() {
    try {
        const response = await foAuthFetch(`${FO_NOTIF_API_BASE}/MarkAllRead`, {
            method: 'POST',
            body: JSON.stringify({})
        });
        const data = await response.json();
        if (data.success) {
            foNotifs.forEach(n => n.isRead = true);
            renderFoNotifs();
            updateFoBellBadge(0);
            showFoToast('All notifications marked as read', 'success');
        }
    } catch (error) {
        showFoToast('Failed to mark all as read', 'error');
    }
}

async function foClearAll() {
    const confirmed = await Swal.fire({
        title: 'Clear all notifications?',
        text: 'This action cannot be undone.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#10b981',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Yes, clear all'
    }).then(r => r.isConfirmed);
    if (!confirmed) return;
    try {
        const response = await foAuthFetch(`${FO_NOTIF_API_BASE}/ClearAllNotifications`, {
            method: 'POST'
        });
        const data = await response.json();
        if (data.success) {
            foNotifs = [];
            renderFoNotifs();
            updateFoBellBadge(0);
            showFoToast('All notifications cleared', 'success');
        }
    } catch (error) {
        showFoToast('Failed to clear notifications', 'error');
    }
}

document.addEventListener('DOMContentLoaded', async () => {
    if (window.jQuery) {
        $.ajaxSetup({
            beforeSend: function (xhr) {
                const token = getFoAuthToken();
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
    
    await getFoUnreadCount();
    await getFoNotifications();
    
    setInterval(async () => {
        await getFoUnreadCount();
        await getFoNotifications();
    }, 30000);

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
        const label = (typeof fbT === 'function') ? fbT(item.i18n) : item.text;
        menu.insertAdjacentHTML('beforeend', `
            <li><a href="${item.url}" class="fo-nav-link${active ? ' active' : ''}" title="${label}">
                <i class="${item.icon}"></i><span data-i18n="${item.i18n}">${label}</span>
            </a></li>`);
    });
}

function buildFoDrawerNav() {
    const nav = document.getElementById('foDrawerNav');
    if (!nav) return;
    nav.innerHTML = '';
    FO_NAV.forEach(item => {
        const active = location.pathname.toLowerCase().startsWith(item.url.toLowerCase());
        const label = (typeof fbT === 'function') ? fbT(item.i18n) : item.text;
        nav.insertAdjacentHTML('beforeend', `
            <li><a href="${item.url}" class="fo-nav-link${active ? ' active' : ''}">
                <i class="${item.icon}"></i><span data-i18n="${item.i18n}">${label}</span>
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

let foDrawerOpen = false;
function toggleFoDrawer() {
    foDrawerOpen = !foDrawerOpen;
    document.getElementById('foSidebar')?.classList.toggle('open', foDrawerOpen);
    document.getElementById('foOverlay')?.classList.toggle('open', foDrawerOpen);
}

function toggleFoNotif(e) {
    if (e) e.stopPropagation();
    const drawer = document.getElementById('foNotif');
    if (drawer) {
        drawer.classList.toggle('open');
        if (drawer.classList.contains('open')) {
            getFoNotifications();
        }
    }
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
                <i class="fi fi-rr-bell" style="font-size:18px;color:var(--fo-blue)"></i>
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

function loadFoUserProfile() {
    const avatarEl = document.getElementById('foAvatar');
    if (!avatarEl) return;
    
    const base = window.API_BASE || 'http://localhost:5020/api/FieldOfficer';
    foAuthFetch(base + `/profile/me`)
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

function foOpenAuthorizedWindow(path) {
    const token = getFoAuthToken();
    if (!token) {
        handleFoUnauthorized({ status: 401 });
        return;
    }

    const base = window.API_BASE || 'http://localhost:5020/api/FieldOfficer';
    const url = path.startsWith('http') ? path : `${base}${path.startsWith('/') ? '' : '/'}${path}`;
    
    fetch(url, {
        method: 'GET',
        headers: { Authorization: `Bearer ${token}` }
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

window.loadFoUserProfile = loadFoUserProfile;
window.foOpenAuthorizedWindow = foOpenAuthorizedWindow;

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
