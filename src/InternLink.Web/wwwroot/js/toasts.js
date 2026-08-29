// toasts.js — Bootstrap toast helper exposed globally as showToast(message, variant).
// variant: 'success' | 'danger' | 'warning' | 'info' | 'primary' (maps to text-bg-*).
(function () {
    function showToast(message, variant) {
        variant = variant || 'primary';
        var container = document.getElementById('toastContainer');
        if (!container) {
            return;
        }

        var toast = document.createElement('div');
        toast.className = 'toast align-items-center text-bg-' + variant + ' border-0';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        var flex = document.createElement('div');
        flex.className = 'd-flex';

        var bodyEl = document.createElement('div');
        bodyEl.className = 'toast-body';
        bodyEl.textContent = message;

        var closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'btn-close btn-close-white me-2 m-auto';
        closeBtn.setAttribute('data-bs-dismiss', 'toast');
        closeBtn.setAttribute('aria-label', 'Close');

        flex.appendChild(bodyEl);
        flex.appendChild(closeBtn);
        toast.appendChild(flex);
        container.appendChild(toast);

        var instance = window.bootstrap.Toast.getOrCreateInstance(toast, { delay: 4000 });
        toast.addEventListener('hidden.bs.toast', function () { toast.remove(); });
        instance.show();
    }

    window.showToast = showToast;
})();
