// chat.js — mock interview chat surface.
// Handles starting a session, sending one turn at a time, and ending the interview.
import { api } from '/js/api.js';

const setup = document.getElementById('mockSetup');
const chat = document.getElementById('mockChat');

if (chat) {
    const chatLog = document.getElementById('chatLog');
    const chatForm = document.getElementById('chatForm');
    const chatInput = document.getElementById('chatInput');
    const chatSendBtn = document.getElementById('chatSendBtn');
    const chatError = document.getElementById('chatError');
    const endBtn = document.getElementById('endInterviewBtn');
    const recentSessions = document.getElementById('recentSessions');

    const startBtn = document.getElementById('startInterviewBtn');
    const roleInput = document.getElementById('mockRole');
    const jobSelect = document.getElementById('mockJob');
    const startError = document.getElementById('startError');

    const sessionsUrl = chat.dataset.sessionsUrl;
    let sessionId = chat.dataset.sessionId || null;
    let pending = false;

    function scrollToLatest() {
        chatLog.scrollTop = chatLog.scrollHeight;
    }

    function appendTurn(speaker, text) {
        const row = document.createElement('div');
        row.className = `chat-row from-${speaker}`;

        const stack = document.createElement('div');

        const label = document.createElement('div');
        label.className = 'chat-speaker';
        label.textContent = speaker === 'student' ? 'You' : 'Interviewer';

        const bubble = document.createElement('div');
        bubble.className = 'chat-bubble';
        // textContent, never innerHTML: the reply is model output and the answer is the student's own typing.
        bubble.textContent = text;

        stack.append(label, bubble);
        row.append(stack);
        chatLog.append(row);
        scrollToLatest();
        return row;
    }

    function showTypingIndicator() {
        const row = document.createElement('div');
        row.className = 'chat-row from-interviewer';
        row.dataset.typing = 'true';
        row.innerHTML = '<div class="chat-bubble chat-typing"><span></span><span></span><span></span></div>';
        chatLog.append(row);
        scrollToLatest();
        return row;
    }

    function setPending(isPending) {
        pending = isPending;
        chatInput.disabled = isPending;
        chatSendBtn.disabled = isPending;
        endBtn.disabled = isPending;
    }

    function showChatError(message) {
        chatError.textContent = message;
        chatError.classList.remove('d-none');
    }

    function clearChatError() {
        chatError.classList.add('d-none');
        chatError.textContent = '';
    }

    if (startBtn) {
        startBtn.addEventListener('click', async () => {
            const role = roleInput.value.trim();
            if (!role) {
                startError.textContent = 'Tell us which role you are interviewing for.';
                startError.classList.remove('d-none');
                roleInput.focus();
                return;
            }

            startError.classList.add('d-none');
            startBtn.disabled = true;
            startBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span><span>Starting…</span>';

            try {
                const result = await api.post(chat.dataset.startUrl, {
                    role,
                    jobId: jobSelect.value || null
                });

                sessionId = result.sessionId;
                chat.dataset.sessionId = sessionId;
                document.getElementById('chatRoleLabel').textContent = role;

                setup.hidden = true;
                if (recentSessions) {
                    recentSessions.hidden = true;
                }
                chat.hidden = false;

                appendTurn('interviewer', result.firstQuestion);
                chatInput.focus();
            } catch (error) {
                startError.textContent = error.message;
                startError.classList.remove('d-none');
            } finally {
                startBtn.disabled = false;
                startBtn.innerHTML = '<i class="bi bi-play-circle"></i><span>Start Interview</span>';
            }
        });
    }

    chatForm.addEventListener('submit', async (event) => {
        event.preventDefault();
        if (pending || !sessionId) {
            return;
        }

        const reply = chatInput.value.trim();
        if (!reply) {
            return;
        }

        clearChatError();
        appendTurn('student', reply);
        chatInput.value = '';
        setPending(true);

        const typing = showTypingIndicator();

        try {
            const result = await api.post(`${sessionsUrl}/${sessionId}/Message`, { studentReply: reply });
            typing.remove();
            appendTurn('interviewer', result.aiReply);
        } catch (error) {
            typing.remove();
            showChatError(error.message);
        } finally {
            setPending(false);
            chatInput.focus();
        }
    });

    // Enter sends; Shift+Enter keeps the newline for multi-paragraph answers.
    chatInput.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            chatForm.requestSubmit();
        }
    });

    endBtn.addEventListener('click', async () => {
        if (pending || !sessionId) {
            return;
        }

        if (!window.confirm('End the interview and generate your report?')) {
            return;
        }

        clearChatError();
        setPending(true);
        endBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span><span>Building report…</span>';

        try {
            const result = await api.post(`${sessionsUrl}/${sessionId}/End`);
            window.location.href = result.reportUrl;
        } catch (error) {
            showChatError(error.message);
            setPending(false);
            endBtn.innerHTML = '<i class="bi bi-stop-circle"></i><span>End Interview</span>';
        }
    });

    scrollToLatest();
}
