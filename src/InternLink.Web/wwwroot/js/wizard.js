import { api } from './api.js';

export class ResumeWizard {
    constructor(options) {
        this.resumeId = options.resumeId;
        this.currentStepIndex = 0;
        this.steps = ['personal-info', 'education', 'experience', 'skills', 'review'];
        this.availableSkills = options.availableSkills || [];
        this.initialData = options.initialData || {};

        this.initDOMElements();
        this.bindEvents();
        this.renderCurrentStep();
    }

    initDOMElements() {
        this.stepContainers = document.querySelectorAll('.wizard-step-panel');
        this.stepIndicators = document.querySelectorAll('.wizard-step-indicator');
        this.btnPrev = document.getElementById('wizardPrevBtn');
        this.btnNext = document.getElementById('wizardNextBtn');
        this.btnSave = document.getElementById('wizardSaveBtn');
        this.btnFinalize = document.getElementById('wizardFinalizeBtn');
        this.saveStatusEl = document.getElementById('saveStatusIndicator');

        // Education
        this.educationList = document.getElementById('educationItemsList');
        this.btnAddEducation = document.getElementById('btnAddEducation');

        // Experience
        this.experienceList = document.getElementById('experienceItemsList');
        this.btnAddExperience = document.getElementById('btnAddExperience');

        // Skills
        this.skillsList = document.getElementById('selectedSkillsList');
        this.skillSelect = document.getElementById('skillPickerSelect');
        this.skillProficiency = document.getElementById('skillProficiencyInput');
        this.btnAddSkill = document.getElementById('btnAddSkill');

        // Review & Finalize preview container
        this.reviewContainer = document.getElementById('reviewSummaryContainer');
    }

    bindEvents() {
        if (this.btnPrev) {
            this.btnPrev.addEventListener('click', () => this.goToPrevStep());
        }

        if (this.btnNext) {
            this.btnNext.addEventListener('click', () => this.handleNextStep());
        }

        if (this.btnSave) {
            this.btnSave.addEventListener('click', () => this.saveCurrentStep(true));
        }

        if (this.btnFinalize) {
            this.btnFinalize.addEventListener('click', () => this.finalizeResume());
        }

        this.stepIndicators.forEach((indicator, index) => {
            indicator.addEventListener('click', () => {
                this.saveCurrentStep(false).then(() => {
                    this.currentStepIndex = index;
                    this.renderCurrentStep();
                });
            });
        });

        if (this.btnAddEducation) {
            this.btnAddEducation.addEventListener('click', () => this.addEducationItem());
        }

        if (this.btnAddExperience) {
            this.btnAddExperience.addEventListener('click', () => this.addExperienceItem());
        }

        if (this.btnAddSkill) {
            this.btnAddSkill.addEventListener('click', () => this.addSkillItem());
        }
    }

    renderCurrentStep() {
        const stepName = this.steps[this.currentStepIndex];

        this.stepContainers.forEach(panel => {
            panel.classList.toggle('d-none', panel.dataset.step !== stepName);
        });

        this.stepIndicators.forEach((indicator, index) => {
            indicator.classList.toggle('active', index === this.currentStepIndex);
            indicator.classList.toggle('completed', index < this.currentStepIndex);
        });

        if (this.btnPrev) {
            this.btnPrev.disabled = (this.currentStepIndex === 0);
        }

        if (this.btnNext) {
            this.btnNext.classList.toggle('d-none', this.currentStepIndex === this.steps.length - 1);
        }

        if (this.btnFinalize) {
            this.btnFinalize.classList.toggle('d-none', this.currentStepIndex !== this.steps.length - 1);
        }

        if (stepName === 'review') {
            this.renderReviewSummary();
        }
    }

    async handleNextStep() {
        const saved = await this.saveCurrentStep(false);
        if (saved && this.currentStepIndex < this.steps.length - 1) {
            this.currentStepIndex++;
            this.renderCurrentStep();
        }
    }

