// analytics.js — Executive Analytics Cockpit
document.addEventListener('DOMContentLoaded', () => {
    const dataIsland = document.getElementById('analyticsData');
    if (!dataIsland) return;

    let payload;
    try {
        payload = JSON.parse(dataIsland.textContent || '{}');
    } catch (e) {
        console.error('Failed to parse analytics payload JSON:', e);
        return;
    }

    // Status -> Color Mapping (Unified with ATS and Application Funnel)
    const STATUS_COLORS = {
        'Applied': '#64748B',    // Slate
        'Screened': '#0F6B5C',   // Brand Deep Teal
        'Scheduled': '#F2A33C',  // Warm Amber
        'Offered': '#10B981',    // Emerald Green
        'Rejected': '#EF4444'    // Rose Red
    };

    // 1. Initialize 7-Day Application Submission Trend Line Chart
    const trendCtx = document.getElementById('dailyTrendChart');
    if (trendCtx && payload.dailyTrend && Array.isArray(payload.dailyTrend)) {
        const labels = payload.dailyTrend.map(d => d.formattedDate || d.date);
        const data = payload.dailyTrend.map(d => d.count);

        const ctx = trendCtx.getContext('2d');
        const gradient = ctx.createLinearGradient(0, 0, 0, 300);
        gradient.addColorStop(0, 'rgba(15, 107, 92, 0.25)');
        gradient.addColorStop(1, 'rgba(15, 107, 92, 0.00)');

        new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'New Applications',
                    data: data,
                    borderColor: '#0F6B5C',
                    backgroundColor: gradient,
                    borderWidth: 2.5,
                    fill: true,
                    tension: 0.35,
                    pointBackgroundColor: '#0F6B5C',
                    pointBorderColor: '#FFFFFF',
                    pointBorderWidth: 2,
                    pointRadius: 4,
                    pointHoverRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#1E293B',
                        titleColor: '#F8FAFC',
                        bodyColor: '#F8FAFC',
                        padding: 10,
                        cornerRadius: 8,
                        displayColors: false,
                        callbacks: {
                            label: function (context) {
                                const val = context.parsed.y;
                                return `${val} ${val === 1 ? 'application' : 'applications'} submitted`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { color: '#64748B', font: { size: 12 } }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: '#F1F5F9' },
                        ticks: {
                            color: '#64748B',
                            stepSize: 1,
                            precision: 0,
                            font: { size: 12 }
                        }
                    }
                }
            }
        });
    }

    // 2. Initialize Application Status Breakdown Doughnut Chart
    const statusCtx = document.getElementById('statusBreakdownChart');
    if (statusCtx && payload.statusBreakdown) {
        const statuses = ['Applied', 'Screened', 'Scheduled', 'Offered', 'Rejected'];
        const counts = statuses.map(s => payload.statusBreakdown[s] || 0);
        const colors = statuses.map(s => STATUS_COLORS[s]);

        const totalApplications = counts.reduce((a, b) => a + b, 0);

        new Chart(statusCtx.getContext('2d'), {
            type: 'doughnut',
            data: {
                labels: statuses,
                datasets: [{
                    data: counts,
                    backgroundColor: colors,
                    borderWidth: 2,
                    borderColor: '#FFFFFF',
                    hoverOffset: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '68%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 16,
                            color: '#475569',
                            font: { size: 12, weight: '500' }
                        }
                    },
                    tooltip: {
                        backgroundColor: '#1E293B',
                        titleColor: '#F8FAFC',
                        bodyColor: '#F8FAFC',
                        padding: 10,
                        cornerRadius: 8,
                        callbacks: {
                            label: function (context) {
                                const count = context.parsed;
                                const percentage = totalApplications > 0 
                                    ? ((count / totalApplications) * 100).toFixed(1) 
                                    : 0;
                                return ` ${context.label}: ${count} (${percentage}%)`;
                            }
                        }
                    }
                }
            }
        });
    }
});
