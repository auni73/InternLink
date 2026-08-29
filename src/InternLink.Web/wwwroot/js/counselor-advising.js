// Counselor Advising live markdown preview & character counter
document.addEventListener('DOMContentLoaded', function () {
    const markdownInput = document.getElementById('narrativeMarkdownInput');
    const previewContainer = document.getElementById('markdownPreviewContainer');
    const charCounter = document.getElementById('markdownCharCount');
    const maxChars = 5000;

    function escapeHtml(text) {
        return text
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function renderSimpleClientMarkdown(raw) {
        if (!raw || !raw.trim()) {
            return '<em class="text-muted">Live preview will appear here as you type...</em>';
        }

        // Basic client-side markdown representation (server enforces final Markdig rendering with DisableHtml)
        let html = escapeHtml(raw);

        // Bold & Italic
        html = html.replace(/\*\*\*(.*?)\*\*\*/g, '<strong><em>$1</em></strong>');
        html = html.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        html = html.replace(/\*(.*?)\*/g, '<em>$1</em>');

        // Headers (# ...)
        html = html.replace(/^### (.*$)/gim, '<h6 class="fw-bold mt-2">$1</h6>');
        html = html.replace(/^## (.*$)/gim, '<h5 class="fw-bold mt-2">$1</h5>');
        html = html.replace(/^# (.*$)/gim, '<h4 class="fw-bold mt-2">$1</h4>');

        // Blockquotes
        html = html.replace(/^\> (.*$)/gim, '<blockquote class="border-start border-3 border-primary ps-3 my-2 text-muted">$1</blockquote>');

        // Unordered lists
        html = html.replace(/^\- (.*$)/gim, '<li>$1</li>');
        html = html.replace(/(<li>.*<\/li>)/s, '<ul class="mb-2">$1</ul>');

        // Inline code
        html = html.replace(/`([^`]+)`/g, '<code class="bg-light px-1 rounded text-primary">$1</code>');

        // Line breaks
        html = html.replace(/\n/g, '<br />');

        return html;
    }

    function updatePreview() {
        if (!markdownInput) return;
        const val = markdownInput.value || '';
        const count = val.length;

        if (charCounter) {
            charCounter.textContent = `${count.toLocaleString()} / ${maxChars.toLocaleString()}`;
            if (count > maxChars) {
                charCounter.classList.add('text-danger');
                charCounter.classList.remove('text-muted');
            } else {
                charCounter.classList.remove('text-danger');
                charCounter.classList.add('text-muted');
            }
        }

        if (previewContainer) {
            previewContainer.innerHTML = renderSimpleClientMarkdown(val);
        }
    }

    if (markdownInput) {
        markdownInput.addEventListener('input', updatePreview);
        updatePreview();
    }

    // Preserve active tab on reload if hash exists
    if (window.location.hash) {
        const targetTabTrigger = document.querySelector(`[data-bs-target="${window.location.hash}"]`) || 
                                 document.querySelector(`a[href="${window.location.hash}"]`);
        if (targetTabTrigger && typeof bootstrap !== 'undefined') {
            const tab = new bootstrap.Tab(targetTabTrigger);
            tab.show();
        }
    }
});
