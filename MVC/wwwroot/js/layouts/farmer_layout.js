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

/* SweetAlert2 Helpers */

/* Shared base — structural only, no colors */
const _fbBase = {
    color: '#f1f5f9',
    customClass: {
        popup:         'fb-swal-popup',
        title:         'fb-swal-title',
        confirmButton: 'fb-swal-confirm',
        cancelButton:  'fb-swal-cancel',
        icon:          'notranslate'
    }
};

/* SUCCESS */
window.fbSuccess = function(title, text = '') {
    return Swal.fire({
        ..._fbBase,
        icon:               'success',
        iconColor:          '#34d399',
        background:         'linear-gradient(135deg, #052e16 0%, #064e3b 100%)',
        title, text,
        confirmButtonColor: '#059669',
        timer:              3000,
        timerProgressBar:   true,
        showConfirmButton:  false,
    });
};

/* ERROR */
window.fbError = function(title, text = '') {
    return Swal.fire({
        ..._fbBase,
        icon:               'error',
        iconColor:          '#f87171',
        background:         'linear-gradient(135deg, #2d0a0a 0%, #450a0a 100%)',
        title, text,
        confirmButtonColor: '#dc2626',
    });
};

/* WARNING */
window.fbWarning = function(title, text = '') {
    return Swal.fire({
        ..._fbBase,
        icon:               'warning',
        iconColor:          '#fbbf24',
        background:         'linear-gradient(135deg, #1c1200 0%, #3d2800 100%)',
        title, text,
        confirmButtonColor: '#d97706',
    });
};

/* INFO */
window.fbAlert = function(message, title = 'FarmBridge') {
    return Swal.fire({
        ..._fbBase,
        icon:               'info',
        iconColor:          '#60a5fa',
        background:         'linear-gradient(135deg, #0b1526 0%, #0f2746 100%)',
        title,
        text:               message,
        confirmButtonColor: '#2563eb',
    });
};

/* LOADING */
window.fbLoading = function(show, message = 'Processing...') {
    const loader = document.getElementById('fbLoader');
    const text   = document.getElementById('fbLoaderText');
    
    if (show) {
        if (text) text.textContent = message;
        if (loader) loader.classList.add('active');
    } else {
        if (loader) loader.classList.remove('active');
    }
};

/* CONFIRM */
window.fbConfirm = function(title, text = '', confirmLabel = 'Yes, Confirm') {
    return Swal.fire({
        ..._fbBase,
        icon:               'question',
        iconColor:          '#a78bfa',
        background:         'linear-gradient(135deg, #120d26 0%, #1e1346 100%)',
        title, text,
        showCancelButton:   true,
        confirmButtonText:  confirmLabel,
        cancelButtonText:   'Cancel',
        confirmButtonColor: '#7c3aed',
        cancelButtonColor:  '#374151',
    }).then(r => r.isConfirmed);
};

function esc(s) {
    return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}
