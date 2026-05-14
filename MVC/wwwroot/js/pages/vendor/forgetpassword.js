/**
 * Vendor Forget Password Logic
 * Handles OTP requesting and password reset via Kendo UI Forms.
 */

(function ($) {
    const API_BASE = "http://localhost:5020";

    // --- INITIALIZATION ---
    $(document).ready(function () {
        initNotifications();
        buildRequestForm();
        buildResetForm();

        // Navigation back to Step 1
        $("#backToStep1").on("click", function (e) {
            e.preventDefault();
            $("#resetPasswordSection").fadeOut(200, function () {
                $("#requestOtpSection").fadeIn(200);
            });
        });

        // Resend OTP logic
        $("#resendOtpBtn").on("click", function() {
            const email = localStorage.getItem("resetEmail");
            if(email) {
                sendOtpRequest(email);
            }
        });
    });

    function initNotifications() {
        const config = {
            allowHideAfter: 3000,
            width: 350,
            position: { pinned: true, top: 30, right: 30 },
            stacking: "down"
        };
        $("#notificationRequest").kendoNotification(config);
        $("#notificationReset").kendoNotification(config);
    }

    // --- STEP 1: REQUEST LOGIC ---
    function buildRequestForm() {
        $("#requestOtpForm").kendoForm({
            formData: { Email: "" },
            items: [{
                field: "Email",
                label: "Email Address",
                validation: { required: true, email: true }
            }],
            buttonsTemplate: '<button type="submit" class="k-button k-button-lg k-button-solid-success">Send OTP →</button>',
            submit: function (e) {
                e.preventDefault();
                sendOtpRequest(e.model.Email);
            }
        });
    }

    function sendOtpRequest(emailValue) {
        $.ajax({
            url: API_BASE + "/api/auth/forgot-password",
            method: "POST",
            contentType: "application/json",
            data: JSON.stringify({ email: emailValue }),
            success: function () {
                localStorage.setItem("resetEmail", emailValue);
                const notification = $("#notificationRequest").getKendoNotification();
                if (notification) notification.show("OTP Sent to " + emailValue, "success");
                
                $("#requestOtpSection").fadeOut(300, function () {
                    $("#resetPasswordSection").fadeIn(300);
                });
            },
            error: function () {
                const notification = $("#notificationRequest").getKendoNotification();
                if (notification) notification.show("Unable to send OTP. Please try again.", "error");
            }
        });
    }

    // --- STEP 2: RESET LOGIC ---
    function buildResetForm() {
        $("#verifyResetForm").kendoForm({
            formData: { otp: "", newPassword: "", confirmPassword: "" },
            items: [
                {
                    field: "otp",
                    label: "Verification Code",
                    validation: { required: true }
                },
                {
                    field: "newPassword",
                    label: "New Password",
                    editor: function(container, options) {
                        $('<input type="password" class="k-textbox" name="' + options.field + '" required minlength="8" />')
                            .appendTo(container);
                    },
                    validation: { required: true }
                },
                {
                    field: "confirmPassword",
                    label: "Confirm New Password",
                    editor: function(container, options) {
                        $('<input type="password" class="k-textbox" name="' + options.field + '" required />')
                            .appendTo(container);
                    },
                    validation: {
                        required: true,
                        validatePasswordMatch: function(input) {
                            if (input.is("[name='confirmPassword']")) {
                                const form = $("#verifyResetForm").data("kendoForm");
                                const pass = form.editable.options.model.newPassword;
                                if (input.val() !== pass) {
                                    input.attr("data-validatePasswordMatch-msg", "Passwords do not match!");
                                    return false;
                                }
                            }
                            return true;
                        }
                    }
                }
            ],
            buttonsTemplate: '<button type="submit" class="k-button k-button-lg k-button-solid-success">Reset Password ✓</button>',
            submit: function (e) {
                e.preventDefault();

                $.ajax({
                    url: API_BASE + "/api/auth/reset-password",
                    method: "POST",
                    contentType: "application/json",
                    data: JSON.stringify({
                        email: localStorage.getItem("resetEmail"),
                        otp: e.model.otp,
                        newPassword: e.model.newPassword,
                        confirmPassword: e.model.confirmPassword
                    }),
                    success: function (res) {
                        const notification = $("#notificationReset").getKendoNotification();
                        if (notification) notification.show("Success! Redirecting to login...", "success");
                        setTimeout(() => {
                            window.location.href = "/vendor/login";
                        }, 2000);
                    },
                    error: function (xhr) {
                        const errorMsg = xhr.responseJSON?.message || "Invalid OTP or request expired.";
                        const notification = $("#notificationReset").getKendoNotification();
                        if (notification) notification.show(errorMsg, "error");
                    }
                });
            }
        });
    }

})(jQuery);
