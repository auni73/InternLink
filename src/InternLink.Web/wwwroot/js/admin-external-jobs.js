// admin-external-jobs.js — External Job Ingestion & AI Smart-Paste handling
import { api } from './api.js';

document.addEventListener('DOMContentLoaded', () => {
    initExternalSync();
    initSmartPaste();
    initCreateExternalJob();
});

function initExternalSync() {
    const btnSync = document.getElementById('btnSyncExternal');
    const syncText = document.getElementById('syncBtnText');
    if (!btnSync) return;

    btnSync.addEventListener('click', async () => {
        const originalHtml = btnSync.innerHTML;
        btnSync.disabled = true;
        btnSync.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Syncing BDJobs & APIs...';

        try {
            const result = await api.post('/Admin/Jobs/External/Sync');
            let detailMsg = `Ingestion Finished: ${result.ingested} new vacancies ingested, ${result.skipped} duplicates skipped.`;
            if (result.failed > 0) {
                detailMsg += ` (${result.failed} items had issues)`;
            }

            showAlert('success', detailMsg);
            setTimeout(() => window.location.reload(), 1500);
        } catch (err) {
            showAlert('danger', 'External job sync failed: ' + (err.message || 'Unknown network error.'));
        } finally {
            btnSync.disabled = false;
            btnSync.innerHTML = originalHtml;
        }
    });
}

function initSmartPaste() {
    const btnSample = document.getElementById('btnInsertSampleCircular');
    const btnExtract = document.getElementById('btnExtractCircular');
    const textarea = document.getElementById('rawCircularText');
    const extractSpinner = document.getElementById('extractSpinner');

    if (btnSample && textarea) {
        btnSample.addEventListener('click', () => {
            textarea.value = `Brain Station 23 PLC
Hiring: Trainee Software Engineer (.NET & Cloud)
Location: Mohakhali, Dhaka (On-Site)
Application Deadline: 25 October 2026

We are looking for enthusiastic CSE/SWE graduates for our next engineering batch.
Responsibilities:
- Build modular REST APIs and web services using C# and ASP.NET Core.
- Write efficient SQL Server queries and work with Entity Framework Core.
- Collaborate with frontend teams working on Bootstrap 5 and React.
- Participate in agile sprints and code reviews.

Requirements & Prerequisites:
- BSc in Computer Science, Software Engineering, or related technical field.
- Minimum CGPA 3.20.
- Hands-on experience with C#, SQL, and Git version control.
- Good technical communication and problem-solving skills.

Apply online at: https://brainstation-23.com/careers/trainee-se-2026`;
        });
    }

    if (btnExtract && textarea) {
        btnExtract.addEventListener('click', async () => {
            const rawContent = textarea.value.trim();
            if (!rawContent) {
                alert('Please enter or paste raw circular text first.');
                return;
            }

            btnExtract.disabled = true;
            if (extractSpinner) extractSpinner.classList.remove('d-none');

            try {
                const parsed = await api.post('/Admin/Jobs/External/Parse', {
                    rawContent: rawContent,
                    sourceName: 'Manual / Circular'
                });

                // Populate Form Fields
                const titleInput = document.getElementById('extJobTitle');
                const companyInput = document.getElementById('extCompanyName');
                const applyUrlInput = document.getElementById('extApplyUrl');
                const locationSelect = document.getElementById('extLocationType');
                const deadlineInput = document.getElementById('extDeadline');
                const descInput = document.getElementById('extCoreDesc');
                const criteriaInput = document.getElementById('extCriteria');
                const skillsInput = document.getElementById('extSkillTags');

                if (titleInput) titleInput.value = parsed.title || '';
                if (companyInput) companyInput.value = parsed.companyNameSnapshot || '';
                if (descInput) descInput.value = parsed.coreDescription || '';
                if (criteriaInput) criteriaInput.value = parsed.selectionCriteria || '';
                if (locationSelect) locationSelect.value = parsed.locationType !== undefined ? parsed.locationType : 1;

                if (parsed.deadLine && deadlineInput) {
                    try {
                        const d = new Date(parsed.deadLine);
                        deadlineInput.value = d.toISOString().split('T')[0];
                    } catch {
                        // ignore date format error
                    }
                }

                if (parsed.suggestedSkillNames && parsed.suggestedSkillNames.length > 0 && skillsInput) {
                    skillsInput.value = parsed.suggestedSkillNames.join(', ');
                }

                // If URL was found in circular text
                const urlMatch = rawContent.match(/https?:\/\/[^\s]+/);
                if (urlMatch && applyUrlInput && !applyUrlInput.value) {
                    applyUrlInput.value = urlMatch[0];
                }

                // Switch to Review & Publish Tab
                const tabReview = document.getElementById('tab-review-link');
                if (tabReview && window.bootstrap) {
                    const tab = new bootstrap.Tab(tabReview);
                    tab.show();
                }
            } catch (err) {
                alert('AI Circular extraction error: ' + (err.message || 'Check console.'));
            } finally {
                btnExtract.disabled = false;
                if (extractSpinner) extractSpinner.classList.add('d-none');
            }
        });
    }
}

