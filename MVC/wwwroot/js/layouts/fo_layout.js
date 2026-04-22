/* FarmBridge — Field Officer Layout JS */

const FO_NAV = [
    { icon: 'fi fi-rr-apps', i18n: 'nav_dashboard', text: 'Dashboard', url: '/FieldOfficer/Dashboard' },
    { icon: 'fi fi-rr-users', i18n: 'nav_my_farmers', text: 'My Farmers', url: '/FieldOfficer/Farmers' },
    { icon: 'fi fi-rr-checkbox', i18n: 'nav_approvals', text: 'Approvals', url: '/FieldOfficer/Approvals' },
    { icon: 'fi fi-rr-user', i18n: 'nav_profile', text: 'Profile', url: '/FieldOfficer/Profile' },
];

let foNotifs = [], foCurrentTab = 'all';

document.addEventListener('DOMContentLoaded', () => {
    buildFoNav();
    buildFoDrawerNav();
    buildFoBottomNav();
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
    document.getElementById('foNotif')?.classList.toggle('open');
}

function loadFoNotifs() {
    const base = window.API_BASE || 'http://localhost:5020/api/FieldOfficer';
    fetch(base + '/GetNotifications')
        .then(r => {
            if (!r.ok) return null;
            const ct = r.headers.get('content-type');
            if (!ct || ct.indexOf('application/json') === -1) return null;
            return r.json();
        })
        .then(res => { if (res && res.success) { foNotifs = res.data || []; renderFoNotifs(); } })
        .catch(() => { });
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
            <i class="fi fi-rr-bell" style="font-size:18px;color:var(--fo-blue)"></i>
            <div><div style="font-size:13px;font-weight:600;">${esc(n.title)}</div>
            <div style="font-size:12px;color:var(--fo-muted);">${esc(n.message || '')}</div></div>
        </div>`).join('');
}

function foMarkAllRead() {
    foNotifs.forEach(n => n.isRead = true);
    renderFoNotifs();
    fetch((window.API_BASE || '') + '/MarkAllNotificationsRead', { method: 'POST' }).catch(() => { });
}

function foClearAll() {
    foNotifs = [];
    renderFoNotifs();
    fetch((window.API_BASE || '') + '/ClearAllNotifications', { method: 'POST' }).catch(() => { });
}

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
