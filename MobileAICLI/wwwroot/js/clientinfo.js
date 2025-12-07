window.clientInfo = {
    get: () => {
        const docEl = document.documentElement;
        const viewportMeta = document.querySelector('meta[name="viewport"]');
        return {
            innerWidth: window.innerWidth,
            innerHeight: window.innerHeight,
            devicePixelRatio: window.devicePixelRatio,
            screenWidth: window.screen?.width || null,
            screenHeight: window.screen?.height || null,
            orientation: window.screen?.orientation?.type || null,
            userAgent: navigator.userAgent,
            language: navigator.language,
            platform: navigator.platform,
            viewportMeta: viewportMeta ? viewportMeta.getAttribute('content') : null,
            zoom: docEl ? Number((docEl.clientWidth / window.innerWidth).toFixed(3)) : null
        };
    }
};
