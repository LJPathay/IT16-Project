/* layout-core.js - Shared application logic */

function showToast(message, type = 'success') {
    const toastId = 'toast-' + Date.now();
    const isSuccess = type === 'success';
    
    const toastHtml = `
        <div id="${toastId}" class="position-fixed bottom-0 end-0 p-4" style="z-index: 9999; max-width: 400px; pointer-events: none;">
            <div class="toast-wow-card animate__animated animate__backInUp" style="pointer-events: auto;">
                <div class="skeleton-content">
                    <div class="shimmer-circle"></div>
                    <div class="shimmer-lines"><div class="shimmer-line short"></div><div class="shimmer-line long"></div></div>
                </div>
                <div class="actual-content" style="display: none;">
                    <div class="success-icon-wrapper ${isSuccess ? 'bg-gold-glow' : 'bg-danger-glow'}">
                        <i class="fas ${isSuccess ? 'fa-check-circle' : 'fa-times-circle'} animate__animated animate__rotateIn"></i>
                    </div>
                    <div class="success-text">
                        <h6 class="mb-0 fw-bold text-dark">${isSuccess ? 'Operation Successful' : 'Action Failed'}</h6>
                        <p class="mb-0 text-muted x-small">${message}</p>
                    </div>
                </div>
            </div>
        </div>
        <style>
            .toast-wow-card { background: #fff; border: 1px solid #e2e8f0; border-left: 4px solid ${isSuccess ? '#d4af37' : '#ef4444'}; border-radius: 1rem; padding: 1rem 1.25rem; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1); display: flex; align-items: center; gap: 1rem; min-width: 320px; position: relative; }
            .skeleton-content { display: flex; align-items: center; gap: 1rem; width: 100%; }
            .shimmer-circle { width: 40px; height: 40px; border-radius: 50%; background: #f1f5f9; position: relative; overflow: hidden; flex-shrink: 0; }
            .shimmer-circle::after, .shimmer-line::after { content: ""; position: absolute; top: 0; left: 0; width: 100%; height: 100%; background: linear-gradient(90deg, transparent, rgba(255,255,255,0.8), transparent); animation: shimmerMove 1.5s infinite; }
            .shimmer-lines { flex-grow: 1; }
            .shimmer-line { height: 8px; background: #f1f5f9; border-radius: 4px; margin-bottom: 8px; position: relative; overflow: hidden; }
            .shimmer-line.long { width: 85%; }
            .shimmer-line.short { width: 50%; }
            @keyframes shimmerMove { 0% { transform: translateX(-100%); } 100% { transform: translateX(100%); } }
            .success-icon-wrapper { width: 40px; height: 40px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 1.2rem; flex-shrink: 0; background: ${isSuccess ? '#fffbeb' : '#fef2f2'}; color: ${isSuccess ? '#d4af37' : '#ef4444'}; border: 1px solid ${isSuccess ? '#fef3c7' : '#fee2e2'}; }
            .success-text h6 { font-size: 0.95rem; font-weight: 700; color: #1e293b; margin: 0; }
            .success-text p { font-size: 0.8rem; color: #64748b; margin: 0; }
            .actual-content { display: flex !important; align-items: center; gap: 1rem; width: 100%; }
        </style>
    `;
    
    document.body.insertAdjacentHTML('beforeend', toastHtml);
    
    const toastElement = document.getElementById(toastId);
    const skeleton = toastElement.querySelector('.skeleton-content');
    const actual = toastElement.querySelector('.actual-content');
    
    setTimeout(() => {
        if (skeleton && actual) {
            skeleton.style.display = 'none';
            actual.style.display = 'flex';
            actual.classList.add('animate__animated', 'animate__fadeIn');
        }
    }, 400);

    setTimeout(() => {
        if (toastElement) {
            const card = toastElement.querySelector('.toast-wow-card');
            if (card) {
                card.classList.add('animate__animated', 'animate__fadeOutDown');
                setTimeout(() => toastElement.remove(), 800);
            }
        }
    }, 5400);
}

/* SESSION TIMER */
let idleTimer;
let countdownTimer;
const IDLE_TIME = 5 * 60 * 1000;
const COUNTDOWN_TIME = 3 * 60 * 1000;
let bootstrapModal;

function initSessionTimer() {
    const modalElement = document.getElementById('inactivityModal');
    if (modalElement) {
        bootstrapModal = new bootstrap.Modal(modalElement);
        
        const stayBtn = document.getElementById('stayLoggedInBtn');
        if (stayBtn) stayBtn.addEventListener('click', () => {
            bootstrapModal.hide();
            resetTimer();
        });

        const logoutBtn = document.getElementById('logoutNowBtn');
        if (logoutBtn) logoutBtn.addEventListener('click', performLogout);

        resetTimer();
        
        ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'].forEach(evt => {
            window.addEventListener(evt, resetTimer, true);
        });
    }
}

function resetTimer() {
    const modalElement = document.getElementById('inactivityModal');
    if (modalElement && modalElement.classList.contains('show')) return;
    clearTimeout(idleTimer);
    idleTimer = setTimeout(showInactivityModal, IDLE_TIME);
}

function showInactivityModal() {
    if (bootstrapModal) {
        bootstrapModal.show();
        startCountdown();
    }
}

function startCountdown() {
    let timeLeft = COUNTDOWN_TIME;
    const display = document.getElementById('inactivityCountdown');
    const progress = document.getElementById('inactivityProgressBar');
    
    clearInterval(countdownTimer);
    
    countdownTimer = setInterval(() => {
        timeLeft -= 1000;
        const minutes = Math.floor(timeLeft / 60000);
        const seconds = Math.floor((timeLeft % 60000) / 1000);
        
        if (display) display.innerText = `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
        if (progress) progress.style.width = (timeLeft / COUNTDOWN_TIME) * 100 + '%';

        if (timeLeft <= 0) {
            clearInterval(countdownTimer);
            performLogout();
        }
    }, 1000);
}

function performLogout() {
    const logoutForm = document.getElementById('logoutForm');
    if (logoutForm) logoutForm.submit();
    else window.location.href = '/Account/Logout';
}

document.addEventListener('DOMContentLoaded', () => {
    initSessionTimer();
    const trigger = document.getElementById('toast-trigger');
    if (trigger) {
        showToast(trigger.dataset.message, trigger.dataset.type);
    }
});
