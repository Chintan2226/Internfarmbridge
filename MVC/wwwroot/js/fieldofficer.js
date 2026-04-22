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
        title: 'Session Expired',
        text: 'Please log in again to continue.',
        confirmButtonText: 'Go to Login',
        confirmButtonColor: '#28a745'
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

function loadFoNotifs() {
    const base = window.API_BASE;
    foFetch(base + '/GetNotifications')
        .then(r => r.json())
        .then(res => { if (res?.success && res.data) { foNotifs = res.data || []; renderFoNotifs(); } })
        .catch(err => { console.log('Notifications load skipped'); });
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
function renderFoNotifs() {
    const unread = foNotifs.filter(n => !n.isRead).length;
    const dot = document.getElementById('foBellDot');
    if (dot) dot.hidden = unread === 0;
    const uel = document.getElementById('foUnread');
    if (uel) uel.textContent = unread;

    const list = document.getElementById('foNotifList');
    if (!list) return;

    if (!foNotifs.length) {
        list.innerHTML = `<div style="padding:32px;text-align:center;font-size:13px;color:var(--fo-muted)">No notifications</div>`;
        return;
    }
    list.innerHTML = foNotifs.map(n => `
        <div class="fo-notif-item${n.isRead ? '' : ' unread'}" onclick="location.href='${n.redirectUrl || '#'}'">
            <i class="fi fi-rr-bell" style="font-size:18px;color:#1a5c2a"></i>
            <div><div style="font-size:13px;font-weight:600;">${esc(n.title)}</div>
            <div style="font-size:12px;color:var(--fo-muted);">${esc(n.message || '')}</div></div>
        </div>`).join('');
}

function foMarkAllRead() {
    foNotifs.forEach(n => n.isRead = true);
    renderFoNotifs();
    foFetch(window.API_BASE + '/MarkAllNotificationsRead', { method: 'POST' }).catch(() => { });
}

function foClearAll() {
    foNotifs = [];
    renderFoNotifs();
    foFetch(window.API_BASE + '/ClearAllNotifications', { method: 'POST' }).catch(() => { });
}

window.showFoLoadFailedAlert = function (entityName) {
    Swal.fire({
        icon: 'error',
        title: 'Load Failed',
        text: `Failed to load ${entityName || 'data'}. Please try again.`,
        confirmButtonText: 'OK',
        confirmButtonColor: '#28a745'
    });
};

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