    async goToPrevStep() {
        if (this.currentStepIndex > 0) {
            await this.saveCurrentStep(false);
            this.currentStepIndex--;
            this.renderCurrentStep();
        }
    }

    getStepPayload(stepName) {
        switch (stepName) {
            case 'personal-info': {
                return {
                    fullName: document.getElementById('pi_fullName')?.value?.trim() || '',
                    email: document.getElementById('pi_email')?.value?.trim() || '',
                    phone: document.getElementById('pi_phone')?.value?.trim() || '',
                    location: document.getElementById('pi_location')?.value?.trim() || '',
                    linkedIn: document.getElementById('pi_linkedIn')?.value?.trim() || '',
                    gitHub: document.getElementById('pi_gitHub')?.value?.trim() || '',
                    portfolio: document.getElementById('pi_portfolio')?.value?.trim() || '',
                    summary: document.getElementById('pi_summary')?.value?.trim() || ''
                };
            }
            case 'education': {
                const items = [];
                const rows = document.querySelectorAll('.education-entry-row');
                rows.forEach(row => {
                    items.push({
                        institution: row.querySelector('.edu-institution')?.value?.trim() || '',
                        degree: row.querySelector('.edu-degree')?.value?.trim() || '',
                        fieldOfStudy: row.querySelector('.edu-field')?.value?.trim() || '',
                        startDate: row.querySelector('.edu-start')?.value?.trim() || '',
                        endDate: row.querySelector('.edu-end')?.value?.trim() || '',
                        isCurrent: row.querySelector('.edu-current')?.checked || false,
                        gpa: row.querySelector('.edu-gpa')?.value?.trim() || '',
                        highlights: row.querySelector('.edu-highlights')?.value?.trim() || ''
                    });
                });
                return items;
            }
            case 'experience': {
                const items = [];
                const rows = document.querySelectorAll('.experience-entry-row');
                rows.forEach(row => {
                    items.push({
                        company: row.querySelector('.exp-company')?.value?.trim() || '',
                        role: row.querySelector('.exp-role')?.value?.trim() || '',
                        location: row.querySelector('.exp-location')?.value?.trim() || '',
                        startDate: row.querySelector('.exp-start')?.value?.trim() || '',
                        endDate: row.querySelector('.exp-end')?.value?.trim() || '',
                        isCurrent: row.querySelector('.exp-current')?.checked || false,
                        description: row.querySelector('.exp-desc')?.value?.trim() || '',
                        highlights: row.querySelector('.exp-highlights')?.value?.trim() || ''
                    });
                });
                return items;
            }
            case 'skills': {
                const items = [];
                const rows = document.querySelectorAll('.skill-entry-row');
                rows.forEach(row => {
                    const skillId = row.dataset.skillId;
                    const skillName = row.querySelector('.skill-name')?.textContent?.trim() || '';
                    const prof = parseInt(row.querySelector('.skill-prof-slider')?.value || '3', 10);
                    if (skillId) {
                        items.push({
                            skillId: skillId,
                            skillName: skillName,
                            proficiencyLevel: prof
                        });
                    }
                });
                return items;
            }
            default:
                return null;
        }
    }

    async saveCurrentStep(showToastNotice = true) {
        const stepName = this.steps[this.currentStepIndex];
        if (stepName === 'review') return true;

        const payload = this.getStepPayload(stepName);
        if (!payload) return true;

        this.setSavingState(true);

        try {
            const url = `/Student/Resumes/${this.resumeId}/Step/${stepName}`;
            await api.put(url, payload);

            this.setSavingState(false, 'All changes saved');
            if (showToastNotice && window.showToast) {
                window.showToast('Progress saved successfully.', 'success');
            }
            return true;
        } catch (error) {
            this.setSavingState(false, 'Save error');
            if (window.showToast) {
                window.showToast(error.message || 'Failed to save changes.', 'danger');
            }
            return false;
        }
    }

