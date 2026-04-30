const API_BASE = "http://localhost:5020/api/Admin";
let allNotifs = [];
let currentTab = 'all';

$(document).ready(function () {
    const bodyTitle = $("body").attr("data-page-title");
    const docTitle = (document.title || "").split("-")[0].trim();
    const pageTitle = bodyTitle || docTitle || "Admin";

    initializeKendoUI(pageTitle);
    loadLiveNotifications();
    setInterval(loadLiveNotifications, 5000);

    // Global click listener to close drawer when clicking outside
    $(document).click(function (e) {
        if (!$(e.target).closest('#notifDrawer, #notif-toggle-btn').length) {
            $("#notifDrawer").removeClass('open');
            $("body").removeClass("no-scroll");
        }
    });
});

function initializeKendoUI(title) {
    // 1. AppBar Initialization
    $("#appbar").kendoAppBar({
        items: [
            {
                template: '<button id="menu-toggle" class="topbar-btn"><lord-icon src="https://cdn.lordicon.com/izqdfqdl.json" trigger="hover" style="width:28px;height:28px;"></lord-icon></button>',
                type: "contentItem"
            },
            {
                template: `<div class="ms-3"><span class="topbar-title">${title}</span></div>`,
                type: "contentItem"
            },
            { width: 0, type: "spacer" },
            {
                template: '<button class="topbar-btn me-3" id="notif-toggle-btn" onclick="toggleNotifs(event)"><lord-icon src="https://cdn.lordicon.com/ahxaipjb.json" trigger="hover" style="width:28px;height:28px"></lord-icon><span class="notif-badge">0</span></button>',
                type: "contentItem"
            },
            {
                template: '<div class="topbar-avatar" onclick="location.href=\'/Admin/Dashboard\'">A</div>',
                type: "contentItem"
            }
        ]
    });

    // 2. Sidebar Drawer Setup
    const adminNav = [
        { icon: 'fa-gauge', text: 'Dashboard', url: '/Admin/Dashboard' },
        { icon: 'fa-wheat-awn', text: 'Farmers', url: '/Admin/FarmerManagment' },
        { icon: 'fa-store', text: 'Vendors', url: '/Admin/Vendor' },
        { icon: 'fa-hard-hat', text: 'Field Officers', url: '/Admin/FieldOfficers' },
        { icon: 'fa-user-plus', text: 'Add Officer', url: '/Admin/FOCreate' },
        { icon: 'fa-warehouse', text: 'Warehouses', url: '/Admin/Warehouse' },
        { icon: 'fa-seedling', text: 'Crop Listings', url: '/Admin/Catalog' }
    ];

    let drawerTemplate = `<div class="sidebar-wrapper" style="display:flex; flex-direction:column; height:100%;">
        <div class="sidebar-header"><div class="sidebar-logo"><img src="/Logo/Logo_Without_Bg.svg" width="45"><span>FarmBridge</span></div></div>
        <div class="sidebar-user" style="padding:20px; display:flex; align-items:center; color:white; border-bottom:1px solid rgba(255,255,255,0.1);">
            <div class="topbar-avatar" style="background:var(--green-mid)">A</div>
            <div class="ms-2"><b>Administrator</b><br><small>Admin Panel</small></div>
        </div>
        <ul class="list-unstyled flex-grow-1 mt-3">`;

    adminNav.forEach(item => {
        const isActive = window.location.pathname.toLowerCase().includes(item.url.toLowerCase());
        drawerTemplate += `<li class="k-drawer-item ${isActive ? 'k-selected' : ''}" data-url="${item.url}">
            <i class="fa ${item.icon}"></i><span class="ms-2">${item.text}</span></li>`;
    });

    drawerTemplate += `</ul>
        <div class="sidebar-footer">
            <a href="/Auth/Logout" class="logout-btn"><i class="fa fa-arrow-right-from-bracket"></i> Logout</a>
        </div></div>`;

    if (!$("#admin-sidebar").length) {
        $("body").append(`
            <aside id="admin-sidebar" class="admin-sidebar" aria-hidden="true">
                ${drawerTemplate}
            </aside>
            <div id="admin-sidebar-backdrop" class="admin-sidebar-backdrop"></div>
        `);
    }

    $("body").removeClass("admin-drawer-open");

    $(document).on("click", "#menu-toggle", () => {
        toggleAdminDrawer();
    });

    $(document).on("keydown", (e) => {
        if (e.key === "Escape") {
            closeAdminDrawer();
            $("#notifDrawer").removeClass("open");
            $("body").removeClass("no-scroll");
        }
    });

    $(document).on("click", "#admin-sidebar .k-drawer-item", function () {
        const url = $(this).data("url");
        closeAdminDrawer();
        if (url) {
            window.location.href = url;
        }
    });

    $(document).on("click", "#admin-sidebar-backdrop", () => {
        closeAdminDrawer();
    });
}

function openAdminDrawer() {
    $("body").addClass("admin-drawer-open");
    $("#admin-sidebar").attr("aria-hidden", "false");
}

function closeAdminDrawer() {
    $("body").removeClass("admin-drawer-open");
    $("#admin-sidebar").attr("aria-hidden", "true");
}

