/**
 * Farmer Login Logic
 * Handles login, password visibility, and OTP requests.
 */

(function () {
    'use strict';

    // Notifications
    window.showToast = function (title, message, isError = false) {
        const container = document.getElementById('notificationArea');
        if (!container) return;
        const bgClass = isError ? 'bg-red-600' : 'bg-primary';
        const icon = isError ? 'error' : 'check_circle';
        const toast = document.createElement('div');
        toast.className = `${bgClass} text-white px-5 py-4 rounded-xl shadow-[0_10px_40px_rgba(0,0,0,0.3)] flex items-center gap-3 transform transition-all duration-500 translate-x-full opacity-0 pointer-events-auto border border-white/20 backdrop-blur-md`;
        toast.innerHTML = `<span class="material-symbols-outlined text-2xl drop-shadow-md">${icon}</span><div><h4 class="font-bold text-sm tracking-wide">${title}</h4><p class="text-xs text-white/90 mt-0.5">${message}</p></div>`;
        container.appendChild(toast);
        setTimeout(() => toast.classList.remove('translate-x-full', 'opacity-0'), 10);
        setTimeout(() => { toast.classList.add('translate-x-full', 'opacity-0'); setTimeout(() => toast.remove(), 500); }, 3500);
    };

    // Password toggle
    window.togglePassword = function () {
        const input = document.getElementById('password');
        const icon = document.getElementById('eyeIcon');
        if (input.type === 'password') { input.type = 'text'; icon.textContent = 'visibility_off'; }
        else { input.type = 'password'; icon.textContent = 'visibility'; }
    };

    // Login
    window.handleLogin = function (e) {
        if (e) e.preventDefault();
        const btn = document.getElementById('submitBtn');
        const email = document.getElementById('emailOrPhone').value.trim();
        const pwd = document.getElementById('password').value;

        if (!email || !pwd) { window.showToast('Validation Error', 'Please fill in all required fields.', true); return false; }

        btn.disabled = true;
        btn.innerHTML = `<div class="flex items-center justify-center gap-2"><span class="material-symbols-outlined animate-spin">progress_activity</span> Signing in...</div>`;

        fetch('/Farmer/Login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ emailOrPhone: email, password: pwd })
        })
            .then(res => res.json())
            .then(result => {
                if (result.success) {
                    window.showToast('Success!', 'Login successful! Redirecting...', false);
                    document.cookie = `authToken=${result.token}; path=/; max-age=1800; SameSite=Strict`;

                    // Trigger welcome modal on dashboard
                    localStorage.setItem("showWelcomeModal", "true");

                    setTimeout(() => { window.location.href = '/Farmer/Dashboard'; }, 1400);
                } else {
                    btn.disabled = false;
                    btn.innerHTML = 'Sign In';
                    window.showToast('Login Failed', result.message || 'Invalid email or password.', true);
                }
            })
            .catch(() => {
                btn.disabled = false;
                btn.innerHTML = 'Sign In';
                window.showToast('Network Error', 'Cannot connect to server. Please try again.', true);
            });
        return false;
    };

    window.handleGoogleLogin = function () { window.showToast('Coming Soon', 'Google Login is currently being configured.', false); };

    window.handleForgot = function (e) {
        if (e) e.preventDefault();
        const btn = document.getElementById('forgotBtn');
        const email = document.getElementById('forgotEmail').value.trim();
        const otp = document.getElementById('otp').value.trim();

        if (!email) { window.showToast('Validation Error', 'Please enter your email address.', true); return false; }

        if (!otp) {
            btn.disabled = true;
            btn.innerHTML = `<div class="flex items-center justify-center gap-2"><span class="material-symbols-outlined animate-spin">progress_activity</span> Sending OTP...</div>`;
            fetch('/api/farmer/send-otp', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email }) })
                .then(() => { window.showToast('OTP Sent', 'Check your email for the 6-digit code!', false); btn.disabled = false; btn.innerHTML = 'Verify OTP & Reset'; })
                .catch(() => { window.showToast('Error', 'Error sending OTP. Please retry.', true); btn.disabled = false; btn.innerHTML = 'Send OTP / Reset'; });
        } else {
            window.location.href = `/Farmer/ForgetPassword?email=${encodeURIComponent(email)}&otp=${encodeURIComponent(otp)}`;
        }
        return false;
    };

    document.addEventListener('DOMContentLoaded', function () {
        const forgotLink = document.getElementById('forgotPasswordLink');
        const backLink = document.getElementById('backToLoginLink');
        const loginSection = document.getElementById('loginSection');
        const forgotSection = document.getElementById('forgotPasswordSection');

        if (forgotLink && loginSection && forgotSection) {
            forgotLink.addEventListener('click', function (e) {
                e.preventDefault();
                loginSection.style.display = 'none';
                forgotSection.style.display = 'block';
            });
        }
        if (backLink && loginSection && forgotSection) {
            backLink.addEventListener('click', function (e) {
                e.preventDefault();
                forgotSection.style.display = 'none';
                loginSection.style.display = 'block';
            });
        }
    });
})();