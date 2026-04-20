// FarmBridge — Staff Auth · Login
// Moved from: wwwroot/js/staff-auth.js

// ── Password visibility toggle ────────────────────────────
function togglePassword() {
    const input = document.getElementById('password');
    const icon  = document.getElementById('eyeIcon');
    if (input.type === 'password') {
        input.type = 'text';
        icon.innerHTML = `
            <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8
                     a18.45 18.45 0 0 1 5.06-5.94
                     M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8
                     a18.5 18.5 0 0 1-2.16 3.19
                     m-6.72-1.07a3 3 0 1 1-4.24-4.24"/>
            <line x1="1" y1="1" x2="23" y2="23"/>`;
    } else {
        input.type = 'password';
        icon.innerHTML = `
            <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"/>
            <circle cx="12" cy="12" r="3"/>`;
    }
}

// ── Notification helper ────────────────────────────────────
function showNotification(message, type) {
    const el = document.getElementById('notificationArea');
    el.textContent   = message;
    el.className     = 'sa-notification ' + type;
    el.style.display = 'block';
    setTimeout(() => { el.style.display = 'none'; }, 4500);
}

// ── Login submit ──────────────────────────────────────────
// Role is NOT sent from the client.
// The API reads role from t_users.c_role and encodes it in the JWT.
function handleLogin(e) {
    e.preventDefault();

    const btn   = document.getElementById('submitBtn');
    const email = document.getElementById('email').value.trim();
    const pwd   = document.getElementById('password').value;

    if (!email || !pwd) {
        showNotification('Please fill in all required fields.', 'error');
        return;
    }

    btn.disabled  = true;
    btn.innerHTML = '<span class="sa-spinner"></span> Signing in…';

    const payload = {
        Email:    email.toLowerCase(),
        Password: pwd
    };

    fetch('/StaffAuth/Login', {
        method:  'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
    })
    .then(res => res.json())
    .then(result => {
        if (result.success) {
            showNotification(`Welcome, ${result.fullName}!`, 'success');
            document.cookie =
                `authToken=${result.token}; path=/; max-age=1800; SameSite=Strict`;
            setTimeout(() => {
                window.location.href = result.role === 'admin'
                    ? '/Admin/Dashboard'
                    : '/FieldOfficer/Dashboard';
            }, 1200);
        } else {
            btn.disabled  = false;
            btn.innerHTML = 'Sign In →';
            showNotification(result.message || 'Login failed', 'error');
        }
    })
    .catch(() => {
        btn.disabled  = false;
        btn.innerHTML = 'Sign In →';
        showNotification('Network error occurred', 'error');
    });
}

// ── Forgot-password link ───────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    const fpLink = document.getElementById('forgotPasswordLink');
    if (!fpLink) return;

    fpLink.addEventListener('click', function (e) {
        e.preventDefault();
        const email = document.getElementById('email').value.trim();
        if (!email) {
            showNotification('Enter your email first then click Forgot Password.', 'error');
            return;
        }
        window.location.href =
            `/StaffAuth/ForgotPassword?email=${encodeURIComponent(email)}`;
    });
});
