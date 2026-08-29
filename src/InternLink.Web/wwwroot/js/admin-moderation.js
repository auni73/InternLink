import { api } from './api.js';

document.addEventListener('DOMContentLoaded', () => {
    // 1. Optimistic Job Approval
    document.querySelectorAll('.approve-job-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.preventDefault();
            const jobId = btn.getAttribute('data-job-id');
            const jobCard = document.getElementById(`job-item-${jobId}`);
            if (!jobId || !jobCard) return;

            const originalBtnHtml = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>Approving...';

            try {
                const response = await api.post(`/Admin/Jobs/${jobId}/Approve`);
                
                // Animate removal from queue
                jobCard.style.transition = 'all 0.35s ease-out';
                jobCard.style.opacity = '0';
                jobCard.style.transform = 'translateY(-10px)';

                setTimeout(() => {
                    jobCard.remove();

                    // Update pending count badge
                    const pendingBadge = document.getElementById('pendingJobBadge');
                    if (pendingBadge) {
                        const current = parseInt(pendingBadge.textContent.trim(), 10) || 0;
                        pendingBadge.textContent = Math.max(0, current - 1);
                    }

                    const countHeader = document.getElementById('jobQueueCountHeader');
                    if (countHeader) {
                        const remaining = document.querySelectorAll('.job-approval-item').length;
                        countHeader.textContent = `${remaining} Postings`;
                    }
                }, 350);

                showAdminToast(response.message || 'Job approved and published successfully.', 'success');
            } catch (err) {
                btn.disabled = false;
                btn.innerHTML = originalBtnHtml;
                showAdminToast(err.message || 'Failed to approve job. Please try again.', 'danger');
            }
        });
    });

    // 2. Optimistic Company Approval
    document.querySelectorAll('.approve-company-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.preventDefault();
            const companyId = btn.getAttribute('data-company-id');
            const companyRow = document.getElementById(`company-row-${companyId}`);
            if (!companyId || !companyRow) return;

            const originalBtnHtml = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>';

            try {
                const response = await api.post(`/Admin/Companies/${companyId}/Approve`);

                // Animate removal from pending queue if on pending tab
                const isPendingTab = window.location.href.includes('status=Pending') || !window.location.search.includes('status=');
                if (isPendingTab) {
                    companyRow.style.transition = 'all 0.35s ease-out';
                    companyRow.style.opacity = '0';
                    setTimeout(() => {
                        companyRow.remove();

                        const pendingBadge = document.getElementById('pendingCompanyBadge');
                        if (pendingBadge) {
                            const current = parseInt(pendingBadge.textContent.trim(), 10) || 0;
                            pendingBadge.textContent = Math.max(0, current - 1);
                        }

                        const countHeader = document.getElementById('companyQueueCountHeader');
                        if (countHeader) {
                            const remaining = document.querySelectorAll('tbody tr').length;
                            countHeader.textContent = `${remaining} Organizations`;
                        }
                    }, 350);
                } else {
                    window.location.reload();
                }

                showAdminToast(response.message || 'Company verified successfully.', 'success');
            } catch (err) {
                btn.disabled = false;
                btn.innerHTML = originalBtnHtml;
                showAdminToast(err.message || 'Failed to approve company. Please try again.', 'danger');
            }
        });
    });
});

function showAdminToast(message, type = 'success') {
    const toastContainer = document.getElementById('adminToastContainer') || createToastContainer();
    const toastEl = document.createElement('div');
    toastEl.className = `toast align-items-center text-bg-${type} border-0 shadow`;
    toastEl.setAttribute('role', 'alert');
    toastEl.setAttribute('aria-live', 'assertive');
    toastEl.setAttribute('aria-atomic', 'true');

    toastEl.innerHTML = `
        <div class="d-flex">
            <div class="toast-body d-flex align-items-center gap-2">
                <i class="bi ${type === 'success' ? 'bi-check-circle-fill' : 'bi-exclamation-circle-fill'}"></i>
                <span>${escapeHtml(message)}</span>
            </div>
            <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
        </div>
    `;

    toastContainer.appendChild(toastEl);
    if (window.bootstrap && bootstrap.Toast) {
        const toast = new bootstrap.Toast(toastEl, { delay: 4000 });
        toast.show();
        toastEl.addEventListener('hidden.bs.toast', () => toastEl.remove());
    } else {
        setTimeout(() => toastEl.remove(), 4000);
    }
}

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'adminToastContainer';
    container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
    container.style.zIndex = '1090';
    document.body.appendChild(container);
    return container;
}

function escapeHtml(str) {
    if (!str) return '';
    return String(str)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}
