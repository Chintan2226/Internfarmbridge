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

async function markSingleRead(id) {
    try {
        await fvAuthFetch(`${NOTIF_API_BASE}/MarkAsRead`, {
            method: 'POST',
            body: JSON.stringify(id)
        });

        const n = fvNotifs.find(x => x.id == id);
        if (n) n.isRead = true;

        renderNotifList();
        getUnreadCount();

    } catch (err) {
        console.error("Mark read error:", err);
    }
}

async function deleteSingleNotif(id) {
    if (!confirm('Delete this notification?')) return;

    try {
        await fvAuthFetch(`${NOTIF_API_BASE}/DeleteNotification`, {
            method: 'POST',
            body: JSON.stringify(id)
        });

        fvNotifs = fvNotifs.filter(x => x.id != id);
        renderNotifList();
        getUnreadCount();

        showFvToast('Notification deleted');

    } catch {
        showFvToast('Delete failed', 'error');
    }
}

async function fvMarkAllRead() {
    try {
        await fvAuthFetch(`${NOTIF_API_BASE}/MarkAllRead`, {
            method: 'POST',
            body: JSON.stringify({})
        });

        fvNotifs.forEach(n => n.isRead = true);
        renderNotifList();
        getUnreadCount();

        showFvToast('All marked as read');

    } catch {
        showFvToast('Update failed', 'error');
    }
}

async function fvClearAll() {
    if (!confirm('Clear all notifications?')) return;

    try {
        await fvAuthFetch(`${NOTIF_API_BASE}/ClearAllNotifications`, {
            method: 'POST'
        });

        fvNotifs = [];
        renderNotifList();
        getUnreadCount();

        showFvToast('All notifications cleared');

    } catch {
        showFvToast('Clear failed', 'error');
    }
}

/* ===================== UI ===================== */

function handleNotifClick(id, url) {
    const n = fvNotifs.find(x => x.id == id);

    if (n && !n.isRead) {
        markSingleRead(id);
    }

    if (url && url !== '#') {
        window.location.href = url;
    }
}

function renderNotifList() {
    const list = document.getElementById('fvNotifList');
    if (!list) return;

    const data = currentFvTab === 'unread'
        ? fvNotifs.filter(n => !n.isRead)
        : fvNotifs;

    if (!data.length) {
        list.innerHTML = `<div class="fv-notif-empty">🔔 No notifications</div>`;
        return;
    }

    list.innerHTML = data.map(n => `
        <div class="fv-notif-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
            <div class="fv-notif-content" onclick="handleNotifClick(${n.id}, '${esc(n.redirectUrl || '')}')">
                <div class="fv-notif-icon">
                    <i class="fi fi-rr-bell" style="font-size:18px;color:var(--fv-primary)"></i>
                </div>
                <div style="flex:1">
                    <div style="font-size:13px;font-weight:600;">${esc(n.title || 'Notification')}</div>
                    <div style="font-size:12px;color:var(--fv-muted); margin-top:4px;">${esc(n.message || '')}</div>
                    <div style="font-size:10px;color:#9ca3af; margin-top:4px;">${formatDate(n.createdAt)}</div>
                </div>
            </div>
            <div class="fv-notif-actions-btns">
                ${!n.isRead ? `
                    <button onclick="event.stopPropagation(); markSingleRead(${n.id})" 
                            class="fv-notif-btn" title="Mark as read">
                        ✓
                    </button>
                ` : ''}
                <button onclick="event.stopPropagation(); deleteSingleNotif(${n.id})" 
                        class="fv-notif-btn" title="Delete">
                    ✕
                </button>
            </div>
        </div>
    `).join('');
}

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