// CopilotInteractive.razor JavaScript helpers

window.copilotInteractive = {
    // Scroll to the bottom of the message container
    scrollToBottom: function(elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.scrollTop = element.scrollHeight;
            }
        } catch (error) {
            console.error('Error scrolling to bottom:', error);
        }
    },

    // Scroll to the bottom of the message container smoothly
    scrollToBottomSmooth: function(elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.scrollTo({
                    top: element.scrollHeight,
                    behavior: 'smooth'
                });
            }
        } catch (error) {
            console.error('Error scrolling to bottom:', error);
        }
    },

    // Check if the user is near the bottom of the scroll area
    isNearBottom: function(elementId, threshold = 100) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                const scrollPosition = element.scrollTop + element.clientHeight;
                const scrollHeight = element.scrollHeight;
                return scrollHeight - scrollPosition < threshold;
            }
            return false;
        } catch (error) {
            console.error('Error checking scroll position:', error);
            return false;
        }
    }
};