function toggleAdminDrawer() {
    if ($("body").hasClass("admin-drawer-open")) {
        closeAdminDrawer();
    } else {
        openAdminDrawer();
    }
}

/* Notification Logic */
function toggleNotifs(e) {
    if (e) e.stopPropagation();
    $("#notifDrawer").toggleClass('open');
    $("body").toggleClass("no-scroll", $("#notifDrawer").hasClass('open'));
}

function changeTab(tab) {
    currentTab = tab;
    $('.notif-tab').removeClass('active');
    $(event.target).addClass('active');
    renderNotifications();
}

function loadLiveNotifications() {
    $.get(`${API_BASE}/GetNotifications`, (res) => {
        if (res.success) {
            allNotifs = res.data;
            renderNotifications();
        }
    });
}

function renderNotifications() {
    const unreadCount = allNotifs.filter(x => !x.isRead).length;
    $("#unreadCountDisplay, #tabUnreadCount").text(unreadCount);

    const $badge = $(".notif-badge");
    if (unreadCount > 0) {
        $badge.text(unreadCount > 99 ? '99+' : unreadCount).css("display", "flex").addClass("pulse");
    } else {
        $badge.hide().removeClass("pulse");
    }

    const filtered = currentTab === 'unread' ? allNotifs.filter(x => !x.isRead) : allNotifs;
    if (filtered.length === 0) {
        $("#notifList").html('<div class="p-5 text-center text-muted"><p>No notifications</p></div>');
        return;
    }

    let html = "";
    filtered.forEach(n => {
        // 1. Determine Badge Color & Icons
        const isInfo = (n.type || "").toLowerCase() === "Info";

        let badgeClass = "bg-primary"; // Default Blue (Info)
        let iconColor = "#1a5c2a";    // Default Green
        let iconPath = "yhtmwrae.json"; // Default Info Icon

         if (isInfo) {
            badgeClass = "bg-info";   // Light Blue (Modern Dashboard Style)
            iconColor = "#0288d1";    // Professional Blue
        }

        const timeDisplay = n.createdAt ? new Date(n.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : "Just now";

        html += `
        <div class="notif-item ${n.isRead ? '' : 'unread'}" onclick="location.href='${n.redirectUrl}'">
            <div class="notif-icon-box">
                <lord-icon src="https://cdn.lordicon.com/${iconPath}" trigger="hover" colors=" :${iconColor}" style="width:32px;height:32px"></lord-icon>
            </div>
            <div class="notif-body flex-grow-1">
                <div class="notif-title-row d-flex justify-content-between align-items-center">
                    <span class="notif-title" style="font-weight:600;">${n.title}</span>
                    <span class="badge ${badgeClass} notif-type-tag" style="font-size:9px; text-transform:uppercase;">${n.type || 'INFO'}</span>
                </div>
                <p class="notif-desc mb-1" style="font-size:13px; color:#555;">${n.message}</p>
                <div class="d-flex justify-content-between align-items-center mt-2">
                    <span class="notif-time text-muted" style="font-size:10px;">${timeDisplay}</span>
                    <div class="notif-actions d-flex align-items-center" style="gap: 12px; min-width: 60px; justify-content: flex-end;">
                        ${!n.isRead ? `
                        <button title="Mark as read" class="btn btn-sm p-0 m-0 d-flex align-items-center justify-content-center" onclick="markOneRead(event, '${n.id}')" style="border:none; background:none;">
                            <lord-icon src="https://cdn.lordicon.com/uvofdfal.json" trigger="hover" state="morph-tick" colors="primary:#1a5c2a" style="width:22px;height:22px"></lord-icon>
                        </button>` : ''}
                        <button title="Delete" class="btn btn-sm p-0 m-0 d-flex align-items-center justify-content-center" onclick="deleteOne(event, '${n.id}')" style="border:none; background:none;">
                            <lord-icon src="https://cdn.lordicon.com/oqeixref.json" trigger="hover" colors="primary:#d32f2f" style="width:22px;height:22px"></lord-icon>
                        </button>
                    </div>
                </div>
            </div>
        </div>`;
    });
    $("#notifList").html(html);
}

function markOneRead(e, id) { e.stopPropagation(); $.post(`${API_BASE}/MarkAsRead`, { id }, loadLiveNotifications); }
function deleteOne(e, id) { e.stopPropagation(); Swal.fire({title:"Delete this?", icon:"warning", showCancelButton:true, confirmButtonColor:"#10b981", confirmButtonText:"Yes"}).then(r=>{if(r.isConfirmed) $.post(`${API_BASE}/DeleteNotification`, { id }, loadLiveNotifications);}); }
function markAllAsRead() { $.post(`${API_BASE}/MarkAllRead`, loadLiveNotifications); }
function clearAllNotifs() { Swal.fire({title:"Clear all?", icon:"warning", showCancelButton:true, confirmButtonColor:"#10b981", confirmButtonText:"Yes"}).then(r=>{if(r.isConfirmed) $.post(`${API_BASE}/ClearAllNotifications`, loadLiveNotifications);}); }
