/**
 * Vendor Reset Password Logic
 * Handles the password reset process with Kendo UI Form and strength checking.
 */

(function ($) {
    const API_BASE = "http://localhost:5020";

    // --- INITIALIZATION ---
    $(document).ready(function () {
        const email = window.FB_RESET_EMAIL;
        if (!email) {
            const notification = $("#notificationReset").kendoNotification({
                allowHideAfter: 3000,
                width: 300,
                position: { pinned: true, top: 30, right: 30 }
            }).data("kendoNotification");
            if (notification) notification.show("Email not found. Please try again.", "error");
            setTimeout(() => { window.location.href = "/vendor/login"; }, 2000);
            return;
        }
        
        buildResetForm();
        loadNotification();
    });

    function loadNotification() {
        $("#notificationReset").kendoNotification({
            allowHideAfter: 3000,
            width: 300,
            position: { pinned: true, top: 30, right: 30 }
        });
    }

    function checkPasswordStrength() {
        const password = $("#newPassword").val();
        const strengthDiv = $("#passwordStrength");
        
        if (!password) {
            strengthDiv.html('');
            return;
        }
        
        let strength = 0;
        let message = '';
        let className = '';
        
        if (password.length >= 8) strength++;
        if (password.match(/[a-z]/)) strength++;
        if (password.match(/[A-Z]/)) strength++;
        if (password.match(/[0-9]/)) strength++;
        if (password.match(/[^a-zA-Z0-9]/)) strength++;
        
        switch(strength) {
            case 0: case 1: message = 'Weak'; className = 'strength-weak'; break;
            case 2: case 3: message = 'Fair'; className = 'strength-fair'; break;
            case 4: message = 'Good'; className = 'strength-good'; break;
            case 5: message = 'Strong'; className = 'strength-strong'; break;
        }
        
        strengthDiv.html(`<span class="${className}">🔒 Password Strength: ${message}</span>`);
        strengthDiv.append(`<span style="font-size:10px;color:#64748b;display:block;">Min 8 chars with letters, numbers & special character</span>`);
    }

    function buildResetForm() {
        $("#resetPasswordForm").kendoForm({
            formData: { otp: "", newPassword: "", confirmPassword: "" },
            items: [
                { 
                    field: "otp", 
                    label: "OTP (6 digits)", 
                    validation: { required: true } 
                },
                { 
                    field: "newPassword", 
                    label: "New Password",
                    editor: function (container, options) {
                        $('<input type="password" name="' + options.field + '" id="newPassword" />')
                            .appendTo(container)
                            .kendoTextBox();
                    },
                    validation: { required: true }
                },
                { 
                    field: "confirmPassword", 
                    label: "Confirm Password",
                    editor: function (container, options) {
                        $('<input type="password" name="' + options.field + '" />')
                            .appendTo(container)
                            .kendoTextBox();
                    },
                    validation: { required: true }
                }
            ],
            buttonsTemplate: `
                <button id="resetBtn" type="submit" class="k-button k-button-lg k-button-solid-success" style="width:100%">
                    <span class="btn-text">Reset Password →</span>
                    <span class="btn-loader" style="display:none;">
                        <span class="k-icon k-i-loading"></span> Resetting...
                    </span>
                </button>`,
            submit: function (e) {
                e.preventDefault();
                const notification = $("#notificationReset").getKendoNotification();
                
                if (e.model.newPassword !== e.model.confirmPassword) {
                    if (notification) notification.show("Passwords do not match!", "error");
                    return;
                }
                
                const passwordRegex = /^(?=.*[A-Za-z])(?=.*\d)(?=.*[$!%*#?&])[A-Za-z\d$!%*#?&]{8,}$/;
                if (!passwordRegex.test(e.model.newPassword)) {
                    if (notification) notification.show("Password must be 8+ characters with letters, numbers & special character", "error");
                    return;
                }
                
                const btn = $("#resetBtn");
                const text = btn.find(".btn-text");
                const loader = btn.find(".btn-loader");
                
                btn.prop("disabled", true);
                text.hide();
                loader.show();
                
                $.ajax({
                    url: API_BASE + "/api/auth/reset-password",
                    method: "POST",
                    contentType: "application/json",
                    data: JSON.stringify({
                        Email: window.FB_RESET_EMAIL,
                        Otp: e.model.otp,
                        NewPassword: e.model.newPassword,
                        ConfirmPassword: e.model.confirmPassword
                    }),
                    success: function (response) {
                        if (notification) notification.show(response.message || "Password reset successful!", "success");
                        localStorage.removeItem("resetEmail");
                        setTimeout(() => { 
                            window.location.href = "/vendor/login"; 
                        }, 1500);
                    },
                    error: function (xhr) {
                        let msg = xhr.responseJSON?.message || "Reset failed";
                        if (notification) notification.show(msg, "error");
                        btn.prop("disabled", false);
                        loader.hide();
                        text.show();
                    }
                });
            }
        });
        
        // Add password strength checker
        $(document).on('keyup', '#newPassword', checkPasswordStrength);
    }

})(jQuery);
