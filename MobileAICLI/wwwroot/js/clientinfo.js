window.clientInfo = {
    get: () => {
        const docEl = document.documentElement;
        const viewportMeta = document.querySelector('meta[name="viewport"]');

        const readStyles = (selector, fallbackTag) => {
            let el = document.querySelector(selector);
            let created = false;
            if (!el && fallbackTag) {
                el = document.createElement(fallbackTag);
                el.style.visibility = 'hidden';
                el.style.position = 'absolute';
                el.style.pointerEvents = 'none';
                document.body.appendChild(el);
                created = true;
            }

            if (!el) return null;

            const cs = getComputedStyle(el);
            const result = {
                fontSize: cs.fontSize,
                lineHeight: cs.lineHeight,
                minHeight: cs.minHeight,
                padding: cs.padding,
                paddingTop: cs.paddingTop,
                paddingBottom: cs.paddingBottom,
                paddingLeft: cs.paddingLeft,
                paddingRight: cs.paddingRight
            };

            if (created) {
                document.body.removeChild(el);
            }

            return result;
        };

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
            zoom: docEl ? Number((docEl.clientWidth / window.innerWidth).toFixed(3)) : null,
            styles: {
                body: readStyles('body'),
                button: readStyles('button, .btn', 'button'),
                input: readStyles('input.form-control, .form-control', 'input'),
                select: readStyles('select.form-select, .form-select', 'select')
            }
        };
    }
};
