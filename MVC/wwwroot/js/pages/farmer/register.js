// FarmBridge — Farmer Auth · Register (Kendo UI + Validator)
// Location: wwwroot/js/pages/farmer/register.js

$(document).ready(function () {

    /* ── Initialize Kendo Widgets ── */
    
    const fullName = $("#fullName").kendoTextBox({
        placeholder: "Ramesh Kumar"
    }).data("kendoTextBox");

    const email = $("#email").kendoTextBox({
        placeholder: "you@example.com"
    }).data("kendoTextBox");

    const phone = $("#phone").kendoMaskedTextBox({
        mask: "0000000000",
        promptChar: " "
    }).data("kendoMaskedTextBox");

    const state = $("#state").kendoTextBox({
        placeholder: "Gujarat"
    }).data("kendoTextBox");

    const district = $("#district").kendoTextBox({
        placeholder: "Anand"
    }).data("kendoTextBox");

    const address = $("#address").kendoTextBox({
        placeholder: "Village / Taluka / Street…"
    }).data("kendoTextBox");

    const password = $("#password").kendoTextBox({
        placeholder: "Min. 8 characters"
    }).data("kendoTextBox");
    $("#password").attr("type", "password");

    const confirmPassword = $("#confirmPassword").kendoTextBox({
        placeholder: "Re-enter password"
    }).data("kendoTextBox");
    $("#confirmPassword").attr("type", "password");

    const submitBtn = $("#submitBtn").kendoButton({
        themeColor: "primary",
        enable: true
    }).data("kendoButton");

    /* ── Kendo Validator ── */
    const validator = $("#registerForm").kendoValidator({
        rules: {
            required: function (input) {
                if (input.is("[required]") || input.is("[name='fullName']") || input.is("[name='email']") || input.is("[name='phone']") || input.is("[name='password']") || input.is("[name='confirmPassword']")) {
                    return $.trim(input.val()) !== "";
                }
                return true;
            },
            emailCheck: function (input) {
                if (input.is("[name='email']") && $.trim(input.val()) !== "") {
                    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(input.val());
                }
                return true;
            },
            phoneCheck: function (input) {
                if (input.is("[name='phone']") && $.trim(input.val()) !== "") {
                    return /^[6-9][0-9]{9}$/.test(input.val());
                }
                return true;
            },
            pwCheck: function (input) {
                if (input.is("[name='password']") && $.trim(input.val()) !== "") {
                    const v = input.val();
                    return v.length >= 8 && /[A-Z]/.test(v) && /[0-9]/.test(v);
                }
                return true;
            },
            matchCheck: function (input) {
                if (input.is("[name='confirmPassword']")) {
                    return input.val() === $("#password").val();
                }
                return true;
            }
        },
        messages: {
            required: "This field is required",
            emailCheck: "Enter a valid email",
            phoneCheck: "Enter a valid 10-digit mobile",
            pwCheck: "Min. 8 chars, 1 Uppercase, 1 Number",
            matchCheck: "Passwords do not match"
        },
        validateOnBlur: true,
        errorTemplate: '<span class="k-invalid-msg" data-for="#=name#"><svg width="11" height="11" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/></svg> #=message#</span>',
        validate: function(e) {
            // Apply error styles to Kendo wrappers
            $("#registerForm .k-input").removeClass("is-error is-valid");
            $("#registerForm .k-input").each(function() {
                const input = $(this).find("input");
                const name = input.attr("name");
                const error = $(`#registerForm [data-for='${name}']`);
                if (error.length && error.is(":visible")) {
                    $(this).addClass("is-error");
                } else if (input.val() && !error.is(":visible")) {
                     $(this).addClass("is-valid");
                }
            });
        }
    }).data("kendoValidator");

    /* ── Toast ── */
    function showToast(msg, type) {
        const stack = document.getElementById('toastStack');
        const t = document.createElement('div');
        t.className = 'fa-toast' + (type === 'error' ? ' error' : '');
        const color = type === 'error' ? '#dc2626' : '#16a34a';
        t.innerHTML =
            `<svg width="16" height="16" viewBox="0 0 24 24" fill="${color}">` +
            (type === 'error'
                ? '<path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z"/>'
                : '<path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/>') +
            `</svg><span>${msg}</span>`;
        stack.appendChild(t);
        setTimeout(() => t.remove(), 5000);
    }

    /* ── Password Strength ── */
    const strengthColors = ['', '#dc2626', '#f97316', '#16a34a', '#15522a'];
    const strengthLabels = ['', 'Weak', 'Fair', 'Good', 'Strong'];

    function calcStrength(pw) {
        let s = 0;
        if (pw.length >= 8) s++;
        if (pw.length >= 12) s++;
        if (/[A-Z]/.test(pw)) s++;
        if (/[0-9]/.test(pw)) s++;
        if (/[^A-Za-z0-9]/.test(pw)) s++;
        return Math.min(4, s);
    }

    $("#password").on("input", function() {
        const pw = $(this).val();
        const s = pw.length === 0 ? 0 : calcStrength(pw);
        ['seg1', 'seg2', 'seg3', 'seg4'].forEach((id, i) => {
            document.getElementById(id).style.background =
                i < s ? strengthColors[s] : 'var(--fa-gray-100)';
        });
        const txt = document.getElementById('strengthText');
        txt.textContent = pw.length > 0 ? strengthLabels[s] : '';
        txt.style.color = s > 0 ? strengthColors[s] : 'var(--fa-light)';
    });

    /* ── Eye toggles ── */
    $(".fa-eye-btn").on("click", function() {
        const targetId = $(this).data("target");
        const target = $("#" + targetId);
        const type = target.attr("type") === "password" ? "text" : "password";
        target.attr("type", type);
    });

    /* ── Submit Form ── */
    $("#registerForm").on("submit", function (e) {
        e.preventDefault();

        if (!validator.validate()) {
            showToast("Please fix the validation errors.", "error");
            return;
        }

        submitBtn.enable(false);
        const originalContent = submitBtn.element.html();
        submitBtn.element.html('<span class="fa-spinner"></span> Creating account\u2026');

        const data = {
            fullName: fullName.value().trim(),
            email: email.value().trim(),
            phone: phone.value().trim(),
            state: state.value().trim(),
            district: district.value().trim(),
            address: address.value().trim(),
            password: password.value(),
            confirmPassword: confirmPassword.value()
        };

        fetch('/Farmer/Register', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        })
            .then(r => r.json())
            .then(res => {
                if (res.success) {
                    showToast('Registration successful! Redirecting\u2026', 'success');
                    setTimeout(() => { window.location.href = '/Farmer/Login'; }, 1600);
                } else {
                    showToast(res.message || 'Registration failed.', 'error');
                    resetBtn();
                }
            })
            .catch(() => {
                showToast('Network error. Please try again.', 'error');
                resetBtn();
            });

        function resetBtn() {
            submitBtn.enable(true);
            submitBtn.element.html(originalContent);
        }
    });

});
