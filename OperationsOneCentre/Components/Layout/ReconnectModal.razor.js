// Reconnect modal - aggressive auto-recovery for Blazor Server
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", () => location.reload());

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

let retryCount = 0;
let autoRetryTimer = null;
const MAX_RETRIES = 5;           // Reload page after 5 failed reconnections
const AUTO_RETRY_INTERVAL = 3000; // Try reconnecting every 3s automatically

function handleReconnectStateChanged(event) {
    const state = event.detail.state;

    if (state === "show") {
        retryCount = 0;
        reconnectModal.showModal();
        startAutoRetry(); // Start aggressive auto-retry immediately
    } else if (state === "hide") {
        retryCount = 0;
        stopAutoRetry();
        reconnectModal.close();
    } else if (state === "failed") {
        retryCount++;
        if (retryCount >= MAX_RETRIES) {
            stopAutoRetry();
            location.reload(); // Give up and reload - server will create a fresh circuit
        }
    } else if (state === "rejected") {
        stopAutoRetry();
        location.reload();
    }
}

function startAutoRetry() {
    stopAutoRetry();
    autoRetryTimer = setInterval(() => retry(), AUTO_RETRY_INTERVAL);
    // Also listen for tab becoming visible again
    document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
}

function stopAutoRetry() {
    if (autoRetryTimer) {
        clearInterval(autoRetryTimer);
        autoRetryTimer = null;
    }
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
}

async function retry() {
    try {
        const successful = await Blazor.reconnect();
        if (!successful) {
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                retryCount++;
                if (retryCount >= MAX_RETRIES) {
                    stopAutoRetry();
                    location.reload();
                }
            } else {
                stopAutoRetry();
                reconnectModal.close();
            }
        }
    } catch {
        retryCount++;
        if (retryCount >= MAX_RETRIES) {
            stopAutoRetry();
            location.reload();
        }
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        }
    } catch {
        location.reload();
    }
}

function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        retry();
    }
}
