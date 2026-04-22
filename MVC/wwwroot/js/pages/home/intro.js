/* FarmBridge — Intro Animation (plays only on hard refresh / first visit) */
document.addEventListener("DOMContentLoaded", () => {
    const introOverlay = document.getElementById("intro-overlay");
    const introLogo = document.getElementById("intro-logo");
    const navLogoTarget = document.getElementById("nav-logo-target");
    const body = document.body;

    if (!introOverlay || !introLogo || !navLogoTarget) return;

    // Skip intro if already played this session (back navigation, soft reload)
    /*
    if (sessionStorage.getItem("introPlayed")) {
        introOverlay.style.display = "none";
        introLogo.style.display = "none";
        navLogoTarget.classList.add("visible");
        body.classList.add("intro-complete");
        return;
    }

    // Mark intro as played for this session
    sessionStorage.setItem("introPlayed", "true");
    */

    body.classList.add("intro-active");

    setTimeout(() => {
        navLogoTarget.classList.remove("visible");
        const targetRect = navLogoTarget.getBoundingClientRect();

        requestAnimationFrame(() => {
            introLogo.style.top = targetRect.top + "px";
            introLogo.style.left = targetRect.left + "px";
            introLogo.style.width = targetRect.width + "px";
            introLogo.style.height = targetRect.height + "px";
            introLogo.style.transform = "none";

            setTimeout(() => {
                introOverlay.classList.add("fade-out");
                body.classList.remove("intro-active");
                body.classList.add("intro-complete");
            }, 600);

            setTimeout(() => {
                navLogoTarget.classList.add("visible");
                introOverlay.style.display = "none";
                introLogo.style.display = "none";
            }, 1200);
        });
    }, 2700);
});
