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

  /** @type {Record<string, { id: string, enabled: boolean, available: boolean, runner: string, customRunnerName: string|null }>} */
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

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function applyIndicatorState(items, runners) {
    if (runners && Array.isArray(runners.builtin)) {
      runnersCatalog = {
        builtin: runners.builtin,
        custom: Array.isArray(runners.custom) ? runners.custom : [],
      };
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
          <h3>静止画用</h3>
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
              <button class="mode-chip" type="button" aria-pressed="true" disabled>ランナー</button>
              <button class="mode-chip mode-chip-muted" type="button" aria-pressed="false" disabled>色の変化</button>
              <button class="mode-chip mode-chip-muted" type="button" aria-pressed="false" disabled>静止画モード</button>
            </div>
            <p class="hint">
              いまはランナー表示のみ接続されています。<br />
              色の変化 / 静止画モード はまだ未接続のため保存されません。
            </p>
          </section>

          <section class="section">
            <h2>素材・速度</h2>
            <div class="field-row">
              <label for="runner-pick">ランナー用素材</label>
              <select id="runner-pick">
                ${buildRunnerOptionsHtml(state)}
              </select>
            </div>
            <div class="field-row">
              <label for="still-pick">静止画用素材</label>
              <select id="still-pick" disabled>
                <option>（未設定）</option>
              </select>
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
            <div class="preview-cat" aria-hidden="true"></div>
          </div>
          <strong data-preview-name>${escapeHtml(preview.name)}</strong>
          <p class="hint preview-kind" data-preview-kind>${preview.kind}</p>
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

    postHost({ type: "getIndicators" });
  }

  function renderAssets(kind, requestHost) {
    const shouldRequestHost = requestHost !== false;
    const isRunners = kind === "runners";
    const title = isRunners ? "ランナー用" : "静止画用";

    if (!isRunners) {
      view.innerHTML = `
        <h1 class="page-title">${title}</h1>
        <p class="page-subtitle">静止画モード用の素材ライブラリ。</p>
        <section class="section">
          <h2>登録済み</h2>
          <div class="placeholder-list">
            <div class="placeholder-item">
              <span>まだありません</span>
              <span>—</span>
            </div>
          </div>
          <div class="empty-action">
            <button class="btn" type="button" disabled>新規作成</button>
            <p class="hint">
              静止画モードはまだ接続されていないため、素材の追加・保存はできません。
            </p>
          </div>
        </section>
      `;
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
      if (!data || data.type !== "indicators") return;
      applyIndicatorState(data.items || [], data.runners);
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
