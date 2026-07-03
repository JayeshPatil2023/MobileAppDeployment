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

  document.addEventListener('DOMContentLoaded', () => {
    initSidebarScrollSpy();
    initUploadZones();
    initFormProgress();
    initAlertDismiss();
  });
})();
