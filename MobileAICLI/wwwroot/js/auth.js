window.authHelper = {
    login: async function (password) {
        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ password: password }),
                credentials: 'same-origin'
            });

            if (response.ok) {
                const result = await response.json();
                return result;
            } else {
                return { success: false, error: 'Request failed' };
            }
        } catch (error) {
            return { success: false, error: error.message };
        }
    },

    logout: async function () {
        try {
            await fetch('/api/auth/logout', {
                method: 'POST',
                credentials: 'same-origin'
            });
            return { success: true };
        } catch (error) {
            return { success: false, error: error.message };
        }
    }
};
