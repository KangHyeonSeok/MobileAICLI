// Mobile address bar auto-hide functionality
(function() {
    'use strict';

    // Function to hide address bar on mobile
    function hideAddressBar() {
        if (!window.location.hash) {
            if (document.height < window.outerHeight) {
                document.body.style.height = (window.outerHeight + 50) + 'px';
            }
            setTimeout(function() {
                window.scrollTo(0, 1);
            }, 50);
        }
    }

    // Hide address bar on page load
    window.addEventListener('load', function() {
        setTimeout(hideAddressBar, 0);
    });

    // Re-hide on orientation change
    window.addEventListener('orientationchange', function() {
        setTimeout(hideAddressBar, 100);
    });

    // Prevent pull-to-refresh on mobile
    let lastTouchY = 0;
    let preventPullToRefresh = false;

    document.addEventListener('touchstart', function(e) {
        if (e.touches.length !== 1) { return; }
        lastTouchY = e.touches[0].clientY;
        preventPullToRefresh = window.pageYOffset === 0;
    }, { passive: false });

    document.addEventListener('touchmove', function(e) {
        const touchY = e.touches[0].clientY;
        const touchYDelta = touchY - lastTouchY;
        lastTouchY = touchY;

        if (preventPullToRefresh) {
            // Prevent pull-to-refresh if at top of page
            if (touchYDelta > 0) {
                e.preventDefault();
                return;
            }
        }
    }, { passive: false });

    // Make viewport height consistent (for mobile browsers with dynamic UI)
    function setViewportHeight() {
        const vh = window.innerHeight * 0.01;
        document.documentElement.style.setProperty('--vh', `${vh}px`);
    }

    window.addEventListener('resize', setViewportHeight);
    window.addEventListener('orientationchange', setViewportHeight);
    setViewportHeight();

})();
