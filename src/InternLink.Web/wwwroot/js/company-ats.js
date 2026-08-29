import { api } from '/js/api.js';

class CompanyAtsBoard {
    constructor() {
        this.scheduleModalEl = document.getElementById('scheduleInterviewModal');
        this.scheduleModal = this.scheduleModalEl ? new bootstrap.Modal(this.scheduleModalEl) : null;

        this.detailModalEl = document.getElementById('applicantDetailModal');
        this.detailModal = this.detailModalEl ? new bootstrap.Modal(this.detailModalEl) : null;
        this.detailModalBody = document.getElementById('applicantDetailModalBody');

        this.scheduleAppId = document.getElementById('scheduleAppId');
        this.scheduleCandidateName = document.getElementById('scheduleCandidateName');
        this.scheduleJobTitle = document.getElementById('scheduleJobTitle');
        this.scheduleDateTimeInput = document.getElementById('scheduleDateTimeInput');
        this.meetingLinkInput = document.getElementById('meetingLinkInput');
        this.confirmScheduleBtn = document.getElementById('confirmScheduleBtn');
        this.scheduleValidationAlert = document.getElementById('scheduleValidationAlert');
        this.scheduleValidationErrorMsg = document.getElementById('scheduleValidationErrorMsg');

        this.init();
    }

    init() {
        // Set minimum datetime for schedule input to current time
        if (this.scheduleDateTimeInput) {
            const now = new Date();
            // Format to YYYY-MM-DDTHH:MM for datetime-local
            const offset = now.getTimezoneOffset();
            const localDate = new Date(now.getTime() - (offset * 60 * 1000));
            this.scheduleDateTimeInput.min = localDate.toISOString().slice(0, 16);
        }

        // Bind event listeners for card advance, schedule modal, detail modal
        document.addEventListener('click', (e) => {
            const advanceBtn = e.target.closest('.trigger-advance-btn');
            if (advanceBtn) {
                e.preventDefault();
                const appId = advanceBtn.getAttribute('data-app-id');
                const targetStatus = advanceBtn.getAttribute('data-target-status');
                if (appId && targetStatus) {
                    this.handleDirectAdvance(appId, targetStatus);
                }
                return;
            }

            const scheduleBtn = e.target.closest('.trigger-schedule-modal-btn');
            if (scheduleBtn) {
                e.preventDefault();
                const appId = scheduleBtn.getAttribute('data-app-id');
                const candidateName = scheduleBtn.getAttribute('data-candidate-name') || 'Candidate';
                const jobTitle = scheduleBtn.getAttribute('data-job-title') || 'Job';
                this.openScheduleModal(appId, candidateName, jobTitle);
                return;
            }

            const detailBtn = e.target.closest('.view-applicant-details-btn');
            if (detailBtn) {
                e.preventDefault();
                const appId = detailBtn.getAttribute('data-app-id');
                if (appId) {
                    this.openDetailModal(appId);
                }
                return;
            }
        });

        // Scheduling modal inputs validation
        if (this.scheduleDateTimeInput && this.meetingLinkInput && this.confirmScheduleBtn) {
            const validateInputs = () => this.validateScheduleForm();
            this.scheduleDateTimeInput.addEventListener('input', validateInputs);
            this.scheduleDateTimeInput.addEventListener('change', validateInputs);
            this.meetingLinkInput.addEventListener('input', validateInputs);

            this.confirmScheduleBtn.addEventListener('click', () => this.submitInterviewSchedule());
        }
    }

    validateScheduleForm() {
        if (!this.scheduleDateTimeInput || !this.meetingLinkInput || !this.confirmScheduleBtn) return;

        const dateVal = this.scheduleDateTimeInput.value;
        const linkVal = this.meetingLinkInput.value.trim();

        let isDateValid = false;
        let isLinkValid = false;

        if (dateVal) {
            const selectedDate = new Date(dateVal);
            isDateValid = selectedDate > new Date();
        }

        if (linkVal) {
            try {
                const url = new URL(linkVal);
                isLinkValid = (url.protocol === 'http:' || url.protocol === 'https:');
            } catch {
                isLinkValid = false;
            }
        }

        this.confirmScheduleBtn.disabled = !(isDateValid && isLinkValid);
        if (this.scheduleValidationAlert) {
            this.scheduleValidationAlert.classList.add('d-none');
        }
    }

