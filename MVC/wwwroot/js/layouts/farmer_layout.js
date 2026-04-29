/* FarmBridge — Farmer Layout JS */

let fvNotifs = [], currentFvTab = 'all';
const API_BASE = window.API_BASE || 'http://localhost:5020/api/Farmer';
const NOTIF_API_BASE = 'http://localhost:5020/api/Notification';

// Helper function to get auth token from cookie or storage
function getAuthToken() {
    // Try to get from cookie
    const cookieMatch = document.cookie.match(/authToken=([^;]+)/);
    if (cookieMatch && cookieMatch[1]) {
        const token = cookieMatch[1];
        if (token && token !== 'undefined' && token !== 'null') {
            return token;
        }
    }
    
    // Try localStorage
    const localToken = localStorage.getItem('authToken');
    if (localToken && localToken !== 'undefined' && localToken !== 'null') {
        return localToken;
    }
    
    // Try sessionStorage
    const sessionToken = sessionStorage.getItem('authToken');
    if (sessionToken && sessionToken !== 'undefined' && sessionToken !== 'null') {
        return sessionToken;
    }
    
    console.warn("No auth token found");
    return null;
}

// Generic fetch with auth header
async function authFetch(url, options = {}) {
    const token = getAuthToken();
    
    if (!token) {
        console.error("No auth token available");
        window.location.href = '/Farmer/Login';
        throw new Error("No auth token");
    }
    
    const headers = {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        ...options.headers
    };
    
    const response = await fetch(url, {
        ...options,
        headers
    });
    
    if (response.status === 401) {
        console.error("Unauthorized - redirecting to login");
        window.location.href = '/Farmer/Login';
        throw new Error("Unauthorized");
    }
    
    return response;
}

// Get unread count only (lightweight)
async function getUnreadCount() {
    try {
        const response = await authFetch(`${NOTIF_API_BASE}/UnreadCount`);
        const data = await response.json();
        if (data.success) {
            updateBellBadge(data.count);
            return data.count;
        }
        return 0;
    } catch (error) {
        console.error("Error fetching unread count:", error);
        return 0;
    }
}

// Update bell icon badge
function updateBellBadge(count) {
    // Update bell dot
    const dot = document.getElementById('fvBellDot');
    if (dot) {
        dot.hidden = count === 0;
    }
    
    // Update badge number if exists
    const badge = document.getElementById('fvBellBadge');
    if (badge) {
        if (count > 0) {
            badge.textContent = count > 99 ? '99+' : count;
            badge.style.display = 'flex';
        } else {
            badge.style.display = 'none';
        }
    }
    
    // Update any other unread count displays
    const unreadElements = document.querySelectorAll('.unread-count, .notification-badge');
    unreadElements.forEach(el => {
        if (count > 0) {
            el.textContent = count > 99 ? '99+' : count;
            el.style.display = 'flex';
        } else {
            el.style.display = 'none';
        }
    });
}

// Get notifications with auth
async function getNotifications() {
    try {
        const response = await authFetch(`${NOTIF_API_BASE}/GetNotifications`);
        const data = await response.json();
        if (data.success) {
            console.log("Notifications received:", data.data?.length || 0);
            fvNotifs = data.data || [];
            renderNotifs();
            
            // Update bell badge with unread count
            const unreadCount = fvNotifs.filter(n => !n.isRead).length;
            updateBellBadge(unreadCount);
        }
        return data;
    } catch (error) {
        console.error("Error fetching notifications:", error);
        return { success: false, data: [] };
    }
}

