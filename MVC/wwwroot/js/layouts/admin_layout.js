/* FarmBridge — Admin Layout JS */

let admNotifs = [], admCurrentTab = 'all';

function toggleAdmDrawer() {
    const sb = document.getElementById('admSidebar');
    if (sb) sb.classList.toggle('open');
}

document.addEventListener('DOMContentLoaded', () => {
    loadAdmNotifs();
    setInterval(loadAdmNotifs, 30000);

    document.addEventListener('click', e => {
        const notif   = document.getElementById('admNotif');
        const bellBtn = document.getElementById('admBellBtn');
        if (notif?.classList.contains('open') && !notif.contains(e.target) && !bellBtn?.contains(e.target)) {
            notif.classList.remove('open');
        }
    });
});

function toggleAdmNotif(e) {
    if (e) e.stopPropagation();
    document.getElementById('admNotif')?.classList.toggle('open');
}

function admTab(tab, btn) {
    admCurrentTab = tab;
    document.querySelectorAll('.adm-notif-tabs button').forEach(b => b.classList.remove('active'));
    if (btn) btn.classList.add('active');
    renderAdmNotifs();
}

function loadAdmNotifs() {
    const base = window.API_BASE || 'http://localhost:5020/api/Admin';
    fetch(base + '/GetNotifications')
        .then(r => {
            if (!r.ok) return null;
            const ct = r.headers.get('content-type');
            if (!ct || ct.indexOf('application/json') === -1) return null;
            return r.json();
        })
        .then(res => { if (res && res.success) { admNotifs = res.data || []; renderAdmNotifs(); } })
        .catch(() => {});
}

function renderAdmNotifs() {
    const unread = admNotifs.filter(n => !n.isRead).length;
    const dot    = document.getElementById('admBellDot');
    if (dot) dot.hidden = unread === 0;
    const uel = document.getElementById('admUnread');
    if (uel) uel.textContent = unread;
    const ub = document.getElementById('admUnreadBadge');
    if (ub) ub.textContent = unread;

    const filtered = admCurrentTab === 'unread' ? admNotifs.filter(n => !n.isRead) : admNotifs;
    const list = document.getElementById('admNotifList');
    if (!list) return;

    if (!filtered.length) {
        list.innerHTML = `<div style="padding:32px;text-align:center;font-size:13px;color:var(--adm-muted)">No notifications</div>`;
        return;
    }
    list.innerHTML = filtered.map(n => `
        <div class="adm-notif-item${n.isRead ? '' : ' unread'}"
             onclick="admMarkRead(${n.id}); location.href='${n.redirectUrl || '#'}'">
            <i class="fi fi-rr-bell" style="font-size:18px;color:var(--adm-lime-dk)"></i>
            <div><div style="font-size:13px;font-weight:600;">${esc(n.title)}</div>
            <div style="font-size:12px;color:var(--adm-muted);">${esc(n.message || '')}</div></div>
        </div>`).join('');
}

function admMarkAllRead() {
    admNotifs.forEach(n => n.isRead = true);
    renderAdmNotifs();
    fetch((window.API_BASE || '') + '/MarkAllNotificationsRead', { method: 'POST' }).catch(() => {});
}

function admClearAll() {
    admNotifs = [];
    renderAdmNotifs();
    fetch((window.API_BASE || '') + '/ClearAllNotifications', { method: 'POST' }).catch(() => {});
}

function admMarkRead(id) {
    const n = admNotifs.find(x => x.id === id);
    if (n) n.isRead = true;
    renderAdmNotifs();
}

/* Toast utility */
window.showAdminToast = function(title, message, type = 'success') {
    const container = document.getElementById('admToasts');
    if (!container) return;
    const toast = document.createElement('div');
    toast.className = `adm-toast ${type}`;
    toast.innerHTML = `<div style="flex:1"><div style="font-weight:600;font-size:13px;">${esc(title)}</div>
        ${message ? `<div style="font-size:12px;color:var(--adm-muted)">${esc(message)}</div>` : ''}
    </div><button onclick="this.closest('.adm-toast').remove()" style="background:none;border:none;cursor:pointer;color:var(--adm-muted)">✕</button>`;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 4500);
};

/* Backward compat alias — Admin views that call showToast(msg, type) */
window.showToast = function(msg, type = 'success') {
    window.showAdminToast(msg, '', type);
};

function esc(s) {
    return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}
