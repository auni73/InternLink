import { api } from '/js/api.js';

class AssessmentEngine {
    constructor(config) {
        this.skillId = config.skillId;
        this.skillName = config.skillName;
        this.sessionToken = config.sessionToken;
        this.durationSeconds = config.durationMinutes * 60; // 600 seconds
        this.remainingSeconds = this.durationSeconds;
        this.isSubmitted = false;

        this.timerTextEl = document.getElementById('timerText');
        this.timerProgressEl = document.getElementById('timerProgress');
        this.timerBadgeEl = document.getElementById('timerBadge');
        this.submitBtn = document.getElementById('submitAssessmentBtn');
        this.examForm = document.getElementById('assessmentForm');
        this.examContainer = document.getElementById('examContainer');
        this.resultsContainer = document.getElementById('resultsContainer');

        this.init();
    }

    init() {
        if (this.submitBtn) {
            this.submitBtn.addEventListener('click', () => this.submitExam(false));
        }

        this.startTimer();
    }

    startTimer() {
        this.updateTimerDisplay();

        this.timerInterval = setInterval(() => {
            if (this.isSubmitted) {
                clearInterval(this.timerInterval);
                return;
            }

            this.remainingSeconds--;
            this.updateTimerDisplay();

            if (this.remainingSeconds <= 0) {
                clearInterval(this.timerInterval);
                this.handleTimeout();
            }
        }, 1000);
    }

