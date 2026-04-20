/* FarmBridge — Global Scripts (site.js) */

document.addEventListener("DOMContentLoaded", () => {
  // 3. Global Smooth Anchor Scrolling
  document.querySelectorAll('a[href^="#"]').forEach((anchor) => {
    anchor.addEventListener("click", function (e) {
      const href = this.getAttribute("href");
      if (!href || href === "#") {
        if (href === "#") {
          e.preventDefault();
          window.scrollTo({ top: 0, behavior: "smooth" });
        }
        return;
      }
      const target = document.querySelector(href);
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    });
  });

  // Initialize dynamic Google Translator for full-platform sync (Dynamic User Content)
  initDynamicTranslator();

  // Global Language Logic
  const savedLang = localStorage.getItem("preferredLanguage") || "en";
  changeLanguage(savedLang);
});

// --- GOOGLE TRANSLATE LOGIC (For Dynamic Content) ---
window.googleTranslateElementInit = function () {
  new google.translate.TranslateElement(
    {
      pageLanguage: "en",
      includedLanguages: "en,hi,gu",
      autoDisplay: false,
    },
    "google_translate_element",
  );
};

function initDynamicTranslator() {
  const gtDiv = document.createElement("div");
  gtDiv.id = "google_translate_element";
  gtDiv.style.display = "none";
  document.body.appendChild(gtDiv);

  const script = document.createElement("script");
  script.type = "text/javascript";
  script.src =
    "//translate.google.com/translate_a/element.js?cb=googleTranslateElementInit";
  document.body.appendChild(script);

  const style = document.createElement("style");
  style.innerHTML = `
        iframe.goog-te-banner-frame,
        .goog-te-banner-frame.skiptranslate,
        .VIpgJd-ZVi9od-ORHb-OEVmcd { display: none !important; visibility: hidden !important; height: 0 !important; }
        .goog-te-gadget { display: none !important; }
        body, html { top: 0px !important; position: static !important; margin-top: 0px !important; }
        #goog-gt-tt, .goog-te-balloon-frame { display: none !important; visibility: hidden !important; }
        .goog-text-highlight { background-color: transparent !important; box-shadow: none !important; }
        div[id^="goog-"], .VIpgJd-ZVi9od-aZ2wEe-wOHMyf { display: none !important; visibility: hidden !important; }
    `;
  document.head.appendChild(style);
}

// Global Language Change Function
async function changeLanguage(lang) {
  const currentLang = localStorage.getItem("preferredLanguage") || "en";
  if (lang !== currentLang || window.forceLangReload) {
    localStorage.setItem("preferredLanguage", lang);

    // 1. Set Google Translate Cookie
    if (lang === "en") {
      document.cookie = `googtrans=; expires=Thu, 01 Jan 1970 00:00:00 UTC; path=/;`;
      document.cookie = `googtrans=; expires=Thu, 01 Jan 1970 00:00:00 UTC; domain=.${location.hostname}; path=/;`;
    } else {
      document.cookie = `googtrans=/en/${lang}; path=/`;
      document.cookie = `googtrans=/en/${lang}; domain=.${location.hostname}; path=/`;
    }

    // 2. Set backend .AspNetCore.Culture cookie for .resx
    await fetch(`/Language/${lang}`);
    window.location.reload();
    return;
  }

  try {
    // Fetch .resx translations for the static UI
    const response = await fetch(`/Language/${lang}`);
    if (!response.ok) throw new Error("Failed to fetch translations");
    const translations = await response.json();

    document.querySelectorAll("[data-i18n]").forEach((el) => {
      const key = el.getAttribute("data-i18n");
      if (translations[key]) {
        el.innerHTML = translations[key];
        // Prevent Google Translate from re-translating our exact .resx matches
        el.classList.add("notranslate");
      }
    });

    document.querySelectorAll("[data-i18n-placeholder]").forEach((el) => {
      const key = el.getAttribute("data-i18n-placeholder");
      if (translations[key]) el.setAttribute("placeholder", translations[key]);
    });

    const mobileSelect = document.getElementById("mobileLangSelect");
    if (mobileSelect) mobileSelect.value = lang;

    window.dispatchEvent(new Event("resize"));
  } catch (error) {
    console.error("Error loading language:", error);
  }
}

// Global UI Toggles
function toggleMenu() {
  const menu = document.getElementById("mobileMenu");
  if (menu) menu.classList.toggle("open");
}

function toggleLangMenu(event) {
  if (event) event.stopPropagation();
  const menu = document.getElementById("langMenu");
  if (menu) menu.classList.toggle("open");
}

window.addEventListener("click", () => {
  const menu = document.getElementById("langMenu");
  if (menu && menu.classList.contains("open")) menu.classList.remove("open");
});
