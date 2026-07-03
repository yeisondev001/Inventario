// Wrapper global de fetch:
//  - Adjunta automáticamente "Authorization: Bearer <token>" a llamadas relativas o al mismo origen
//  - Si recibe 401, limpia sesión y redirige al login
(function () {
    const TOKEN_KEY = 'authToken';
    const PUBLIC_PATHS = ['/login', '/auth/forgot-password', '/auth/reset-password'];

    function isApiCall(url) {
        if (typeof url !== 'string') return false;
        if (url.startsWith('/')) return true;
        try {
            const u = new URL(url, window.location.origin);
            return u.origin === window.location.origin;
        } catch { return false; }
    }

    function isPublic(url) {
        return PUBLIC_PATHS.some(p => typeof url === 'string' && url.endsWith(p));
    }

    const originalFetch = window.fetch.bind(window);
    window.fetch = async function (input, init) {
        init = init || {};
        const url = typeof input === 'string' ? input : (input && input.url) || '';
        const token = localStorage.getItem(TOKEN_KEY);

        if (token && isApiCall(url) && !isPublic(url)) {
            const headers = new Headers(init.headers || (typeof input !== 'string' ? input.headers : undefined));
            if (!headers.has('Authorization')) {
                headers.set('Authorization', 'Bearer ' + token);
            }
            init.headers = headers;
        }

        const response = await originalFetch(input, init);

        if (response.status === 401 && isApiCall(url) && !isPublic(url)) {
            localStorage.removeItem(TOKEN_KEY);
            sessionStorage.clear();
            if (!window.location.pathname.endsWith('login.html')) {
                window.location.href = '/login.html';
            }
        }

        return response;
    };

    // Verifica al cargar la página: si no hay token y no estamos en login, redirigir
    if (!localStorage.getItem(TOKEN_KEY) && !window.location.pathname.endsWith('login.html')) {
        window.location.href = '/login.html';
    }
})();