    updateTimerDisplay() {
        const mins = Math.floor(Math.max(0, this.remainingSeconds) / 60);
        const secs = Math.max(0, this.remainingSeconds) % 60;
        const timeString = `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;

        if (this.timerTextEl) {
            this.timerTextEl.textContent = timeString;
        }

        const percentage = Math.max(0, (this.remainingSeconds / this.durationSeconds) * 100);
        if (this.timerProgressEl) {
            this.timerProgressEl.style.width = `${percentage}%`;
        }

        // Visual warning when less than 1 minute remains
        if (this.remainingSeconds <= 60) {
            if (this.timerBadgeEl) {
                this.timerBadgeEl.classList.remove('bg-light', 'text-slate-800');
                this.timerBadgeEl.classList.add('bg-danger-subtle', 'text-danger', 'border-danger');
            }
            if (this.timerProgressEl) {
                this.timerProgressEl.classList.remove('bg-primary');
                this.timerProgressEl.classList.add('bg-danger');
            }
        }
    }

    handleTimeout() {
        if (this.isSubmitted) return;
        if (window.showToast) {
            window.showToast('Time expired! Submitting your assessment answers automatically...', 'warning');
        }
        this.submitExam(true);
    }

    collectAnswers() {
        const answers = [];
        const questionCards = document.querySelectorAll('.question-card');

        questionCards.forEach(card => {
            const qId = card.getAttribute('data-question-id');
            const selectedRadio = card.querySelector('input[type="radio"]:checked');
            const selectedIndex = selectedRadio ? parseInt(selectedRadio.value, 10) : null;

            answers.push({
                questionId: qId,
                selectedOptionIndex: selectedIndex
            });
        });

        return answers;
    }

    async submitExam(isAutoSubmit = false) {
        if (this.isSubmitted) return;
        this.isSubmitted = true;

        if (this.timerInterval) {
            clearInterval(this.timerInterval);
        }

        if (this.submitBtn) {
            this.submitBtn.disabled = true;
            this.submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span> Grading Assessment...';
        }

        const answers = this.collectAnswers();
        const payload = {
            skillId: this.skillId,
            sessionToken: this.sessionToken,
            answers: answers
        };

        try {
            const response = await api.post('/Student/Assessments/Submit', payload);
            if (response.success && response.result) {
                this.renderResults(response.result);
            } else {
                throw new Error(response.error || 'Failed to grade assessment.');
            }
        } catch (error) {
            this.isSubmitted = false;
            if (this.submitBtn) {
                this.submitBtn.disabled = false;
                this.submitBtn.innerHTML = '<i class="bi bi-check-circle me-1"></i> Submit Assessment';
            }

            if (window.showToast) {
                window.showToast(error.message || 'Submission failed.', 'danger');
            }
        }
    }

    renderResults(result) {
        if (this.examContainer) this.examContainer.classList.add('d-none');
        if (this.resultsContainer) this.resultsContainer.classList.remove('d-none');

        // Scroll to top of results
        window.scrollTo({ top: 0, behavior: 'smooth' });

        const scoreEl = document.getElementById('resultScore');
        const passBadgeEl = document.getElementById('resultPassBadge');
        const scoreSummaryEl = document.getElementById('resultSummary');
        const feedbackContainer = document.getElementById('feedbackQuestionsList');

        if (scoreEl) scoreEl.textContent = `${result.achievedScore}%`;

        if (passBadgeEl) {
            if (result.isPassed) {
                passBadgeEl.className = 'badge bg-success-subtle text-success border border-success px-3 py-2 fs-6';
                passBadgeEl.innerHTML = '<i class="bi bi-patch-check-fill me-1"></i> Assessment Passed - Verified!';
            } else {
                passBadgeEl.className = 'badge bg-danger-subtle text-danger border border-danger px-3 py-2 fs-6';
                passBadgeEl.innerHTML = '<i class="bi bi-x-circle-fill me-1"></i> Needs Improvement (Pass Mark: 70%)';
            }
        }

        if (scoreSummaryEl) {
            scoreSummaryEl.innerHTML = `You answered <strong>${result.correctCount}</strong> out of <strong>${result.totalQuestions}</strong> questions correctly.`;
        }

        if (feedbackContainer && result.questionFeedback) {
            feedbackContainer.innerHTML = '';

            result.questionFeedback.forEach((q, index) => {
                const isCorrect = q.isCorrect;
                const statusBadge = isCorrect 
                    ? '<span class="badge bg-success-subtle text-success border border-success-subtle"><i class="bi bi-check-circle-fill me-1"></i>Correct</span>'
                    : '<span class="badge bg-danger-subtle text-danger border border-danger-subtle"><i class="bi bi-x-circle-fill me-1"></i>Incorrect</span>';

                let optionsHtml = '';
                q.options.forEach((opt, optIdx) => {
                    let optClass = 'list-group-item';
                    let optIcon = '';

                    if (optIdx === q.correctOptionIndex) {
                        optClass += ' list-group-item-success fw-semibold';
                        optIcon = '<i class="bi bi-check2 text-success me-2"></i>';
                    } else if (optIdx === q.selectedOptionIndex && !isCorrect) {
                        optClass += ' list-group-item-danger';
                        optIcon = '<i class="bi bi-x text-danger me-2"></i>';
                    }

                    optionsHtml += `<li class="${optClass}">${optIcon}${opt}</li>`;
                });

                const cardHtml = `
                    <div class="card mb-3 shadow-sm border ${isCorrect ? 'border-success-subtle' : 'border-danger-subtle'} rounded-3">
                        <div class="card-header bg-white d-flex justify-content-between align-items-center py-3">
                            <span class="fw-semibold">Question ${index + 1}</span>
                            ${statusBadge}
                        </div>
                        <div class="card-body p-4">
                            <h6 class="card-title fw-bold text-slate-800 mb-3">${q.questionText}</h6>
                            <ul class="list-group mb-3">
                                ${optionsHtml}
                            </ul>
                            <div class="p-3 bg-light-subtle rounded-3 small border">
                                <strong>Explanation:</strong> ${q.explanation}
                            </div>
                        </div>
                    </div>
                `;

                feedbackContainer.insertAdjacentHTML('beforeend', cardHtml);
            });
        }
    }
}

window.AssessmentEngine = AssessmentEngine;
