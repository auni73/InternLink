// api.js — THE single fetch wrapper for InternLink.
// Every other JS module imports from here; nobody else calls fetch() directly.
// Non-GET requests automatically attach the antiforgery header (X-CSRF-TOKEN),
// read from the <meta name="request-verification-token"> tag in _Layout.

export class ApiError extends Error {
    constructor(message, status, payload) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
        this.payload = payload;
    }
}

const CSRF_HEADER = 'X-CSRF-TOKEN';

function csrfToken() {
    const meta = document.querySelector('meta[name="request-verification-token"]');
    return meta ? meta.getAttribute('content') : '';
}

async function request(method, url, body) {
    const headers = { 'Accept': 'application/json' };
    const options = { method, headers, credentials: 'same-origin' };

    if (method !== 'GET' && method !== 'HEAD') {
        headers[CSRF_HEADER] = csrfToken();
        if (body !== undefined) {
            headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        }
    }

    const response = await fetch(url, options);

    let payload = null;
    const text = await response.text();
    if (text) {
        try { payload = JSON.parse(text); } catch { payload = text; }
    }

    if (!response.ok) {
        // Server error envelope: { error: "message" }
        const message = (payload && typeof payload === 'object' && payload.error)
            ? payload.error
            : `Request failed (${response.status})`;
        throw new ApiError(message, response.status, payload);
    }

    return payload;
}

export const api = {
    get: (url) => request('GET', url),
    post: (url, body) => request('POST', url, body),
    put: (url, body) => request('PUT', url, body)
};