    openScheduleModal(appId, candidateName, jobTitle) {
        if (!this.scheduleModal) return;

        if (this.scheduleAppId) this.scheduleAppId.value = appId;
        if (this.scheduleCandidateName) this.scheduleCandidateName.textContent = candidateName;
        if (this.scheduleJobTitle) this.scheduleJobTitle.textContent = jobTitle;

        if (this.scheduleDateTimeInput) this.scheduleDateTimeInput.value = '';
        if (this.meetingLinkInput) this.meetingLinkInput.value = '';
        if (this.confirmScheduleBtn) this.confirmScheduleBtn.disabled = true;
        if (this.scheduleValidationAlert) this.scheduleValidationAlert.classList.add('d-none');

        this.scheduleModal.show();
    }

    async submitInterviewSchedule() {
        const appId = this.scheduleAppId ? this.scheduleAppId.value : null;
        const dateVal = this.scheduleDateTimeInput ? this.scheduleDateTimeInput.value : '';
        const linkVal = this.meetingLinkInput ? this.meetingLinkInput.value.trim() : '';

        if (!appId || !dateVal || !linkVal) return;

        const originalBtnHtml = this.confirmScheduleBtn.innerHTML;
        this.confirmScheduleBtn.disabled = true;
        this.confirmScheduleBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Scheduling...';

        try {
            const dateObj = new Date(dateVal);
            const payload = {
                newStatus: 'Scheduled',
                scheduledDateTime: dateObj.toISOString(),
                contextMeetingLink: linkVal
            };

            const response = await api.put(`/Company/Ats/Applications/${appId}/Status`, payload);

            if (this.scheduleModal) {
                this.scheduleModal.hide();
            }

            // Move card to Scheduled column
            this.moveCardToColumn(appId, 'Scheduled', {
                interviewDateTime: response.interviewDateTime || dateObj.toLocaleString(),
                meetingLink: linkVal,
                studentName: this.scheduleCandidateName?.textContent || '',
                jobTitle: this.scheduleJobTitle?.textContent || ''
            });

            this.showToast(response.message || 'Interview scheduled successfully!', 'success');
        } catch (error) {
            if (this.scheduleValidationAlert && this.scheduleValidationErrorMsg) {
                this.scheduleValidationErrorMsg.textContent = error.message || 'Failed to schedule interview.';
                this.scheduleValidationAlert.classList.remove('d-none');
            } else {
                this.showToast(error.message || 'Failed to schedule interview.', 'danger');
            }
        } finally {
            if (this.confirmScheduleBtn) {
                this.confirmScheduleBtn.disabled = false;
                this.confirmScheduleBtn.innerHTML = originalBtnHtml;
            }
        }
    }

    async handleDirectAdvance(appId, targetStatus) {
        const cardEl = document.getElementById(`app-card-${appId}`);
        if (!cardEl) return;

        const currentStatus = cardEl.getAttribute('data-status') || '';
        const candidateName = cardEl.getAttribute('data-candidate-name') || 'Candidate';
        const jobTitle = cardEl.getAttribute('data-job-title') || 'Job';

        // Confirmation for Rejection
        if (targetStatus === 'Rejected') {
            const confirmed = window.confirm(`Are you sure you want to reject ${candidateName}'s application? This action is terminal and will move the application to Not Selected.`);
            if (!confirmed) return;
        }

        // Optimistically move card
        const originalParent = cardEl.parentElement;
        const originalIndex = Array.from(originalParent.children).indexOf(cardEl);
        this.moveCardToColumn(appId, targetStatus, { studentName: candidateName, jobTitle: jobTitle });

        try {
            const payload = { newStatus: targetStatus };
            const response = await api.put(`/Company/Ats/Applications/${appId}/Status`, payload);
            this.showToast(response.message || `Application updated to ${targetStatus}.`, 'success');
        } catch (error) {
            // Revert card on error
            this.moveCardToColumn(appId, currentStatus, { studentName: candidateName, jobTitle: jobTitle });
            this.showToast(error.message || 'Failed to update application status.', 'danger');
        }
    }

