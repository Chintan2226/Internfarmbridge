/* FarmBridge — Admin Layout JS */

let admNotifs = [], admCurrentTab = 'all';
const API_BASE = window.API_BASE || 'http://localhost:5020/api/Admin';
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
        window.location.href = '/Auth/Login';
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
        window.location.href = '/Auth/Login';
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
    const dot = document.getElementById('admBellDot');
    if (dot) {
        dot.hidden = count === 0;
    }
    
    // Update badge number if exists
    const badge = document.getElementById('admBellBadge');
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
            admNotifs = data.data || [];
            renderAdmNotifs();
            
            // Update bell badge with unread count
            const unreadCount = admNotifs.filter(n => !n.isRead).length;
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
            const notification = admNotifs.find(n => n.id == notificationId);
            if (notification) {
                notification.isRead = true;
                renderAdmNotifs();
                
                // Update bell badge
                const unreadCount = admNotifs.filter(n => !n.isRead).length;
                updateBellBadge(unreadCount);
            }
            showAdminToast('Success', 'Notification marked as read', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error marking as read:", error);
        showAdminToast('Error', 'Failed to mark as read', 'error');
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
            admNotifs = admNotifs.filter(n => n.id != notificationId);
            renderAdmNotifs();
            
            // Update bell badge
            const unreadCount = admNotifs.filter(n => !n.isRead).length;
            updateBellBadge(unreadCount);
            
            showAdminToast('Success', 'Notification deleted', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error deleting notification:", error);
        showAdminToast('Error', 'Failed to delete notification', 'error');
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
            admNotifs.forEach(n => n.isRead = true);
            renderAdmNotifs();
            
            // Update bell badge to 0
            updateBellBadge(0);
            
            showAdminToast('Success', 'All notifications marked as read', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error marking all as read:", error);
        showAdminToast('Error', 'Failed to mark all as read', 'error');
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
            admNotifs = [];
            renderAdmNotifs();
            
            // Update bell badge to 0
            updateBellBadge(0);
            
            showAdminToast('Success', 'All notifications cleared', 'success');
        }
        return data;
    } catch (error) {
        console.error("Error clearing notifications:", error);
        showAdminToast('Error', 'Failed to clear notifications', 'error');
    }
}

// Toggle sidebar drawer
function toggleAdmDrawer() {
    const sb = document.getElementById('admSidebar');
    if (sb) sb.classList.toggle('open');
}

document.addEventListener('DOMContentLoaded', async () => {
    console.log("Admin layout initialized");
    
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
        const notif = document.getElementById('admNotif');
        const bellBtn = document.getElementById('admBellBtn');
        if (notif?.classList.contains('open') && !notif.contains(e.target) && !bellBtn?.contains(e.target)) {
            notif.classList.remove('open');
        }
    });
    
    // Close sidebar when clicking outside
    document.addEventListener('click', e => {
        const sidebar = document.getElementById('admSidebar');
        const menuBtn = document.getElementById('admMenuBtn');
        if (sidebar?.classList.contains('open') && !sidebar.contains(e.target) && !menuBtn?.contains(e.target)) {
            sidebar.classList.remove('open');
        }
    });
});

function toggleAdmNotif(e) {
    if (e) e.stopPropagation();
    const drawer = document.getElementById('admNotif');
    if (drawer) {
        drawer.classList.toggle('open');
        // Refresh notifications when opening drawer
        if (drawer.classList.contains('open')) {
            getNotifications();
        }
    }
}

function admTab(tab, btn) {
    admCurrentTab = tab;
    document.querySelectorAll('.adm-notif-tabs button').forEach(b => b.classList.remove('active'));
    if (btn) btn.classList.add('active');
    renderAdmNotifs();
}

function renderAdmNotifs() {
    const unreadCount = admNotifs.filter(n => !n.isRead).length;
    
    // Update bell badge
    updateBellBadge(unreadCount);
    
    // Update unread count in header
    const uel = document.getElementById('admUnread');
    if (uel) uel.textContent = unreadCount;
    
    const ub = document.getElementById('admUnreadBadge');
    if (ub) ub.textContent = unreadCount;

    // Filter based on current tab
    const filtered = admCurrentTab === 'unread' ? admNotifs.filter(n => !n.isRead) : admNotifs;
    const list = document.getElementById('admNotifList');
    if (!list) return;

    if (!filtered.length) {
        list.innerHTML = `<div style="padding:32px;text-align:center;font-size:13px;color:#6b7280">No notifications</div>`;
        return;
    }
    
    list.innerHTML = filtered.map(n => `
        <div class="adm-notif-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}">
            <div class="adm-notif-content" onclick="handleNotificationClick(${n.id}, '${escapeHtml(n.redirectUrl || '')}')">
                <div class="adm-notif-icon">
                    <i class="fi fi-rr-bell" style="font-size:18px;color:#10b981"></i>
                </div>
                <div style="flex:1">
                    <div style="font-size:13px;font-weight:600;">${escapeHtml(n.title)}</div>
                    <div style="font-size:12px;color:#6b7280; margin-top:4px;">${escapeHtml(n.message || '')}</div>
                    <div style="font-size:10px;color:#9ca3af; margin-top:4px;">${formatDate(n.createdAt)}</div>
                </div>
            </div>
            <div class="adm-notif-actions">
                ${!n.isRead ? `
                    <button onclick="event.stopPropagation(); markNotificationAsRead(${n.id})" 
                            class="adm-notif-btn" title="Mark as read">
                        ✓
                    </button>
                ` : ''}
                <button onclick="event.stopPropagation(); deleteNotification(${n.id})" 
                        class="adm-notif-btn" title="Delete">
                    ✕
                </button>
            </div>
        </div>
    `).join('');
}

// Handle notification click - mark as read and redirect
async function handleNotificationClick(notificationId, redirectUrl) {
    // Mark as read if not already read
    const notification = admNotifs.find(n => n.id == notificationId);
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
window.showAdminToast = function(title, message, type = 'success') {
    const container = document.getElementById('admToasts');
    if (!container) {
        // Create container if it doesn't exist
        const newContainer = document.createElement('div');
        newContainer.id = 'admToasts';
        newContainer.className = 'adm-toast-container';
        document.body.appendChild(newContainer);
    }
    
    const toastContainer = document.getElementById('admToasts');
    const toast = document.createElement('div');
    toast.className = `adm-toast ${type}`;
    toast.innerHTML = `
        <div style="flex:1">
            <div style="font-weight:600;font-size:13px;">${escapeHtml(title)}</div>
            ${message ? `<div style="font-size:12px;color:#6b7280">${escapeHtml(message)}</div>` : ''}
        </div>
        <button onclick="this.closest('.adm-toast').remove()" style="background:none;border:none;cursor:pointer;color:#9ca3af;font-size:16px;">✕</button>
    `;
    toastContainer.appendChild(toast);
    setTimeout(() => toast.remove(), 4500);
};

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

// Backward compatibility
window.showToast = function(msg, type = 'success') {
    window.showAdminToast(msg, '', type);
};

// Expose functions globally
window.admMarkAllRead = markAllAsRead;
window.admClearAll = clearAllNotifications;
window.markNotificationAsRead = markNotificationAsRead;
window.deleteNotification = deleteNotification;
window.toggleAdmNotif = toggleAdmNotif;
window.toggleAdmDrawer = toggleAdmDrawer;
window.admTab = admTab;
window.getUnreadCount = getUnreadCount;

console.log("Admin notification system loaded");