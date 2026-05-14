/**
 * Vendor Login Logic
 * Handles authentication, password toggles, and view state.
 */

// Tailwind Configuration (Global for CDN)
window.tailwind = window.tailwind || {};
window.tailwind.config = {
    darkMode: "class",
    theme: {
        extend: {
            colors: {
                primary: "#00450d",
                "primary-container": "#065f18",
                "on-surface": "#1a1c1a",
                "on-surface-variant": "#41493e",
            },
            fontFamily: {
                headline: ["Plus Jakarta Sans"],
                body: ["Plus Jakarta Sans"],
            }
        }
    }
};

(function () {
    const API_BASE = "http://localhost:5020";

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
    };

    // Toast Notifications
    window.showToast = function(title, message, isError = false) {
        const container = document.getElementById('notificationArea');
        if (!container) return;

        const bgClass = isError ? 'bg-red-600' : 'bg-primary';
        const icon = isError ? 'error' : 'check_circle';

        const toast = document.createElement('div');
        toast.className = `${bgClass} text-white px-5 py-4 rounded-xl shadow-[0_10px_40px_rgba(0,0,0,0.3)] flex items-center gap-3 transform transition-all duration-500 translate-x-full opacity-0 pointer-events-auto border border-white/20 backdrop-blur-md`;
        
        toast.innerHTML = `
            <span class="material-symbols-outlined text-2xl drop-shadow-md">${icon}</span>
            <div>
                <h4 class="font-bold text-sm tracking-wide">${title}</h4>
                <p class="text-xs text-white/90 mt-0.5">${message}</p>
            </div>
        `;

        container.appendChild(toast);

        setTimeout(() => toast.classList.remove('translate-x-full', 'opacity-0'), 10);
        setTimeout(() => {
            toast.classList.add('translate-x-full', 'opacity-0');
            setTimeout(() => toast.remove(), 500);
        }, 3500);
    };

    // Vendor Login
    window.handleVendorLogin = async function() {
        const emailOrPhone = document.getElementById('emailOrPhone').value.trim();
        const password = document.getElementById('password').value;
        const btn = document.getElementById('loginBtn');

        if (!emailOrPhone || !password) {
            window.showToast("Missing Fields", "Please enter both Email/Phone and Password.", true);
            return;
        }

        btn.disabled = true;
        btn.innerHTML = `<span class="material-symbols-outlined animate-spin">progress_activity</span> Signing in...`;

        try {
            const response = await fetch(API_BASE + "/api/vendor/login", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ emailOrPhone: emailOrPhone, password: password })
            });

            const result = await response.json();

            if (response.ok) {
                window.showToast("Success", "Login successful! Redirecting...", false);
                document.cookie = "authToken=" + result.token + "; path=/; max-age=1800";
                setTimeout(() => { window.location.href = "/vendor/Catalog"; }, 1200);
            } else {
                btn.disabled = false;
                btn.innerHTML = `<span>Sign In</span><span class="material-symbols-outlined text-lg">arrow_forward</span>`;
                window.showToast("Login Failed", result.message || "Invalid credentials.", true);
            }
        } catch (error) {
            btn.disabled = false;
            btn.innerHTML = `<span>Sign In</span><span class="material-symbols-outlined text-lg">arrow_forward</span>`;
            window.showToast("Network Error", "Cannot connect to server. Please try again.", true);
        }
    };

    // Vendor Forgot Password
    window.handleVendorForgot = async function() {
        const email = document.getElementById('forgotEmail').value.trim();
        const btn = document.getElementById('forgotBtn');

        if (!email) {
            window.showToast("Missing Email", "Please enter your registered email address.", true);
            return;
        }

        btn.disabled = true;
        btn.innerHTML = `<span class="material-symbols-outlined animate-spin">progress_activity</span> Sending OTP...`;

        try {
            const response = await fetch(API_BASE + "/api/auth/forgot-password", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ Email: email })
            });

            if (response.ok) {
                localStorage.setItem("resetEmail", email);
                window.location.href = "/Vendor/ForgetPassword";
            } else {
                const result = await response.json();
                btn.disabled = false;
                btn.innerHTML = `<span>Send OTP</span><span class="material-symbols-outlined text-lg">arrow_forward</span>`;
                window.showToast("Failed", result.message || "Failed to send OTP. Try again.", true);
            }
        } catch (error) {
            btn.disabled = false;
            btn.innerHTML = `<span>Send OTP</span><span class="material-symbols-outlined text-lg">arrow_forward</span>`;
            window.showToast("Network Error", "Cannot connect to server.", true);
        }
    };

    // View Toggling
    document.addEventListener('DOMContentLoaded', () => {
        const loginSec = document.getElementById('loginSection');
        const forgotSec = document.getElementById('forgotPasswordSection');

        const forgotLink = document.getElementById('forgotPasswordLink');
        if (forgotLink) {
            forgotLink.addEventListener('click', (e) => {
                e.preventDefault();
                loginSec.classList.add('hidden');
                forgotSec.classList.remove('hidden');
            });
        }

        const backLink = document.getElementById('backToLoginLink');
        if (backLink) {
            backLink.addEventListener('click', (e) => {
                e.preventDefault();
                forgotSec.classList.add('hidden');
                loginSec.classList.remove('hidden');
            });
        }
    });

    window.changeLanguage = function(lang) {
        if (typeof window.fbSetLanguage === 'function') {
            window.fbSetLanguage(lang);
        } else {
            console.warn("fbSetLanguage not defined");
        }
        document.getElementById('langMenu')?.classList.add('hidden');
    };

})();