    setSavingState(isSaving, text = '') {
        if (!this.saveStatusEl) return;
        if (isSaving) {
            this.saveStatusEl.innerHTML = '<span class="spinner-border spinner-border-sm text-primary me-1"></span> Saving...';
        } else {
            this.saveStatusEl.innerHTML = `<i class="bi bi-check2-circle text-success me-1"></i> ${text || 'Saved'}`;
        }
    }

    addEducationItem(data = {}) {
        if (!this.educationList) return;
        const entryId = 'edu_' + Date.now();
        const wrapper = document.createElement('div');
        wrapper.className = 'education-entry-row card p-3 mb-3 border bg-light-subtle rounded-3';
        wrapper.id = entryId;

        wrapper.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="fw-semibold text-primary"><i class="bi bi-mortarboard me-1"></i>Education Program</span>
                <button type="button" class="btn btn-sm btn-outline-danger border-0 remove-entry-btn">
                    <i class="bi bi-trash"></i> Remove
                </button>
            </div>
            <div class="row g-3">
                <div class="col-md-6">
                    <label class="form-label small fw-medium">Institution / University *</label>
                    <input type="text" class="form-control form-control-sm edu-institution" value="${data.institution || ''}" placeholder="e.g. Ahsanullah University of Science and Technology" required />
                </div>
                <div class="col-md-6">
                    <label class="form-label small fw-medium">Degree *</label>
                    <input type="text" class="form-control form-control-sm edu-degree" value="${data.degree || ''}" placeholder="e.g. B.Sc. Engineering" required />
                </div>
                <div class="col-md-6">
                    <label class="form-label small fw-medium">Field of Study</label>
                    <input type="text" class="form-control form-control-sm edu-field" value="${data.fieldOfStudy || ''}" placeholder="e.g. Computer Science & Engineering" />
                </div>
                <div class="col-md-3">
                    <label class="form-label small fw-medium">Start Date</label>
                    <input type="text" class="form-control form-control-sm edu-start" value="${data.startDate || ''}" placeholder="e.g. 2021" />
                </div>
                <div class="col-md-3">
                    <label class="form-label small fw-medium">End Date</label>
                    <input type="text" class="form-control form-control-sm edu-end" value="${data.endDate || ''}" placeholder="e.g. 2025" />
                </div>
                <div class="col-md-4">
                    <label class="form-label small fw-medium">CGPA / Grade</label>
                    <input type="text" class="form-control form-control-sm edu-gpa" value="${data.gpa || ''}" placeholder="e.g. 3.85 / 4.00" />
                </div>
                <div class="col-md-8">
                    <label class="form-label small fw-medium">Highlights / Achievements</label>
                    <input type="text" class="form-control form-control-sm edu-highlights" value="${data.highlights || ''}" placeholder="e.g. Dean's Honor List, Merit Scholarship" />
                </div>
            </div>
        `;

        wrapper.querySelector('.remove-entry-btn').addEventListener('click', () => wrapper.remove());
        this.educationList.appendChild(wrapper);
    }

    addExperienceItem(data = {}) {
        if (!this.experienceList) return;
        const entryId = 'exp_' + Date.now();
        const wrapper = document.createElement('div');
        wrapper.className = 'experience-entry-row card p-3 mb-3 border bg-light-subtle rounded-3';
        wrapper.id = entryId;

        wrapper.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="fw-semibold text-primary"><i class="bi bi-briefcase me-1"></i>Work / Internship Experience</span>
                <button type="button" class="btn btn-sm btn-outline-danger border-0 remove-entry-btn">
                    <i class="bi bi-trash"></i> Remove
                </button>
            </div>
            <div class="row g-3">
                <div class="col-md-6">
                    <label class="form-label small fw-medium">Company / Organization *</label>
                    <input type="text" class="form-control form-control-sm exp-company" value="${data.company || ''}" placeholder="e.g. TechCorp Solutions" required />
                </div>
                <div class="col-md-6">
                    <label class="form-label small fw-medium">Job Title / Role *</label>
                    <input type="text" class="form-control form-control-sm exp-role" value="${data.role || ''}" placeholder="e.g. Software Engineering Intern" required />
                </div>
                <div class="col-md-4">
                    <label class="form-label small fw-medium">Location</label>
                    <input type="text" class="form-control form-control-sm exp-location" value="${data.location || ''}" placeholder="e.g. Dhaka, Bangladesh (Hybrid)" />
                </div>
                <div class="col-md-4">
                    <label class="form-label small fw-medium">Start Date</label>
                    <input type="text" class="form-control form-control-sm exp-start" value="${data.startDate || ''}" placeholder="e.g. Jun 2024" />
                </div>
                <div class="col-md-4">
                    <label class="form-label small fw-medium">End Date</label>
                    <input type="text" class="form-control form-control-sm exp-end" value="${data.endDate || ''}" placeholder="e.g. Aug 2024 (or Present)" />
                </div>
                <div class="col-12">
                    <label class="form-label small fw-medium">Key Responsibilities & Description</label>
                    <textarea rows="2" class="form-control form-control-sm exp-desc" placeholder="Developed REST APIs using ASP.NET Core and optimized SQL queries...">${data.description || ''}</textarea>
                </div>
                <div class="col-12">
                    <label class="form-label small fw-medium">Key Highlights / Bullet Points (one per line)</label>
                    <textarea rows="2" class="form-control form-control-sm exp-highlights" placeholder="• Improved query latency by 35%&#10;• Designed automated test suite">${data.highlights || ''}</textarea>
                </div>
            </div>
        `;

        wrapper.querySelector('.remove-entry-btn').addEventListener('click', () => wrapper.remove());
        this.experienceList.appendChild(wrapper);
    }

