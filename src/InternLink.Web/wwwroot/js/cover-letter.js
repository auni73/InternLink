// cover-letter.js — generate, edit, save, and download the letter on the job detail page.
// Nothing persists without an explicit Save click, and nothing regenerates without an explicit Regenerate click.
import { api } from './api.js';

function countWords(text) {
    const trimmed = (text ?? '').trim();
    return trimmed ? trimmed.split(/\s+/).length : 0;
}

document.addEventListener('DOMContentLoaded', () => {
    const card = document.getElementById('coverLetterCard');
    if (!card) return;

    const generateBtn = document.getElementById('generateCoverLetterBtn');
    const regenerateBtn = document.getElementById('regenerateCoverLetterBtn');
    const saveBtn = document.getElementById('saveCoverLetterBtn');
    const editor = document.getElementById('coverLetterEditor');
    const skeleton = document.getElementById('coverLetterSkeleton');
    const textarea = document.getElementById('coverLetterText');
    const wordCount = document.getElementById('coverLetterWordCount');
    const status = document.getElementById('coverLetterStatus');
    const downloadField = document.getElementById('coverLetterDownloadField');
    const downloadForm = document.getElementById('coverLetterDownloadForm');

    const generateUrl = card.getAttribute('data-generate-url');
    const saveUrl = card.getAttribute('data-save-url');

    function setStatus(message, cssClass) {
        status.textContent = message;
        status.className = `small ${cssClass}`;
    }

    function refreshWordCount() {
        wordCount.textContent = `${countWords(textarea.value)} words`;
    }

    async function generate() {
        generateBtn.disabled = true;
        if (regenerateBtn) regenerateBtn.disabled = true;
        skeleton.hidden = false;
        setStatus('', '');

        try {
            const result = await api.post(generateUrl);
            textarea.value = result?.generatedText ?? '';
            editor.hidden = false;
            refreshWordCount();
            setStatus('Draft ready — edit it before saving.', 'text-muted');
        } catch (error) {
            setStatus(error?.message ?? 'Could not generate a letter right now.', 'text-danger');
            editor.hidden = textarea.value.trim().length === 0;
        } finally {
            skeleton.hidden = true;
            generateBtn.disabled = false;
            if (regenerateBtn) regenerateBtn.disabled = false;
        }
    }

    async function save() {
        const finalText = textarea.value.trim();
        if (!finalText) {
            setStatus('Write or generate a letter first.', 'text-danger');
            return;
        }

        saveBtn.disabled = true;
        try {
            const result = await api.post(saveUrl, { finalText });
            setStatus(result?.message ?? 'Saved to your application.', 'text-success');
        } catch (error) {
            setStatus(error?.message ?? 'Could not save the letter.', 'text-danger');
        } finally {
            saveBtn.disabled = false;
        }
    }

    generateBtn.addEventListener('click', generate);
    regenerateBtn?.addEventListener('click', generate);
    saveBtn?.addEventListener('click', save);
    textarea?.addEventListener('input', refreshWordCount);

    // The download posts a plain form, so the current editor content has to be copied across first.
    downloadForm?.addEventListener('submit', () => {
        downloadField.value = textarea.value;
    });
});