    moveCardToColumn(appId, targetStatus, extraData = {}) {
        const cardEl = document.getElementById(`app-card-${appId}`);
        const targetColumn = document.getElementById(`column-${targetStatus}`);
        if (!cardEl || !targetColumn) return;

        const sourceStatus = cardEl.getAttribute('data-status');
        if (sourceStatus === targetStatus && targetStatus !== 'Scheduled') return;

        // Animate out / in
        cardEl.style.transition = 'all 0.2s ease';
        cardEl.style.opacity = '0.4';

        setTimeout(() => {
            targetColumn.prepend(cardEl);
            cardEl.setAttribute('data-status', targetStatus);
            cardEl.style.opacity = '1';

            // Re-render advance dropdown inside card
            this.updateCardDropdown(cardEl, appId, targetStatus, extraData);

            // Update interview chip if applicable
            const interviewChip = cardEl.querySelector('.interview-info-chip');
            if (interviewChip) {
                if (targetStatus === 'Scheduled' && extraData.interviewDateTime) {
                    const timeEl = interviewChip.querySelector('.interview-time-text');
                    const linkEl = interviewChip.querySelector('.interview-link-text');
                    if (timeEl) timeEl.textContent = extraData.interviewDateTime;
                    if (linkEl && extraData.meetingLink) {
                        linkEl.textContent = extraData.meetingLink;
                        linkEl.href = extraData.meetingLink;
                    }
                    interviewChip.classList.remove('d-none');
                } else if (targetStatus !== 'Scheduled') {
                    interviewChip.classList.add('d-none');
                }
            }

            // Recalculate column counters
            this.recalculateCounters();
        }, 150);
    }

    updateCardDropdown(cardEl, appId, newStatus, extraData) {
        const container = cardEl.querySelector('.card-advance-menu-container');
        if (!container) return;

        const candidateName = extraData.studentName || cardEl.getAttribute('data-candidate-name') || 'Candidate';
        const jobTitle = extraData.jobTitle || cardEl.getAttribute('data-job-title') || 'Job';

        if (newStatus === 'Applied') {
            container.innerHTML = `
                <div class="dropdown">
                    <button class="btn btn-sm btn-outline-primary dropdown-toggle py-0 px-2 fw-medium" type="button" data-bs-toggle="dropdown" aria-expanded="false">
                        Advance
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end shadow-sm border-0 small">
                        <li>
                            <a class="dropdown-item d-flex align-items-center gap-2 trigger-advance-btn" href="javascript:void(0)" 
                               data-app-id="${appId}" data-target-status="Screened">
                                <i class="bi bi-search text-primary"></i>Screen Candidate
                            </a>
                        </li>
                        <li><hr class="dropdown-divider"></li>
                        <li>
                            <a class="dropdown-item d-flex align-items-center gap-2 text-danger trigger-advance-btn" href="javascript:void(0)" 
                               data-app-id="${appId}" data-target-status="Rejected">
                                <i class="bi bi-x-circle text-danger"></i>Reject Application
                            </a>
                        </li>
                    </ul>
                </div>`;
        } else if (newStatus === 'Screened') {
            container.innerHTML = `
                <div class="dropdown">
                    <button class="btn btn-sm btn-outline-warning dropdown-toggle py-0 px-2 fw-medium" type="button" data-bs-toggle="dropdown" aria-expanded="false">
                        Advance
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end shadow-sm border-0 small">
                        <li>
                            <a class="dropdown-item d-flex align-items-center gap-2 trigger-schedule-modal-btn" href="javascript:void(0)" 
                               data-app-id="${appId}" data-candidate-name="${candidateName}" data-job-title="${jobTitle}">
                                <i class="bi bi-calendar-event text-warning"></i>Schedule Interview
                            </a>
                        </li>
                        <li><hr class="dropdown-divider"></li>
                        <li>
                            <a class="dropdown-item d-flex align-items-center gap-2 text-danger trigger-advance-btn" href="javascript:void(0)" 
                               data-app-id="${appId}" data-target-status="Rejected">
                                <i class="bi bi-x-circle text-danger"></i>Reject Application
                            </a>
                        </li>
                    </ul>
                </div>`;
        } else if (newStatus === 'Scheduled') {
            container.innerHTML = `
                <div class="dropdown">
                    <button class="btn btn-sm btn-outline-success dropdown-toggle py-0 px-2 fw-medium" type="button" data-bs-toggle="dropdown" aria-expanded="false">
                        Advance
                    </button>
                    <ul class="dropdown-menu dropdown-menu-end shadow-sm border-0 small">
                        <li>
                            <a class="dropdown-item d-flex align-items-center gap-2 trigger-advance-btn" href="javascript:void(0)" 
                               data-app-id="${appId}" data-target-status="Offered">
                                <i class="bi bi-check-circle-fill text-success"></i>Extend Offer
                            </a>
                        </li>
                        <li><hr class="dropdown-divider"></li>
                        <li>
                            <a class="dropdown-item d-flex align-items-center gap-2 text-danger trigger-advance-btn" href="javascript:void(0)" 
                               data-app-id="${appId}" data-target-status="Rejected">
                                <i class="bi bi-x-circle text-danger"></i>Reject Application
                            </a>
                        </li>
                    </ul>
                </div>`;
        } else if (newStatus === 'Offered') {
            container.innerHTML = `
                <span class="badge bg-success-subtle text-success border border-success-subtle px-2 py-1 small rounded-pill">
                    <i class="bi bi-check-lg me-1"></i>Offer Extended
                </span>`;
        } else if (newStatus === 'Rejected') {
            container.innerHTML = `
                <span class="badge bg-danger-subtle text-danger border border-danger-subtle px-2 py-1 small rounded-pill">
                    <i class="bi bi-x-lg me-1"></i>Not Selected
                </span>`;
        }
    }