    addSkillItem(skillId, skillName, profLevel = 3) {
        if (!this.skillsList) return;

        const id = skillId || this.skillSelect?.value;
        if (!id) return;

        const name = skillName || (this.skillSelect ? this.skillSelect.options[this.skillSelect.selectedIndex].text : '');
        const prof = profLevel || parseInt(this.skillProficiency?.value || '3', 10);

        // Check if already added
        const existing = this.skillsList.querySelector(`[data-skill-id="${id}"]`);
        if (existing) {
            existing.querySelector('.skill-prof-slider').value = prof;
            existing.querySelector('.skill-prof-badge').textContent = `Level ${prof}/5`;
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'skill-entry-row card p-3 mb-2 border rounded-3 bg-light-subtle';
        wrapper.dataset.skillId = id;

        wrapper.innerHTML = `
            <div class="d-flex justify-content-between align-items-center mb-2">
                <span class="fw-semibold text-slate-800 skill-name">${name}</span>
                <div class="d-flex align-items-center gap-2">
                    <span class="badge bg-primary rounded-pill skill-prof-badge">Level ${prof}/5</span>
                    <button type="button" class="btn btn-sm btn-outline-danger border-0 remove-skill-btn">
                        <i class="bi bi-x-lg"></i>
                    </button>
                </div>
            </div>
            <div class="d-flex align-items-center gap-3">
                <span class="small text-muted">Novice (1)</span>
                <input type="range" class="form-range skill-prof-slider flex-grow-1" min="1" max="5" value="${prof}" />
                <span class="small text-muted">Expert (5)</span>
            </div>
        `;

        const slider = wrapper.querySelector('.skill-prof-slider');
        const badge = wrapper.querySelector('.skill-prof-badge');
        slider.addEventListener('input', (e) => {
            badge.textContent = `Level ${e.target.value}/5`;
        });

        wrapper.querySelector('.remove-skill-btn').addEventListener('click', () => wrapper.remove());
        this.skillsList.appendChild(wrapper);
    }

    renderReviewSummary() {
        if (!this.reviewContainer) return;

        const pi = this.getStepPayload('personal-info');
        const edu = this.getStepPayload('education');
        const exp = this.getStepPayload('experience');
        const skills = this.getStepPayload('skills');

        let html = `
            <div class="card border p-4 mb-4 rounded-3 shadow-sm">
                <div class="d-flex justify-content-between align-items-start border-bottom pb-3 mb-3">
                    <div>
                        <h4 class="fw-bold text-primary mb-1">${pi.fullName || 'Name Not Provided'}</h4>
                        <div class="text-muted small">${[pi.email, pi.phone, pi.location].filter(Boolean).join(' • ')}</div>
                    </div>
                </div>

                ${pi.summary ? `
                    <div class="mb-3">
                        <h6 class="fw-bold text-slate-800 text-uppercase small">Professional Summary</h6>
                        <p class="small text-muted">${pi.summary}</p>
                    </div>
                ` : ''}

                <div class="mb-3">
                    <h6 class="fw-bold text-slate-800 text-uppercase small">Education (${edu.length})</h6>
                    ${edu.length > 0 ? edu.map(e => `
                        <div class="mb-2">
                            <div class="fw-semibold small">${e.degree} ${e.fieldOfStudy ? 'in ' + e.fieldOfStudy : ''} — <span class="text-primary">${e.institution}</span></div>
                            <div class="text-muted small">${e.startDate} - ${e.endDate} ${e.gpa ? '• CGPA: ' + e.gpa : ''}</div>
                        </div>
                    `).join('') : '<p class="small text-muted">No education entries added.</p>'}
                </div>

                <div class="mb-3">
                    <h6 class="fw-bold text-slate-800 text-uppercase small">Experience (${exp.length})</h6>
                    ${exp.length > 0 ? exp.map(x => `
                        <div class="mb-2">
                            <div class="fw-semibold small">${x.role} — <span class="text-primary">${x.company}</span></div>
                            <div class="text-muted small">${x.startDate} - ${x.endDate}</div>
                            ${x.description ? `<div class="small text-muted mt-1">${x.description}</div>` : ''}
                        </div>
                    `).join('') : '<p class="small text-muted">No experience entries added.</p>'}
                </div>

                <div>
                    <h6 class="fw-bold text-slate-800 text-uppercase small">Skills (${skills.length})</h6>
                    <div class="d-flex flex-wrap gap-1">
                        ${skills.length > 0 ? skills.map(s => `
                            <span class="badge bg-light text-dark border">${s.skillName} (Lvl ${s.proficiencyLevel})</span>
                        `).join('') : '<p class="small text-muted">No skills added.</p>'}
                    </div>
                </div>
            </div>
        `;

        this.reviewContainer.innerHTML = html;
    }

    async finalizeResume() {
        if (!confirm('Finalize this resume and generate official PDF? You can continue editing at any time.')) {
            return;
        }

        this.setSavingState(true);
        if (this.btnFinalize) {
            this.btnFinalize.disabled = true;
            this.btnFinalize.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Generating PDF...';
        }

        try {
            // Save all steps first
            await this.saveCurrentStep(false);

            const url = `/Student/Resumes/${this.resumeId}/Finalize`;
            const result = await api.post(url, {});

            if (result && result.success) {
                if (window.showToast) {
                    window.showToast('Resume finalized and PDF generated!', 'success');
                }
                setTimeout(() => {
                    window.location.href = '/Student/Resumes';
                }, 1200);
            }
        } catch (error) {
            this.setSavingState(false, 'Finalization error');
            if (this.btnFinalize) {
                this.btnFinalize.disabled = false;
                this.btnFinalize.innerHTML = '<i class="bi bi-file-earmark-pdf me-2"></i> Finalize & Generate PDF';
            }
            if (window.showToast) {
                window.showToast(error.message || 'Failed to generate PDF resume.', 'danger');
            }
        }
    }
}

window.ResumeWizard = ResumeWizard;
