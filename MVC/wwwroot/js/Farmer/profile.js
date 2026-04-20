/**
 * Farmer Profile — profile.js
 * JWT Bearer token is read from cookie: "authToken"
 * Multi-farmer support enabled
 */

$(document).ready(function () {

    /* ──────────────────────────────────────────────
       AUTH HELPERS
    ────────────────────────────────────────────── */

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function authHeaders() {

        var token = getToken();

        if (!token) {
            redirectToLogin();
            return {};
        }

        return {
            "Authorization": "Bearer " + token
        };
    }

    function handleUnauthorized(xhr) {

        if (!xhr || xhr.status === 401 || xhr.status === 403 || !getToken()) {
            redirectToLogin();
            return true;
        }

        return false;
    }

    function redirectToLogin() {
        window.location.href = "/Farmer/Login";
    }

    function showLoadError(section) {
        console.error("Failed to load:", section);
    }

    /* Abort if token missing */

    if (!getToken()) {
        redirectToLogin();
        return;
    }

    console.log("Farmer ID:", window.FARMER_ID);


    /* ──────────────────────────────────────────────
       1. LOAD PROFILE DATA
    ────────────────────────────────────────────── */

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/profile`,

        type: "GET",

        headers: authHeaders(),

        success: function (res) {

            if (res.success) {

                var p = res.data;

                /* Sidebar */

                $("#farmerNameLabel")
                    .text(p.fullName || "FarmBridge User");

                $("#farmerMetaLabel")
                    .text((p.district || "") + ", " + (p.state || ""));


                /* Form fields */

                $("#fullName").val(p.fullName);

                $("#phoneNo").val(p.phone);

                $("#farmRegion").val(
                    (p.state || "").toLowerCase()
                );

                $("#specialty").val(p.primaryCrop);

                $("#bankName").val(p.bankName);

                $("#acctName").val(
                    p.accountHolderName
                );

                $("#acctNo").val(
                    p.accountNumber
                );

                $("#ifscCode").val(
                    p.ifscCode
                );
            }

        },

        error: function (xhr) {

            if (handleUnauthorized(xhr)) return;

            showLoadError("profile");

        }

    });

});


/* ──────────────────────────────────────────────
   SAVE PROFILE
────────────────────────────────────────────── */

function saveProfile() {

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    var token = getToken();

    if (!token) {
        window.location.href = "/Farmer/Login";
        return;
    }

    var payload = {

        FarmerId: window.FARMER_ID,

        FullName: $("#fullName").val(),

        Phone: $("#phoneNo").val(),

        State: $("#farmRegion option:selected").text(),

        District: "Updated via UI",

        PrimaryCrop: $("#specialty").val(),

        BankName: $("#bankName").val(),

        AccountHolderName: $("#acctName").val(),

        AccountNumber: $("#acctNo").val(),

        IfscCode: $("#ifscCode").val()
    };


    $.ajax({

        url: `${window.API_BASE}/profile/update`,

        type: "PUT",

        contentType: "application/json",

        headers: {
            "Authorization": "Bearer " + token
        },

        data: JSON.stringify(payload),

        success: function () {

            kendo.alert(
                "Profile updated successfully!"
            );

            reloadProfile();

        },

        error: function (xhr) {

            if (xhr.status === 401 || xhr.status === 403) {
                window.location.href = "/Farmer/Login";
                return;
            }

            console.error("Failed to update profile");

        }

    });

}


/* ──────────────────────────────────────────────
   RELOAD PROFILE
────────────────────────────────────────────── */

function reloadProfile() {

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    var token = getToken();

    $.ajax({

        url: `${window.API_BASE}/${window.FARMER_ID}/profile`,

        type: "GET",

        headers: {
            "Authorization": "Bearer " + token
        },

        success: function (res) {

            if (res.success) {

                var p = res.data;

                $("#farmerNameLabel")
                    .text(p.fullName || "FarmBridge User");

                $("#farmerMetaLabel")
                    .text((p.district || "") + ", " + (p.state || ""));

            }

        }

    });

}


/* ──────────────────────────────────────────────
   LOGOUT
────────────────────────────────────────────── */

function logoutUser() {

    document.cookie =
        "authToken=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;";

    window.location.href = "/Farmer/Login";

}