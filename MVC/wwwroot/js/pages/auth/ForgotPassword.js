$(document).ready(function () {

    var currentStep = 0;
    var otpTimerInterval = null;
    var otpSecondsLeft = 300;
    var RESEND_COOLDOWN = 30;
    var resendCooldown = 0;
    var resendInterval = null;

    // Placeholder translations
    var placeholders = {
        en: { fp_ph_email: "e.g. farmer@example.com", fp_ph_new_pw: "Enter new password", fp_ph_confirm_pw: "Re-enter password" },
        hi: { fp_ph_email: "जैसे farmer@example.com", fp_ph_new_pw: "नया पासवर्ड दर्ज करें", fp_ph_confirm_pw: "पासवर्ड दोबारा दर्ज करें" },
        gu: { fp_ph_email: "દા.ત. farmer@example.com", fp_ph_new_pw: "નવો પાસવર્ડ દાખલ કરો", fp_ph_confirm_pw: "પાસવર્ડ ફરીથી દાખલ કરો" }
    };

    function updatePlaceholders(lang) {
        var ph = placeholders[lang] || placeholders.en;
        $("[data-i18n-placeholder]").each(function () {
            var key = $(this).data("i18n-placeholder");
            if (ph[key]) $(this).attr("placeholder", ph[key]);
        });
    }

    // Language dropdown
    $("#fpLangBtn").on("click", function (e) {
        e.stopPropagation();
        $("#fpLangMenu").toggleClass("open");
    });
    $(document).on("click", function () { $("#fpLangMenu").removeClass("open"); });

    // Language buttons
    $(".fp-lang-dropdown button").on("click", function () {
        var lang = $(this).attr("onclick").match(/'(\w+)'/)[1];
        updatePlaceholders(lang);
        $("#fpLangMenu").removeClass("open");
    });

    // Apply on page load
    updatePlaceholders(localStorage.getItem("preferredLanguage") || "en");

    // Kendo Notification
    var notification = $("#fpNotification").kendoNotification({
        position: { top: 20, right: 20 },
        autoHideAfter: 3500,
        stacking: "down",
        templates: [
            { type: "success", template: '<div style="padding:3px 0"><i class="fi fi-rr-badge-check"></i> #= message #</div>' },
            { type: "error", template: '<div style="padding:3px 0"><i class="fi fi-rr-cross-circle"></i> #= message #</div>' }
        ]
    }).data("kendoNotification");

    // Kendo Loader
    $("#fpLoader").kendoLoader({ type: "pulsing", themeColor: "primary", size: "large" });

    // Step indicator
    function updateStepIndicator(step) {
        $(".fp-si-step").removeClass("active done");
        $(".fp-si-line").removeClass("done");
        for (var i = 1; i <= 3; i++) {
            if (i < step + 1) $("#si" + i).addClass("done");
            else if (i === step + 1) $("#si" + i).addClass("active");
        }
        if (step >= 1) $("#siLine1").addClass("done");
        if (step >= 2) $("#siLine2").addClass("done");
    }

    // OTP boxes
    var $otpInputs = $(".fp-otp-input");

    $otpInputs.on("input", function () {
        var val = $(this).val().replace(/[^0-9]/g, "");
        $(this).val(val);
        if (val) {
            $(this).addClass("filled");
            var idx = parseInt($(this).data("index"));
            if (idx < 5) $otpInputs.eq(idx + 1).focus();
        } else {
            $(this).removeClass("filled");
        }
    });

    $otpInputs.on("keydown", function (e) {
        if (e.key === "Backspace" && !$(this).val()) {
            var idx = parseInt($(this).data("index"));
            if (idx > 0) $otpInputs.eq(idx - 1).focus().val("").removeClass("filled");
        }
    });

    // Paste support
    $otpInputs.first().on("paste", function (e) {
        e.preventDefault();
        var pasted = (e.originalEvent.clipboardData || window.clipboardData)
                     .getData("text").replace(/[^0-9]/g, "").substring(0, 6);
        pasted.split("").forEach(function (d, i) {
            if (i < 6) $otpInputs.eq(i).val(d).addClass("filled");
        });
        if (pasted.length > 0) $otpInputs.eq(Math.min(pasted.length, 5)).focus();
    });

    function getOtpValue() {
        var val = "";
        $otpInputs.each(function () { val += $(this).val(); });
        return val;
    }

    // Navigation
    function goToStep(step) {
        $(".fp-step").removeClass("fp-step-active");
        $(["#step1", "#step2", "#step3"][step]).addClass("fp-step-active");
        updateStepIndicator(step);
        currentStep = step;
    }

    // Button handlers
    $("#btnSendOtp").on("click", onSendOtp);
    $("#btnVerifyOtp").on("click", onVerifyOtp);
    $("#btnResetPassword").on("click", onResetPassword);
    $("#btnBackStep1").on("click", function () { goToStep(0); });
    $("#btnBackStep2").on("click", function () { goToStep(1); });
    $("#btnResendOtp").on("click", onResendOtp);

    // Step 1
    function onSendOtp() {
        var email = $.trim($("#txtEmail").val());
        hideError("emailError");

        if (!email) { showError("emailError", "Please enter your email."); return; }
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) { showError("emailError", "Enter a valid email."); return; }

        showLoader();
        setTimeout(function () {
            hideLoader();
            notification.show({ message: "Verification code sent!" }, "success");
            $("#emailDisplay").text(maskEmail(email));
            goToStep(1);
            startOtpTimer();
            startResendCooldown();
            $otpInputs.first().focus();
        }, 1200);
    }

    // Step 2
    function onVerifyOtp() {
        var otp = getOtpValue();
        hideError("otpError");
        if (otp.length !== 6) { showError("otpError", "Enter the complete 6-digit code."); return; }

        showLoader();
        setTimeout(function () {
            hideLoader();
            clearInterval(otpTimerInterval);
            clearInterval(resendInterval);
            notification.show({ message: "OTP verified!" }, "success");
            goToStep(2);
        }, 1000);
    }

    // Step 3
    function onResetPassword() {
        var newPw = $("#txtNewPassword").val();
        var confirmPw = $("#txtConfirmPassword").val();
        var valid = true;
        hideError("newPwError");
        hideError("confirmPwError");

        if (getPasswordStrength(newPw).score < 4) {
            showError("newPwError", "Password does not meet all requirements.");
            valid = false;
        }
        if (!confirmPw) {
            showError("confirmPwError", "Please confirm your password.");
            valid = false;
        } else if (newPw !== confirmPw) {
            showError("confirmPwError", "Passwords do not match.");
            valid = false;
        }
        if (!valid) return;

        showLoader();
        setTimeout(function () { hideLoader(); showSuccessDialog(); }, 1200);
    }

    // Password strength
    $("#txtNewPassword").on("input", function () {
        var pw = $(this).val();
        var s = getPasswordStrength(pw);

        $(".fp-sbar").removeClass("s-weak s-fair s-good s-strong");
        var cls = ["", "s-weak", "s-fair", "s-good", "s-strong"][s.score] || "";
        var lbl = ["", "Weak", "Fair", "Good", "Strong"][s.score] || "";

        for (var i = 1; i <= s.score; i++) { $("#bar" + i).addClass(cls); }

        var $lbl = $("#pwStrengthLabel").removeClass("s-weak s-fair s-good s-strong");
        (pw && s.score > 0) ? $lbl.text(lbl).addClass(cls) : $lbl.text("");

        toggleRule("ruleLength", s.hasLength);
        toggleRule("ruleLetter", s.hasLetter);
        toggleRule("ruleNumber", s.hasNumber);
        toggleRule("ruleSpecial", s.hasSpecial);
    });

    function getPasswordStrength(pw) {
        var r = {
            hasLength: pw.length >= 8,
            hasLetter: /[a-zA-Z]/.test(pw),
            hasNumber: /[0-9]/.test(pw),
            hasSpecial: /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(pw),
            score: 0
        };
        r.score = [r.hasLength, r.hasLetter, r.hasNumber, r.hasSpecial].filter(Boolean).length;
        return r;
    }

    function toggleRule(id, ok) {
        var el = $("#" + id);
        if (ok) {
            el.addClass("passed");
            el.find("i").removeClass("fi-rr-circle").addClass("fi-rr-check-circle");
        } else {
            el.removeClass("passed");
            el.find("i").removeClass("fi-rr-check-circle").addClass("fi-rr-circle");
        }
    }

    // Password toggle
    $("#toggleNewPw").on("click", function () { togglePw("#txtNewPassword", this); });
    $("#toggleConfirmPw").on("click", function () { togglePw("#txtConfirmPassword", this); });

    function togglePw(sel, btn) {
        var $input = $(sel), $icon = $(btn).find("i");
        if ($input.attr("type") === "password") {
            $input.attr("type", "text");
            $icon.removeClass("fi-rr-eye").addClass("fi-rr-eye-crossed");
        } else {
            $input.attr("type", "password");
            $icon.removeClass("fi-rr-eye-crossed").addClass("fi-rr-eye");
        }
    }

    $("#txtConfirmPassword").on("input", function () {
        var n = $("#txtNewPassword").val(), c = $(this).val();
        (c && n !== c) ? showError("confirmPwError", "Passwords do not match.") : hideError("confirmPwError");
    });

    // OTP timer & resend
    function startOtpTimer() {
        otpSecondsLeft = 300;
        updateTimer();
        otpTimerInterval = setInterval(function () {
            otpSecondsLeft--;
            updateTimer();
            if (otpSecondsLeft <= 0) {
                clearInterval(otpTimerInterval);
                notification.show({ message: "OTP expired." }, "error");
            }
        }, 1000);
    }

    function updateTimer() {
        var m = Math.floor(otpSecondsLeft / 60).toString().padStart(2, "0");
        var s = (otpSecondsLeft % 60).toString().padStart(2, "0");
        $("#otpTimer").text(m + ":" + s);
    }

    function startResendCooldown() {
        resendCooldown = RESEND_COOLDOWN;
        var $btn = $("#btnResendOtp");
        $btn.prop("disabled", true);
        updateResendText();

        resendInterval = setInterval(function () {
            resendCooldown--;
            if (resendCooldown <= 0) {
                clearInterval(resendInterval);
                $btn.prop("disabled", false).find("span").text("Resend Code");
            } else {
                updateResendText();
            }
        }, 1000);
    }

    function updateResendText() {
        $("#btnResendOtp span").text("Resend (" + resendCooldown + "s)");
    }

    function onResendOtp() {
        showLoader();
        setTimeout(function () {
            hideLoader();
            notification.show({ message: "New code sent!" }, "success");
            clearInterval(otpTimerInterval);
            startOtpTimer();
            startResendCooldown();
            $otpInputs.val("").removeClass("filled").first().focus();
        }, 800);
    }

    // Success modal translations
    var successTexts = {
        en: { title: "Password Reset Successful!", msg: "Your password has been updated successfully. You will be redirected to the login page.", btn: "Go to Login" },
        hi: { title: "पासवर्ड रीसेट सफल!", msg: "आपका पासवर्ड अपडेट हो गया। आपको लॉगिन पर भेजा जाएगा।", btn: "लॉगिन पर जाएं" },
        gu: { title: "પાસવર્ડ રીસેટ સફળ!", msg: "તમારો પાસવર્ડ અપડેટ થયો. તમને લૉગિન પર મોકલવામાં આવશે.", btn: "લૉગિન પર જાઓ" }
    };

    function showSuccessDialog() {
        var lang = localStorage.getItem("preferredLanguage") || "en";
        var t = successTexts[lang] || successTexts.en;

        var html = '<div class="fp-modal-overlay">' +
            '<div class="fp-modal">' +
                '<div class="fp-modal-check"><i class="fi fi-rr-badge-check"></i></div>' +
                '<h3>' + t.title + '</h3>' +
                '<p>' + t.msg + '</p>' +
                '<button class="fp-btn-primary fp-modal-btn" id="btnGoLogin">' + t.btn + '</button>' +
            '</div>' +
        '</div>';

        $("body").append(html);
        setTimeout(function () { $(".fp-modal-overlay").addClass("visible"); }, 10);
        $(document).on("click", "#btnGoLogin", function () { window.location.href = "/"; });
    }

    // Helpers
    function showError(id, msg) { $("#" + id).text(msg).addClass("visible"); }
    function hideError(id) { $("#" + id).removeClass("visible").text(""); }
    function showLoader() { $("#fpLoaderOverlay").fadeIn(150); }
    function hideLoader() { $("#fpLoaderOverlay").fadeOut(150); }
    function maskEmail(e) {
        var p = e.split("@");
        return p[0].length <= 2 ? e : p[0][0] + "***" + p[0].slice(-1) + "@" + p[1];
    }
});
