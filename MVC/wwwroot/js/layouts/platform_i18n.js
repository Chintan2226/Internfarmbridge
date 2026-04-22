/* FarmBridge — Platform i18n (EN / HI / GU)
   Shared across Admin, Field Officer, Farmer/Vendor layouts.
   Usage: include BEFORE layout-specific JS.
   - Reads window.CURRENT_LANG (set by server from cookie)
   - Loads .resx translations from /Language/{code}
   - Applies translations to [data-i18n] and [data-i18n-placeholder]
   - switchLang(code) sets cookie via /Language/{code} then reloads
*/

window.FB_I18N = {
    en: {
        /* ── Common ── */
        nav_logout: 'Logout',
        nav_profile: 'Profile',
        nav_notifications: 'Notifications',
        notif_unread: 'unread',
        notif_mark_all: 'Mark all read',
        notif_clear_all: 'Clear all',
        notif_tab_all: 'All',
        notif_tab_unread: 'Unread',
        notif_empty: 'No notifications',

        /* ── Admin Sidebar ── */
        nav_overview: 'Overview',
        nav_farmers: 'Farmers',
        nav_vendors: 'Vendors',
        nav_field_officers: 'Field Officers',
        nav_orders: 'Orders',
        nav_crop_catalog: 'Crop Catalog',
        nav_reports: 'Reports',

        /* ── FO Sidebar ── */
        nav_dashboard: 'Dashboard',
        nav_my_farmers: 'My Farmers',
        nav_approvals: 'Approvals',

        /* ── FV Sidebar ── */
        nav_marketplace: 'Marketplace',
        nav_my_orders: 'My Orders',
        nav_wishlist: 'Wishlist',
        nav_my_crops: 'My Crops',
        nav_qc_slots: 'QC Slots',
        nav_payments: 'Payments',
        nav_inquiries: 'Inquiries',

        /* ── Language picker ── */
        lang_switch: 'Language',
    },

    hi: {
        /* ── Common ── */
        nav_logout: 'लॉगआउट',
        nav_profile: 'प्रोफाइल',
        nav_notifications: 'सूचनाएं',
        notif_unread: 'अपठित',
        notif_mark_all: 'सभी पढ़ें',
        notif_clear_all: 'सब साफ़ करें',
        notif_tab_all: 'सभी',
        notif_tab_unread: 'अपठित',
        notif_empty: 'कोई सूचना नहीं',

        /* ── Admin Sidebar ── */
        nav_overview: 'अवलोकन',
        nav_farmers: 'किसान',
        nav_vendors: 'विक्रेता',
        nav_field_officers: 'फील्ड ऑफिसर',
        nav_orders: 'ऑर्डर',
        nav_crop_catalog: 'फसल कैटलॉग',
        nav_reports: 'रिपोर्ट',

        /* ── FO Sidebar ── */
        nav_dashboard: 'डैशबोर्ड',
        nav_my_farmers: 'मेरे किसान',
        nav_approvals: 'अनुमोदन',

        /* ── FV Sidebar ── */
        nav_marketplace: 'मार्केटप्लेस',
        nav_my_orders: 'मेरे ऑर्डर',
        nav_wishlist: 'विशलिस्ट',
        nav_my_crops: 'मेरी फसलें',
        nav_qc_slots: 'QC स्लॉट',
        nav_payments: 'भुगतान',
        nav_inquiries: 'पूछताछ',

        /* ── Language picker ── */
        lang_switch: 'भाषा',
    },

    gu: {
        /* ── Common ── */
        nav_logout: 'લૉગઆઉટ',
        nav_profile: 'પ્રોફાઇલ',
        nav_notifications: 'સૂચનાઓ',
        notif_unread: 'અવાંચ્યા',
        notif_mark_all: 'બધા વાંચ્યા',
        notif_clear_all: 'બધા સાફ કરો',
        notif_tab_all: 'બધા',
        notif_tab_unread: 'અવાંચ્યા',
        notif_empty: 'કોઈ સૂચના નથી',

        /* ── Admin Sidebar ── */
        nav_overview: 'ઝાંખી',
        nav_farmers: 'ખેડૂત',
        nav_vendors: 'વિક્રેતા',
        nav_field_officers: 'ફીલ્ડ ઓફિસર',
        nav_orders: 'ઓર્ડર',
        nav_crop_catalog: 'પાક કેટેલોગ',
        nav_reports: 'અહેવાલ',

        /* ── FO Sidebar ── */
        nav_dashboard: 'ડૅશબોર્ડ',
        nav_my_farmers: 'મારા ખેડૂત',
        nav_approvals: 'મંજૂરી',

        /* ── FV Sidebar ── */
        nav_marketplace: 'માર્કેટપ્લેસ',
        nav_my_orders: 'મારા ઓર્ડર',
        nav_wishlist: 'ઇચ્છા સૂચિ',
        nav_my_crops: 'મારો પાક',
        nav_qc_slots: 'QC સ્લોટ્સ',
        nav_payments: 'ચુકવણી',
        nav_inquiries: 'પૂછપરછ',

        /* ── Language picker ── */
        lang_switch: 'ભાષા',
    }
};

