(() => {
  "use strict";

  // --- Seletores Utilitários ---
  const $ = (selector) => document.querySelector(selector);
  const $$ = (selector) => Array.from(document.querySelectorAll(selector));

  // --- Estado da Aplicação ---
  const state = {
    mode: "movie", // movie | season | episode
    scan: null,
    scanId: null,
    preview: null,
    mediaInfoReport: null,
    settings: {},
    systemStatus: {},
    currentBrowsePath: null,
  };

  // --- Elementos do DOM ---
  const dom = {
    appVersion: $("#appVersion"),
    hostStatusText: $("#hostStatusText"),
    themeToggle: $("#themeToggle"),
    folderInput: $("#folderInput"),
    btnChooseFolder: $("#btnChooseFolder"),
    btnScanFolder: $("#btnScanFolder"),
    btnBrowseServer: $("#btnBrowseServer"),
    recentChips: $("#recentChips"),

    cardMediaInfoTrigger: $("#cardMediaInfoTrigger"),
    badgeMediaInfo: $("#badgeMediaInfo"),
    descMediaInfo: $("#descMediaInfo"),
    cardTmdbTrigger: $("#cardTmdbTrigger"),
    badgeTmdb: $("#badgeTmdb"),
    descTmdb: $("#descTmdb"),

    modeTabs: $$(".mode-tab"),
    wrapYear: $("#wrapYear"),
    wrapSeason: $("#wrapSeason"),
    wrapEpisode: $("#wrapEpisode"),
    wrapCustomStreaming: $("#wrapCustomStreaming"),
    wrapSecondGroup: $("#wrapSecondGroup"),
    yearReqNotice: $("#yearReqNotice"),

    checkTwoGroups: $("#checkTwoGroups"),
    checkDots: $("#checkDots"),
    btnUpdatePreview: $("#btnUpdatePreview"),

    anatomyPills: $("#anatomyPills"),
    newRootInput: $("#newRootInput"),
    postTitleText: $("#postTitleText"),
    btnCopyFolderName: $("#btnCopyFolderName"),
    btnCopyPostTitle: $("#btnCopyPostTitle"),
    issuesPanel: $("#issuesPanel"),

    selectAllCheckbox: $("#selectAllCheckbox"),
    filesTableBody: $("#filesTableBody"),
    tableStats: $("#tableStats"),
    btnApply: $("#btnApply"),
    btnUndo: $("#btnUndo"),
    btnOpenFolderNative: $("#btnOpenFolderNative"),

    // Modais
    modalGuide: $("#modalGuide"),
    btnOpenGuide: $("#btnOpenGuide"),
    btnCloseGuide: $("#btnCloseGuide"),
    guideNavLinks: $$(".guide-nav a"),

    modalSettings: $("#modalSettings"),
    btnOpenSettings: $("#btnOpenSettings"),
    btnCloseSettings: $("#btnCloseSettings"),
    cfgTmdbKey: $("#cfgTmdbKey"),
    cfgTitlePreference: $("#cfgTitlePreference"),
    cfgDefaultGroup: $("#cfgDefaultGroup"),
    cfgMediaInfoDesc: $("#cfgMediaInfoDesc"),
    btnInstallMediaInfo: $("#btnInstallMediaInfo"),
    btnSaveSettings: $("#btnSaveSettings"),

    modalTmdb: $("#modalTmdb"),
    btnCloseTmdb: $("#btnCloseTmdb"),
    tmdbQueryInput: $("#tmdbQueryInput"),
    tmdbTypeSelect: $("#tmdbTypeSelect"),
    btnExecuteTmdbSearch: $("#btnExecuteTmdbSearch"),
    tmdbNotice: $("#tmdbNotice"),
    tmdbResultsGrid: $("#tmdbResultsGrid"),

    modalBrowser: $("#modalBrowser"),
    btnCloseBrowser: $("#btnCloseBrowser"),
    browserCrumbs: $("#browserCrumbs"),
    browserList: $("#browserList"),
    browserCurrentPath: $("#browserCurrentPath"),
    btnSelectCurrentBrowserFolder: $("#btnSelectCurrentBrowserFolder"),

    modalMediaInfo: $("#modalMediaInfo"),
    btnCloseMediaInfo: $("#btnCloseMediaInfo"),
    mediaInfoBody: $("#mediaInfoBody"),
    btnCopyMediaInfoJson: $("#btnCopyMediaInfoJson"),
    btnApplyMediaInfoSuggestions: $("#btnApplyMediaInfoSuggestions"),

    busyOverlay: $("#busyOverlay"),
    busyText: $("#busyText"),
    toastContainer: $("#toastContainer"),
  };

  // --- API Client ---
  async function api(endpoint, options = {}) {
    const isPost = options.method === "POST" || options.body !== undefined;
    const config = {
      method: isPost ? "POST" : "GET",
      headers: { "Content-Type": "application/json" },
    };

    if (options.body !== undefined) {
      config.body = JSON.stringify(options.body);
    }

    try {
      const response = await fetch(endpoint, config);
      const text = await response.text();
      let data;
      try {
        data = text ? JSON.parse(text) : {};
      } catch {
        data = { error: text || "Resposta inválida do servidor." };
      }

      if (!response.ok) {
        throw new Error(data.error || data.detail || `Erro ${response.status}`);
      }

      return data;
    } catch (err) {
      showToast(err.message, "error");
      throw err;
    }
  }

  // --- Toast Notifications ---
  function showToast(message, type = "success") {
    const toast = document.createElement("div");
    toast.className = `toast ${type}`;

    let iconSvg = "";
    if (type === "success") {
      iconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="color: var(--success);"><polyline points="20 6 9 17 4 12"/></svg>`;
    } else if (type === "error") {
      iconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="color: var(--error);"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`;
    } else if (type === "warning") {
      iconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="color: var(--warning);"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>`;
    } else {
      iconSvg = `<svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="color: var(--info);"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>`;
    }

    toast.innerHTML = `
      ${iconSvg}
      <div style="flex: 1; font-size: 0.825rem; font-weight: 500;">${escapeHtml(message)}</div>
      <button style="background: none; border: none; color: var(--text-muted); cursor: pointer; font-size: 0.9rem;">✕</button>
    `;

    dom.toastContainer.appendChild(toast);

    const closeBtn = toast.querySelector("button");
    closeBtn.onclick = () => {
      toast.classList.add("hiding");
      setTimeout(() => toast.remove(), 200);
    };

    setTimeout(() => {
      if (document.body.contains(toast)) {
        toast.classList.add("hiding");
        setTimeout(() => toast.remove(), 200);
      }
    }, 4500);
  }

  function setBusy(show, text = "Processando…") {
    dom.busyText.textContent = text;
    dom.busyOverlay.classList.toggle("hidden", !show);
  }

  function escapeHtml(str = "") {
    return String(str).replace(/[&<>"']/g, (c) => ({
      "&": "&amp;",
      "<": "&lt;",
      ">": "&gt;",
      '"': "&quot;",
      "'": "&#39;",
    })[c]);
  }

  // --- Gerenciamento de Tema ---
  function initTheme() {
    const saved = localStorage.getItem("bacate-theme") || "dark";
    document.documentElement.setAttribute("data-theme", saved);

    dom.themeToggle.onclick = () => {
      const current = document.documentElement.getAttribute("data-theme");
      const themes = ["dark", "oled", "light"];
      const nextIndex = (themes.indexOf(current) + 1) % themes.length;
      const next = themes[nextIndex];
      document.documentElement.setAttribute("data-theme", next);
      localStorage.setItem("bacate-theme", next);
      showToast(`Tema alterado para: ${next.toUpperCase()}`, "info");
    };
  }

  // --- Formulário & Dados ---
  function getFormFields() {
    const fields = {};
    $$("[data-field]").forEach((el) => {
      fields[el.dataset.field] = el.type === "checkbox" ? el.checked : el.value.trim();
    });
    return fields;
  }

  function setFormFields(values) {
    if (!values) return;
    Object.entries(values).forEach(([k, v]) => {
      const el = $(`[data-field="${k}"]`);
      if (el) {
        if (el.type === "checkbox") el.checked = Boolean(v);
        else el.value = v ?? "";
      }
    });

    // Ajusta visibilidade de campos condicionais
    if (values.streaming) {
      dom.wrapCustomStreaming.classList.toggle("hidden", values.streaming !== "custom");
    }
    triggerLivePreview();
  }

  // --- Atualização de Prévia ---
  async function refreshPreview() {
    const fields = getFormFields();
    const payload = {
      scanId: state.scanId,
      fields: fields,
      mode: state.mode,
      dots: dom.checkDots.checked,
      twoGroups: dom.checkTwoGroups.checked,
    };

    try {
      const result = await api("/api/preview", { body: payload });
      state.preview = result;
      renderPreview(result);
    } catch {
      // Erro reportado pelo wrapper da API
    }
  }

  let debounceTimer;
  function triggerLivePreview() {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(refreshPreview, 120);
  }

  function renderPreview(result) {
    if (!result) return;

    // 1. Anatomia do Nome
    const tokens = result.folder.tokens || [];
    if (tokens.length > 0) {
      dom.anatomyPills.innerHTML = tokens
        .map((tok) => {
          const kindClass = `tok-${tok.kind}`;
          return `<span class="anatomy-token ${kindClass}" title="${tok.field}">${escapeHtml(tok.text)}</span>`;
        })
        .join("");
    } else {
      dom.anatomyPills.innerHTML = `<span style="color: var(--text-muted); font-size: 0.85rem;">Nome base vazio. Preencha o nome da obra.</span>`;
    }

    // 2. Pasta Principal & Título do Post
    dom.newRootInput.value = result.folder.name;
    dom.postTitleText.textContent = result.postTitle || "-";

    // 3. Avisos & Validações
    dom.issuesPanel.innerHTML = (result.issues || [])
      .map((issue) => {
        return `<div class="issue-item ${issue.level}">
          <span>●</span>
          <span>${escapeHtml(issue.message)}</span>
        </div>`;
      })
      .join("");

    // 4. Tabela de Arquivos
    const previews = result.files || [];
    if (previews.length === 0) {
      dom.filesTableBody.innerHTML = `<tr><td colspan="5" style="text-align: center; padding: 40px; color: var(--text-muted);">Nenhum arquivo na lista.</td></tr>`;
      dom.tableStats.innerHTML = `<span><b>0</b> arquivos</span>`;
      return;
    }

    const rowsHtml = previews
      .map((item, idx) => {
        const isPending = item.status === "pending";
        const isConflict = item.status === "conflict";
        const isChecked = !isPending && !isConflict;

        const badgeClass = item.status;
        const badgeLabel =
          item.status === "ok"
            ? item.id || "Pronto"
            : item.status === "pending"
            ? "Revisar"
            : item.status === "conflict"
            ? "Conflito"
            : "Igual";

        return `
        <tr data-index="${idx}" data-path="${escapeHtml(item.relPath)}">
          <td><input type="checkbox" class="file-checkbox" ${isChecked ? "checked" : ""}></td>
          <td class="col-old-name" title="${escapeHtml(item.relPath)}">${escapeHtml(item.relPath)}</td>
          <td class="col-new-name">
            <input type="text" class="new-name-input" value="${escapeHtml(item.NewName || item.newName)}">
          </td>
          <td><span class="badge ${badgeClass}">${escapeHtml(badgeLabel)}</span></td>
          <td><span class="badge ${badgeClass}">${escapeHtml(item.status.toUpperCase())}</span></td>
        </tr>`;
      })
      .join("");

    dom.filesTableBody.innerHTML = rowsHtml;
    updateTableCounters();
  }

  function updateTableCounters() {
    const total = $$(".file-checkbox").length;
    const selected = $$(".file-checkbox:checked").length;
    dom.tableStats.innerHTML = `<span><b>${total}</b> arquivos • <b>${selected}</b> selecionados</span>`;
  }

  // --- Varredura de Pasta ---
  async function scanFolder(path) {
    if (!path) return showToast("Informe o caminho da pasta primeiro.", "warning");

    setBusy(true, "Analisando diretório e arquivos…");
    try {
      const scan = await api("/api/scan", { body: { path } });
      state.scan = scan;
      state.scanId = scan.scanId;
      dom.folderInput.value = scan.root;

      // Sugestão de modo
      if (scan.suggestedMode) {
        setMode(scan.suggestedMode);
      }
      if (scan.suggestedSeason) {
        $('[data-field="season"]').value = scan.suggestedSeason;
      }

      // Preenchimento de dados preliminares do release
      if (scan.parsed && !scan.parsed.isEmpty) {
        const p = scan.parsed;
        const updates = {};
        if (p.title) updates.title = p.title;
        if (p.year) updates.year = p.year;
        if (p.source) updates.source = p.source;
        if (p.streaming) updates.streaming = p.streaming;
        if (p.releaseType) updates.releaseType = p.releaseType;
        if (p.edition) updates.edition = p.edition;
        if (p.dualMulti) updates.dualMulti = p.dualMulti;
        if (p.group) updates.group = p.group;
        if (p.originalGroup) updates.originalGroup = p.originalGroup;
        if (p.twoGroups !== null) dom.checkTwoGroups.checked = p.twoGroups;
        if (p.repack) updates.repack = true;
        if (p.hybrid) updates.hybrid = true;
        setFormFields(updates);
      }

      // Atualiza MediaInfo card
      if (scan.largestVideo) {
        dom.descMediaInfo.textContent = `Pronto para analisar: ${scan.largestVideo.split(/[\\/]/).pop()}`;
      } else {
        dom.descMediaInfo.textContent = "Nenhum arquivo de vídeo encontrado na pasta.";
      }

      showToast(`${scan.count} arquivo(s) carregados com sucesso!`);
      await refreshPreview();
    } catch {
      // Erro já reportado
    } finally {
      setBusy(false);
    }
  }

  function setMode(mode) {
    state.mode = mode;
    dom.modeTabs.forEach((tab) => tab.classList.toggle("active", tab.dataset.mode === mode));

    dom.wrapSeason.classList.toggle("hidden", mode !== "season");
    dom.wrapEpisode.classList.toggle("hidden", mode !== "episode");
    dom.yearReqNotice.style.display = mode === "movie" ? "inline" : "none";

    triggerLivePreview();
  }

  // --- Assistente MediaInfo ---
  async function runMediaInfo(filePath) {
    if (!filePath && state.scan && state.scan.largestVideo) {
      filePath = `${state.scan.root}/${state.scan.largestVideo}`;
    }

    if (!filePath) {
      return showToast("Carregue uma pasta com vídeos primeiro.", "warning");
    }

    setBusy(true, "Executando análise profunda com MediaInfo…");
    try {
      const report = await api("/api/mediainfo/analyze", { body: { path: filePath } });
      state.mediaInfoReport = report;
      renderMediaInfoModal(report);
      dom.modalMediaInfo.showModal();
    } catch {
      // Erro reportado
    } finally {
      setBusy(false);
    }
  }

  function renderMediaInfoModal(report) {
    const s = report.suggestions;
    const v = report.video;
    const audioList = report.audio || [];

    dom.mediaInfoBody.innerHTML = `
      <div style="background: var(--bg-input); padding: 14px 18px; border-radius: var(--radius-md); border: 1px solid var(--border); margin-bottom: 20px;">
        <h4 style="font-size: 0.95rem; font-weight: 750; color: var(--primary);">Arquivo Analisado</h4>
        <p style="font-family: var(--font-mono); font-size: 0.8rem; word-break: break-all; margin-top: 4px;">${escapeHtml(report.file)}</p>
      </div>

      <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 20px;">
        <div style="background: var(--bg-card-subtle); padding: 14px; border-radius: var(--radius-sm); border: 1px solid var(--border);">
          <h5 style="font-size: 0.8rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted); margin-bottom: 8px;">Vídeo Detectado</h5>
          <p style="font-size: 0.85rem;"><b>Resolução:</b> ${v ? v.resolution : "N/D"} (${v ? v.width + "x" + v.height : ""})</p>
          <p style="font-size: 0.85rem;"><b>Codec / Família:</b> ${v ? v.format : "N/D"} ${v && v.encoderLibrary ? `(${v.encoderLibrary})` : ""}</p>
          <p style="font-size: 0.85rem;"><b>Range / HDR:</b> ${v && v.range ? v.range : "SDR"}</p>
          <p style="font-size: 0.85rem;"><b>Frame Rate:</b> ${v ? v.frameRate.toFixed(3) + " fps" : "N/D"} ${v && v.hfr ? "(HFR)" : ""}</p>
        </div>

        <div style="background: var(--bg-card-subtle); padding: 14px; border-radius: var(--radius-sm); border: 1px solid var(--border);">
          <h5 style="font-size: 0.8rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted); margin-bottom: 8px;">Áudio Principal & Sugestões</h5>
          <p style="font-size: 0.85rem;"><b>Codec de Áudio:</b> ${s.audioCodec || "N/D"}</p>
          <p style="font-size: 0.85rem;"><b>Canais:</b> ${s.channels || "N/D"}</p>
          <p style="font-size: 0.85rem;"><b>Dolby Atmos:</b> ${s.atmos ? "Sim" : "Não"}</p>
          <p style="font-size: 0.85rem;"><b>Classificação DUAL/MULTi:</b> ${s.dualMulti || "Sem tag"}</p>
        </div>
      </div>

      <h5 style="font-size: 0.8rem; font-weight: 700; text-transform: uppercase; color: var(--text-muted); margin-bottom: 8px;">Faixas de Áudio no Arquivo (${audioList.length})</h5>
      <div style="border: 1px solid var(--border); border-radius: var(--radius-sm); max-height: 180px; overflow-y: auto;">
        ${audioList
          .map((a, i) => {
            return `<div style="padding: 8px 12px; border-bottom: 1px solid var(--border); font-size: 0.8rem; display: flex; justify-content: space-between;">
              <span>#${i + 1} <b>${escapeHtml(a.codec || a.format)} ${escapeHtml(a.channels)}</b> (${escapeHtml(a.language || "Sem idioma")})</span>
              <span>${a.title ? escapeHtml(a.title) : ""} ${a.atmos ? '<span class="badge ok">Atmos</span>' : ""}</span>
            </div>`;
          })
          .join("")}
      </div>
    `;
  }

  function applyMediaInfoSuggestions() {
    if (!state.mediaInfoReport || !state.mediaInfoReport.suggestions) return;
    const s = state.mediaInfoReport.suggestions;

    const updates = {};
    if (s.resolution) updates.resolution = s.resolution;
    if (s.range) updates.range = s.range;
    if (s.hfr !== undefined) updates.hfr = s.hfr;
    if (s.audioCodec) updates.audioCodec = s.audioCodec;
    if (s.channels) updates.channels = s.channels;
    if (s.atmos !== undefined) updates.atmos = s.atmos;
    if (s.dualMulti) updates.dualMulti = s.dualMulti;

    // Sugere codec de vídeo se puder ser deduzido com precisão
    if (s.codecFamily === "avc") {
      const currentSrc = $('[data-field="source"]').value;
      updates.videoCodec = currentSrc && currentSrc.includes("WEB") ? "H.264" : "AVC";
    } else if (s.codecFamily === "hevc") {
      updates.videoCodec = "HEVC";
    }

    setFormFields(updates);
    dom.modalMediaInfo.close();
    showToast("Tags técnicas aplicadas com base no MediaInfo!");
  }

  // --- Assistente TMDB ---
  async function searchTmdb(query, type) {
    if (!query) return;

    dom.tmdbNotice.textContent = "Pesquisando no TMDB…";
    dom.tmdbResultsGrid.innerHTML = "";

    try {
      const res = await api(`/api/tmdb/search?query=${encodeURIComponent(query)}&type=${type}`);
      dom.tmdbNotice.textContent = res.results.length ? `${res.results.length} resultado(s) encontrado(s):` : "Nenhum resultado encontrado.";

      dom.tmdbResultsGrid.innerHTML = res.results
        .map((item) => {
          const poster = item.poster || "assets/icon-256.png";
          return `
          <div class="tmdb-card" data-id="${item.id}" data-type="${item.type}">
            <img class="tmdb-poster" src="${poster}" alt="${escapeHtml(item.title)}" loading="lazy">
            <div class="tmdb-card-info">
              <span class="tmdb-card-title">${escapeHtml(item.title)}</span>
              <span class="tmdb-card-meta">${item.year || "Ano desc."} • ${item.type === "tv" ? "Série" : "Filme"}</span>
            </div>
          </div>`;
        })
        .join("");

      $$(".tmdb-card").forEach((card) => {
        card.onclick = () => selectTmdbItem(card.dataset.type, card.dataset.id);
      });
    } catch {
      dom.tmdbNotice.textContent = "Falha ao consultar o TMDB. Verifique sua chave nas configurações.";
    }
  }

  async function selectTmdbItem(type, id) {
    setBusy(true, "Obtendo dados oficiais do TMDB…");
    try {
      const details = await api(`/api/tmdb/details?type=${type}&id=${id}`);
      const pref = state.settings.titlePreference || "en";

      let chosenTitle = details.titles.en;
      if (pref === "pt" && details.titles.pt) chosenTitle = details.titles.pt;
      else if (pref === "original" && details.titles.original) chosenTitle = details.titles.original;

      const updates = { title: chosenTitle };
      if (details.year) updates.year = details.year;

      if (type === "tv") {
        setMode("season");
        if (details.seasons && details.seasons.length > 0) {
          const s1 = details.seasons.find((s) => s.number === 1) || details.seasons[0];
          updates.season = `S${String(s1.number).padStart(2, "0")}`;
        }
      } else {
        setMode("movie");
      }

      setFormFields(updates);
      dom.modalTmdb.close();
      showToast(`Título definido: ${chosenTitle}`);
    } catch {
      // Erro reportado
    } finally {
      setBusy(false);
    }
  }

  // --- Navegador de Diretórios do Servidor (Web/Docker/OMV) ---
  async function browseServer(path) {
    setBusy(true, "Listando diretórios…");
    try {
      const listing = await api(`/api/browse?path=${encodeURIComponent(path || "")}`);
      state.currentBrowsePath = listing.path;
      dom.browserCurrentPath.textContent = listing.path;

      // Breadcrumbs
      dom.browserCrumbs.innerHTML = (listing.crumbs || [])
        .map((c) => `<span class="browser-crumb" data-path="${escapeHtml(c.path)}">${escapeHtml(c.name)} /</span>`)
        .join(" ");

      $$(".browser-crumb").forEach((crumb) => {
        crumb.onclick = () => browseServer(crumb.dataset.path);
      });

      // Lista de pastas
      const dirs = listing.dirs || [];
      if (dirs.length === 0) {
        dom.browserList.innerHTML = `<div style="padding: 24px; text-align: center; color: var(--text-muted);">Nenhuma subpasta aqui.</div>`;
      } else {
        dom.browserList.innerHTML = dirs
          .map((d) => {
            return `
            <div class="browser-item" data-path="${escapeHtml(d.path)}">
              <span style="display: flex; align-items: center; gap: 8px; font-weight: 600;">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="color: var(--primary);"><path d="M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13c0 1.1.9 2 2 2Z"/></svg>
                ${escapeHtml(d.name)}
              </span>
              <span style="font-size: 0.75rem; color: var(--text-muted);">Abrir →</span>
            </div>`;
          })
          .join("");

        $$(".browser-item").forEach((item) => {
          item.onclick = () => browseServer(item.dataset.path);
        });
      }

      if (!dom.modalBrowser.open) {
        dom.modalBrowser.showModal();
      }
    } catch {
      // Erro reportado
    } finally {
      setBusy(false);
    }
  }

  // --- Aplicação de Renomeação ---
  async function applyRename() {
    if (!state.scan) return showToast("Carregue uma pasta primeiro.", "warning");

    const form = getFormFields();
    if (!form.title) return showToast("Informe o Nome da Obra antes de aplicar.", "error");

    const changes = [];
    $$("#filesTableBody tr").forEach((row) => {
      const cb = row.querySelector(".file-checkbox");
      if (cb && cb.checked) {
        const src = row.dataset.path;
        const newNameInput = row.querySelector(".new-name-input");
        if (src && newNameInput) {
          changes.push({ source: src, newName: newNameInput.value.trim() });
        }
      }
    });

    const newRoot = dom.newRootInput.value.trim();
    if (changes.length === 0 && newRoot === state.scan.rootName) {
      return showToast("Nenhuma alteração selecionada para aplicar.", "warning");
    }

    if (!confirm(`Deseja aplicar o novo nome para ${changes.length} arquivo(s) e para a pasta principal?`)) {
      return;
    }

    setBusy(true, "Renomeando arquivos de forma transacional…");
    try {
      const res = await api("/api/apply", {
        body: {
          root: state.scan.root,
          newRootName: newRoot,
          changes: changes,
        },
      });

      showToast(`Sucesso! ${res.renamed} arquivo(s) renomeados.`);
      await scanFolder(res.root);
    } catch {
      // Erro reportado
    } finally {
      setBusy(false);
    }
  }

  async function undoRename() {
    setBusy(true, "Restaurando nomes da operação anterior…");
    try {
      const res = await api("/api/undo");
      showToast(`Desfeito com sucesso! ${res.restored} item(ns) restaurados.`);
      if (res.root) {
        await scanFolder(res.root);
      }
    } catch {
      // Erro reportado
    } finally {
      setBusy(false);
    }
  }

  // --- Configurações ---
  async function loadSettings() {
    try {
      const data = await api("/api/settings");
      state.settings = data;
      dom.cfgTmdbKey.value = data.tmdbKey || "";
      dom.cfgTitlePreference.value = data.titlePreference || "en";
      dom.cfgDefaultGroup.value = data.defaultGroup || "";
    } catch {
      // Erro silencioso
    }
  }

  async function saveSettings() {
    const payload = {
      tmdbKey: dom.cfgTmdbKey.value,
      titlePreference: dom.cfgTitlePreference.value,
      defaultGroup: dom.cfgDefaultGroup.value,
    };

    try {
      const res = await api("/api/settings", { body: payload });
      state.settings = res.settings;
      dom.modalSettings.close();
      showToast("Configurações salvas com sucesso!");
      initSystem();
    } catch {
      // Erro reportado
    }
  }

  // --- Inicialização do Sistema ---
  async function initSystem() {
    try {
      const status = await api("/api/status");
      state.systemStatus = status;

      dom.appVersion.textContent = `v${status.version} Pro`;
      dom.hostStatusText.textContent = `${status.mode === "desktop" ? "Desktop Nativo" : "Servidor Web"}${
        status.inContainer ? " (Docker/Podman)" : ""
      }`;

      // MediaInfo Status
      if (status.mediaInfo.available) {
        dom.badgeMediaInfo.textContent = `v${status.mediaInfo.version || "OK"}`;
        dom.badgeMediaInfo.className = "badge ok";
        dom.cfgMediaInfoDesc.textContent = `Disponível em: ${status.mediaInfo.path} (v${status.mediaInfo.version})`;
        dom.btnInstallMediaInfo.classList.add("hidden");
      } else {
        dom.badgeMediaInfo.textContent = "Indisponível";
        dom.badgeMediaInfo.className = "badge warning";
        dom.cfgMediaInfoDesc.textContent = status.mediaInfo.hint || "Não encontrado.";
        if (status.mediaInfo.canInstall) {
          dom.btnInstallMediaInfo.classList.remove("hidden");
        }
      }

      // TMDB Status
      if (status.tmdb.configured) {
        dom.badgeTmdb.textContent = "Ativo";
        dom.badgeTmdb.className = "badge ok";
      } else {
        dom.badgeTmdb.textContent = "Sem chave";
        dom.badgeTmdb.className = "badge warning";
      }

      // Pastas recentes
      if (status.recent && status.recent.length > 0) {
        dom.recentChips.innerHTML = status.recent
          .slice(0, 4)
          .map((r) => `<span class="chip" data-path="${escapeHtml(r)}" title="${escapeHtml(r)}">${escapeHtml(r.split(/[\\/]/).pop())}</span>`)
          .join("");

        $$(".chip").forEach((chip) => {
          chip.onclick = () => scanFolder(chip.dataset.path);
        });
      }

      // Startup folder se enviado pelo host
      if (status.startupFolder && !state.scan) {
        scanFolder(status.startupFolder);
      }
    } catch {
      dom.hostStatusText.textContent = "Sem conexão";
    }
  }

  // --- Bind de Eventos ---
  function bindEvents() {
    initTheme();

    // Pastas
    dom.btnChooseFolder.onclick = async () => {
      try {
        const res = await api("/api/select-folder");
        if (res && res.path) {
          dom.folderInput.value = res.path;
          scanFolder(res.path);
        }
      } catch {
        // Modo servidor abre o browser de pastas
        browseServer(dom.folderInput.value);
      }
    };

    dom.btnScanFolder.onclick = () => scanFolder(dom.folderInput.value.trim());

    dom.btnBrowseServer.onclick = () => browseServer(dom.folderInput.value.trim());

    dom.btnSelectCurrentBrowserFolder.onclick = () => {
      if (state.currentBrowsePath) {
        dom.folderInput.value = state.currentBrowsePath;
        dom.modalBrowser.close();
        scanFolder(state.currentBrowsePath);
      }
    };

    // Modos
    dom.modeTabs.forEach((tab) => {
      tab.onclick = () => setMode(tab.dataset.mode);
    });

    // Assistentes
    dom.cardMediaInfoTrigger.onclick = () => runMediaInfo();
    dom.cardTmdbTrigger.onclick = () => {
      const title = $('[data-field="title"]').value.trim();
      if (title) {
        dom.tmdbQueryInput.value = title;
        dom.tmdbTypeSelect.value = state.mode === "movie" ? "movie" : "tv";
        searchTmdb(title, dom.tmdbTypeSelect.value);
      }
      dom.modalTmdb.showModal();
    };

    dom.btnExecuteTmdbSearch.onclick = () => searchTmdb(dom.tmdbQueryInput.value.trim(), dom.tmdbTypeSelect.value);
    dom.tmdbQueryInput.onkeydown = (e) => {
      if (e.key === "Enter") searchTmdb(dom.tmdbQueryInput.value.trim(), dom.tmdbTypeSelect.value);
    };

    dom.btnApplyMediaInfoSuggestions.onclick = applyMediaInfoSuggestions;
    dom.btnCopyMediaInfoJson.onclick = () => {
      if (state.mediaInfoReport) {
        navigator.clipboard.writeText(JSON.stringify(state.mediaInfoReport, null, 2));
        showToast("JSON copiado para a área de transferência!");
      }
    };

    // Campos com gatilho condicional
    $('[data-field="streaming"]').onchange = (e) => {
      dom.wrapCustomStreaming.classList.toggle("hidden", e.target.value !== "custom");
      triggerLivePreview();
    };

    dom.checkTwoGroups.onchange = (e) => {
      dom.wrapSecondGroup.classList.toggle("hidden", !e.target.checked);
      triggerLivePreview();
    };

    dom.checkDots.onchange = triggerLivePreview;

    // Atualização de formulário em tempo real
    $$("[data-field]").forEach((el) => {
      el.addEventListener("input", triggerLivePreview);
      el.addEventListener("change", triggerLivePreview);
    });

    dom.btnUpdatePreview.onclick = refreshPreview;

    // Seleção na tabela
    dom.selectAllCheckbox.onchange = (e) => {
      $$(".file-checkbox").forEach((cb) => (cb.checked = e.target.checked));
      updateTableCounters();
    };

    dom.filesTableBody.addEventListener("change", (e) => {
      if (e.target.classList.contains("file-checkbox")) {
        updateTableCounters();
      }
    });

    // Copiar
    dom.btnCopyFolderName.onclick = () => {
      if (dom.newRootInput.value) {
        navigator.clipboard.writeText(dom.newRootInput.value);
        showToast("Nome da pasta copiado!");
      }
    };

    dom.btnCopyPostTitle.onclick = () => {
      if (dom.postTitleText.textContent && dom.postTitleText.textContent !== "-") {
        navigator.clipboard.writeText(dom.postTitleText.textContent);
        showToast("Título do post copiado!");
      }
    };

    // Ações de renomear
    dom.btnApply.onclick = applyRename;
    dom.btnUndo.onclick = undoRename;

    dom.btnOpenFolderNative.onclick = async () => {
      if (state.scan && state.scan.root) {
        try {
          await api("/api/reveal", { body: { path: state.scan.root } });
        } catch {
          // Erro silencioso
        }
      } else {
        showToast("Carregue uma pasta primeiro.", "warning");
      }
    };

    // Modais
    dom.btnOpenGuide.onclick = () => dom.modalGuide.showModal();
    dom.btnCloseGuide.onclick = () => dom.modalGuide.close();

    dom.btnOpenSettings.onclick = () => {
      loadSettings();
      dom.modalSettings.showModal();
    };
    dom.btnCloseSettings.onclick = () => dom.modalSettings.close();
    dom.btnCloseTmdb.onclick = () => dom.modalTmdb.close();
    dom.btnCloseBrowser.onclick = () => dom.modalBrowser.close();
    dom.btnCloseMediaInfo.onclick = () => dom.modalMediaInfo.close();

    dom.btnSaveSettings.onclick = saveSettings;

    dom.btnInstallMediaInfo.onclick = async () => {
      setBusy(true, "Baixando e instalando MediaInfo CLI oficial…");
      try {
        await api("/api/mediainfo/install");
        showToast("MediaInfo instalado com sucesso!");
        initSystem();
      } catch {
        // Erro reportado
      } finally {
        setBusy(false);
      }
    };

    // Navegação no Guia
    dom.guideNavLinks.forEach((link) => {
      link.onclick = (e) => {
        e.preventDefault();
        dom.guideNavLinks.forEach((l) => l.classList.remove("active"));
        link.classList.add("active");
        const targetId = link.getAttribute("href");
        const targetEl = dom.modalGuide.querySelector(targetId);
        if (targetEl) targetEl.scrollIntoView({ behavior: "smooth" });
      };
    });
  }

  // --- Boot ---
  document.addEventListener("DOMContentLoaded", () => {
    bindEvents();
    loadSettings();
    initSystem();
  });
})();
