// Mobile viewport and address bar fixes
(function () {
    'use strict';

    // Set accurate viewport height for mobile browsers
    function setViewportHeight() {
        // Use visualViewport if available (more accurate on mobile)
        const vh = (window.visualViewport ? window.visualViewport.height : window.innerHeight) * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);

        // Also set the actual height on html/body for mobile Chrome
        document.documentElement.style.height = window.innerHeight + 'px';
        document.body.style.height = window.innerHeight + 'px';
    }

    // Initialize immediately
    setViewportHeight();

    // Update on various events
    window.addEventListener('load', setViewportHeight);
    window.addEventListener('resize', setViewportHeight);
    window.addEventListener('orientationchange', function () {
        // Delay for orientation change to complete
        setTimeout(setViewportHeight, 100);
    });

    // Use visualViewport API if available
    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', setViewportHeight);
        window.visualViewport.addEventListener('scroll', setViewportHeight);
    }

    // Prevent body scroll/bounce on iOS
    document.body.addEventListener('touchmove', function (e) {
        // Only prevent if scrolling at the top of page and scrolling up
        if (document.body.scrollTop === 0 && e.touches[0].clientY > 0) {
            // Allow normal scrolling
        }
    }, { passive: true });

    // Fix for mobile Chrome address bar
    // Force recalculation after paint
    requestAnimationFrame(function () {
        setViewportHeight();
    });

})();
