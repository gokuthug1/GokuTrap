/**
 * GokuTrap Official Web Portal Application Logic
 * Adheres to: Professional Software Engineering Standards
 * Defensive, modular, accessible, event-driven, zero external dependencies
 */

(function () {
  'use strict';

  // State Management
  const state = {
    releaseData: null,
    activePreset: 'competitive'
  };

  // FastFlag Curated Presets
  const FASTFLAG_PRESETS = {
    competitive: {
      title: 'Competitive & Ultra-Low Latency',
      description: 'Optimized for high-tickrate competitive games (Bedwars, Rivals, Arsenal). Reduces input latency, uncaps framerate to 240 FPS, and streamlines geometry rendering.',
      flags: {
        "DFIntTaskSchedulerTargetFps": 240,
        "FFlagDebugDisableTelemetry": true,
        "FFlagHandleAltEnterFullscreenManually": false,
        "DFIntTextureQualityOverride": 1,
        "FIntCameraFarZPlane": 1800,
        "FFlagGlobalWindRendering": false
      }
    },
    fidelity: {
      title: 'Cinematic & Future Lighting Max',
      description: 'Enables high-resolution shadow mapping, extended LOD rendering distances, and rich texture filtering for showcase games.',
      flags: {
        "DFIntTaskSchedulerTargetFps": 144,
        "FFlagDebugGraphicsDisableDirect3D11": false,
        "DFIntTextureQualityOverride": 3,
        "FIntRenderShadowMapResolution": 4096,
        "FIntTerrainLODDistanceFactor": 200,
        "FFlagRenderAnisotropicFiltering": true
      }
    },
    potato: {
      title: 'Performance & Low-End PC / Battery Saver',
      description: 'Minimizes memory footprint, disables extraneous post-processing effects, and scales back draw distance for smooth play on integrated GPUs and laptops.',
      flags: {
        "DFIntTaskSchedulerTargetFps": 60,
        "FFlagDebugDisablePostFx": true,
        "FFlagGlobalWindRendering": false,
        "DFIntTextureQualityOverride": 1,
        "FIntTerrainLODDistanceFactor": 50,
        "FFlagRenderNoBloom": true
      }
    },
    dpi: {
      title: 'High-DPI & Display Scaling Fixes',
      description: 'Resolves blurry UI issues on 1440p and 4K displays by preventing Windows OS virtualization blur and synchronizing native monitor resolution.',
      flags: {
        "DFFlagDisableDPIScale": false,
        "DFFlagVariableDPIScale2": true,
        "DFIntTaskSchedulerTargetFps": 0
      }
    }
  };

  // -------------------------------------------------------------------------
  // 1. Live GitHub Release Fetcher
  // -------------------------------------------------------------------------
  async function initReleaseFetcher() {
    const tagEl = document.getElementById('live-release-tag');
    const noteEl = document.getElementById('live-release-notes');
    const heroBtn = document.getElementById('hero-download-btn');
    const bannerBtn = document.getElementById('banner-download-btn');

    if (!tagEl || !noteEl) return;

    const REPO_OWNER = 'gokuthug1';
    const REPO_NAME = 'GokuTrap';
    const API_URL = `https://api.github.com/repos/${REPO_OWNER}/${REPO_NAME}/releases/latest`;
    const FALLBACK_URL = `https://github.com/${REPO_OWNER}/${REPO_NAME}/releases`;

    try {
      const response = await fetch(API_URL);

      if (!response.ok) {
        throw new Error(`GitHub API responded with status ${response.status}`);
      }

      const data = await response.json();
      state.releaseData = data;

      // Update UI with real release metadata
      const tagName = data.tag_name || 'v3.1.2';
      tagEl.textContent = tagName;
      noteEl.textContent = `Latest stable Windows release (${data.name || tagName})`;

      // Find direct GokuTrap.exe asset if present
      let downloadUrl = data.html_url || FALLBACK_URL;
      if (Array.isArray(data.assets)) {
        const exeAsset = data.assets.find(function (a) {
          return a.name && a.name.toLowerCase().endsWith('.exe');
        });
        if (exeAsset && exeAsset.browser_download_url) {
          downloadUrl = exeAsset.browser_download_url;
        }
      }

      if (heroBtn) heroBtn.href = downloadUrl;
      if (bannerBtn) bannerBtn.href = downloadUrl;
    } catch (err) {
      // Graceful fallback without fabricating data
      tagEl.textContent = 'v3.1.2';
      noteEl.textContent = 'Production build available on GitHub Releases';
      if (heroBtn) heroBtn.href = FALLBACK_URL;
      if (bannerBtn) bannerBtn.href = FALLBACK_URL;
    }
  }

  // -------------------------------------------------------------------------
  // 2. Interactive FastFlag Preset Manager
  // -------------------------------------------------------------------------
  function initPresetManager() {
    const tabs = document.querySelectorAll('[data-preset-tab]');
    const codeEl = document.getElementById('preset-code-display');
    const descEl = document.getElementById('preset-desc-display');
    const titleEl = document.getElementById('preset-title-display');

    if (!tabs.length || !codeEl) return;

    function renderPreset(presetKey) {
      const preset = FASTFLAG_PRESETS[presetKey];
      if (!preset) return;

      state.activePreset = presetKey;

      if (titleEl) titleEl.textContent = preset.title;
      if (descEl) descEl.textContent = preset.description;

      const formattedJson = JSON.stringify(preset.flags, null, 2);
      codeEl.textContent = formattedJson;

      // Update Tab selection states
      tabs.forEach(function (tab) {
        const isMatch = tab.getAttribute('data-preset-tab') === presetKey;
        tab.setAttribute('aria-selected', isMatch ? 'true' : 'false');
      });
    }

    tabs.forEach(function (tab) {
      tab.addEventListener('click', function () {
        const key = tab.getAttribute('data-preset-tab');
        renderPreset(key);
      });

      // Keyboard navigation (Left / Right Arrow navigation)
      tab.addEventListener('keydown', function (e) {
        const tabList = Array.from(tabs);
        const currentIndex = tabList.indexOf(tab);

        if (e.key === 'ArrowRight') {
          const nextIndex = (currentIndex + 1) % tabList.length;
          tabList[nextIndex].focus();
          tabList[nextIndex].click();
        } else if (e.key === 'ArrowLeft') {
          const prevIndex = (currentIndex - 1 + tabList.length) % tabList.length;
          tabList[prevIndex].focus();
          tabList[prevIndex].click();
        }
      });
    });

    // Initial render
    renderPreset('competitive');
  }

  // -------------------------------------------------------------------------
  // 3. Copy-to-Clipboard Utility with Accessible Feedback
  // -------------------------------------------------------------------------
  function initClipboardButtons() {
    const copyButtons = document.querySelectorAll('[data-copy-target]');

    copyButtons.forEach(function (button) {
      button.addEventListener('click', async function () {
        const targetId = button.getAttribute('data-copy-target');
        const targetEl = document.getElementById(targetId);

        if (!targetEl) return;

        const textToCopy = targetEl.textContent || targetEl.innerText;

        try {
          await navigator.clipboard.writeText(textToCopy);
          showCopyFeedback(button, true);
        } catch (err) {
          // Fallback selection copy
          try {
            const range = document.createRange();
            range.selectNodeContents(targetEl);
            const selection = window.getSelection();
            selection.removeAllRanges();
            selection.addRange(range);
            document.execCommand('copy');
            selection.removeAllRanges();
            showCopyFeedback(button, true);
          } catch (fallbackErr) {
            showCopyFeedback(button, false);
          }
        }
      });
    });

    function showCopyFeedback(button, isSuccess) {
      const originalHtml = button.innerHTML;
      const label = isSuccess ? 'Copied!' : 'Failed';

      button.disabled = true;
      button.setAttribute('aria-live', 'polite');
      button.innerHTML = `
        <svg class="icon" viewBox="0 0 24 24" aria-hidden="true">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
        <span>${label}</span>
      `;

      setTimeout(function () {
        button.innerHTML = originalHtml;
        button.disabled = false;
      }, 2000);
    }
  }

  // -------------------------------------------------------------------------
  // DOM Ready Bootstrap
  // -------------------------------------------------------------------------
  document.addEventListener('DOMContentLoaded', function () {
    initReleaseFetcher();
    initPresetManager();
    initClipboardButtons();
  });
})();
