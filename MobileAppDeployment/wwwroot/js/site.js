(function () {
  'use strict';

  function getScrollOffset() {
    const styles = getComputedStyle(document.documentElement);
    const headerH = parseFloat(styles.getPropertyValue('--header-h')) || 64;
    const rem = parseFloat(styles.fontSize) || 16;
    return headerH + rem * 1.5;
  }

  /**
   * Highlights the sidebar link for the section currently in view.
   * Section IDs are read from the sidebar markup (data-section), so highlight
   * stays correct when form section order/IDs change.
   */
  function initSidebarScrollSpy() {
    const links = Array.from(document.querySelectorAll('.sidebar-nav a[data-section]'));
    if (!links.length) return;

    const sectionIds = links
      .map((link) => link.dataset.section)
      .filter(Boolean);

    const observer = new IntersectionObserver(
      (entries) => {
        // Prefer the top-most intersecting section so reordered sections highlight correctly.
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top);

        if (!visible.length) return;

        const activeId = visible[0].target.id;
        links.forEach((link) => {
          link.classList.toggle('active', link.dataset.section === activeId);
        });
      },
      { rootMargin: '-20% 0px -60% 0px', threshold: 0 }
    );

    sectionIds.forEach((id) => {
      const el = document.getElementById(id);
      if (el) observer.observe(el);
    });

    links.forEach((link) => {
      link.addEventListener('click', (e) => {
        e.preventDefault();
        const target = document.getElementById(link.dataset.section);
        if (!target) return;

        links.forEach((l) => l.classList.toggle('active', l === link));

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

      function clearPreview() {
        const filenameEl = zone.querySelector('.upload-filename');
        const previewWrap = document.getElementById(zone.dataset.preview);
        const previewImg = previewWrap?.querySelector('img');
        const statusEl = zone.querySelector('.upload-status');

        zone.classList.remove('has-file');
        if (filenameEl) filenameEl.textContent = '';
        if (previewWrap) previewWrap.classList.remove('visible');
        if (previewImg) previewImg.removeAttribute('src');
        if (statusEl) {
          statusEl.textContent = 'Awaiting upload';
          statusEl.classList.add('upload-status--pending');
          statusEl.classList.remove('upload-status--ok');
        }
      }

      /**
       * Validates extension, optional max bytes, and exact pixel size from data-* attributes.
       * Rejects the file (clears input) when invalid so a bad asset cannot be saved.
       */
      function validateSelectedImage(file) {
        const allowedExt = (input.dataset.allowedExt || '')
          .split(',')
          .map((x) => x.trim().toLowerCase())
          .filter(Boolean);
        const exactWidth = parseInt(input.dataset.exactWidth || '0', 10);
        const exactHeight = parseInt(input.dataset.exactHeight || '0', 10);
        const maxBytes = parseInt(input.dataset.maxBytes || '0', 10);

        if (!allowedExt.length && !exactWidth && !exactHeight && !maxBytes) {
          return Promise.resolve(null);
        }

        const name = file.name || '';
        const dot = name.lastIndexOf('.');
        const ext = dot >= 0 ? name.slice(dot).toLowerCase() : '';

        if (allowedExt.length && !allowedExt.includes(ext)) {
          const labels = allowedExt.map((e) => e.replace('.', '').toUpperCase());
          return Promise.resolve(
            `This file must be ${labels.join(' or ')}.`
          );
        }

        if (maxBytes > 0 && file.size > maxBytes) {
          const mb = maxBytes / (1024 * 1024);
          const sizeLabel = mb >= 1
            ? `${Number.isInteger(mb) ? mb : mb.toFixed(1)} MB`
            : `${Math.round(maxBytes / 1024)} KB`;
          return Promise.resolve(`This file must be ${sizeLabel} or smaller.`);
        }

        if (!exactWidth || !exactHeight || !file.type.startsWith('image/')) {
          return Promise.resolve(null);
        }

        return new Promise((resolve) => {
          const objectUrl = URL.createObjectURL(file);
          const img = new Image();
          img.onload = () => {
            URL.revokeObjectURL(objectUrl);
            if (img.naturalWidth !== exactWidth || img.naturalHeight !== exactHeight) {
              resolve(
                `Image must be exactly ${exactWidth} × ${exactHeight} px (uploaded image is ${img.naturalWidth} × ${img.naturalHeight} px).`
              );
            } else {
              resolve(null);
            }
          };
          img.onerror = () => {
            URL.revokeObjectURL(objectUrl);
            resolve('Could not read this image. Upload a valid PNG or JPEG.');
          };
          img.src = objectUrl;
        });
      }

      // After StartDeployment (or any server postback), paint zones red when ModelState
      // already rendered a field-validation-error next to the upload card.
      if (validationMessage && validationMessage.classList.contains('field-validation-error')) {
        zone.classList.add('upload-zone--invalid');
      }

      zone.addEventListener('click', () => input.click());

      input.addEventListener('change', async () => {
        const file = input.files && input.files[0];
        const filenameEl = zone.querySelector('.upload-filename');
        const previewWrap = document.getElementById(zone.dataset.preview);
        const previewImg = previewWrap?.querySelector('img');

        if (!file) {
          clearPreview();
          return;
        }

        const error = await validateSelectedImage(file);
        if (error) {
          input.value = '';
          clearPreview();
          // Keep existing server file indicator only when a previous upload remains.
          if (input.dataset.hasExisting === 'true') {
            zone.classList.add('has-file');
          }
          setUploadValidationError(error);
          return;
        }

        zone.classList.add('has-file');
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
    initAlertDismiss();
    initFieldHelp();
    initProgressPoller();
  });
})();
