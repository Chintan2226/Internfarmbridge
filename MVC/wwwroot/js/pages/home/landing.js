/* FarmBridge — Landing Page Logic */
document.addEventListener('DOMContentLoaded', () => {
    const navbar = document.getElementById('navbar');
    const sections = document.querySelectorAll('section[id]');
    const navIndicator = document.querySelector('.nav-indicator');
    const navLinks = document.querySelectorAll('.nav-link');
    const scrollIndicator = document.querySelector('.scroll-indicator');

    function updateIndicator() {
        const activeLink = document.querySelector('.nav-link.active');
        if (activeLink && navIndicator) {
            const rect = activeLink.getBoundingClientRect();
            const parentRect = activeLink.parentElement.getBoundingClientRect();
            navIndicator.style.width = `${rect.width}px`;
            navIndicator.style.left = `${rect.left - parentRect.left}px`;
            navIndicator.style.opacity = '1';
        } else if (navIndicator) {
            navIndicator.style.opacity = '0';
        }
    }

    // Throttled scroll handler using requestAnimationFrame
    let ticking = false;
    window.addEventListener('scroll', () => {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(() => {
            onScroll();
            ticking = false;
        });
    }, { passive: true });

    function onScroll() {
        const scrollY = window.scrollY;

        if (scrollY > 50) navbar.classList.add('scrolled');
        else navbar.classList.remove('scrolled');

        let currentSectionId = '';
        sections.forEach(section => {
            if (scrollY >= section.offsetTop - 200) {
                currentSectionId = section.getAttribute('id');
            }
        });

        if (currentSectionId === 'hero' || currentSectionId === 'stats' || scrollY < 200) {
            currentSectionId = '';
        }

        navLinks.forEach(link => {
            const href = link.getAttribute('href');
            link.classList.toggle('active', (href === '#' && currentSectionId === '') || (href === '#' + currentSectionId));
        });

        if (scrollIndicator) {
            scrollIndicator.style.opacity = scrollY > 100 ? '0' : '0.8';
            scrollIndicator.style.pointerEvents = scrollY > 100 ? 'none' : 'auto';
        }

        updateIndicator();
    }

    // Reveal animations
    const revealObserver = new IntersectionObserver((entries) => {
        entries.forEach((entry, i) => {
            if (entry.isIntersecting) {
                setTimeout(() => entry.target.classList.add('visible'), i * 40);
            }
        });
    }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });

    document.querySelectorAll('.reveal, .reveal-left, .reveal-right').forEach(el => revealObserver.observe(el));

    // Stats counter animation
    const statsObserver = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.querySelectorAll('.stat-num').forEach(num => {
                    const text = num.textContent;
                    if (text.includes('12')) animateCounter(num, 12, '', 'k+');
                    else if (text.includes('98')) animateCounter(num, 98, '', '%');
                    else if (text.includes('4')) animateCounter(num, 4, '₹', 'Cr+');
                    else if (text.includes('18')) animateCounter(num, 18, '', '+');
                });
                statsObserver.unobserve(entry.target);
            }
        });
    }, { threshold: 0.5 });

    const statsBand = document.querySelector('.stats-band');
    if (statsBand) statsObserver.observe(statsBand);

    function animateCounter(el, target, prefix = '', suffix = '') {
        const duration = 1800;
        const step = target / (duration / 16);
        let current = 0;
        const timer = setInterval(() => {
            current = Math.min(current + step, target);
            el.innerHTML = prefix + Math.floor(current) + `<span class="stat-unit">${suffix}</span>`;
            if (current >= target) clearInterval(timer);
        }, 16);
    }

    updateIndicator();
    window.addEventListener('resize', updateIndicator);

    // Hero slider
    let currentSlide = 0;
    const slides = document.querySelectorAll('.hero-slider img');
    const totalSlides = slides.length;

    function showSlide(index) {
        if (totalSlides === 0) return;
        slides.forEach(slide => slide.classList.remove('active'));
        slides[index].classList.add('active');
    }

    if (totalSlides > 0) {
        showSlide(0);
        setTimeout(() => {
            currentSlide = (currentSlide + 1) % totalSlides;
            showSlide(currentSlide);
            setInterval(() => {
                currentSlide = (currentSlide + 1) % totalSlides;
                showSlide(currentSlide);
            }, 5000);
        }, 8000);
    }

    // Typewriter effect (language-aware)
    const line1El = document.getElementById('typewriter-line1');
    const line2El = document.getElementById('typewriter-line2');

    if (line1El && line2El) {
        const heroTexts = {
            en: { line1: 'Centralized Procurement', line2: 'for a Smarter Bharat' },
            hi: { line1: 'एक स्मार्ट भारत के लिए', line2: 'केंद्रीकृत खरीद' },
            gu: { line1: 'સ્માર્ટ ભારત માટે', line2: 'કેન્દ્રીકૃત ખરીદી' }
        };

        const typeSpeed = 70;
        const pauseBetweenLines = 400;

        function typeLine(element, text, speed, callback) {
            let i = 0;
            function tick() {
                element.textContent = text.substring(0, i + 1);
                i++;
                if (i < text.length) {
                    element._typingTimer = setTimeout(tick, speed);
                } else if (callback) {
                    callback();
                }
            }
            tick();
        }

        function startTyping(delay) {
            clearTimeout(line1El._typingTimer);
            clearTimeout(line2El._typingTimer);
            line1El.textContent = '';
            line2El.textContent = '';

            const lang = localStorage.getItem('preferredLanguage') || 'en';
            const texts = heroTexts[lang] || heroTexts.en;

            setTimeout(() => {
                typeLine(line1El, texts.line1, typeSpeed, () => {
                    setTimeout(() => {
                        typeLine(line2El, texts.line2, typeSpeed);
                    }, pauseBetweenLines);
                });
            }, delay);
        }

        // const introDelay = sessionStorage.getItem("introPlayed") ? 300 : 3800;
        const introDelay = 3800; // Force full delay for intro animation every time
        startTyping(introDelay);

        const origChangeLanguage = window.changeLanguage;
        if (origChangeLanguage) {
            window.changeLanguage = async function(lang) {
                await origChangeLanguage(lang);
                startTyping(200);
            };
        }
    }
});
