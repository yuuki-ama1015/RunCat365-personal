(() => {
  const INDICATORS = [
    { id: "cpu", label: "CPU", blurb: "プロセッサ使用率に連動します。" },
    { id: "gpu", label: "GPU", blurb: "GPU 使用率に連動します。" },
    { id: "memory", label: "Memory", blurb: "メモリ使用率に連動します。" },
    { id: "temperature", label: "Temperature", blurb: "温度に連動します。" },
  ];

  const view = document.getElementById("view");
  const sidebar = document.getElementById("sidebar");
  const app = document.querySelector(".app");
  const toggle = document.getElementById("sidebar-toggle");

  /** @type {Record<string, { id: string, enabled: boolean, available: boolean }>} */
  let indicatorState = {};

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
  }

  function applyIndicatorState(items) {
    indicatorState = {};
    (items || []).forEach((item) => {
      if (!item || !item.id) return;
      indicatorState[item.id] = {
        id: item.id,
        enabled: !!item.enabled,
        available: item.available !== false,
      };
    });

    document.querySelectorAll("[data-indicator-id]").forEach((card) => {
      const id = card.dataset.indicatorId;
      const state = indicatorState[id];
      if (!state) return;

      const checkbox = card.querySelector('input[type="checkbox"]');
      const hint = card.querySelector(".unavailable-hint");
      if (!(checkbox instanceof HTMLInputElement)) return;

      const available = state.available;
      checkbox.disabled = !available;
      checkbox.checked = available && state.enabled;
      if (hint) {
        hint.hidden = available;
      }
    });
  }

  function renderHome() {
    const cards = INDICATORS.map((item) => {
      const state = indicatorState[item.id];
      const available = !state || state.available;
      const checked = available && state ? state.enabled : false;
      const disabledAttr = available ? "" : " disabled";
      const checkedAttr = checked ? " checked" : "";
      const hintHidden = available ? " hidden" : "";
      return `
      <article class="card" data-indicator-id="${item.id}">
        <h3>${item.label}</h3>
        <p>${item.blurb}</p>
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

    view.innerHTML = `
      <h1 class="page-title">ホーム</h1>
      <p class="page-subtitle">トレイに表示するインジケーターを切り替えます。</p>
      <div class="card-grid">${cards}</div>
    `;

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

    postHost({ type: "getIndicators" });
  }

  function renderIndicator(id) {
    const item = INDICATORS.find((x) => x.id === id) || {
      id,
      label: id,
      blurb: "",
    };

    view.innerHTML = `
      <h1 class="page-title">${item.label}</h1>
      <p class="page-subtitle">個別設定（プレースホルダー）。詳細連携は Phase 3 以降です。</p>
      <div class="detail-layout">
        <div>
          <section class="section">
            <h2>有効化</h2>
            <label class="toggle">
              <input type="checkbox" checked disabled />
              トレイに表示
            </label>
          </section>

          <section class="section">
            <h2>表示モード</h2>
            <div class="mode-row">
              <button class="mode-chip" type="button" aria-pressed="true">ランナー</button>
              <button class="mode-chip" type="button" aria-pressed="false">色の変化</button>
              <button class="mode-chip" type="button" aria-pressed="false">静止画モード</button>
            </div>
            <p class="hint">
              ランナー と 色の変化 は併用できます。静止画モードは単独専用です。<br />
              ランナー ON → ランナー用素材から選択 / 静止画モード ON → 静止画用素材から選択。
            </p>
          </section>

          <section class="section">
            <h2>素材・速度</h2>
            <div class="field-row">
              <label for="runner-pick">ランナー用素材</label>
              <select id="runner-pick" disabled>
                <option>Cat（組み込み）</option>
                <option>（カスタムランナー — 未接続）</option>
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
            <div class="preview-cat" title="静的モック"></div>
          </div>
          <strong>プレビュー</strong>
          <p class="hint" style="margin:6px 0 0">静的モック（プレースホルダー）</p>
        </aside>
      </div>
    `;

    view.querySelectorAll(".mode-chip").forEach((chip) => {
      chip.addEventListener("click", () => {
        const label = chip.textContent.trim();
        if (label === "静止画モード") {
          view.querySelectorAll(".mode-chip").forEach((c) => {
            c.setAttribute(
              "aria-pressed",
              c === chip ? "true" : "false"
            );
          });
          return;
        }
        const still = [...view.querySelectorAll(".mode-chip")].find(
          (c) => c.textContent.trim() === "静止画モード"
        );
        if (still) still.setAttribute("aria-pressed", "false");
        const next = chip.getAttribute("aria-pressed") !== "true";
        chip.setAttribute("aria-pressed", String(next));
      });
    });
  }

  function renderAssets(kind) {
    const isRunners = kind === "runners";
    const title = isRunners ? "ランナー用" : "静止画用";
    const empty = isRunners
      ? "アニメーション用フレームセットを追加します。"
      : "静止画モード用の画像を追加します。";

    view.innerHTML = `
      <h1 class="page-title">${title}</h1>
      <p class="page-subtitle">素材ライブラリ（プレースホルダー）。</p>
      <section class="section">
        <h2>登録済み</h2>
        <div class="placeholder-list">
          <div class="placeholder-item">
            <span>${isRunners ? "Cat（組み込み）" : "サンプル静止画（未接続）"}</span>
            <span>モック</span>
          </div>
          <div class="placeholder-item">
            <span>カスタム素材はまだありません</span>
            <span>—</span>
          </div>
        </div>
        <div class="empty-action">
          <button class="btn" type="button" disabled>新規作成</button>
          <p class="hint">${empty}</p>
        </div>
      </section>
    `;
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
      const children = document.querySelector(`[data-children="${key}"]`);
      const expanded = group.getAttribute("aria-expanded") !== "false";
      group.setAttribute("aria-expanded", String(!expanded));
      children?.classList.toggle("collapsed", expanded);
    });
  });

  if (isHostAvailable()) {
    chrome.webview.addEventListener("message", (event) => {
      const data = event.data;
      if (!data || data.type !== "indicators") return;
      applyIndicatorState(data.items || []);
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
