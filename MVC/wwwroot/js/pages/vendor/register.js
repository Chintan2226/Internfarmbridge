/**
 * Vendor Registration Logic
 * Handles validation, business logic, and API integration for registration.
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
        }, 4500);
    };

    window.handleVendorRegister = async function() {
        const businessName = document.getElementById('businessName').value.trim();
        const contactPerson = document.getElementById('contactPerson').value.trim();
        const phone = document.getElementById('phone').value.trim();
        const email = document.getElementById('email').value.trim();
        const gstin = document.getElementById('gstin').value.trim().toUpperCase();
        const password = document.getElementById('password').value;
        const confirmPassword = document.getElementById('confirmPassword').value;
        const btn = document.getElementById('registerBtn');

        if (!businessName || !contactPerson || !phone || !email || !password || !confirmPassword) {
            window.showToast("Missing Fields", "Please fill out all required fields.", true);
            return;
        }

        if (!/^[^\s\x40]+\x40[^\s\x40]+\.[^\s\x40]+$/.test(email)) {
            window.showToast("Invalid Email", "Please enter a valid email address.", true);
            return;
        }

        if (!/^\+?[0-9\s\-()]{7,15}$/.test(phone)) {
            window.showToast("Invalid Phone", "Please enter a valid phone number.", true);
            return;
        }

        if (gstin !== "" && !/^([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1})$/.test(gstin)) {
            window.showToast("Invalid GSTIN", "Please enter a valid GST format or leave it blank.", true);
            return;
        }

        if (password.length < 8) {
            window.showToast("Weak Password", "Password must be at least 8 characters.", true);
            return;
        }

        if (password !== confirmPassword) {
            window.showToast("Password Mismatch", "Your passwords do not match.", true);
            return;
        }

        btn.disabled = true;
        const originalText = btn.innerHTML;
        btn.innerHTML = `<div class="flex items-center justify-center gap-2">
                            <span class="material-symbols-outlined animate-spin">progress_activity</span> 
                            Registering...
                         </div>`;

        const payload = {
            businessName: businessName,
            contactPerson: contactPerson,
            email: email,
            phone: phone,
            gstin: gstin || "",
            password: password,
            confirmPassword: confirmPassword
        };

        try {
            const response = await fetch(API_BASE + "/api/vendor/register", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            if (response.ok) {
                window.showToast('Welcome!', 'Registration successful! Redirecting...', false);
                setTimeout(() => { window.location.href = '/vendor/login'; }, 1500);
            } else {
                const result = await response.json();
                resetBtn();
                window.showToast('Registration Failed', result.message || 'Unable to register account.', true);
            }
        } catch (error) {
            resetBtn();
            window.showToast('Network Error', 'Cannot connect to server. Please try again.', true);
        }

        function resetBtn() {
            btn.disabled = false;
            btn.innerHTML = originalText;
        }
    };

    window.changeLanguage = function(lang) {
        if (typeof window.fbSetLanguage === 'function') {
            window.fbSetLanguage(lang);
        } else {
            console.warn("fbSetLanguage not defined");
        }
        document.getElementById('langMenu')?.classList.add('hidden');
    };

})();
