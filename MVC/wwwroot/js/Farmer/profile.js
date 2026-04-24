/**
 * Farmer Profile JS
 * Handles profile loading, saving, and photo management.
 */

// Auth Helpers
function getToken() {
    var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
    return match ? decodeURIComponent(match[1]) : null;
}

function authHeaders() {
    var token = getToken();
    if (!token) {
        window.location.href = "/Farmer/Login";
        return {};
    }
    return { "Authorization": "Bearer " + token };
}

function handleUnauthorized(xhr) {
    if (!xhr || xhr.status === 401 || xhr.status === 403 || !getToken()) {
        window.location.href = "/Farmer/Login";
        return true;
    }
    return false;
}

// Initialization
$(document).ready(function () {
    if (!getToken() || !window.FARMER_ID) {
        window.location.href = "/Farmer/Login";
        return;
    }
    loadProfile();
    loadProfileKpis();
});

// Load Profile Data
function loadProfile() {
    $.ajax({
        url: `${window.API_BASE}/${window.FARMER_ID}/profile`,
        type: "GET",
        headers: authHeaders(),
        success: function (res) {
            if (!res.success) return;
            var p = res.data;

            // Sidebar info
            var displayName = p.fullName || window.FARMER_NAME || "FarmBridge User";
            $("#farmerNameLabel").removeClass("skeleton").text(displayName);
            $("#farmerMetaLabel").removeClass("skeleton").text([p.district, p.state].filter(Boolean).join(", ") || fbT("profile_location_not_set"));

            // Avatar placeholder
            var firstLetter = displayName.charAt(0).toUpperCase();
            $("#avatarPlaceholder").html(`<span style="font-size: 32px; color: #10b981; font-weight: 800;">${firstLetter}</span>`);

            // Bind fields
            $("#fullName").removeClass("skeleton").val(p.fullName || "");
            $("#phoneNo").removeClass("skeleton").val(p.phone || "");
            $("#farmState").removeClass("skeleton").val(p.state || "");
            $("#farmDistrict").removeClass("skeleton").val(p.district || "");
            $("#address").removeClass("skeleton").val(p.address || "");
            $("#bankName").removeClass("skeleton").val(p.bankName || "");
            $("#branchName").removeClass("skeleton").val(p.branchName || "");
            $("#acctName").removeClass("skeleton").val(p.accountHolderName || "");
            $("#acctNo").removeClass("skeleton").val(p.accountNumber || "");
            $("#ifscCode").removeClass("skeleton").val(p.ifscCode || "");
            $("#upiId").removeClass("skeleton").val(p.upiId || "");
            $("#acctType").removeClass("skeleton");
            if (p.accountType) $("#acctType").val(p.accountType);

            // Photo management
            window.CURRENT_PHOTO_URL = p.imageUrl || "";
            if (p.imageUrl) {
                $("#farmerPhoto").attr("src", p.imageUrl).show();
                $("#avatarPlaceholder").hide();
                $("#btnDeletePhoto").show();
            } else {
                $("#farmerPhoto").hide();
                $("#avatarPlaceholder").show();
                $("#btnDeletePhoto").hide();
            }
        },
        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            fbError(fbT("profile_load_error_title"), fbT("profile_load_error_msg"));
        }
    });
}

// Load Sidebar KPIs
function loadProfileKpis() {
    $.ajax({
        url: `${window.API_BASE}/${window.FARMER_ID}/dashboard`,
        type: "GET",
        headers: authHeaders(),
        success: function (res) {
            if (!res.success || !res.data) return;
            var kpis = res.data.kpis;
            if (kpis) {
                $("#statActiveListings").removeClass("skeleton").text(kpis.totalCropsListed || 0);
                $("#statQCPassed").removeClass("skeleton").text(kpis.confirmedQcSlots || 0);
            }
        }
    });
}

