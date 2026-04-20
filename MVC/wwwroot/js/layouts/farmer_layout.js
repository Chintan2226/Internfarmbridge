/* FarmBridge Layout JS */

let fvNotifs = [], currentFvTab = 'all';

document.addEventListener('DOMContentLoaded', () => {

    loadNotifs();
    setInterval(loadNotifs, 30000);

    document.addEventListener('click', e => {
        const notif = document.getElementById('fvNotif');
        const bellBtn = document.getElementById('fvBellBtn');
        if (notif?.classList.contains('open') && !notif.contains(e.target) && !bellBtn?.contains(e.target)) {
            notif.classList.remove('open');
        }
    });
});





let fvDrawerOpen = false;
function toggleDrawer() {
    fvDrawerOpen = !fvDrawerOpen;
    document.getElementById('fvSidebar')?.classList.toggle('open', fvDrawerOpen);
    document.getElementById('fvOverlay')?.classList.toggle('open', fvDrawerOpen);
}

function toggleNotifDrawer(e) {
    if (e) e.stopPropagation();
    document.getElementById('fvNotif')?.classList.toggle('open');
}

function fvTab(tab, btn) {
    currentFvTab = tab;
    document.querySelectorAll('.fv-notif-tabs button').forEach(b => b.classList.remove('active'));
    if (btn) btn.classList.add('active');
    renderNotifs();
}

function loadNotifs() {
    const base = window.API_BASE || 'http://localhost:5020/api/Vendor';
    fetch(base + '/GetNotifications')
        .then(r => {
            if (!r.ok) return null;
            const ct = r.headers.get('content-type');
            if (!ct || ct.indexOf('application/json') === -1) return null;
            return r.json();
        })
        .then(res => { if (res && res.success) { fvNotifs = res.data || []; renderNotifs(); } })
        .catch(() => { });
}

function renderNotifs() {
    const unread = fvNotifs.filter(n => !n.isRead).length;
    const dot = document.getElementById('fvBellDot');
    if (dot) dot.hidden = unread === 0;
    const uel = document.getElementById('fvUnread');
    if (uel) uel.textContent = unread;
    const ub = document.getElementById('fvUnreadBadge');
    if (ub) ub.textContent = unread;

    const filtered = currentFvTab === 'unread' ? fvNotifs.filter(n => !n.isRead) : fvNotifs;
    const list = document.getElementById('fvNotifList');
    if (!list) return;

    if (!filtered.length) {
        list.innerHTML = `<div class="fv-notif-empty">🔔 No notifications</div>`;
        return;
    }
    list.innerHTML = filtered.map(n => `
        <div class="fv-notif-item${n.isRead ? '' : ' unread'}" onclick="location.href='${n.redirectUrl || '#'}'">
            <i class="fi fi-rr-bell"></i>
            <div><div style="font-size:13px;font-weight:600;">${esc(n.title || 'Notification')}</div>
            <div style="font-size:12px;color:var(--fv-muted);">${esc(n.message || '')}</div></div>
        </div>`).join('');
}

function fvMarkAllRead() {
    fvNotifs.forEach(n => n.isRead = true);
    renderNotifs();
    fetch((window.API_BASE || '') + '/MarkAllNotificationsRead', { method: 'POST' }).catch(() => { });
}

function fvClearAll() {
    fvNotifs = [];
    renderNotifs();
    fetch((window.API_BASE || '') + '/ClearAllNotifications', { method: 'POST' }).catch(() => { });
}

/* Toast utility — available globally */
window.showFvToast = window.showToast = function (message, type = 'success') {
    const container = document.getElementById('fvToasts');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `fv-toast ${type}`;
    toast.innerHTML = `<span style="flex:1">${esc(message)}</span>
        <button class="fv-toast-close" onclick="this.closest('.fv-toast').remove()">✕</button>`;
    container.appendChild(toast);
    setTimeout(() => { toast.classList.add('fade-out'); setTimeout(() => toast.remove(), 300); }, 3000);
};

function esc(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
