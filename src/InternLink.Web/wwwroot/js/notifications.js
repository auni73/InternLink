// notifications.js — Polls unread count and handles dropdown notifications.
import { api } from '/js/api.js';

let pollIntervalId = null;

function escapeHtml(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

export function initNotifications() {
    const container = document.getElementById('notificationDropdownContainer');
    if (!container) return; // Not signed in or no notification bell in view

    const badge = document.getElementById('notificationBadge');
    const headerBadge = document.getElementById('notificationUnreadHeaderBadge');
    const bellIcon = document.getElementById('notificationBellIcon');
    const bellBtn = document.getElementById('notificationBellBtn');
    const listContainer = document.getElementById('notificationListContainer');

    function updateBadge(count) {
        if (!badge || !headerBadge) return;

        const num = parseInt(count, 10) || 0;
        if (num > 0) {
            badge.textContent = num > 99 ? '99+' : num;
            badge.classList.remove('d-none');
            headerBadge.textContent = `${num} new`;
            headerBadge.className = 'badge bg-danger-subtle text-danger rounded-pill small';
            if (bellIcon) {
                bellIcon.className = 'bi bi-bell-fill fs-5 text-primary';
            }
        } else {
            badge.textContent = '0';
            badge.classList.add('d-none');
            headerBadge.textContent = '0 new';
            headerBadge.className = 'badge bg-light text-muted rounded-pill small border';
            if (bellIcon) {
                bellIcon.className = 'bi bi-bell fs-5 text-body';
            }
        }
    }

    async function pollUnreadCount() {
        try {
            const data = await api.get('/Notifications/UnreadCount');
            if (data && typeof data.count === 'number') {
                updateBadge(data.count);
            }
        } catch (err) {
            // Silently ignore background polling errors
            console.debug('Failed to poll notification unread count', err);
        }
    }

    async function loadRecentNotifications() {
        if (!listContainer) return;

        listContainer.innerHTML = `
            <div class="p-4 text-center text-muted">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                <span class="small">Loading notifications...</span>
            </div>
        `;

        try {
            const data = await api.get('/Notifications/Recent?take=10');
            const items = data?.items || [];

            if (items.length === 0) {
                listContainer.innerHTML = `
                    <div class="p-4 text-center text-muted">
                        <i class="bi bi-bell-slash fs-3 text-secondary d-block mb-2"></i>
                        <span class="small">No notifications yet.</span>
                    </div>
                `;
                return;
            }

            let html = '<div class="list-group list-group-flush">';
            items.forEach(item => {
                const unreadClass = !item.isRead ? 'bg-primary-subtle bg-opacity-25' : '';
                const unreadDot = !item.isRead 
                    ? '<span class="badge bg-primary p-1 rounded-circle flex-shrink-0" style="width: 8px; height: 8px;" title="Unread"></span>' 
                    : '';

                html += `
                    <a href="${escapeHtml(item.eventRoutingUrl || '#')}" 
                       class="list-group-item list-group-item-action p-3 d-flex align-items-start gap-3 text-decoration-none border-bottom notification-item ${unreadClass}"
                       data-id="${item.id}"
                       data-url="${escapeHtml(item.eventRoutingUrl || '#')}"
                       data-read="${item.isRead}">
                        <div class="mt-1">${unreadDot}</div>
                        <div class="flex-grow-1 min-w-0">
                            <p class="mb-1 text-slate-800 small fw-medium text-break">${escapeHtml(item.textPayload)}</p>
                            <span class="text-muted small" style="font-size: 0.75rem;">
                                <i class="bi bi-clock me-1"></i>${escapeHtml(item.timeAgo)}
                            </span>
                        </div>
                    </a>
                `;
            });
            html += '</div>';

            listContainer.innerHTML = html;

            // Attach click handler for each notification item
            listContainer.querySelectorAll('.notification-item').forEach(el => {
                el.addEventListener('click', async (e) => {
                    e.preventDefault();
                    const id = el.getAttribute('data-id');
                    const url = el.getAttribute('data-url');
                    const isRead = el.getAttribute('data-read') === 'true';

                    if (!isRead && id) {
                        // Optimistically mark as read in UI
                        el.setAttribute('data-read', 'true');
                        el.classList.remove('bg-primary-subtle', 'bg-opacity-25');
                        const dot = el.querySelector('.badge.bg-primary');
                        if (dot) dot.remove();

                        const currentCount = parseInt(badge?.textContent, 10) || 0;
                        if (currentCount > 0) {
                            updateBadge(currentCount - 1);
                        }

                        // Fire background read request
                        try {
                            await api.post(`/Notifications/${id}/Read`);
                        } catch (err) {
                            console.warn('Failed to mark notification read', err);
                        }
                    }

                    if (url && url !== '#') {
                        window.location.href = url;
                    }
                });
            });

        } catch (err) {
            listContainer.innerHTML = `
                <div class="p-4 text-center text-danger">
                    <i class="bi bi-exclamation-triangle fs-4 d-block mb-1"></i>
                    <span class="small">Failed to load notifications.</span>
                </div>
            `;
        }
    }

    // When dropdown is opened, load latest notifications
    if (bellBtn) {
        bellBtn.addEventListener('click', () => {
            loadRecentNotifications();
        });
    }

    // Initial check
    pollUnreadCount();

    // Poll every 30 seconds
    if (pollIntervalId) {
        clearInterval(pollIntervalId);
    }
    pollIntervalId = setInterval(pollUnreadCount, 30000);

    window.addEventListener('beforeunload', () => {
        if (pollIntervalId) {
            clearInterval(pollIntervalId);
            pollIntervalId = null;
        }
    });
}

// Auto-initialize when loaded
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initNotifications);
} else {
    initNotifications();
}