// Save Profile Info
function saveProfile() {
    var token = getToken();
    if (!token) return;

    var fullName = $("#fullName").val().trim();
    if (!fullName) {
        fbAlert(fbT("profile_val_name_req"), fbT("profile_val_error_title"));
        return;
    }

    var state = $("#farmState").val().trim();
    var district = $("#farmDistrict").val().trim();
    var address = $("#address").val().trim();

    if (!state) { fbAlert(fbT("profile_val_state_req"), fbT("profile_val_error_title")); return; }
    if (!district) { fbAlert(fbT("profile_val_district_req"), fbT("profile_val_error_title")); return; }
    if (!address) { fbAlert(fbT("profile_val_address_req"), fbT("profile_val_error_title")); return; }

    var bankName = $("#bankName").val().trim();
    var branchName = $("#branchName").val().trim();
    var acctName = $("#acctName").val().trim();
    var acctNo = $("#acctNo").val().trim();
    var ifscCode = $("#ifscCode").val().trim().toUpperCase();
    var upiId = $("#upiId").val().trim();
    var acctType = $("#acctType").val();

    if (bankName || acctName || acctNo || ifscCode) {
        if (!bankName || !acctName || !acctNo || !ifscCode || !acctType) {
            fbAlert(fbT("profile_val_bank_all_req"), fbT("profile_val_error_title"));
            return;
        }
        if (!/^[A-Z]{4}0[A-Z0-9]{6}$/.test(ifscCode)) {
            fbAlert(fbT("profile_val_ifsc_invalid"), fbT("profile_val_error_title"));
            return;
        }
    }

    var payload = {
        FarmerId: window.FARMER_ID,
        FullName: fullName,
        Phone: $("#phoneNo").val().trim(),
        State: state,
        District: district,
        Address: address,
        BankName: bankName,
        BranchName: branchName,
        AccountHolderName: acctName,
        AccountNumber: acctNo,
        IfscCode: ifscCode,
        UpiId: upiId,
        AccountType: acctType,
        ImageUrl: window.CURRENT_PHOTO_URL || ""
    };

    $.ajax({
        url: `${window.API_BASE}/profile/update`,
        type: "PUT",
        contentType: "application/json",
        headers: { "Authorization": "Bearer " + token },
        data: JSON.stringify(payload),
        success: function () {
            fbSuccess(fbT("profile_updated_title"), fbT("profile_updated_success"));
            loadProfile();
            loadProfileKpis();
        },
        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            fbError(fbT("profile_save_failed_title"), fbT("profile_save_failed_msg"));
        }
    });
}

// Photo Handlers
function handlePhotoUpload(input) {
    if (!input.files || !input.files[0]) return;
    var file = input.files[0];
    if (file.size > 5 * 1024 * 1024) {
        fbAlert("File size exceeds 5MB limit.", fbT("profile_upload_failed_title"));
        return;
    }

    var formData = new FormData();
    formData.append("file", file);
    fbLoading(true, fbT("profile_upload_loading"));

    $.ajax({
        url: `${window.API_BASE}/profile/photo/upload`,
        type: "POST",
        headers: authHeaders(),
        data: formData,
        processData: false,
        contentType: false,
        success: function (res) {
            fbLoading(false);
            if (res.success && res.imageUrl) {
                window.CURRENT_PHOTO_URL = res.imageUrl;
                $("#farmerPhoto").attr("src", res.imageUrl).show();
                $("#avatarPlaceholder").hide();
                $("#btnDeletePhoto").show();
                saveProfile();
            }
        },
        error: function () {
            fbLoading(false);
            fbError(fbT("profile_upload_failed_title"), fbT("profile_upload_failed_msg"));
        }
    });
}

function deletePhoto() {
    if (!window.CURRENT_PHOTO_URL) return;
    fbConfirm(fbT("profile_delete_confirm_title"), fbT("profile_delete_confirm_msg"), function() {
        fbLoading(true, fbT("profile_delete_loading"));
        $.ajax({
            url: `${window.API_BASE}/profile/photo/delete?imageUrl=${encodeURIComponent(window.CURRENT_PHOTO_URL)}`,
            type: "DELETE",
            headers: authHeaders(),
            success: function () {
                fbLoading(false);
                window.CURRENT_PHOTO_URL = "";
                $("#farmerPhoto").hide();
                $("#avatarPlaceholder").show();
                $("#btnDeletePhoto").hide();
                saveProfile();
                fbSuccess(fbT("profile_delete_success_title"), fbT("profile_delete_success_msg"));
            },
            error: function () {
                fbLoading(false);
                fbError(fbT("profile_delete_failed_title"), fbT("profile_delete_failed_msg"));
            }
        });
    });
}

// Logout
function logoutUser() {
    document.cookie = "authToken=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";
    localStorage.clear();
    sessionStorage.clear();
    window.location.href = "/Farmer/Login";
}