    recalculateCounters() {
        const statuses = ['Applied', 'Screened', 'Scheduled', 'Offered', 'Rejected'];
        let total = 0;

        statuses.forEach(status => {
            const column = document.getElementById(`column-${status}`);
            const countBadge = document.getElementById(`count-${status}`);
            if (column && countBadge) {
                const count = column.querySelectorAll('.ats-applicant-card').length;
                countBadge.textContent = count;
                total += count;
            }
        });

        const totalHeader = document.getElementById('totalApplicantsCount');
        if (totalHeader) {
            totalHeader.textContent = `${total} Active Applications`;
        }
    }

    async openDetailModal(appId) {
        if (!this.detailModal || !this.detailModalBody) return;

        this.detailModalBody.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading Candidate Details...</span>
                </div>
            </div>`;

        this.detailModal.show();

        try {
            const data = await api.get(`/Company/Ats/Applications/${appId}`);
            this.renderDetailModalContent(data);
        } catch (error) {
            this.detailModalBody.innerHTML = `
                <div class="alert alert-danger mb-0">
                    <i class="bi bi-exclamation-triangle-fill me-2"></i>${error.message || 'Failed to load applicant details.'}
                </div>`;
        }
    }

    renderDetailModalContent(data) {
        if (!this.detailModalBody) return;

        // Applicant-supplied values reach a company user's browser here. Cover letter text in
        // particular is free-form student input, so every interpolation below must be escaped.
        const esc = (value) => String(value ?? '').replace(/[&<>"']/g, c => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[c]));

        const verifiedSkillsHtml = (data.verifiedSkills && data.verifiedSkills.length > 0)
            ? data.verifiedSkills.map(s => `
                <span class="badge bg-success-subtle text-success border border-success-subtle p-2 d-inline-flex align-items-center gap-1">
                    <i class="bi bi-patch-check-fill"></i> ${esc(s.skillName)} (${Number(s.bestScore) || 0}%)
                </span>`).join(' ')
            : '<span class="text-muted small">No verified skills recorded yet.</span>';

        const interviewInfoHtml = (data.status === 2 && data.interviewDateTime)
            ? `
                <div class="alert alert-warning border border-warning-subtle d-flex align-items-center gap-3 rounded-3 mb-3">
                    <i class="bi bi-camera-video-fill fs-4 text-warning-emphasis"></i>
                    <div>
                        <div class="fw-bold text-warning-emphasis">Interview Scheduled</div>
                        <div class="small text-slate-700">${esc(new Date(data.interviewDateTime).toLocaleString())}</div>
                        ${data.meetingLink ? `<a href="${esc(data.meetingLink)}" target="_blank" rel="noopener noreferrer" class="small fw-semibold text-warning-emphasis"><i class="bi bi-box-arrow-up-right me-1"></i>Join Video Call</a>` : ''}
                    </div>
                </div>`
            : '';

        const coverLetterHtml = data.coverLetterText
            ? `
                <div class="mb-3">
                    <h6 class="fw-bold text-slate-800 mb-2">Cover Letter</h6>
                    <div class="p-3 bg-light rounded-3 text-slate-700 small border" style="white-space: pre-wrap;">${esc(data.coverLetterText)}</div>
                </div>`
            : '';

        const resumeBtnHtml = data.attachedResumeId
            ? `
                <a href="/Company/Ats/Applications/${data.applicationId}/Resume" target="_blank" class="btn btn-primary d-inline-flex align-items-center gap-2">
                    <i class="bi bi-file-earmark-pdf"></i> Download Official Resume
                </a>`
            : `
                <button class="btn btn-secondary" disabled>
                    <i class="bi bi-file-earmark-x me-1"></i>No Resume Attached
                </button>`;

        this.detailModalBody.innerHTML = `
            <div>
                <!-- Candidate Header -->
                <div class="d-flex justify-content-between align-items-start gap-3 pb-3 border-bottom mb-3">
                    <div>
                        <h4 class="fw-bold text-slate-800 mb-1">${esc(data.studentName)}</h4>
                        <div class="text-muted small">
                            <i class="bi bi-briefcase me-1"></i>Applied for: <strong>${esc(data.jobTitle)}</strong>
                        </div>
                    </div>
                    <div>
                        ${resumeBtnHtml}
                    </div>
                </div>

                <!-- Recruiter-relevant details card -->
                <div class="row g-3 mb-3">
                    <div class="col-md-4">
                        <div class="p-3 bg-light rounded-3 border">
                            <div class="text-muted small">Department</div>
                            <div class="fw-bold text-slate-800">${esc(data.department)}</div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="p-3 bg-light rounded-3 border">
                            <div class="text-muted small">Academic CGPA</div>
                            <div class="fw-bold text-slate-800">${Number(data.cgpa).toFixed(2)} / 4.00</div>
                        </div>
                    </div>
                    <div class="col-md-4">
                        <div class="p-3 bg-light rounded-3 border">
                            <div class="text-muted small">Submitted Date</div>
                            <div class="fw-bold text-slate-800">${new Date(data.submittedAt).toLocaleDateString()}</div>
                        </div>
                    </div>
                </div>

                ${interviewInfoHtml}

                <!-- Verified Skills (Derived >= 70) -->
                <div class="mb-3">
                    <h6 class="fw-bold text-slate-800 mb-2">Verified Assessment Skills (&ge;70%)</h6>
                    <div class="d-flex flex-wrap gap-2">
                        ${verifiedSkillsHtml}
                    </div>
                </div>

                ${coverLetterHtml}

                <!-- Skill gap: same shared partial the student sees -->
                <div class="border-top pt-3"
                     id="atsSkillGap"
                     data-skill-gap-url="/Company/Ats/Applications/${esc(data.applicationId)}/SkillGap">
                    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                        <h6 class="fw-bold text-slate-800 mb-0">Skill Gap Analysis</h6>
                        <button type="button" id="atsSkillGapBtn" class="btn btn-sm btn-outline-primary d-inline-flex align-items-center gap-2">
                            <i class="bi bi-clipboard-data"></i><span>Analyze Candidate Fit</span>
                        </button>
                    </div>
                    <div id="atsSkillGapStatus" class="alert alert-warning d-none py-2 px-3 small" role="alert"></div>
                    <div id="atsSkillGapContent" hidden></div>
                </div>
            </div>`;

        this.bindSkillGap();
    }

    bindSkillGap() {
        const root = this.detailModalBody.querySelector('#atsSkillGap');
        if (!root) return;

        const button = root.querySelector('#atsSkillGapBtn');
        const content = root.querySelector('#atsSkillGapContent');
        const status = root.querySelector('#atsSkillGapStatus');

        button.addEventListener('click', async () => {
            status.classList.add('d-none');
            button.disabled = true;
            button.innerHTML = '<span class="spinner-border spinner-border-sm"></span><span>Analyzing…</span>';

            try {
                content.innerHTML = await api.get(root.dataset.skillGapUrl);
                content.hidden = false;
                button.innerHTML = '<i class="bi bi-arrow-clockwise"></i><span>Refresh Analysis</span>';
            } catch (error) {
                status.textContent = error.message;
                status.classList.remove('d-none');
                button.innerHTML = '<i class="bi bi-clipboard-data"></i><span>Analyze Candidate Fit</span>';
            } finally {
                button.disabled = false;
            }
        });
    }

    showToast(message, type = 'info') {
        if (typeof window.showToast === 'function') {
            window.showToast(message, type);
        } else {
            alert(message);
        }
    }
}

// Auto-instantiate on DOM load
document.addEventListener('DOMContentLoaded', () => {
    window.companyAtsBoard = new CompanyAtsBoard();
});
