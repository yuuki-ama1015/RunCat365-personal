(() => {
  const INDICATORS = [
    { id: "cpu", label: "CPU", blurb: "プロセッサ使用率に連動します。" },
    { id: "gpu", label: "GPU", blurb: "GPU 使用率に連動します。" },
    { id: "memory", label: "Memory", blurb: "メモリ使用率に連動します。" },
    { id: "temperature", label: "Temperature", blurb: "温度に連動します。" },
  ];

  const view = document.getElementById("view");
  const app = document.querySelector(".app");
  const toggle = document.getElementById("sidebar-toggle");

  /** @type {Record<string, { id: string, enabled: boolean, available: boolean, runner: string, customRunnerName: string|null, colorTintEnabled: boolean, colorTintStrength: number, runnerSpeedEnabled: boolean, stillModeEnabled: boolean, stillSetName: string|null, stillCrossfadeEnabled: boolean }>} */
  let indicatorState = {};

  /** @type {{ builtin: Array<{ id: string, label: string }>, custom: Array<{ name: string, frameCount?: number }> }} */
  let runnersCatalog = {
    builtin: [
      { id: "Cat", label: "Cat" },
      { id: "Parrot", label: "Parrot" },
      { id: "Horse", label: "Horse" },
    ],
    custom: [],
  };

  /** @type {Array<{ name: string, frameCount?: number }>} */
  let stillSetsCatalog = [];

  /** @type {number} */
  let previewLoad = 50;
  /** @type {number} */
  let previewTick = 0;
  /** @type {number|null} */
  let previewTimer = null;
  /** @type {string|null} */
  let previewIndicatorId = null;

  function isHostAvailable() {
    return !!(window.chrome && chrome.webview);
  }

  function postHost(message) {
    if (!isHostAvailable()) return;
    chrome.webview.postMessage(message);
  }

  function currentRoute() {
    const hash = location.hash.replace(/^#/, "");
    return hash || "home";
  }

  function setActiveNav(route) {
    document.querySelectorAll(".tree-item").forEach((el) => {
      el.classList.toggle("active", el.dataset.route === route);
    });

    document.querySelectorAll(".tree-group").forEach((group) => {
      const key = group.dataset.group;
      let active = route === key;
      if (!active && key === "indicators") {
        active = route.startsWith("indicator/");
      }
      if (!active && key === "assets") {
        active = route.startsWith("assets/");
      }
      group.classList.toggle("active", active);
    });
  }

  function runnerSelectValue(state) {
    if (!state) return "builtin:Cat";
    if (state.customRunnerName) {
      return `custom:${state.customRunnerName}`;
    }
    return `builtin:${state.runner || "Cat"}`;
  }

  function runnerPreviewLabel(state) {
    if (!state) return { name: "Cat", kind: "組み込み" };
    if (state.customRunnerName) {
      return { name: state.customRunnerName, kind: "カスタム" };
    }
    const builtin = runnersCatalog.builtin.find((r) => r.id === state.runner);
    return {
      name: builtin ? builtin.label : state.runner || "Cat",
      kind: "組み込み",
    };
  }

  function buildRunnerOptionsHtml(state) {
    const selected = runnerSelectValue(state);
    const builtinOptions = runnersCatalog.builtin
      .map((runner) => {
        const value = `builtin:${runner.id}`;
        const selectedAttr = value === selected ? " selected" : "";
        return `<option value="${value}"${selectedAttr}>${runner.label}（組み込み）</option>`;
      })
      .join("");
    const customOptions = runnersCatalog.custom
      .map((runner) => {
        const value = `custom:${runner.name}`;
        const selectedAttr = value === selected ? " selected" : "";
        const escaped = escapeHtml(runner.name);
        return `<option value="${value}"${selectedAttr}>${escaped}（カスタム）</option>`;
      })
      .join("");
    return builtinOptions + customOptions;
  }


  function buildStillOptionsHtml(state) {
    const selected = state && state.stillSetName ? state.stillSetName : "";
    const placeholderSelected = selected ? "" : " selected";
    let html = `<option value=""${placeholderSelected}>（未設定）</option>`;
    stillSetsCatalog.forEach((set) => {
      const value = set.name;
      const selectedAttr = value === selected ? " selected" : "";
      const frames =
        typeof set.frameCount === "number" ? `（${set.frameCount} 枚）` : "";
      html += `<option value="${escapeHtml(value)}"${selectedAttr}>${escapeHtml(value)}${frames}</option>`;
    });
    return html;
  }

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function clampStrength(value) {
    const n = Number(value);
    if (!Number.isFinite(n)) return 100;
    return Math.max(0, Math.min(100, Math.round(n)));
  }

  function applyIndicatorState(items, runners, stillSets) {
    if (runners && Array.isArray(runners.builtin)) {
      runnersCatalog = {
        builtin: runners.builtin,
        custom: Array.isArray(runners.custom) ? runners.custom : [],
      };
    }
    if (Array.isArray(stillSets)) {
      stillSetsCatalog = stillSets;
    }

    indicatorState = {};
    (items || []).forEach((item) => {
      if (!item || !item.id) return;
      indicatorState[item.id] = {
        id: item.id,
        enabled: !!item.enabled,
        available: item.available !== false,
        runner: item.runner || "Cat",
        customRunnerName: item.customRunnerName || null,
        colorTintEnabled: !!item.colorTintEnabled,
        colorTintStrength: clampStrength(item.colorTintStrength),
        runnerSpeedEnabled: item.runnerSpeedEnabled !== false,
        stillModeEnabled: !!item.stillModeEnabled,
        stillSetName: item.stillSetName || null,
        stillCrossfadeEnabled: !!item.stillCrossfadeEnabled,
      };
    });

    document.querySelectorAll("[data-indicator-id]").forEach((card) => {
      const id = card.dataset.indicatorId;
      const state = indicatorState[id];
      if (!state) return;

      const checkbox = card.querySelector('input[type="checkbox"]');
      const hint = card.querySelector(".unavailable-hint");
      const runnerLabel = card.querySelector("[data-card-runner]");
      if (!(checkbox instanceof HTMLInputElement)) return;

      const available = state.available;
      checkbox.disabled = !available;
      checkbox.checked = available && state.enabled;
      if (hint) {
        hint.hidden = available;
      }
      if (runnerLabel) {
        const preview = runnerPreviewLabel(state);
        runnerLabel.textContent = `${preview.name}（${preview.kind}）`;
      }
    });

    const detailRoot = view.querySelector("[data-indicator-detail]");
    if (detailRoot) {
      syncIndicatorDetail(detailRoot.dataset.indicatorDetail);
    }
  }


  function stopPreviewLoop() {
    if (previewTimer != null) {
      clearTimeout(previewTimer);
      previewTimer = null;
    }
  }

  function requestPreview(id, options) {
    if (!id) return;
    const opts = options || {};
    if (opts.resetTick) {
      stopPreviewLoop();
      previewTick = 0;
    }
    postHost({
      type: "getPreview",
      id,
      load: previewLoad,
      tick: previewTick,
    });
  }

  function schedulePreviewTick(id, intervalMs) {
    stopPreviewLoop();
    if (!id || !(intervalMs > 0)) return;
    previewTimer = window.setTimeout(() => {
      previewTick += 1;
      requestPreview(id);
    }, intervalMs);
  }

  function applyPreviewMessage(data) {
    if (!data || data.type !== "preview") return;
    const detailRoot = view.querySelector("[data-indicator-detail]");
    if (!detailRoot) return;
    const id = detailRoot.dataset.indicatorDetail;
    if (!id || data.id !== id) return;

    const img = view.querySelector("[data-preview-image]");
    const placeholder = view.querySelector("[data-preview-placeholder]");
    const loadValue = view.querySelector("[data-preview-load-value]");
    const labelEl = view.querySelector("[data-preview-frame-label]");

    if (img instanceof HTMLImageElement) {
      if (data.imageDataUrl) {
        img.src = data.imageDataUrl;
        img.hidden = false;
        if (placeholder) placeholder.hidden = true;
      } else {
        img.removeAttribute("src");
        img.hidden = true;
        if (placeholder) placeholder.hidden = false;
      }
    }
    if (loadValue) loadValue.textContent = String(Math.round(Number(data.load) || previewLoad));
    if (labelEl) {
      labelEl.textContent = data.label || "";
    }

    if (data.stillMode) {
      stopPreviewLoop();
    } else if (typeof data.intervalMs === "number" && data.intervalMs > 0) {
      schedulePreviewTick(id, data.intervalMs);
    } else {
      stopPreviewLoop();
    }
  }

  function syncIndicatorDetail(id) {
    const state = indicatorState[id];
    if (!state) return;

    const checkbox = view.querySelector(
      `input[data-enable-toggle="${id}"]`
    );
    const hint = view.querySelector(".unavailable-hint");
    const runnerPick = view.querySelector("#runner-pick");
    const previewName = view.querySelector("[data-preview-name]");
    const previewKind = view.querySelector("[data-preview-kind]");

    if (checkbox instanceof HTMLInputElement) {
      checkbox.disabled = !state.available;
      checkbox.checked = state.available && state.enabled;
    }
    if (hint) {
      hint.hidden = state.available;
    }
    if (runnerPick instanceof HTMLSelectElement) {
      runnerPick.innerHTML = buildRunnerOptionsHtml(state);
      runnerPick.disabled = false;
      runnerPick.value = runnerSelectValue(state);
    }

    const preview = runnerPreviewLabel(state);
    if (previewName) previewName.textContent = preview.name;
    if (previewKind) previewKind.textContent = preview.kind;

    const stillOn = !!state.stillModeEnabled;
    const runnerChip = view.querySelector('[data-mode-chip="runner"]');
    const tintChip = view.querySelector('[data-mode-chip="tint"]');
    const stillChip = view.querySelector('[data-mode-chip="still"]');
    if (runnerChip) {
      const runnerOn = !stillOn && state.runnerSpeedEnabled;
      runnerChip.setAttribute("aria-pressed", runnerOn ? "true" : "false");
      runnerChip.classList.toggle("mode-chip-muted", !runnerOn);
      runnerChip.disabled = stillOn;
    }
    if (tintChip) {
      const tintOn = !stillOn && state.colorTintEnabled;
      tintChip.setAttribute("aria-pressed", tintOn ? "true" : "false");
      tintChip.classList.toggle("mode-chip-muted", !tintOn);
      tintChip.disabled = stillOn;
    }
    if (stillChip) {
      stillChip.setAttribute("aria-pressed", stillOn ? "true" : "false");
      stillChip.classList.toggle("mode-chip-muted", !stillOn);
      stillChip.disabled = false;
    }

    if (runnerPick instanceof HTMLSelectElement) {
      runnerPick.disabled = stillOn;
    }

    const stillPick = view.querySelector("#still-pick");
    if (stillPick instanceof HTMLSelectElement) {
      stillPick.innerHTML = buildStillOptionsHtml(state);
      stillPick.disabled = !stillOn;
      stillPick.value = state.stillSetName || "";
    }

    const crossfadeRow = view.querySelector("[data-still-crossfade-row]");
    if (crossfadeRow) {
      crossfadeRow.hidden = !stillOn;
    }
    const crossfadeToggle = view.querySelector("#still-crossfade");
    if (crossfadeToggle instanceof HTMLInputElement) {
      crossfadeToggle.disabled = !stillOn;
      crossfadeToggle.checked = !!state.stillCrossfadeEnabled;
    }

    const strengthSlider = view.querySelector("#tint-strength");
    const strengthValue = view.querySelector("[data-tint-strength-value]");
    if (strengthSlider instanceof HTMLInputElement) {
      strengthSlider.disabled = stillOn || !state.colorTintEnabled;
      strengthSlider.value = String(state.colorTintStrength);
    }
    if (strengthValue) {
      strengthValue.textContent = String(state.colorTintStrength);
    }

    if (previewIndicatorId === id) {
      requestPreview(id, { resetTick: true });
    }
  }

  function bindEnableToggles() {
    view.querySelectorAll("[data-enable-toggle]").forEach((input) => {
      input.addEventListener("change", () => {
        if (!(input instanceof HTMLInputElement)) return;
        const id = input.dataset.enableToggle;
        if (!id || input.disabled) return;
        postHost({
          type: "setIndicatorEnabled",
          id,
          enabled: input.checked,
        });
      });
    });
  }

  function buildIndicatorCardsHtml() {
    return INDICATORS.map((item) => {
      const state = indicatorState[item.id];
      const available = !state || state.available;
      const checked = available && state ? state.enabled : false;
      const disabledAttr = available ? "" : " disabled";
      const checkedAttr = checked ? " checked" : "";
      const hintHidden = available ? " hidden" : "";
      const preview = runnerPreviewLabel(state);
      return `
      <article class="card" data-indicator-id="${item.id}">
        <h3>${item.label}</h3>
        <p>${item.blurb}</p>
        <p class="card-runner" data-card-runner>${escapeHtml(preview.name)}（${preview.kind}）</p>
        <div class="card-actions">
          <label class="toggle">
            <input type="checkbox" data-enable-toggle="${item.id}"${checkedAttr}${disabledAttr} />
            トレイに表示
          </label>
          <a class="btn secondary" href="#indicator/${item.id}">詳しく設定</a>
        </div>
        <p class="hint unavailable-hint"${hintHidden}>このPCでは使えません</p>
      </article>`;
    }).join("");
  }

  function renderHome() {
    view.innerHTML = `
      <h1 class="page-title">ホーム</h1>
      <p class="page-subtitle">トレイに表示するインジケーターを切り替えます。</p>
      <div class="card-grid">${buildIndicatorCardsHtml()}</div>
    `;

    bindEnableToggles();
    postHost({ type: "getIndicators" });
  }

  function renderIndicatorsParent() {
    view.innerHTML = `
      <h1 class="page-title">個別設定</h1>
      <p class="page-subtitle">インジケーターごとの表示と素材を設定します。</p>
      <div class="card-grid">${buildIndicatorCardsHtml()}</div>
    `;

    bindEnableToggles();
    postHost({ type: "getIndicators" });
  }

  function renderAssetsParent() {
    view.innerHTML = `
      <h1 class="page-title">素材</h1>
      <p class="page-subtitle">トレイ表示に使う素材ライブラリです。</p>
      <div class="card-grid">
        <article class="card">
          <h3>ランナー用</h3>
          <p>アニメーション用フレームセットのライブラリ。</p>
          <div class="card-actions">
            <a class="btn secondary" href="#assets/runners">開く</a>
          </div>
        </article>
        <article class="card">
          <h3>静止画モード用</h3>
          <p>静止画モード用の素材ライブラリ。</p>
          <div class="card-actions">
            <a class="btn secondary" href="#assets/stills">開く</a>
          </div>
        </article>
      </div>
    `;
  }

  function renderIndicator(id) {
    const item = INDICATORS.find((x) => x.id === id) || {
      id,
      label: id,
      blurb: "",
    };
    const state = indicatorState[id];
    const available = !state || state.available;
    const checked = available && state ? state.enabled : false;
    const disabledAttr = available ? "" : " disabled";
    const checkedAttr = checked ? " checked" : "";
    const hintHidden = available ? " hidden" : "";
    const preview = runnerPreviewLabel(state);

    view.innerHTML = `
      <h1 class="page-title">${item.label}</h1>
      <p class="page-subtitle">${item.blurb || "個別設定"}</p>
      <div class="detail-layout" data-indicator-detail="${id}">
        <div>
          <section class="section">
            <h2>有効化</h2>
            <label class="toggle">
              <input type="checkbox" data-enable-toggle="${id}"${checkedAttr}${disabledAttr} />
              トレイに表示
            </label>
            <p class="hint unavailable-hint"${hintHidden}>このPCでは使えません</p>
          </section>

          <section class="section">
            <h2>表示モード</h2>
            <div class="mode-row">
              <button class="mode-chip${state && !state.stillModeEnabled && state.runnerSpeedEnabled !== false ? "" : " mode-chip-muted"}" type="button" data-mode-chip="runner" aria-pressed="${state && !state.stillModeEnabled && state.runnerSpeedEnabled !== false ? "true" : "false"}"${state && state.stillModeEnabled ? " disabled" : ""}>ランナー</button>
              <button class="mode-chip${state && !state.stillModeEnabled && state.colorTintEnabled ? "" : " mode-chip-muted"}" type="button" data-mode-chip="tint" aria-pressed="${state && !state.stillModeEnabled && state.colorTintEnabled ? "true" : "false"}"${state && state.stillModeEnabled ? " disabled" : ""}>色の変化</button>
              <button class="mode-chip${state && state.stillModeEnabled ? "" : " mode-chip-muted"}" type="button" data-mode-chip="still" aria-pressed="${state && state.stillModeEnabled ? "true" : "false"}">静止画モード</button>
            </div>
            <p class="hint">
              ランナーと色の変化は組み合わせて使えます。静止画モードは排他で、負荷帯ごとにフレームを切り替えます。
            </p>
            <div class="field-row tint-strength-row">
              <label for="tint-strength">濃さ <span data-tint-strength-value>${state ? clampStrength(state.colorTintStrength) : 100}</span></label>
              <input type="range" id="tint-strength" min="0" max="100" value="${state ? clampStrength(state.colorTintStrength) : 100}"${state && state.colorTintEnabled && !(state && state.stillModeEnabled) ? "" : " disabled"} />
            </div>
          </section>

          <section class="section">
            <h2>素材・速度</h2>
            <div class="field-row">
              <label for="runner-pick">ランナー用素材</label>
              <select id="runner-pick"${state && state.stillModeEnabled ? " disabled" : ""}>
                ${buildRunnerOptionsHtml(state)}
              </select>
            </div>
            <div class="field-row">
              <label for="still-pick">静止画モード用素材</label>
              <select id="still-pick"${state && state.stillModeEnabled ? "" : " disabled"}>
                ${buildStillOptionsHtml(state)}
              </select>
            </div>
            <div class="field-row" data-still-crossfade-row${state && state.stillModeEnabled ? "" : " hidden"}>
              <label class="toggle" for="still-crossfade">
                <input type="checkbox" id="still-crossfade"${state && state.stillCrossfadeEnabled ? " checked" : ""}${state && state.stillModeEnabled ? "" : " disabled"} />
                なめらか切替
              </label>
            </div>
            <div class="field-row">
              <label for="speed-link">速度の連動</label>
              <select id="speed-link" disabled>
                <option>このインジケーターの値</option>
              </select>
            </div>
          </section>
        </div>

        <aside class="sticky-preview" aria-label="プレビュー">
          <div class="preview-stage">
            <img class="preview-image" data-preview-image alt="" hidden />
            <div class="preview-placeholder" data-preview-placeholder aria-hidden="true">素材なし</div>
          </div>
          <strong data-preview-name>${escapeHtml(preview.name)}</strong>
          <p class="hint preview-kind" data-preview-kind>${preview.kind}</p>
          <p class="hint preview-frame-label" data-preview-frame-label></p>
          <div class="field-row preview-load-row">
            <label for="preview-load">プレビュー負荷 <span data-preview-load-value>${previewLoad}</span></label>
            <input type="range" id="preview-load" min="0" max="100" value="${previewLoad}" />
          </div>
        </aside>
      </div>
    `;

    const enableToggle = view.querySelector(`[data-enable-toggle="${id}"]`);
    if (enableToggle instanceof HTMLInputElement) {
      enableToggle.addEventListener("change", () => {
        if (enableToggle.disabled) return;
        postHost({
          type: "setIndicatorEnabled",
          id,
          enabled: enableToggle.checked,
        });
      });
    }

    const runnerChip = view.querySelector('[data-mode-chip="runner"]');
    if (runnerChip) {
      runnerChip.addEventListener("click", () => {
        if (runnerChip.disabled) return;
        const pressed = runnerChip.getAttribute("aria-pressed") === "true";
        postHost({
          type: "setRunnerSpeedEnabled",
          id,
          enabled: !pressed,
        });
      });
    }

    const tintChip = view.querySelector('[data-mode-chip="tint"]');
    if (tintChip) {
      tintChip.addEventListener("click", () => {
        if (tintChip.disabled) return;
        const pressed = tintChip.getAttribute("aria-pressed") === "true";
        postHost({
          type: "setColorTintEnabled",
          id,
          enabled: !pressed,
        });
      });
    }

    const stillChip = view.querySelector('[data-mode-chip="still"]');
    if (stillChip) {
      stillChip.addEventListener("click", () => {
        const pressed = stillChip.getAttribute("aria-pressed") === "true";
        postHost({
          type: "setStillModeEnabled",
          id,
          enabled: !pressed,
        });
      });
    }

    const strengthSlider = view.querySelector("#tint-strength");
    const strengthValue = view.querySelector("[data-tint-strength-value]");
    if (strengthSlider instanceof HTMLInputElement) {
      strengthSlider.addEventListener("input", () => {
        if (strengthValue) strengthValue.textContent = strengthSlider.value;
      });
      strengthSlider.addEventListener("change", () => {
        if (strengthSlider.disabled) return;
        postHost({
          type: "setColorTintStrength",
          id,
          strength: clampStrength(strengthSlider.value),
        });
      });
    }

    const runnerPick = view.querySelector("#runner-pick");
    if (runnerPick instanceof HTMLSelectElement) {
      runnerPick.addEventListener("change", () => {
        const value = runnerPick.value || "";
        if (value.startsWith("custom:")) {
          postHost({
            type: "setCustomRunner",
            id,
            name: value.slice("custom:".length),
          });
          return;
        }
        if (value.startsWith("builtin:")) {
          postHost({
            type: "setRunner",
            id,
            runner: value.slice("builtin:".length),
          });
        }
      });
    }

    const stillPick = view.querySelector("#still-pick");
    if (stillPick instanceof HTMLSelectElement) {
      stillPick.addEventListener("change", () => {
        const value = stillPick.value || "";
        if (!value) return;
        postHost({
          type: "setStillSet",
          id,
          name: value,
        });
      });
    }

    const crossfadeToggle = view.querySelector("#still-crossfade");
    if (crossfadeToggle instanceof HTMLInputElement) {
      crossfadeToggle.addEventListener("change", () => {
        if (crossfadeToggle.disabled) return;
        postHost({
          type: "setStillCrossfadeEnabled",
          id,
          enabled: crossfadeToggle.checked,
        });
      });
    }

    const previewLoadSlider = view.querySelector("#preview-load");
    const previewLoadValue = view.querySelector("[data-preview-load-value]");
    if (previewLoadSlider instanceof HTMLInputElement) {
      previewLoadSlider.addEventListener("input", () => {
        previewLoad = Math.max(0, Math.min(100, Number(previewLoadSlider.value) || 0));
        if (previewLoadValue) previewLoadValue.textContent = String(previewLoad);
        requestPreview(id, { resetTick: true });
      });
    }

    previewIndicatorId = id;
    stopPreviewLoop();
    requestPreview(id, { resetTick: true });
    postHost({ type: "getIndicators" });
  }

  function renderAssets(kind, requestHost) {
    const shouldRequestHost = requestHost !== false;
    const isRunners = kind === "runners";
    const title = isRunners ? "ランナー用" : "静止画モード用";

    if (!isRunners) {
      const stillRows =
        stillSetsCatalog.length === 0
          ? `
          <div class="placeholder-item">
            <span>まだありません</span>
            <span>—</span>
          </div>`
          : stillSetsCatalog
              .map((set) => {
                const name = escapeHtml(set.name);
                const frames =
                  typeof set.frameCount === "number"
                    ? `${set.frameCount} 枚`
                    : "セット";
                return `
          <div class="asset-row" data-still-set="${name}">
            <div class="asset-row-main">
              <strong>${name}</strong>
              <span class="asset-meta">${frames}</span>
            </div>
            <div class="asset-row-actions">
              <button class="btn secondary" type="button" data-edit-still="${name}">編集</button>
              <button class="btn danger" type="button" data-delete-still="${name}">削除</button>
            </div>
          </div>`;
              })
              .join("");

      view.innerHTML = `
        <h1 class="page-title">${title}</h1>
        <p class="page-subtitle">静止画モード用の素材ライブラリ（2〜16 枚の透過 PNG）。</p>
        <section class="section">
          <h2>登録済み</h2>
          <div class="asset-list">
            ${stillRows}
          </div>
          <div class="empty-action">
            <button class="btn" type="button" data-create-still>新規作成</button>
            <p class="hint">
              低負荷→高負荷の順でフレームを並べます。負荷帯でハードカット切り替えします。
            </p>
          </div>
        </section>
      `;

      const createBtn = view.querySelector("[data-create-still]");
      if (createBtn) {
        createBtn.addEventListener("click", () => {
          postHost({ type: "openStillSetEditor" });
        });
      }
      view.querySelectorAll("[data-edit-still]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const name = btn.getAttribute("data-edit-still");
          if (!name) return;
          postHost({ type: "openStillSetEditor", name });
        });
      });
      view.querySelectorAll("[data-delete-still]").forEach((btn) => {
        btn.addEventListener("click", () => {
          const name = btn.getAttribute("data-delete-still");
          if (!name) return;
          if (!confirm(`「${name}」を削除しますか？`)) return;
          postHost({ type: "deleteStillSet", name });
        });
      });

      if (shouldRequestHost) postHost({ type: "getIndicators" });
      return;
    }

    const builtinRows = runnersCatalog.builtin
      .map((runner) => {
        const label = escapeHtml(runner.label || runner.id);
        return `
          <div class="asset-row">
            <div class="asset-row-main">
              <strong>${label}</strong>
              <span class="asset-meta">組み込み</span>
            </div>
            <div class="asset-row-actions">
              <span class="hint hint-inline">削除不可</span>
            </div>
          </div>`;
      })
      .join("");

    const customRows =
      runnersCatalog.custom.length === 0
        ? `
          <div class="placeholder-item">
            <span>カスタム素材はまだありません</span>
            <span>—</span>
          </div>`
        : runnersCatalog.custom
            .map((runner) => {
              const name = escapeHtml(runner.name);
              const frames =
                typeof runner.frameCount === "number"
                  ? `${runner.frameCount} フレーム`
                  : "カスタム";
              return `
          <div class="asset-row" data-custom-runner="${name}">
            <div class="asset-row-main">
              <strong>${name}</strong>
              <span class="asset-meta">${frames}</span>
            </div>
            <div class="asset-row-actions">
              <button class="btn secondary" type="button" data-edit-runner="${name}">編集</button>
              <button class="btn danger" type="button" data-delete-runner="${name}">削除</button>
            </div>
          </div>`;
            })
            .join("");

    view.innerHTML = `
      <h1 class="page-title">${title}</h1>
      <p class="page-subtitle">アニメーション用フレームセットのライブラリ。</p>
      <section class="section">
        <h2>登録済み</h2>
        <div class="asset-list">
          ${builtinRows}
          ${customRows}
        </div>
        <div class="empty-action">
          <button class="btn" type="button" data-create-runner>新規作成</button>
          <p class="hint">既存のカスタムランナー編集ウィンドウでフレームを追加・保存します。</p>
        </div>
      </section>
    `;

    const createBtn = view.querySelector("[data-create-runner]");
    if (createBtn) {
      createBtn.addEventListener("click", () => {
        postHost({ type: "openCustomRunnerEditor" });
      });
    }

    view.querySelectorAll("[data-edit-runner]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const name = btn.getAttribute("data-edit-runner");
        if (!name) return;
        postHost({ type: "openCustomRunnerEditor", name });
      });
    });

    view.querySelectorAll("[data-delete-runner]").forEach((btn) => {
      btn.addEventListener("click", () => {
        const name = btn.getAttribute("data-delete-runner");
        if (!name) return;
        if (!confirm(`「${name}」を削除しますか？`)) return;
        postHost({ type: "deleteCustomRunner", name });
      });
    });

    if (shouldRequestHost) postHost({ type: "getIndicators" });
  }

  function renderNotFound(route) {
    view.innerHTML = `
      <h1 class="page-title">ページが見つかりません</h1>
      <p class="page-subtitle">ルート: ${route}</p>
      <a class="btn" href="#home">ホームへ戻る</a>
    `;
  }

  function render() {
    const route = currentRoute();
    setActiveNav(route);

    if (!route.startsWith("indicator/")) {
      stopPreviewLoop();
      previewIndicatorId = null;
    }

    if (route === "home") {
      renderHome();
      return;
    }

    if (route === "indicators") {
      renderIndicatorsParent();
      return;
    }

    if (route === "assets") {
      renderAssetsParent();
      return;
    }

    const indicatorMatch = /^indicator\/([a-z]+)$/.exec(route);
    if (indicatorMatch) {
      renderIndicator(indicatorMatch[1]);
      return;
    }

    const assetsMatch = /^assets\/(runners|stills)$/.exec(route);
    if (assetsMatch) {
      renderAssets(assetsMatch[1]);
      return;
    }

    renderNotFound(route);
  }

  function updateToggleChrome(collapsed) {
    const label = collapsed ? "メニューを表示" : "メニューを隠す";
    toggle.title = label;
    toggle.setAttribute("aria-label", label);
    toggle.setAttribute("aria-expanded", String(!collapsed));
  }

  toggle.addEventListener("click", () => {
    const collapsed = app.classList.toggle("sidebar-collapsed");
    updateToggleChrome(collapsed);
  });

  document.querySelectorAll(".tree-group").forEach((group) => {
    group.addEventListener("click", () => {
      const key = group.dataset.group;
      if (!key) return;
      const children = document.querySelector(`[data-children="${key}"]`);
      const route = currentRoute();

      if (route === key) {
        const expanded = group.getAttribute("aria-expanded") !== "false";
        group.setAttribute("aria-expanded", String(!expanded));
        children?.classList.toggle("collapsed", expanded);
        return;
      }

      group.setAttribute("aria-expanded", "true");
      children?.classList.remove("collapsed");
      location.hash = key;
    });
  });

  if (isHostAvailable()) {
    chrome.webview.addEventListener("message", (event) => {
      const data = event.data;
      if (!data) return;
      if (data.type === "preview") {
        applyPreviewMessage(data);
        return;
      }
      if (data.type !== "indicators") return;
      applyIndicatorState(data.items || [], data.runners, data.stillSets || []);
      const route = currentRoute();
      const assetsMatch = /^assets\/(runners|stills)$/.exec(route);
      if (assetsMatch) {
        renderAssets(assetsMatch[1], false);
      }
    });
  }

  window.addEventListener("hashchange", render);
  updateToggleChrome(false);
  if (!location.hash) {
    location.hash = "home";
  } else {
    render();
  }
})();
