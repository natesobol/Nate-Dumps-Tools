const archiveInput = document.getElementById('archiveInput');
const scanBtn = document.getElementById('scanBtn');
const downloadBtn = document.getElementById('downloadBtn');
const statusEl = document.getElementById('status');
const warningsEl = document.getElementById('warnings');
const resultCount = document.getElementById('resultCount');
const resultsTable = document.getElementById('resultsTable').querySelector('tbody');
const formatSelect = document.getElementById('formatSelect');

function setStatus(message, type = 'info') {
    statusEl.textContent = message;
    statusEl.style.color = type === 'error' ? '#f59e0b' : '#94a3b8';
}

function renderWarnings(warnings) {
    warningsEl.innerHTML = '';
    warnings.forEach((w) => {
        const li = document.createElement('li');
        li.textContent = w;
        warningsEl.appendChild(li);
    });
}

function renderResults(audioMessages) {
    resultsTable.innerHTML = '';
    audioMessages.forEach((item) => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${item.fileName}</td>
            <td>${item.relativePath}</td>
            <td>${item.extension.toUpperCase()}</td>
            <td>${new Date(item.timestamp).toLocaleString()}</td>
        `;
        resultsTable.appendChild(tr);
    });
    resultCount.textContent = `${audioMessages.length} file${audioMessages.length === 1 ? '' : 's'} found`;
}

function buildFormData() {
    const formData = new FormData();
    const files = archiveInput.files;
    for (const file of files) {
        formData.append('files', file);
    }
    formData.append('targetFormat', formatSelect.value);
    return formData;
}

async function scan() {
    if (!archiveInput.files.length) {
        setStatus('Please choose at least one ZIP or audio file.', 'error');
        return;
    }

    setStatus('Scanning for voice notes...');
    renderWarnings([]);
    renderResults([]);

    try {
        const response = await fetch('/api/scan', { method: 'POST', body: buildFormData() });
        const payload = await response.json();

        if (!response.ok) {
            setStatus(payload.error || 'Unable to scan files.', 'error');
            renderWarnings(payload.warnings || []);
            return;
        }

        renderResults(payload.audioMessages || []);
        renderWarnings(payload.warnings || []);
        setStatus('Scan complete. Ready to download.');
    } catch (error) {
        setStatus(error.message, 'error');
    }
}

async function downloadConverted() {
    if (!archiveInput.files.length) {
        setStatus('Please choose at least one ZIP or audio file.', 'error');
        return;
    }

    setStatus('Converting and packaging audio...');
    renderWarnings([]);

    try {
        const response = await fetch('/api/download', { method: 'POST', body: buildFormData() });
        if (!response.ok) {
            let message = 'Unable to convert files.';
            try {
                const payload = await response.json();
                message = payload.error || payload.title || message;
                renderWarnings(payload.warnings || []);
            } catch (_) {
                // ignore
            }
            setStatus(message, 'error');
            return;
        }

        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'voice-notes.zip';
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);

        setStatus('Download started.');
    } catch (error) {
        setStatus(error.message, 'error');
    }
}

scanBtn.addEventListener('click', scan);
downloadBtn.addEventListener('click', downloadConverted);
