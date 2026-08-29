// resume-analysis.js — drives the ATS review page: skeleton, score ring, issue lists, rewrite cards.
import { api } from './api.js';

const RING_RADIUS = 54;
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS;
const UNAVAILABLE_SCORE = -1;

function ringColor(score) {
    if (score >= 75) return '#198754';
    if (score >= 50) return '#fd7e14';
    return '#dc3545';
}

function escapeHtml(value) {
    const div = document.createElement('div');
    div.textContent = value ?? '';
    return div.innerHTML;
}

function ringMarkup(score) {
    const pct = Math.max(0, Math.min(100, score));
    const filled = (pct / 100) * RING_CIRCUMFERENCE;

    return `
        <svg width="140" height="140" viewBox="0 0 140 140" role="img" aria-label="ATS score ${pct} out of 100">
            <circle cx="70" cy="70" r="${RING_RADIUS}" fill="none" stroke="#e9ecef" stroke-width="12"></circle>
            <circle cx="70" cy="70" r="${RING_RADIUS}" fill="none" stroke="${ringColor(pct)}" stroke-width="12"
                    stroke-linecap="round" stroke-dasharray="${filled} ${RING_CIRCUMFERENCE}"
                    transform="rotate(-90 70 70)"></circle>
            <text x="70" y="78" text-anchor="middle" font-size="30" font-weight="700" fill="#212529">${pct}</text>
        </svg>`;
}

function listMarkup(icon, title, items, emptyText) {
    const body = items && items.length
        ? `<ul class="mb-0 ps-3 small text-muted">${items.map(i => `<li class="mb-1">${escapeHtml(i)}</li>`).join('')}</ul>`
        : `<p class="mb-0 small text-muted fst-italic">${escapeHtml(emptyText)}</p>`;

    return `
        <div class="col-lg-6">
            <div class="card border-0 shadow-sm rounded-3 h-100">
                <div class="card-body">
                    <h3 class="h6 fw-bold text-slate-800 mb-3"><i class="bi ${icon} me-2 text-primary"></i>${escapeHtml(title)}</h3>
                    ${body}
                </div>
            </div>
        </div>`;
}

function suggestionMarkup(s) {
    return `
        <div class="card border-0 shadow-sm rounded-3 mb-3">
            <div class="card-body">
                <div class="row g-3 align-items-stretch">
                    <div class="col-md-6">
                        <div class="text-uppercase small fw-semibold text-danger mb-1">Before</div>
                        <div class="p-3 bg-danger-subtle rounded-3 small text-slate-800">${escapeHtml(s.originalText)}</div>
                    </div>
                    <div class="col-md-6">
                        <div class="text-uppercase small fw-semibold text-success mb-1">After</div>
                        <div class="p-3 bg-success-subtle rounded-3 small text-slate-800">${escapeHtml(s.suggestedText)}</div>
                    </div>
                </div>
                <div class="text-muted small mt-3"><i class="bi bi-lightbulb me-1"></i>${escapeHtml(s.reason)}</div>
            </div>
        </div>`;
}

// Mirrors the real result layout so the page does not jump when content arrives.
function skeletonMarkup() {
    const rows = n => Array.from({ length: n }, () => '<span class="placeholder col-12 mb-2"></span>').join('');

    return `
        <div class="card border-0 shadow-sm rounded-3 mb-4">
            <div class="card-body p-4 d-flex flex-wrap align-items-center gap-4">
                <svg width="140" height="140" viewBox="0 0 140 140" aria-hidden="true">
                    <circle cx="70" cy="70" r="${RING_RADIUS}" fill="none" stroke="#e9ecef" stroke-width="12"></circle>
                </svg>
                <div class="flex-grow-1 placeholder-glow" style="min-width:240px">${rows(3)}</div>
            </div>
        </div>
        <div class="row g-3 mb-4">
            <div class="col-lg-6"><div class="card border-0 shadow-sm rounded-3"><div class="card-body placeholder-glow">${rows(3)}</div></div></div>
            <div class="col-lg-6"><div class="card border-0 shadow-sm rounded-3"><div class="card-body placeholder-glow">${rows(3)}</div></div></div>
        </div>`;
}

function unavailableMarkup(message) {
    return `
        <div class="card border-0 shadow-sm rounded-3">
            <div class="card-body text-center p-5">
                <i class="bi bi-arrow-clockwise display-6 text-muted d-block mb-3"></i>
                <h3 class="h5 fw-bold text-slate-800 mb-2">Analysis unavailable</h3>
                <p class="text-muted mb-3">${escapeHtml(message)}</p>
                <button type="button" class="btn btn-outline-primary" id="retryAnalysisBtn">Try again</button>
            </div>
        </div>`;
}

function resultMarkup(result) {
    const score = result.score ?? {};
    const suggestions = result.suggestions ?? [];

    const suggestionsSection = suggestions.length
        ? `<h3 class="h6 fw-bold text-slate-800 mb-3"><i class="bi bi-pencil-square me-2 text-primary"></i>Suggested rewrites</h3>
           ${suggestions.map(suggestionMarkup).join('')}`
        : '';

    return `
        <div class="card border-0 shadow-sm rounded-3 mb-4">
            <div class="card-body p-4 d-flex flex-wrap align-items-center gap-4">
                ${ringMarkup(score.atsScore)}
                <div class="flex-grow-1" style="min-width:240px">
                    <h3 class="h6 fw-bold text-slate-800 mb-2">Structure critique</h3>
                    <p class="text-muted mb-0">${escapeHtml(score.structureCritique)}</p>
                </div>
            </div>
        </div>
        <div class="row g-3 mb-4">
            ${listMarkup('bi-spellcheck', 'Grammar & wording', score.grammarIssues, 'No grammar issues flagged.')}
            ${listMarkup('bi-key', 'Missing keywords', score.missingKeywords, 'No missing keywords flagged.')}
        </div>
        ${suggestionsSection}`;
}

document.addEventListener('DOMContentLoaded', () => {
    const root = document.getElementById('resumeAnalysis');
    const output = document.getElementById('analysisResult');
    const button = document.getElementById('runAnalysisBtn');
    const jobSelect = document.getElementById('targetJobSelect');
    if (!root || !output || !button) return;

    const baseUrl = root.getAttribute('data-analyze-url');

    async function run() {
        const targetJobId = jobSelect?.value ?? '';
        const url = targetJobId ? `${baseUrl}?targetJobId=${encodeURIComponent(targetJobId)}` : baseUrl;

        button.disabled = true;
        output.hidden = false;
        output.innerHTML = skeletonMarkup();

        try {
            const result = await api.post(url);

            output.innerHTML = (result?.score?.atsScore ?? UNAVAILABLE_SCORE) === UNAVAILABLE_SCORE
                ? unavailableMarkup(result?.score?.structureCritique ?? 'Please try again in a moment.')
                : resultMarkup(result);
        } catch (error) {
            output.innerHTML = unavailableMarkup(error?.message ?? 'Please try again in a moment.');
        } finally {
            button.disabled = false;
            document.getElementById('retryAnalysisBtn')?.addEventListener('click', run);
        }
    }

    button.addEventListener('click', run);
});
