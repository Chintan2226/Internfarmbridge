/* Video Showcase — auto-play on scroll, sound after first interaction */
(function () {
    var video   = document.getElementById('showcase-video');
    var section = document.getElementById('videos');
    if (!video || !section) return;

    var hasUserInteracted = false;
    var isInView = false;

    function onFirstInteraction() {
        hasUserInteracted = true;
        if (isInView && video.muted) {
            video.muted  = false;
            video.volume = 0.6;
        }
        document.removeEventListener('click', onFirstInteraction);
        document.removeEventListener('touchstart', onFirstInteraction);
        document.removeEventListener('keydown', onFirstInteraction);
    }

    document.addEventListener('click', onFirstInteraction);
    document.addEventListener('touchstart', onFirstInteraction);
    document.addEventListener('keydown', onFirstInteraction);

    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                isInView = true;
                if (hasUserInteracted) {
                    video.muted  = false;
                    video.volume = 0.6;
                }
                video.play().catch(function () {});
            } else {
                isInView = false;
                video.muted = true;
                video.pause();
            }
        });
    }, { threshold: 0.35 });

    observer.observe(section);
})();