function initCreateExternalJob() {
    const btnSubmit = document.getElementById('btnSubmitExternalJob');
    if (!btnSubmit) return;

    btnSubmit.addEventListener('click', async () => {
        const title = document.getElementById('extJobTitle')?.value.trim();
        const company = document.getElementById('extCompanyName')?.value.trim();
        const sourceName = document.getElementById('extSourceName')?.value.trim() || 'Manual';
        const applyUrl = document.getElementById('extApplyUrl')?.value.trim();
        const locationType = parseInt(document.getElementById('extLocationType')?.value || '1', 10);
        const deadlineVal = document.getElementById('extDeadline')?.value;
        const desc = document.getElementById('extCoreDesc')?.value.trim();
        const criteria = document.getElementById('extCriteria')?.value.trim();
        const skillsRaw = document.getElementById('extSkillTags')?.value.trim();

        if (!title || !company || !applyUrl || !desc) {
            alert('Please complete all required fields (Title, Company, Apply URL, and Description).');
            return;
        }

        const skillNames = skillsRaw
            ? skillsRaw.split(',').map(s => s.trim()).filter(s => s.length > 0)
            : [];

        let deadlineDate = new Date();
        deadlineDate.setDate(deadlineDate.getDate() + 30);
        if (deadlineVal) {
            const parsed = new Date(deadlineVal);
            if (!isNaN(parsed.getTime())) {
                deadlineDate = parsed;
            }
        }

        btnSubmit.disabled = true;
        btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span> Publishing...';

        try {
            await api.post('/Admin/Jobs/External/Create', {
                title,
                companyNameSnapshot: company,
                externalSourceName: sourceName,
                externalApplyUrl: applyUrl,
                locationType,
                deadLine: deadlineDate.toISOString(),
                coreDescription: desc,
                selectionCriteria: criteria,
                skillNames
            });

            showAlert('success', 'External job vacancy published successfully!');
            setTimeout(() => window.location.reload(), 1200);
        } catch (err) {
            alert('Failed to publish external job: ' + (err.message || 'Unknown error.'));
            btnSubmit.disabled = false;
            btnSubmit.innerHTML = '<i class="bi bi-check-circle me-1"></i>Publish External Job';
        }
    });
}

function showAlert(type, message) {
    const container = document.getElementById('adminAlertContainer') || document.querySelector('.container-fluid');
    if (!container) return;

    const alert = document.createElement('div');
    alert.className = `alert alert-${type} alert-dismissible fade show shadow-sm rounded-3 mb-4`;
    alert.role = 'alert';
    alert.innerHTML = `
        <div class="d-flex align-items-center gap-2">
            <i class="bi bi-${type === 'success' ? 'check-circle-fill' : 'exclamation-triangle-fill'} fs-5"></i>
            <div>${message}</div>
            <button type="button" class="btn-close ms-auto" data-bs-dismiss="alert" aria-label="Close"></button>
        </div>
    `;

    container.insertBefore(alert, container.firstChild);
}
