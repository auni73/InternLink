// interview-questions.js — generates the tabbed question bank.
import { api } from '/js/api.js';

const root = document.getElementById('questionBank');

if (root) {
    const generateBtn = document.getElementById('generateQuestionsBtn');
    const roleInput = document.getElementById('questionRole');
    const jobSelect = document.getElementById('questionJob');
    const status = document.getElementById('questionStatus');
    const results = document.getElementById('questionResults');
    const tabs = document.getElementById('questionCategoryTabs');
    const panes = document.getElementById('questionCategoryPanes');

    const CATEGORIES = ['Technical', 'HR', 'Situational'];
    const ICONS = { Technical: 'bi-code-slash', HR: 'bi-people', Situational: 'bi-signpost-split' };

    function showStatus(message, variant) {
        status.textContent = message;
        status.className = `alert alert-${variant}`;
    }

    function clearStatus() {
        status.className = 'alert d-none';
        status.textContent = '';
    }

    function buildCard(question, index) {
        const col = document.createElement('div');
        col.className = 'col-lg-6';

        const card = document.createElement('div');
        card.className = 'card border-0 shadow-sm rounded-3 h-100 transition-hover';

        const body = document.createElement('div');
        body.className = 'card-body p-4 d-flex gap-3';

        const badge = document.createElement('span');
        badge.className = 'badge bg-primary-subtle text-primary border border-primary-subtle rounded-pill align-self-start px-3 py-2';
        badge.textContent = index;

        const text = document.createElement('p');
        text.className = 'mb-0 lh-lg';
        text.textContent = question.questionText;

        body.append(badge, text);
        card.append(body);
        col.append(card);
        return col;
    }

    function render(questions) {
        tabs.replaceChildren();
        panes.replaceChildren();

        const populated = CATEGORIES.filter(c => questions.some(q => q.category === c));
        if (populated.length === 0) {
            showStatus('No questions came back. Try generating again.', 'warning');
            results.hidden = true;
            return;
        }

        populated.forEach((category, position) => {
            const inCategory = questions.filter(q => q.category === category);
            const paneId = `questionPane-${category}`;

            const tab = document.createElement('li');
            tab.className = 'nav-item';
            tab.innerHTML = `<button class="nav-link ${position === 0 ? 'active' : ''}" data-bs-toggle="tab"
                data-bs-target="#${paneId}" type="button" role="tab">
                <i class="bi ${ICONS[category]} me-1"></i>${category}
                <span class="badge bg-secondary-subtle text-secondary-emphasis ms-1">${inCategory.length}</span>
            </button>`;
            tabs.append(tab);

            const pane = document.createElement('div');
            pane.className = `tab-pane fade ${position === 0 ? 'show active' : ''}`;
            pane.id = paneId;
            pane.setAttribute('role', 'tabpanel');

            const row = document.createElement('div');
            row.className = 'row g-3';
            inCategory.forEach((q, i) => row.append(buildCard(q, i + 1)));

            pane.append(row);
            panes.append(pane);
        });

        results.hidden = false;
    }

    generateBtn.addEventListener('click', async () => {
        const role = roleInput.value.trim();
        if (!role) {
            showStatus('Tell us which role you are preparing for.', 'warning');
            roleInput.focus();
            return;
        }

        clearStatus();
        generateBtn.disabled = true;
        generateBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span><span>Generating…</span>';

        try {
            const result = await api.post(root.dataset.questionsUrl, {
                role,
                jobId: jobSelect.value || null
            });
            render(result.questions ?? []);
        } catch (error) {
            showStatus(error.message, 'warning');
            results.hidden = true;
        } finally {
            generateBtn.disabled = false;
            generateBtn.innerHTML = '<i class="bi bi-stars"></i><span>Generate Questions</span>';
        }
    });
}
