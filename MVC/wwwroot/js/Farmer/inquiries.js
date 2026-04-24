/**
 * Farmer Inquiries — inquiries.js
 * JWT Bearer token is read from cookie: "authToken"
 * Multi-farmer support enabled
 */

$(document).ready(function () {

    // AUTH HELPERS

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function authHeaders() {
        var token = getToken();
        if (!token) { redirectToLogin(); return {}; }
        return { "Authorization": "Bearer " + token };
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

    // Abort if token missing
    if (!getToken()) {
        redirectToLogin();
        return;
    }

    console.log("Farmer ID:", window.FARMER_ID);


    // 1. INQUIRIES KPIs

    $.ajax({
        url: `${window.API_BASE}/${window.FARMER_ID}/inquiries`,
        type: "GET",
        headers: authHeaders(),

        success: function (res) {
            if (res.success) {
                var data = res.data || [];
                $("#kpiTotal").removeClass("skeleton").text(data.length);
                $("#kpiPending").removeClass("skeleton").text(data.filter(x => x.status === "open").length);
                $("#kpiSolved").removeClass("skeleton").text(data.filter(x => x.status === "closed").length);
            }
        },

        error: function (xhr) {
            if (handleUnauthorized(xhr)) return;
            console.error("Failed to load inquiries KPIs");
        }
    });


    // 2. TEXTAREA WIDGET

    $("#messageBody").kendoTextArea({ rows: 5 });

    $("#btnResetInquiry").on("click", function () {
        $("#inquiryForm")[0].reset();
    });

    $("#btnSubmitInquiry").on("click", submitInquiry);
});


// SUBMIT INQUIRY

function submitInquiry() {

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
        Department: $("#inquiryType").val(),
        Subject: $("#subject").val(),
        Details: $("#messageBody").val()
    };

    if (!payload.Department || !payload.Subject || !payload.Details) {
        fbAlert("Please fill out Department, Subject, and Details.", "Missing Fields");
        return;
    }

    $.ajax({
        url: `${window.API_BASE}/inquiries/submit`,
        type: "POST",
        contentType: "application/json",
        headers: { "Authorization": "Bearer " + token },
        data: JSON.stringify(payload),

        success: function () {
            fbSuccess("Ticket Submitted", "Your ticket has been logged. The relevant department will respond shortly.");
            $("#inquiryForm")[0].reset();
            $("#messageBody").data("kendoTextArea").value("");
            reloadInquiryKPIs();
        },

        error: function (xhr) {
            if (xhr.status === 401 || xhr.status === 403) {
                window.location.href = "/Farmer/Login";
                return;
            }
            fbError("Submission Failed", "Could not submit your inquiry. Please try again.");
        }
    });
}


// RELOAD KPIs AFTER SUBMIT

function reloadInquiryKPIs() {

    function getToken() {
        var match = document.cookie.match(/(?:^|;\s*)authToken=([^;]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    var token = getToken();

    $.ajax({
        url: `${window.API_BASE}/${window.FARMER_ID}/inquiries`,
        type: "GET",
        headers: { "Authorization": "Bearer " + token },

        success: function (res) {
            if (res.success) {
                var data = res.data || [];
                $("#kpiTotal").removeClass("skeleton").text(data.length);
                $("#kpiPending").removeClass("skeleton").text(data.filter(x => x.status === "open").length);
                $("#kpiSolved").removeClass("skeleton").text(data.filter(x => x.status === "closed").length);
            }
        }
    });
}