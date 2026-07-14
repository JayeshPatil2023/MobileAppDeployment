(function () {
  'use strict';

  const formSections = ['sec-org', 'sec-app', 'sec-identifiers', 'sec-apple', 'sec-assets', 'sec-onesignal', 'sec-firebase', 'sec-contact'];

  function getScrollOffset() {
    const styles = getComputedStyle(document.documentElement);
    const headerH = parseFloat(styles.getPropertyValue('--header-h')) || 64;
    const rem = parseFloat(styles.fontSize) || 16;
    return headerH + rem * 1.5;
  }

  function initSidebarScrollSpy() {
    const links = document.querySelectorAll('.sidebar-nav a[data-section]');
    if (!links.length) return;

    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          const id = entry.target.id;
          links.forEach((link) => {
            link.classList.toggle('active', link.dataset.section === id);
          });
        });
      },
      { rootMargin: '-20% 0px -60% 0px', threshold: 0 }
    );

    formSections.forEach((id) => {
      const el = document.getElementById(id);
      if (el) observer.observe(el);
    });

    links.forEach((link) => {
      link.addEventListener('click', (e) => {
        e.preventDefault();
        const target = document.getElementById(link.dataset.section);
        if (!target) return;

        const top = target.getBoundingClientRect().top + window.scrollY - getScrollOffset();
        window.scrollTo({ top, behavior: 'smooth' });
      });
    });
  }

  function initUploadZones() {
    document.querySelectorAll('[data-upload-zone]').forEach((zone) => {
      const inputId = zone.dataset.uploadZone;
      const input = document.getElementById(inputId);
      if (!input) return;

      const validationMessage = document.querySelector(`[data-valmsg-for="${inputId}"]`);

      function setUploadValidationError(message) {
        if (validationMessage) {
          validationMessage.textContent = message || '';
          validationMessage.classList.toggle('field-validation-error', !!message);
          validationMessage.classList.toggle('field-validation-valid', !message);
        }

        zone.classList.toggle('upload-zone--invalid', !!message);
      }

      // After StartDeployment (or any server postback), paint zones red when ModelState
      // already rendered a field-validation-error next to the upload card.
      if (validationMessage && validationMessage.classList.contains('field-validation-error')) {
        zone.classList.add('upload-zone--invalid');
      }

      zone.addEventListener('click', () => input.click());

      input.addEventListener('change', () => {
        const file = input.files && input.files[0];
        const filenameEl = zone.querySelector('.upload-filename');
        const previewWrap = document.getElementById(zone.dataset.preview);
        const previewImg = previewWrap?.querySelector('img');

        if (!file) {
          zone.classList.remove('has-file');
          if (filenameEl) filenameEl.textContent = '';
          return;
        }

        zone.classList.add('has-file');
        // Clear server/client invalid styling once the user picks a replacement file.
        setUploadValidationError('');
        if (filenameEl) filenameEl.textContent = file.name;

        const statusEl = zone.querySelector('.upload-status');
        if (statusEl) {
          statusEl.textContent = 'File detected';
          statusEl.classList.remove('upload-status--pending');
          statusEl.classList.add('upload-status--ok');
        }

        if (previewWrap && previewImg && file.type.startsWith('image/')) {
          const reader = new FileReader();
          reader.onload = (e) => {
            previewImg.src = e.target.result;
            previewWrap.classList.add('visible');
          };
          reader.readAsDataURL(file);
        }
      });
    });
  }

  function initFormProgress() {
    const form = document.getElementById('deployment-form');
    const fill = document.getElementById('prog-fill');
    const pct = document.getElementById('prog-pct');
    if (!form || !fill || !pct) return;

    const requiredSelectors = [
      'input[required]:not([type="file"])',
      'textarea[required]',
      'input[type="file"][required]'
    ];

    function updateProgress() {
      const fields = form.querySelectorAll(requiredSelectors.join(','));
      let filled = 0;

      fields.forEach((field) => {
        if (field.type === 'file') {
          if (field.files && field.files.length > 0) filled++;
          else if (field.dataset.hasExisting === 'true') filled++;
        } else if (field.value && field.value.trim()) {
          filled++;
        }
      });

      const total = fields.length || 1;
      const percent = Math.round((filled / total) * 100);
      fill.style.width = percent + '%';
      pct.textContent = percent + '%';
    }

    form.addEventListener('input', updateProgress);
    form.addEventListener('change', updateProgress);
    updateProgress();
  }

  function initAlertDismiss() {
    document.querySelectorAll('[data-dismiss="alert"]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const banner = btn.closest('.alert-banner');
        if (banner) banner.remove();
      });
    });
  }

  function initFieldHelp() {
    const overlay = document.getElementById('field-help-overlay');
    const titleEl = document.getElementById('field-help-title');
    const bodyEl = document.getElementById('field-help-body');
    const closeBtn = document.getElementById('field-help-close');
    const dataEl = document.getElementById('field-help-data');

    if (!overlay || !titleEl || !bodyEl || !dataEl) return;

    let helpData = {};
    try {
      helpData = JSON.parse(dataEl.textContent || '{}');
    } catch {
      return;
    }

    let lastTrigger = null;

    function closeHelp() {
      overlay.hidden = true;
      document.body.classList.remove('field-help-open');
      if (lastTrigger) lastTrigger.focus();
      lastTrigger = null;
    }

    function openHelp(key, trigger) {
      const entry = helpData[key];
      if (!entry) return;

      lastTrigger = trigger;
      titleEl.textContent = entry.Title || '';
      bodyEl.textContent = entry.Body || '';
      overlay.hidden = false;
      document.body.classList.add('field-help-open');
      closeBtn.focus();
    }

    document.querySelectorAll('.field-help-btn').forEach((btn) => {
      btn.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopPropagation();
        openHelp(btn.dataset.fieldHelp, btn);
      });
    });

    closeBtn?.addEventListener('click', closeHelp);

    overlay.addEventListener('click', (e) => {
      if (e.target === overlay) closeHelp();
    });

    document.addEventListener('keydown', (e) => {
      if (!overlay.hidden && e.key === 'Escape') closeHelp();
    });
  }

  function initProgressPoller() {
    const root =
      document.getElementById('workflow-progress-root') ||
      document.getElementById('merge-progress-root');
    if (!root) return;

    const prefix = root.id.startsWith('workflow') ? 'workflow' : 'merge';
    const statusUrl = root.dataset.statusUrl;
    const bar = document.getElementById(`${prefix}-progress-bar`);
    const fill = document.getElementById(`${prefix}-progress-fill`);
    const messageEl = document.getElementById(`${prefix}-progress-message`);
    const attemptEl = document.getElementById(`${prefix}-progress-attempt`);
    const successBanner = document.getElementById(`${prefix}-success-banner`);
    const successText = document.getElementById(`${prefix}-success-text`);
    const errorBanner = document.getElementById(`${prefix}-error-banner`);
    const errorText = document.getElementById(`${prefix}-error-text`);
    const actions = document.getElementById(`${prefix}-progress-actions`);

    if (!statusUrl || !bar || !fill || !messageEl) return;

    let pollTimer = null;

    function updateUi(data) {
      const percent = Math.max(0, Math.min(100, data.percentComplete ?? 0));
      fill.style.width = `${percent}%`;
      bar.setAttribute('aria-valuenow', String(percent));
      messageEl.textContent = data.currentMessage || 'Working...';

      if (attemptEl && data.attempt > 0 && data.maxRetries > 0) {
        attemptEl.hidden = false;
        attemptEl.textContent = `Attempt ${data.attempt} of ${data.maxRetries}`;
      }

      if (data.status === 'Retrying') {
        bar.classList.add('merge-progress-bar--retrying');
      } else {
        bar.classList.remove('merge-progress-bar--retrying');
      }
    }

    function showTerminalState(data) {
      if (pollTimer) {
        clearInterval(pollTimer);
        pollTimer = null;
      }

      if (actions) actions.hidden = false;

      if (data.status === 'Completed') {
        if (successBanner && successText) {
          successBanner.hidden = false;
          successText.textContent = data.currentMessage || 'Completed successfully.';
        }
        fill.style.width = '100%';
        bar.setAttribute('aria-valuenow', '100');
        return;
      }

      if (data.status === 'Failed' && errorBanner && errorText) {
        errorBanner.hidden = false;
        errorText.textContent = data.errorMessage || 'Operation failed after all retry attempts.';
      }
    }

    async function pollStatus() {
      try {
        const response = await fetch(statusUrl, {
          headers: { Accept: 'application/json' },
          cache: 'no-store'
        });

        if (!response.ok) return;

        const data = await response.json();
        updateUi(data);

        if (data.isTerminal) {
          showTerminalState(data);
        }
      } catch {
        // Keep polling — transient network errors should not stop the UI.
      }
    }

    pollStatus();
    pollTimer = setInterval(pollStatus, 1500);
  }

  document.addEventListener('DOMContentLoaded', () => {
    initSidebarScrollSpy();
    initUploadZones();
    initFormProgress();
    initAlertDismiss();
    initFieldHelp();
    initProgressPoller();
  });
})();
