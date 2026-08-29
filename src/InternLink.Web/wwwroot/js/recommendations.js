// recommendations.js — hydrates the "Recommended for You" section after the main job list has rendered.
import { api } from './api.js';

const SKELETON_COUNT = 3;

function badgeClass(matchPercentage) {
    if (matchPercentage >= 75) return 'bg-success-subtle text-success border-success-subtle';
    if (matchPercentage >= 50) return 'bg-warning-subtle text-warning-emphasis border-warning-subtle';
    return 'bg-secondary-subtle text-secondary border-secondary-subtle';
}

function skeletonMarkup() {
    return Array.from({ length: SKELETON_COUNT }, () => `
        <div class="col-lg-4 col-md-6">
            <div class="card border-0 shadow-sm rounded-3 h-100" aria-hidden="true">
                <div class="card-body">
                    <div class="placeholder-glow">
                        <span class="placeholder col-7 mb-2"></span>
                        <span class="placeholder col-4 mb-3"></span>
                        <span class="placeholder col-12"></span>
                        <span class="placeholder col-10"></span>
                    </div>
                </div>
            </div>
        </div>`).join('');
}

function cardMarkup(job) {
    const deadline = new Date(job.deadLine).toLocaleDateString(undefined, {
        year: 'numeric', month: 'short', day: 'numeric'
    });

    return `
        <div class="col-lg-4 col-md-6">
            <div class="card border-0 shadow-sm rounded-3 h-100">
                <div class="card-body d-flex flex-column">
                    <div class="d-flex justify-content-between align-items-start gap-2 mb-2">
                        <h4 class="h6 fw-bold text-slate-800 mb-0">${escapeHtml(job.title)}</h4>
                        <span class="badge rounded-pill border ${badgeClass(job.matchPercentage)} flex-shrink-0">
                            ${job.matchPercentage}% match
                        </span>
                    </div>
                    <div class="text-muted small mb-2">
                        <i class="bi bi-building me-1"></i>${escapeHtml(job.companyName)}
                        <span class="mx-1">&middot;</span>${escapeHtml(job.locationType)}
                    </div>
                    <p class="text-muted small mb-3">${escapeHtml(job.reason)}</p>
                    <div class="mt-auto d-flex justify-content-between align-items-center">
                        <span class="text-muted small"><i class="bi bi-calendar-event me-1"></i>${deadline}</span>
                        <a href="/Student/Jobs/Details/${job.jobId}" class="btn btn-sm btn-outline-primary">View</a>
                    </div>
                </div>
            </div>
        </div>`;
}

function escapeHtml(value) {
    const div = document.createElement('div');
    div.textContent = value ?? '';
    return div.innerHTML;
}

document.addEventListener('DOMContentLoaded', async () => {
    const section = document.getElementById('recommendations');
    const grid = document.getElementById('recommendationsGrid');
    const degradedNote = document.getElementById('recommendationsDegradedNote');
    if (!section || !grid) return;

    const url = section.getAttribute('data-recommendations-url');
    if (!url) return;

    section.hidden = false;
    grid.innerHTML = skeletonMarkup();

    try {
        const result = await api.get(url);
        const jobs = result?.jobs ?? [];

        if (jobs.length === 0) {
            section.hidden = true;
            return;
        }

        if (degradedNote) degradedNote.hidden = !result.degraded;
        grid.innerHTML = jobs.map(cardMarkup).join('');
    } catch {
        // Recommendations are additive; a failure here must never disturb the job list below.
        section.hidden = true;
    }
});
