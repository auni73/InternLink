// skill-gap.js — loads the shared skill gap panel on the student job detail page.
// The endpoint returns rendered Razor markup, so the student and company views cannot drift.
import { api } from '/js/api.js';

const card = document.getElementById('skillGapCard');

if (card) {
    const button = document.getElementById('loadSkillGapBtn');
    const content = document.getElementById('skillGapContent');
    const status = document.getElementById('skillGapStatus');

    button.addEventListener('click', async () => {
        status.classList.add('d-none');
        button.disabled = true;
        button.innerHTML = '<span class="spinner-border spinner-border-sm"></span><span>Analyzing…</span>';

        try {
            const markup = await api.get(card.dataset.skillGapUrl);
            content.innerHTML = markup;
            content.hidden = false;
            button.innerHTML = '<i class="bi bi-arrow-clockwise"></i><span>Refresh Analysis</span>';
        } catch (error) {
            status.textContent = error.message;
            status.classList.remove('d-none');
            button.innerHTML = '<i class="bi bi-clipboard-data"></i><span>Analyze My Fit</span>';
        } finally {
            button.disabled = false;
        }
    });
}
