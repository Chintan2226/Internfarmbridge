// FarmBridge — Farmer Auth · Register (Vanilla JS + Tailwind)
// Location: wwwroot/js/pages/farmer/register.js

// Eye toggles
window.togglePassword = function(inputId, iconId) {
    const input = document.getElementById(inputId);
    const icon = document.getElementById(iconId);
    if (input.type === 'password') {
        input.type = 'text';
        icon.textContent = 'visibility_off';
    } else {
        input.type = 'password';
        icon.textContent = 'visibility';
    }
}

// Password Strength
document.addEventListener('DOMContentLoaded', function() {
    const pwdInput = document.getElementById("password");
    if(!pwdInput) return;

    const colors = ['rgba(0,0,0,0.1)', '#dc2626', '#f97316', '#16a34a', '#14532d'];
    const labels = ['', 'Weak', 'Fair', 'Good', 'Strong'];

    pwdInput.addEventListener("input", function() {
        const pw = this.value;
        let s = 0;
        
        if (pw.length >= 8) s++;
        if (pw.length >= 12) s++;
        if (/[A-Z]/.test(pw)) s++;
        if (/[0-9]/.test(pw)) s++;
        if (/[^A-Za-z0-9]/.test(pw)) s++;
        
        const strength = pw.length === 0 ? 0 : Math.min(4, s);

        for(let i = 1; i <= 4; i++) {
            document.getElementById('seg' + i).style.backgroundColor = (i <= strength) ? colors[strength] : colors[0];
        }

        const txt = document.getElementById('strengthText');
        txt.textContent = pw.length > 0 ? labels[strength] : '';
        txt.style.color = strength > 0 ? colors[strength] : 'inherit';
    });
});

// Form Submission & Validation
window.handleRegister = function(e) {
    if (e) e.preventDefault();

    const fullName = document.getElementById('fullName').value.trim();
    const email = document.getElementById('email').value.trim();
    const phone = document.getElementById('phone').value.trim();
    const state = document.getElementById('state').value.trim();
    const district = document.getElementById('district').value.trim();
    const address = document.getElementById('address').value.trim();
    const pwd = document.getElementById('password').value;
    const confirmPwd = document.getElementById('confirmPassword').value;
    const btn = document.getElementById('submitBtn');

    if (!fullName || !email || !phone || !pwd || !confirmPwd) {
        window.showToast("Missing Fields", "Please fill out all required fields marked with *", true);
        return false;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        window.showToast("Invalid Email", "Please enter a valid email address.", true);
        return false;
    }

    if (!/^[6-9][0-9]{9}$/.test(phone)) {
        window.showToast("Invalid Phone", "Please enter a valid 10-digit mobile number.", true);
        return false;
    }

    if (pwd.length < 8 || !/[A-Z]/.test(pwd) || !/[0-9]/.test(pwd)) {
        window.showToast("Weak Password", "Password must be at least 8 chars, contain 1 uppercase letter and 1 number.", true);
        return false;
    }

    if (pwd !== confirmPwd) {
        window.showToast("Password Mismatch", "Your passwords do not match.", true);
        return false;
    }

    btn.disabled = true;
    const originalText = btn.innerHTML;
    btn.innerHTML = `<div class="flex items-center justify-center gap-2">
                        <span class="material-symbols-outlined animate-spin">progress_activity</span> 
                        Creating account...
                     </div>`;

    const payload = {
        fullName: fullName,
        email: email,
        phone: phone,
        state: state,
        district: district,
        address: address,
        password: pwd,
        confirmPassword: confirmPwd
    };

    fetch('/Farmer/Register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    })
    .then(r => r.json())
    .then(res => {
        if (res.success) {
            window.showToast('Welcome!', 'Registration successful! Redirecting...', false);
            setTimeout(() => { window.location.href = '/Farmer/Login'; }, 1600);
        } else {
            resetBtn();
            window.showToast('Registration Failed', res.message || 'Unable to register account.', true);
        }
    })
    .catch(() => {
        resetBtn();
        window.showToast('Network Error', 'Cannot connect to server. Please try again.', true);
    });

    function resetBtn() {
        btn.disabled = false;
        btn.innerHTML = originalText;
    }

    return false;
}