window.fbTranslations = window.fbTranslations || {};
window.fbSupportedLanguages = ['en', 'hi', 'gu'];
window.fbDefaultLanguage = 'en';
window.fbFallbackTranslations = window.FB_I18N;

function fbNormalizeLang(lang) {
    if (!lang || typeof lang !== 'string') {
        lang = window.CURRENT_LANG || window.localStorage.getItem('preferredLanguage') || window.fbDefaultLanguage;
    }
    return lang.toString().trim().toLowerCase();
}

async function fbLoadTranslations(lang) {
    lang = fbNormalizeLang(lang);
    if (!window.fbSupportedLanguages.includes(lang)) {
        lang = window.fbDefaultLanguage;
    }

    if (window.fbTranslations[lang] && Object.keys(window.fbTranslations[lang]).length > 0) {
        return window.fbTranslations[lang];
    }

    let serverTranslations = {};
    try {
        const response = await fetch(`/Language/${lang}`);
        if (response.ok) {
            serverTranslations = await response.json();
        }
    } catch (error) {
        console.warn('fbLoadTranslations failed:', error);
    }

    // Merge server translations with hardcoded fallback
    const fallback = (window.fbFallbackTranslations && window.fbFallbackTranslations[lang]) || 
                     (window.fbFallbackTranslations && window.fbFallbackTranslations[window.fbDefaultLanguage]) || {};
    
    window.fbTranslations[lang] = { ...fallback, ...serverTranslations };
    return window.fbTranslations[lang];
}

function fbT(key) {
    const lang = fbNormalizeLang();
    const t = window.fbTranslations[lang] || {};
    const fallback = (window.fbFallbackTranslations && window.fbFallbackTranslations[lang]) || 
                     (window.fbFallbackTranslations && window.fbFallbackTranslations[window.fbDefaultLanguage]) || {};
    
    return t[key] || fallback[key] || key;
}

async function fbApplyLang(lang) {
    const translations = await fbLoadTranslations(lang);
    window.CURRENT_LANG = fbNormalizeLang(lang);

    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        if (!key) return;
        const translation = translations[key] || fbT(key);
        if (translation) {
            el.innerHTML = translation;
            el.classList.add('notranslate');
        }
    });

    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        if (!key) return;
        const translation = translations[key] || fbT(key);
        if (translation) {
            el.setAttribute('placeholder', translation);
        }
    });

    document.querySelectorAll('.fb-lang-option').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.lang === window.CURRENT_LANG);
    });
}

function setGoogleTranslateCookie(lang) {
    if (lang === 'en') {
        document.cookie = `googtrans=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;`;
        document.cookie = `googtrans=; expires=Thu, 01 Jan 1970 00:00:00 UTC; domain=.${location.hostname}; path=/;`;
    } else {
        document.cookie = `googtrans=/en/${lang}; path=/`;
        document.cookie = `googtrans=/en/${lang}; domain=.${location.hostname}; path=/`;
    }
}

async function switchLang(lang) {
    lang = fbNormalizeLang(lang);
    if (!window.fbSupportedLanguages.includes(lang)) {
        lang = window.fbDefaultLanguage;
    }

    localStorage.setItem('preferredLanguage', lang);
    setGoogleTranslateCookie(lang);

    try {
        await fetch(`/Language/${lang}`);
    } catch (error) {
        console.warn('switchLang fetch failed:', error);
    }

    window.location.reload();
}

window.changeLanguage = switchLang;

window.toggleLangDropdown = function(e) {
    if (e) e.preventDefault();
    if (e) e.stopPropagation();
    document.querySelectorAll('.fb-lang-dropdown').forEach(d => {
        const isOpen = d.classList.contains('open');
        if (isOpen) {
            d.classList.remove('open');
            d.style.opacity = '0';
            d.style.pointerEvents = 'none';
            d.style.transform = 'translateY(-10px)';
        } else {
            d.classList.add('open');
            d.style.opacity = '1';
            d.style.pointerEvents = 'auto';
            d.style.transform = 'translateY(0)';
            d.style.visibility = 'visible';
            d.style.display = 'flex';
        }
    });
};

document.addEventListener('click', e => {
    if (!e.target.closest('.fb-lang-switcher')) {
        document.querySelectorAll('.fb-lang-dropdown').forEach(d => {
            d.classList.remove('open');
            d.style.opacity = '0';
            d.style.pointerEvents = 'none';
            d.style.transform = 'translateY(-10px)';
        });
    }
});

function fbInit() {
    console.log('FB_I18N: Initializing for', window.USER_ROLE);
    fbApplyLang(window.CURRENT_LANG || window.localStorage.getItem('preferredLanguage') || window.fbDefaultLanguage);
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', fbInit);
} else {
    fbInit();
}