// Mark single notification as read
async function markNotificationAsRead(notificationId) {
    try {
        const response = await authFetch(`${NOTIF_API_BASE}/MarkAsRead`, {
            method: 'POST',
            body: JSON.stringify(notificationId)
        });
        const data = await response.json();
        if (data.success) {
            // Update local state
            const notification = fvNotifs.find(n => n.id == notificationId);
            if (notification) {
                notification.isRead = true;
                renderNotifs();
                
                // Update bell badge
                const unreadCount = fvNotifs.filter(n => !n.isRead).length;
                updateBellBadge(unreadCount);
            }
            showFarmerToast('Success', 'Notification marked as read', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error marking as read:", error);
        showFarmerToast('Error', 'Failed to mark as read', 'error');
    }
}

// Delete single notification
async function deleteNotification(notificationId) {
    if (!confirm('Delete this notification?')) return;
    
    try {
        const response = await authFetch(`${NOTIF_API_BASE}/DeleteNotification`, {
            method: 'POST',
            body: JSON.stringify(notificationId)
        });
        const data = await response.json();
        if (data.success) {
            // Remove from local array
            fvNotifs = fvNotifs.filter(n => n.id != notificationId);
            renderNotifs();
            
            // Update bell badge
            const unreadCount = fvNotifs.filter(n => !n.isRead).length;
            updateBellBadge(unreadCount);
            
            showFarmerToast('Success', 'Notification deleted', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error deleting notification:", error);
        showFarmerToast('Error', 'Failed to delete notification', 'error');
    }
}

// Mark all as read
async function markAllAsRead() {
    try {
        const response = await authFetch(`${NOTIF_API_BASE}/MarkAllRead`, {
            method: 'POST',
            body: JSON.stringify({})
        });
        const data = await response.json();
        if (data.success) {
            // Update local state
            fvNotifs.forEach(n => n.isRead = true);
            renderNotifs();
            
            // Update bell badge to 0
            updateBellBadge(0);
            
            showFarmerToast('Success', 'All notifications marked as read', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error marking all as read:", error);
        showFarmerToast('Error', 'Failed to mark all as read', 'error');
    }
}

// Clear all notifications
async function clearAllNotifications() {
    if (!confirm('Clear all notifications? This action cannot be undone.')) return;
    
    try {
        const response = await authFetch(`${NOTIF_API_BASE}/ClearAllNotifications`, {
            method: 'POST'
        });
        const data = await response.json();
        if (data.success) {
            fvNotifs = [];
            renderNotifs();
            
            // Update bell badge to 0
            updateBellBadge(0);
            
            showFarmerToast('Success', 'All notifications cleared', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error clearing notifications:", error);
        showFarmerToast('Error', 'Failed to clear notifications', 'error');
    }
}

let fvDrawerOpen = false;
function toggleDrawer() {
    fvDrawerOpen = !fvDrawerOpen;
    document.getElementById('fvSidebar')?.classList.toggle('open', fvDrawerOpen);
    document.getElementById('fvOverlay')?.classList.toggle('open', fvDrawerOpen);
}

function toggleNotifDrawer(e) {
    if (e) e.stopPropagation();
    const drawer = document.getElementById('fvNotif');
    if (drawer) {
        drawer.classList.toggle('open');
        // Refresh notifications when opening drawer
        if (drawer.classList.contains('open')) {
            getNotifications();
        }
    }
}

function fvTab(tab, btn) {
    currentFvTab = tab;
    const tabButtons = document.querySelectorAll('.fv-notif-tabs button');
    if (tabButtons.length > 0) {
        tabButtons.forEach(b => b.classList.remove('active'));
        if (btn) btn.classList.add('active');
    }
    renderNotifs();
}

function renderNotifs() {
    const unreadCount = fvNotifs.filter(n => !n.isRead).length;
    
    // Update bell badge
    updateBellBadge(unreadCount);
    
    // Update unread count in header
    const uel = document.getElementById('fvUnread');
    if (uel) uel.textContent = unreadCount;
    
    const ub = document.getElementById('fvUnreadBadge');
    if (ub) ub.textContent = unreadCount;

    // Filter based on current tab
    const filtered = currentFvTab === 'unread' ? fvNotifs.filter(n => !n.isRead) : fvNotifs;
    const list = document.getElementById('fvNotifList');
    if (!list) return;

    if (!filtered.length) {
        list.innerHTML = `<div class="fv-notif-empty" style="padding:32px;text-align:center;font-size:13px;color:var(--fv-muted)">🔔 No notifications</div>`;
        return;
    }
    
    list.innerHTML = filtered.map(n => `
        <div class="fv-notif-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
            <div class="fv-notif-content" onclick="handleNotificationClick(${n.id}, '${escapeHtml(n.redirectUrl || '')}')">
                <div class="fv-notif-icon">
                    <i class="fi fi-rr-bell" style="font-size:18px;color:var(--primary)"></i>
                </div>
                <div style="flex:1">
                    <div style="font-size:13px;font-weight:600;">${escapeHtml(n.title || 'Notification')}</div>
                    <div style="font-size:12px;color:var(--text-muted); margin-top:4px;">${escapeHtml(n.message || '')}</div>
                    <div style="font-size:10px;color:#9ca3af; margin-top:4px;">${formatDate(n.createdAt)}</div>
                </div>
            </div>
            <div class="fv-notif-actions">
                ${!n.isRead ? `
                    <button onclick="event.stopPropagation(); markNotificationAsRead(${n.id})" 
                            class="fv-notif-btn" title="Mark as read">
                        ✓
                    </button>
                ` : ''}
                <button onclick="event.stopPropagation(); deleteNotification(${n.id})" 
                        class="fv-notif-btn" title="Delete">
                    ✕
                </button>
            </div>
        </div>
    `).join('');
}

// Handle notification click - mark as read and redirect
async function handleNotificationClick(notificationId, redirectUrl) {
    // Mark as read if not already read
    const notification = fvNotifs.find(n => n.id == notificationId);
    if (notification && !notification.isRead) {
        await markNotificationAsRead(notificationId);
    }
    
    // Redirect if URL provided
    if (redirectUrl && redirectUrl !== '#') {
        window.location.href = redirectUrl;
    }
}

// Format date for display
function formatDate(dateString) {
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

// Toast notification
window.showFarmerToast = function(title, message, type = 'success') {
    let container = document.getElementById('fvToasts');
    if (!container) {
        // Create container if it doesn't exist
        container = document.createElement('div');
        container.id = 'fvToasts';
        container.className = 'fv-toast-container';
        document.body.appendChild(container);
    }
    
    const toast = document.createElement('div');
    toast.className = `fv-toast ${type}`;
    toast.innerHTML = `
        <div style="flex:1">
            <div style="font-weight:600;font-size:13px;">${escapeHtml(title)}</div>
            ${message ? `<div style="font-size:12px;color:var(--fv-muted)">${escapeHtml(message)}</div>` : ''}
        </div>
        <button onclick="this.closest('.fv-toast').remove()" style="background:none;border:none;cursor:pointer;color:#9ca3af;font-size:16px;">✕</button>
    `;
    container.appendChild(toast);
    setTimeout(() => toast.remove(), 4500);
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

// Escape HTML to prevent XSS
function escapeHtml(s) {
    if (!s) return '';
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

// Backward compatibility for old toast function
window.showFvToast = window.showToast = function(message, type = 'success') {
    window.showFarmerToast(message, '', type);
};

// Initialize on DOM load
document.addEventListener('DOMContentLoaded', async () => {
    console.log("Farmer layout initialized");
    
    // First, get unread count for bell badge (lightweight)
    await getUnreadCount();
    
    // Then load all notifications
    await getNotifications();
    
    // Auto-refresh unread count every 30 seconds
    setInterval(async () => {
        await getUnreadCount();
        await getNotifications();
    }, 30000);

    // Close notification drawer when clicking outside
    document.addEventListener('click', e => {
        const notif = document.getElementById('fvNotif');
        const bellBtn = document.getElementById('fvBellBtn');
        if (notif?.classList.contains('open') && !notif.contains(e.target) && !bellBtn?.contains(e.target)) {
            notif.classList.remove('open');
        }
    });
    
    // Close sidebar when clicking outside
    document.addEventListener('click', e => {
        const sidebar = document.getElementById('fvSidebar');
        const menuBtn = document.getElementById('fvMenuBtn');
        if (sidebar?.classList.contains('open') && !sidebar.contains(e.target) && !menuBtn?.contains(e.target)) {
            sidebar.classList.remove('open');
        }
    });
});

// Expose functions globally
window.fvMarkAllRead = markAllAsRead;
window.fvClearAll = clearAllNotifications;
window.markNotificationAsRead = markNotificationAsRead;
window.deleteNotification = deleteNotification;
window.toggleNotifDrawer = toggleNotifDrawer;
window.toggleDrawer = toggleDrawer;
window.fvTab = fvTab;
window.getUnreadCount = getUnreadCount;

console.log("Farmer notification system loaded");