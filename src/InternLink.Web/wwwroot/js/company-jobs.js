/**
 * Company Jobs Management Script
 * Handles dynamic skill weighting and job closing confirmation.
 */
document.addEventListener('DOMContentLoaded', () => {
    initSkillWeightManager();
    initCloseJobModal();
});

const WEIGHT_LABELS = {
    1: '1 - Nice to have',
    2: '2 - Helpful',
    3: '3 - Preferred',
    4: '4 - Important',
    5: '5 - Critical'
};

function initSkillWeightManager() {
    const addSkillBtn = document.getElementById('addSkillBtn');
    const skillSelect = document.getElementById('skillPickerSelect');
    const container = document.getElementById('selectedSkillsContainer');
    const emptyNotice = document.getElementById('noSkillsNotice');

    if (!addSkillBtn || !skillSelect || !container) return;

    // Initialize existing sliders
    container.querySelectorAll('.skill-weight-slider').forEach(slider => {
        bindSliderEvent(slider);
    });

    addSkillBtn.addEventListener('click', () => {
        const skillId = skillSelect.value;
        const skillName = skillSelect.options[skillSelect.selectedIndex]?.getAttribute('data-name');

        if (!skillId || !skillName) {
            alert('Please select a skill to add.');
            return;
        }

        // Check if skill is already in list
        const existing = container.querySelector(`[data-skill-id="${skillId}"]`);
        if (existing) {
            alert(`"${skillName}" is already added.`);
            return;
        }

        const index = container.querySelectorAll('.skill-row-item').length;
        const card = document.createElement('div');
        card.className = 'card bg-light border p-3 rounded-3 skill-row-item mb-2';
        card.setAttribute('data-skill-id', skillId);

        card.innerHTML = `
            <input type="hidden" name="SelectedSkills[${index}].SkillId" value="${skillId}" />
            <input type="hidden" name="SelectedSkills[${index}].SkillName" value="${escapeHtml(skillName)}" />
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="fw-bold text-slate-800">${escapeHtml(skillName)}</span>
                <div class="d-flex align-items-center gap-2">
                    <span class="badge bg-primary weight-label" id="label_${index}">${WEIGHT_LABELS[3]}</span>
                    <button type="button" class="btn btn-sm btn-outline-danger border-0 remove-skill-btn" title="Remove skill">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
            </div>
            <div class="d-flex align-items-center gap-3">
                <span class="small text-muted" style="min-width: 80px;">Weight (1-5):</span>
                <input type="range" class="form-range skill-weight-slider" name="SelectedSkills[${index}].Weight" min="1" max="5" value="3" data-target-label="label_${index}" />
            </div>
        `;

        container.appendChild(card);
        if (emptyNotice) emptyNotice.style.display = 'none';

        bindSliderEvent(card.querySelector('.skill-weight-slider'));
        card.querySelector('.remove-skill-btn').addEventListener('click', () => {
            card.remove();
            reindexSkills();
        });

        // Reset dropdown
        skillSelect.value = '';
    });

    // Bind remove buttons for initial items
    container.querySelectorAll('.remove-skill-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            const card = e.target.closest('.skill-row-item');
            if (card) {
                card.remove();
                reindexSkills();
            }
        });
    });

    function reindexSkills() {
        const items = container.querySelectorAll('.skill-row-item');
        if (items.length === 0 && emptyNotice) {
            emptyNotice.style.display = 'block';
        }

        items.forEach((card, idx) => {
            const hiddenId = card.querySelector('input[name*="SkillId"]');
            const hiddenName = card.querySelector('input[name*="SkillName"]');
            const slider = card.querySelector('input[name*="Weight"]');
            const label = card.querySelector('.weight-label');

            if (hiddenId) hiddenId.name = `SelectedSkills[${idx}].SkillId`;
            if (hiddenName) hiddenName.name = `SelectedSkills[${idx}].SkillName`;
            if (slider) {
                slider.name = `SelectedSkills[${idx}].Weight`;
                slider.setAttribute('data-target-label', `label_${idx}`);
            }
            if (label) label.id = `label_${idx}`;
        });
    }

    function bindSliderEvent(slider) {
        if (!slider) return;
        slider.addEventListener('input', (e) => {
            const val = parseInt(e.target.value, 10);
            const targetId = e.target.getAttribute('data-target-label');
            const labelEl = document.getElementById(targetId);
            if (labelEl) {
                labelEl.textContent = WEIGHT_LABELS[val] || `${val}`;
                if (val === 5) {
                    labelEl.className = 'badge bg-danger weight-label';
                } else if (val >= 3) {
                    labelEl.className = 'badge bg-primary weight-label';
                } else {
                    labelEl.className = 'badge bg-secondary weight-label';
                }
            }
        });
    }
}

function initCloseJobModal() {
    const closeButtons = document.querySelectorAll('.trigger-close-job-btn');
    const modalJobTitle = document.getElementById('closeJobModalTitle');
    const confirmForm = document.getElementById('closeJobConfirmForm');

    if (!closeButtons.length || !confirmForm) return;

    closeButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const jobId = btn.getAttribute('data-job-id');
            const jobTitle = btn.getAttribute('data-job-title');

            if (modalJobTitle) modalJobTitle.textContent = jobTitle || 'this job posting';
            confirmForm.action = `/Company/Jobs/Close/${jobId}`;
        });
    });
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, "&amp;")
              .replace(/</g, "&lt;")
              .replace(/>/g, "&gt;")
              .replace(/"/g, "&quot;")
              .replace(/'/g, "&#039;");
}
