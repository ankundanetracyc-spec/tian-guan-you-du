(() => {
  'use strict';
  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
  const catalog = window.CRAFT_CATALOG;
  const materials = new Map(catalog.materials.map(m => [m.name, m]));
  const rootPath = '../../../';
  const notebookIcon = rootPath + 'textures/ui/alchemy_20261019/formula_notebook_v1.png';
  const bottleIcon = rootPath + 'textures/items/alchemy_20261024/porcelain_bottle_closed.png';
  const swordIcon = rootPath + 'textures/ui/forged_swords_icons_20261025_v1/slender_sword.png';
  const pageNames = {recipes: '百工', records: '百工', alchemy: '天工峰 · 丹房', forging: '天工峰 · 器作'};
  const purposes = {huichun: '恢复生命。', ninggang: '抵挡伤害。', jinji: '短时提高物理防御。', qingshen: '获得身法。', xusheng: '后续两次自身回合开始时恢复生命。', jiegu: '移除外伤并恢复生命。', hezhong: '移除内伤并恢复生命。', naling: '补入当前层灵力。', fanyuan: '恢复大世界精力。', dingmai: '成功突破至云息四层后，补入吐纳进度。', guiyuan_dingxi: '成功突破至云息十层后，缩短归元九息所需时间。'};
  const sceneUse = {huichun: '世界 / 战斗', ninggang: '战斗', jinji: '战斗', qingshen: '战斗', xusheng: '战斗', jiegu: '战斗', hezhong: '战斗', naling: '世界', fanyuan: '世界', dingmai: '突破', guiyuan_dingxi: '突破'};
  const polarities = ['至阴', '阴', '偏阴', '和', '偏阳', '阳', '至阳'];
  const grades = ['', '九品', '八品', '七品', '六品', '五品', '四品', '三品', '二品', '一品'];
  const alchemySlotNames = ['君药', '臣药', '佐药一', '佐药二', '使药一', '佐药三', '使药二', '使药三'];
  const slotNumber = [-1, 0, 1, 2, 3, 4, 5, 6];
  const unlocks = {'-1': 8, 0: 8, 1: 8, 2: 68, 3: 8, 4: 508, 5: 32, 6: 203};
  const allRoutes = catalog.pills.flatMap(p => p.routes.map((r, i) => ({...r, pillId: p.id, pillName: p.name, routeIndex: i})));
  const initial = catalog.pills[0].routes[2];
  const state = {
    page: pageNames[location.hash.slice(1)] ? location.hash.slice(1) : 'recipes',
    pillId: 'huichun', routeIndex: 2, recipeTab: 'builtin', recipeQuery: '', savedSelected: 0, saved: [],
    alchemy: initial.ingredients.map(i => ({...i})), alchemyGroup: 0, alchemyName: initial.name,
    forging: catalog.forgingExamples[0].Inputs.map(i => ({name: i.Material, role: i.Role, dose: i.Quantity})),
    forgingGroup: 0, forgingProficiency: 60, recordTab: 'records', recordEmpty: false
  };
  const esc = s => String(s).replace(/[&<>"']/g, c => ({'&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'}[c]));
  const image = (src, alt = '', cls = '') => `<img src="${esc(src)}" alt="${esc(alt)}" ${cls ? `class="${cls}"` : ''}>`;
  const pill = () => catalog.pills.find(p => p.id === state.pillId) || catalog.pills[0];
  const roleLabel = slot => slot < 0 ? '君' : slot === 0 ? '臣' : [1, 2, 4].includes(slot) ? '佐' : '使';
  const roleName = role => role === '复合身材' ? '辅体' : role;
  const materialFacts = name => {
    const m = materials.get(name);
    return m ? `${m.element || '无属'} · ${polarities[Math.max(0, Math.min(6, m.polarity + 3))]} · ${Object.entries(m.utilities).map(([u, g]) => u + grades[g]).join('、')}` : '';
  };
  const empty = (title, copy = '', compact = false) => `<div class="empty ${compact ? 'compact' : ''}">${image(notebookIcon)}<h3>${esc(title)}</h3>${copy ? `<p>${esc(copy)}</p>` : ''}</div>`;
  const column = (title, content, cls = '') => `<section class="column"><h2 class="column-title">${title}</h2><div class="column-body ${cls}">${content}</div></section>`;
  const line = (name, val, prominent = false) => `<div class="cost-line"><dt>${name}</dt><dd${prominent ? ' class="prominent"' : ''}>${val}</dd></div>`;
  let toastTimer;
  let modalFocus;
  let modalCleanup;
  let lastPage = state.page;

  function fit() {
    const viewport = $('#viewport');
    $('#stage').style.setProperty('--scale', Math.min(viewport.clientWidth / 1664, viewport.clientHeight / 936));
  }
  function toast(message) {
    clearTimeout(toastTimer);
    $('#toast').textContent = message;
    $('#toast').hidden = false;
    toastTimer = setTimeout(() => { $('#toast').hidden = true; }, 2400);
  }
  function closeModal() {
    if (modalCleanup) modalCleanup();
    modalCleanup = undefined;
    $('#overlay').hidden = true;
    $('#modal').replaceChildren();
    if (modalFocus?.isConnected) modalFocus.focus({preventScroll: true});
  }
  function openModal(title, content, after) {
    if (!$('#overlay').hidden) closeModal();
    modalFocus = document.activeElement;
    $('#modal').innerHTML = `<div class="modal-header"><h2 id="modal-title">${esc(title)}</h2><button data-close>收起</button></div>${content}`;
    $('#overlay').hidden = false;
    $('[data-close]', $('#modal')).onclick = closeModal;
    $('#modal').focus({preventScroll: true});
    if (after) after($('#modal'));
  }
  $('.overlay-shade').onclick = closeModal;

  function navRecipes() {
    return `<div class="subnav"><button data-subtab="builtin" class="${state.recipeTab === 'builtin' ? 'active' : ''}">内置样方</button><button data-subtab="saved" class="${state.recipeTab === 'saved' ? 'active' : ''}">我的丹方<span class="count">${state.saved.length || ''}</span></button></div><input class="search" id="recipe-search" aria-label="查找丹药或丹方" placeholder="查找丹药或丹方" value="${esc(state.recipeQuery)}"><div class="scroll-list pill-list" id="pill-list"></div><div class="catalog-count">${state.recipeTab === 'builtin' ? '11 种丹药　·　25 套样方' : '记录配伍，留待下次开炉'}</div>`;
  }
  function updateRecipeList() {
    const query = state.recipeQuery.trim();
    if (state.recipeTab === 'saved') {
      const rows = state.saved.map((r, i) => ({r, i})).filter(({r}) => !query || r.name.includes(query));
      $('#pill-list').innerHTML = rows.length ? rows.map(({r, i}) => `<button class="list-row ${i === state.savedSelected ? 'selected' : ''}" data-saved="${i}"><span>${esc(r.name)}<small class="subline">${r.ingredients.length} 味　${r.ingredients.reduce((a, x) => a + x.dose, 0)} 份</small></span><small>›</small></button>`).join('') : empty(state.saved.length ? '未找到丹方' : '尚无自拟丹方', state.saved.length ? '试试其他名称。' : '可在丹房记下当前配伍。', true);
      $$('[data-saved]').forEach(b => b.onclick = () => { state.savedSelected = Number(b.dataset.saved); render(); });
    } else {
      const rows = catalog.pills.filter(p => !query || p.name.includes(query) || p.routes.some(r => r.name.includes(query)));
      $('#pill-list').innerHTML = rows.length ? rows.map(p => `<button class="list-row ${p.id === state.pillId ? 'selected' : ''}" data-pill="${p.id}"><span>${p.rank === 7 ? '七品' : '九品'}${p.name}</span><small>${p.routes.length} 方</small></button>`).join('') : empty('未找到丹方', '试试其他名称。', true);
      $$('[data-pill]').forEach(b => b.onclick = () => {
        state.pillId = b.dataset.pill;
        const index = pill().routes.findIndex(r => query && r.name.includes(query));
        state.routeIndex = index >= 0 ? index : 0;
        render();
      });
    }
  }
  function ingredientsHtml(entries) {
    return [...entries].sort((a, b) => a.slot - b.slot).map(i => `<div class="ingredient-row"><span class="role-seal">${roleLabel(i.slot)}</span>${image(materials.get(i.name)?.icon || '', i.name)}<span>${esc(i.name)}</span><span class="dose">× ${i.dose}</span></div>`).join('');
  }
  function renderRecipes() {
    const p = pill();
    const saved = state.recipeTab === 'saved' ? state.saved[state.savedSelected] : null;
    const recipe = state.recipeTab === 'saved' ? saved : p.routes[state.routeIndex];
    const middle = recipe ? `<h2 class="detail-title">${esc(recipe.name)}</h2>${saved ? '<div class="tags"><span class="tag">自拟丹方</span></div>' : `<div class="tags"><span class="tag">${p.rank === 7 ? '七品' : '九品'}${p.name}</span><span class="tag">君药核心 · ${p.core}</span></div><div class="route-tabs">${p.routes.map((r, i) => `<button class="tab ${i === state.routeIndex ? 'active' : ''}" data-route="${i}">${esc(r.name)}</button>`).join('')}</div>`}${ingredientsHtml(recipe.ingredients)}<p class="direction">${saved ? '按所记配伍查阅君、臣、佐、使与用量。' : esc(recipe.direction)}</p>` : empty('尚未选取丹方', '记录丹方后，可在此查阅投料。');
    const right = state.recipeTab === 'saved' ? `<h3 class="section-heading">丹方记事</h3><p class="body-copy">丹方记下材料、药位与份数，成丹由实际配伍决定。</p><div class="rule"></div><h3 class="section-heading">炼丹记录</h3>${empty('尚无炼丹记录', '开炉后的投入与成果在此留存。', true)}` : `<div class="pill-intro">${image(p.icon || bottleIcon, p.name)}<div><h2>${esc(p.name)}</h2><span class="tag">${sceneUse[p.id]}</span></div></div><h3 class="section-heading">用途</h3><p class="body-copy">${purposes[p.id]}</p><div class="rule"></div><h3 class="section-heading">炼丹记录</h3>${empty('尚无炼丹记录', '开炉后的投入与成果在此留存。', true)}`;
    $('#screen-content').innerHTML = column('丹方册', navRecipes(), 'no-scroll') + column('配伍详情', middle) + column('丹药与记录', right);
    updateRecipeList();
    $('#recipe-search').oninput = e => { state.recipeQuery = e.target.value; updateRecipeList(); };
    $$('[data-subtab]').forEach(b => b.onclick = () => { state.recipeTab = b.dataset.subtab; state.recipeQuery = ''; render(); });
    $$('[data-route]').forEach(b => b.onclick = () => { state.routeIndex = Number(b.dataset.route); render(); });
    setFooter('共 11 种丹药 · 25 套样方　｜　开炉请前往天工峰丹房', '收起', backToParent);
  }

  function forgingMetrics(inputs) {
    const costs = [0, .75, 1, 1.4, 2, 3, 4.5, 7, 11, 17];
    const rows = inputs.map(i => {
      const m = materials.get(i.name);
      const gs = Object.values(m?.utilities || {}).sort((a, b) => b - a);
      const v = gs.length ? Math.max(1, Math.min(27, Math.ceil(gs[0] * 2 + (gs[1] || 0) * .5 + gs.slice(2).reduce((a, g) => a + g, 0) * .25))) : 0;
      const base = 1 + .13 * v + .0065 * v * v;
      const f = gs.reduce((a, g) => a + costs[g], 0);
      const r = i.role === '质材' ? 1.4 : i.role === '和材' ? .9 : 1.2;
      const q = Math.max(0, i.dose - 1);
      return {...i, load: (base + f) * r * (1 + .4 * q + .04 * q * q)};
    });
    const units = inputs.reduce((a, i) => a + i.dose, 0);
    const hours = Math.ceil(units / 3);
    const demand = Math.ceil(Number(rows.reduce((a, i) => a + i.load, 0).toFixed(8)));
    return {units, hours, energy: hours * 2, demand, rows};
  }
  function renderRecords() {
    const r = catalog.record;
    const inputs = r.inputs.map(i => ({name: i.Material, role: i.Role, dose: i.Quantity}));
    const cost = forgingMetrics(inputs);
    const nav = `<div class="subnav"><button data-record-tab="records" class="${state.recordTab === 'records' ? 'active' : ''}">炼制记录</button><button data-record-tab="artifacts" class="${state.recordTab === 'artifacts' ? 'active' : ''}">持有器物</button></div>`;
    const left = nav + (state.recordEmpty ? empty('尚无炼制存录', '炼成器物后，可在此查看投入与用度。') : `<button class="list-row record-row selected" aria-pressed="true">${image(swordIcon, '')}<span><strong>${r.name}</strong><span class="subline">${r.tier}　·　${cost.units} 份投料</span></span></button><div class="catalog-count">${state.recordTab === 'records' ? '共 1 次炼制' : '共 1 件器物'}</div>`);
    const center = state.recordEmpty ? empty('尚未选取存录', '选中一条记录，查看成器与投料。') : `<div class="artifact-head">${image(swordIcon, r.name)}<div><h2>${r.name}</h2><div class="tags"><span class="tag">${r.tier}</span><span class="tag">剑器</span></div></div></div><h3 class="section-heading">当次投料</h3>${inputs.map(i => `<div class="artifact-input"><span>${roleName(i.role)}</span><span class="material">${esc(i.name)}</span>${image(materials.get(i.name)?.icon || '')}<span class="amount">× ${i.dose}</span></div>`).join('')}`;
    const right = state.recordEmpty ? empty('暂无用度记录', '', true) : `<h3 class="section-heading">炼制用度</h3><dl class="cost-list">${line('当时炼器造诣', r.proficiency)}${line('投入材料', `${new Set(inputs.map(i => i.name)).size} 种 · ${cost.units} 份`)}${line('工时', `${cost.hours} 时辰`)}${line('精力', cost.energy)}${line('灵识需求', cost.demand)}</dl><div class="rule"></div><h3 class="section-heading">成器去处</h3><p class="body-copy">收入行囊。</p><p class="artifact-note">材料与用度随本次炼制留存，查看存录不消耗资源。</p>`;
    $('#screen-content').innerHTML = column('器物与存录', left) + column('成器存录', center) + column('当次用度', right);
    $$('[data-record-tab]').forEach(b => b.onclick = () => { state.recordTab = b.dataset.recordTab; render(); });
    setFooter('查看器物与过往炼制　｜　开炼请前往天工峰器作', '收起', backToParent);
  }

  function slotCard(entry, slot, compact = false) {
    const name = alchemySlotNames[slotNumber.indexOf(slot)];
    if (unlocks[slot] > 68) return `<div class="locked-slot"><span>${name}</span><span>造诣 ${unlocks[slot]} 开放</span></div>`;
    return `<div class="slot-card ${compact ? 'compact-slot' : ''}"><h3>${name}<button class="text-button" data-clear-slot="${slot}" ${entry ? '' : 'disabled'}>移除</button></h3><button class="slot-material" data-pick-slot="${slot}" aria-label="${name}：${entry ? '更换' + esc(entry.name) : '选择药材'}">${entry ? image(materials.get(entry.name)?.icon || '', entry.name) : image(notebookIcon)}<span><span class="name">${entry ? esc(entry.name) : '选择药材　›'}</span><span class="facts">${entry ? esc(materialFacts(entry.name)) : '尚未投料'}</span></span></button>${entry ? stepper(entry.dose, 'alchemy', slot, 0) : ''}</div>`;
  }
  function stepper(dose, type, key, max) {
    return `<div class="stepper"><span>份数</span><button data-step="${type}:${key}:-1" aria-label="减少${type === 'alchemy' ? alchemySlotNames[slotNumber.indexOf(Number(key))] : esc(key)}份数" ${dose <= 1 ? 'disabled' : ''}>−</button><input type="number" min="1" ${max ? `max="${max}"` : ''} step="1" value="${dose}" data-quantity="${type}:${key}" aria-label="${type === 'alchemy' ? alchemySlotNames[slotNumber.indexOf(Number(key))] : esc(key)}份数"><button data-step="${type}:${key}:1" aria-label="增加${type === 'alchemy' ? alchemySlotNames[slotNumber.indexOf(Number(key))] : esc(key)}份数" ${max && dose >= max ? 'disabled' : ''}>＋</button></div>`;
  }
  function renderAlchemy() {
    const entries = state.alchemy;
    const count = entries.length;
    const units = entries.reduce((a, i) => a + i.dose, 0);
    const slots = state.alchemyGroup === 0 ? [-1, 0] : [1, 2, 4, 3, 5, 6];
    const left = `<div class="subnav"><button class="${state.alchemyGroup === 0 ? 'active' : ''}" data-alchemy-group="0">君药 · 臣药</button><button class="${state.alchemyGroup === 1 ? 'active' : ''}" data-alchemy-group="1">佐药 · 使药</button></div><div class="group-note">${state.alchemyGroup === 0 ? '君药主效，臣药辅成。' : '佐药调理，使药通达。'}</div><div class="scroll-list">${slots.map(s => slotCard(entries.find(i => i.slot === s), s, state.alchemyGroup !== 0)).join('')}</div><div class="slot-toolbar"><span class="small-note">已投 ${count} 味 · ${units} 份</span><button class="text-button" id="clear-alchemy" ${count ? '' : 'disabled'}>清空投料</button></div>`;
    const middle = `<div class="furnace-display"><p class="furnace-caption">当前配伍 · ${esc(state.alchemyName || '自拟配伍')}</p><div class="furnace-shadow"></div><div class="furnace-object">${image(rootPath + 'textures/ui/alchemy_room_20261017/furnace_rear_half_soft_v13.png', '现用丹炉后层')}${image(rootPath + 'textures/ui/alchemy_room_20261017/furnace_front_half_soft_v13.png', '现用丹炉前层')}</div></div><div class="center-tools"><button class="small-button" id="recipe-book">丹方册</button><button class="small-button" id="save-recipe" ${count && entries.some(i => i.slot === -1) ? '' : 'disabled'}>存为丹方</button></div><p class="center-state">配伍由君、臣、佐、使共同成就</p>`;
    const right = `<h3 class="section-heading">本炉用度</h3><dl class="cost-list">${line('可支配灵识 / 总灵识', '— / —')}${line('本炉灵识需求', '—', true)}${line('投料', `${count} 味 · ${units} 份`)}${line('精力', '—')}${line('工时', '—')}</dl><div class="cost-subtitle">药位投料</div><div class="load-scroll">${[...entries].sort((a, b) => a.slot - b.slot).map(i => `<div class="load-line"><span>${roleLabel(i.slot)} · ${esc(i.name)}</span><span>× ${i.dose}</span></div>`).join('') || '<p class="small-note">尚未投料。</p>'}</div><div class="validation${entries.some(i => i.slot === -1) ? '' : ' missing'}">${entries.some(i => i.slot === -1) ? '开炉前核对材料、精力与可支配灵识。' : '尚缺君药，请先选择。'}</div>`;
    $('#screen-content').innerHTML = column('药材配伍', left, 'no-scroll') + column('丹炉', middle, 'work-center') + column('开炉用度', right);
    $$('[data-alchemy-group]').forEach(b => b.onclick = () => { state.alchemyGroup = Number(b.dataset.alchemyGroup); render(); });
    $$('[data-pick-slot]').forEach(b => b.onclick = () => pickMaterial('alchemy', Number(b.dataset.pickSlot)));
    $$('[data-clear-slot]').forEach(b => b.onclick = () => { state.alchemy = entries.filter(i => i.slot !== Number(b.dataset.clearSlot)); state.alchemyName = '自拟配伍'; render(); });
    $('#clear-alchemy').onclick = () => confirmClear('清空当前配伍？', () => { state.alchemy = []; state.alchemyName = '自拟配伍'; render(); });
    $('#recipe-book').onclick = openRecipeBook;
    $('#save-recipe').onclick = saveRecipe;
    wireQuantities();
    setFooter('开炉消耗投料与精力，并推进时间；成丹后鉴看、装瓶领取。', '开炉', null);
  }

  function renderForging() {
    const groups = ['身材', '质材', '性材', '和材'];
    const group = groups[state.forgingGroup];
    const roles = state.forgingGroup === 0 ? ['主体', '承接', '复合身材'] : [group];
    const notes = ['主体、承接与辅体。', '质材承载灵性。', '性材提供效能。', '和材调和相克。'];
    const tabs = `<div class="subnav forge-tabs">${groups.map((g, i) => `<button data-forge-group="${i}" class="${i === state.forgingGroup ? 'active' : ''}">${g}</button>`).join('')}</div><p class="group-note">${notes[state.forgingGroup]}</p>`;
    const left = tabs + `<div class="scroll-list">${roles.map(role => {
      const entries = state.forging.filter(i => i.role === role);
      const z = state.forgingProficiency;
      const fp = catalog.forgingParameters;
      const limit = role === '复合身材' ? (z >= fp.ExtraSourceProficiency ? 1 : 0) : role === '性材' ? (z >= fp.ExtraSourceProficiency ? 2 : 1) : role === '和材' ? (z >= fp.AdvancedAuxiliaryProficiency ? 3 : z >= fp.ExtraAuxiliaryProficiency ? 2 : 1) : 1;
      return `<div class="forge-group"><div class="forge-slot-header"><span>${roleName(role)} <small class="small-note">${entries.length} / ${limit}</small></span><button class="small-button" data-add-role="${role}" ${entries.length >= limit ? 'disabled' : ''}>添加材料</button></div>${entries.length ? entries.map(i => `<div class="slot-card"><button class="slot-material" data-forge-pick="${state.forging.indexOf(i)}" aria-label="更换${roleName(role)}${esc(i.name)}">${image(materials.get(i.name)?.icon || '', i.name)}<span><span class="name">${esc(i.name)}　›</span><span class="facts">${esc(materialFacts(i.name))}</span></span></button>${stepper(i.dose, 'forging', state.forging.indexOf(i), 8)}<button class="text-button" data-forge-remove="${state.forging.indexOf(i)}">移除</button></div>`).join('') : `<p class="forge-empty">${['主体', '承接'].includes(role) ? '尚未投料 · 开炼前需配置' : '尚未投料 · 可选'}</p>`}</div>`;
    }).join('')}</div><div class="slot-toolbar"><span class="small-note">每岗每材 1—8 份</span><button class="text-button" id="clear-forging" ${state.forging.length ? '' : 'disabled'}>清空投料</button></div>`;
    const cost = forgingMetrics(state.forging);
    const fp = catalog.forgingParameters;
    const retained = state.forging.map(i => {
      const m = materials.get(i.name);
      const q = (1 - Math.exp(-i.dose / fp.QuantityScale)) / (1 - Math.exp(-1 / fp.QuantityScale));
      const retention = m.forgingRetention * (fp.Scalars.RetentionFloor + (1 - fp.Scalars.RetentionFloor) * Math.max(0, Math.min(1, (state.forgingProficiency - m.forgingDifficulty) / fp.Scalars.ProficiencySpan)));
      return {polarity: m.polarity, weight: q * retention};
    });
    const weight = retained.reduce((a, m) => a + m.weight, 0);
    const yin = weight ? retained.reduce((a, m) => a + m.polarity * m.weight, 0) / weight : 0;
    const polarityIndex = Math.max(0, Math.min(6, Math.sign(yin) * Math.floor(Math.abs(yin) + .5) + 3));
    const middle = `<div class="forge-scene"><div class="bench-materials">${[...new Set(state.forging.map(i => i.name))].slice(0, 6).map(n => image(materials.get(n).icon, n)).join('')}</div><p class="bench-caption">${state.forging.length ? '当前投料 · ' + cost.units + ' 份' : '尚未投料'}</p></div><div class="polarity"><div class="polarity-title"><span>投入阴阳</span><span>${state.forging.length ? polarities[polarityIndex] : '—'}</span></div><div class="polarity-track"><span class="polarity-marker" style="left:${50 + yin / 6 * 100}%" ${state.forging.length ? '' : 'hidden'}></span></div><div class="polarity-labels">${polarities.map(p => `<span>${p}</span>`).join('')}</div></div><div class="center-tools"><button class="small-button" id="forge-examples">参照投料</button><button class="small-button" id="forge-records">器物册</button></div>`;
    const missing = ['主体', '承接'].filter(r => !state.forging.some(i => i.role === r));
    const right = `<h3 class="section-heading">本次用度</h3><dl class="cost-list">${line('可支配灵识 / 总灵识', '— / —')}${line('本次灵识需求', cost.units ? cost.demand : '—', true)}${line('投料', `${cost.units} 份`)}${line('工时', `${cost.hours} 时辰`)}${line('精力', cost.energy)}</dl><h3 class="cost-subtitle">逐材灵识负担</h3><div class="load-scroll">${cost.rows.map(i => `<div class="load-line"><span>${roleName(i.role)} · ${esc(i.name)} ×${i.dose}</span><span>${Number(i.load.toFixed(2))}</span></div>`).join('') || '<p class="small-note">尚未投料。</p>'}</div><div class="validation${missing.length ? ' missing' : ''}">${missing.length ? '尚缺' + missing.join('、') + '。' : '主体、承接已投料。'}<br>开炼前核对库存、精力与可支配灵识。</div>`;
    $('#screen-content').innerHTML = column('剑类投料', left, 'no-scroll') + column('器作', middle, 'work-center') + column('开炼用度', right);
    $$('[data-forge-group]').forEach(b => b.onclick = () => { state.forgingGroup = Number(b.dataset.forgeGroup); render(); });
    $$('[data-add-role]').forEach(b => b.onclick = () => pickMaterial('forging', b.dataset.addRole));
    $$('[data-forge-pick]').forEach(b => b.onclick = () => pickMaterial('forging', state.forging[Number(b.dataset.forgePick)].role, Number(b.dataset.forgePick)));
    $$('[data-forge-remove]').forEach(b => b.onclick = () => { state.forging.splice(Number(b.dataset.forgeRemove), 1); render(); });
    $('#clear-forging').onclick = () => confirmClear('清空当前投料？', () => { state.forging = []; render(); });
    $('#forge-examples').onclick = () => openModal('参照投料', `<p class="modal-description">载入后替换当前投料，仍可继续调整。</p><div class="modal-body recipe-picker">${catalog.forgingExamples.map((e, i) => `<button class="recipe-choice" data-forge-example="${i}"><h3>${esc(e.Name)}</h3><p>${e.Inputs.map(x => `${roleName(x.Role)} · ${esc(x.Material)} ×${x.Quantity}`).join('　')}</p></button>`).join('')}</div>`, modal => {
      $$('[data-forge-example]', modal).forEach(b => b.onclick = () => { state.forging = catalog.forgingExamples[Number(b.dataset.forgeExample)].Inputs.map(i => ({name: i.Material, role: i.Role, dose: i.Quantity})); closeModal(); render(); });
    });
    $('#forge-records').onclick = () => openModal('器物册', empty('尚无持有器物', '炼成器物后，可在此查阅。'));
    wireQuantities();
    setFooter('剑类炼制　｜　每岗每材 1—8 份，整炉由可支配灵识约束。', '开炼', null);
  }

  function wireQuantities() {
    function update(type, key, newValue) {
      const entry = type === 'alchemy' ? state.alchemy.find(i => i.slot === Number(key)) : state.forging[Number(key)];
      const value = Number(newValue);
      if (!entry || !Number.isSafeInteger(value) || value < 1 || (type === 'forging' && value > 8)) { render(); return; }
      entry.dose = value;
      if (type === 'alchemy') state.alchemyName = '自拟配伍';
      render();
    }
    $$('[data-step]').forEach(b => b.onclick = () => {
      const [type, key, delta] = b.dataset.step.split(':');
      const entry = type === 'alchemy' ? state.alchemy.find(i => i.slot === Number(key)) : state.forging[Number(key)];
      update(type, key, entry.dose + Number(delta));
    });
    $$('[data-quantity]').forEach(input => {
      input.onchange = () => { const [type, key] = input.dataset.quantity.split(':'); update(type, key, input.value); };
      input.onkeydown = e => { if (e.key === 'Enter') input.blur(); };
    });
  }

  function pickMaterial(type, roleOrSlot, replacingIndex = -1) {
    const role = type === 'alchemy' ? alchemySlotNames[slotNumber.indexOf(roleOrSlot)] : roleName(roleOrSlot);
    openModal('选择' + role + (type === 'alchemy' ? '' : '材料'), `<div class="picker-filters"><input class="search" id="material-search" aria-label="查找材料" placeholder="查找材料"><select id="element-filter" aria-label="按五行筛选"><option value="">全部五行</option>${['木', '火', '土', '金', '水'].map(e => `<option>${e}</option>`).join('')}</select><select id="utility-filter" aria-label="按功用筛选"><option value="">全部功用</option>${['生', '聚', '固', '破', '化', '镇', '护', '明', '通', '行', '引', '藏'].map(e => `<option>${e}</option>`).join('')}</select></div><div class="picker-grid" id="material-results"></div><p class="picker-count" id="material-count"></p>`, modal => {
      const eligible = catalog.materials.filter(m => type === 'alchemy' ? !state.alchemy.some(i => i.name === m.name && i.slot !== roleOrSlot) : m.roles.includes(roleOrSlot) && !state.forging.some((i, idx) => i.role === roleOrSlot && i.name === m.name && idx !== replacingIndex));
      function update() {
        const query = $('#material-search').value.trim();
        const element = $('#element-filter').value;
        const utility = $('#utility-filter').value;
        const found = eligible.filter(m => (!query || m.name.includes(query)) && (!element || m.element === element) && (!utility || m.utilities[utility]));
        $('#material-results').innerHTML = found.length ? found.map(m => `<button class="picker-item" data-material="${esc(m.name)}">${image(m.icon, m.name)}<div><h3>${esc(m.name)}</h3><p>${esc(materialFacts(m.name))}<br>可用：—</p></div></button>`).join('') : '<div class="empty"><h3>未找到材料</h3><p>试试其他名称、五行或功用。</p></div>';
        $('#material-count').textContent = `共 ${found.length} 种材料`;
        $$('[data-material]', modal).forEach(b => b.onclick = () => {
          if (type === 'alchemy') {
            const current = state.alchemy.find(i => i.slot === roleOrSlot);
            if (current) current.name = b.dataset.material;
            else state.alchemy.push({name: b.dataset.material, slot: roleOrSlot, dose: 1});
            state.alchemyName = '自拟配伍';
          } else if (replacingIndex >= 0) state.forging[replacingIndex].name = b.dataset.material;
          else state.forging.push({name: b.dataset.material, role: roleOrSlot, dose: 1});
          closeModal(); render();
        });
      }
      $('#material-search').oninput = update;
      $('#element-filter').onchange = update;
      $('#utility-filter').onchange = update;
      update();
      $('#material-search').focus();
    });
  }

  function openRecipeBook() {
    openModal('丹方册', `<div class="subnav"><button id="book-builtin" class="active">内置样方</button><button id="book-saved">我的丹方</button></div><input class="search" id="book-search" aria-label="查找丹方" placeholder="查找丹药或丹方"><div class="modal-body recipe-picker" id="book-results"></div><p class="picker-count">载入只填写配伍，仍可调整药位与份数。</p>`, modal => {
      let saved = false;
      function update() {
        $('#book-builtin').classList.toggle('active', !saved);
        $('#book-saved').classList.toggle('active', saved);
        const query = $('#book-search').value.trim();
        const list = (saved ? state.saved : allRoutes).map((r, i) => ({r, i})).filter(({r}) => !query || r.name.includes(query) || (r.pillName || '').includes(query));
        $('#book-results').innerHTML = list.length ? list.map(({r, i}) => `<button class="recipe-choice" data-load-recipe="${i}"><h3>${esc(r.name)}</h3><p>${saved ? '自拟丹方' : esc(r.pillName)}　·　${r.ingredients.length} 味</p></button>`).join('') : empty('尚无丹方', '试试其他检索，或先记下当前配伍。', true);
        $$('[data-load-recipe]', modal).forEach(b => b.onclick = () => {
          const r = (saved ? state.saved : allRoutes)[Number(b.dataset.loadRecipe)];
          state.alchemy = r.ingredients.map(i => ({...i})); state.alchemyName = r.name;
          closeModal(); render();
        });
      }
      $('#book-builtin').onclick = () => { saved = false; update(); };
      $('#book-saved').onclick = () => { saved = true; update(); };
      $('#book-search').oninput = update;
      update();
    });
  }
  function saveRecipe() {
    openModal('记下当前配伍', `<label class="field">丹方名称<input id="recipe-name" maxlength="40" autocomplete="off" value="${esc(state.alchemyName === '自拟配伍' ? '' : state.alchemyName + '·自拟')}"></label><p class="modal-description">保存材料、药位与份数，留待下次查阅。</p><p id="save-error" class="status-message" role="alert" hidden></p><div class="modal-actions"><button data-cancel-save>取消</button><button class="confirm" id="confirm-save">记下</button></div>`, modal => {
      $('[data-cancel-save]', modal).onclick = closeModal;
      $('#confirm-save').onclick = () => {
        const name = $('#recipe-name').value.trim();
        if (!name || state.saved.some(r => r.name === name)) {
          $('#save-error').textContent = name ? '已有同名丹方，请换一个名称。' : '请填写丹方名称。';
          $('#save-error').hidden = false; return;
        }
        state.saved.push({name, ingredients: state.alchemy.map(i => ({...i}))});
        closeModal(); render(); toast('已记下配伍');
      };
      $('#recipe-name').onkeydown = e => { if (e.key === 'Enter') $('#confirm-save').click(); };
      $('#recipe-name').focus();
    });
  }
  function confirmClear(title, callback) {
    openModal(title, '<p class="modal-description">只移除当前页中的投料安排。</p><div class="modal-actions"><button id="cancel-clear">保留</button><button id="confirm-clear">清空</button></div>', () => {
      $('#cancel-clear').onclick = closeModal;
      $('#confirm-clear').onclick = () => { closeModal(); callback(); };
    });
  }
  function backToParent() {
    const facility = ['alchemy', 'forging'].includes(state.page);
    openModal(facility ? '玉虚仙宗 · 天工峰' : '百工 · 传承与器物', `<div class="parent-choices">${(facility ? [['alchemy', '丹房'], ['forging', '器作']] : [['recipes', '丹药'], ['records', '法宝']]).map(([id, name]) => `<button data-parent-page="${id}">${name}</button>`).join('')}</div>`, modal => {
      $$('[data-parent-page]', modal).forEach(b => b.onclick = () => { closeModal(); navigate(b.dataset.parentPage); });
    });
  }
  function setFooter(text, action, callback) {
    $('#footer-message').textContent = text;
    const button = $('#primary-action');
    button.textContent = action;
    button.disabled = !callback;
    button.onclick = callback;
    button.title = callback ? '' : '本轮只预览界面';
  }
  function render() {
    document.title = `${pageNames[state.page]} · 界面样稿`;
    $('#page-title').textContent = pageNames[state.page];
    $('#breadcrumb').textContent = {recipes: '百工 / 丹药　　丹方与过往炼制', records: '百工 / 法宝　　器物与过往炼制', alchemy: '玉虚仙宗 / 天工峰 / 丹房', forging: '玉虚仙宗 / 天工峰 / 器作'}[state.page];
    $('#character-resources').textContent = {recipes: '丹道造诣 —', records: '炼器造诣 —', alchemy: '丹道造诣 —　精力 — / —', forging: '炼器造诣 —　精力 — / —'}[state.page];
    $('#sample-label').textContent = state.page === 'records' ? (state.recordEmpty ? '空记录状态 · 未连接角色资料' : '现有演出样例 · 非玩家炼制记录') : state.page === 'alchemy' ? '药位按造诣 68 排版 · 角色资料未连接' : state.page === 'forging' ? '岗位按造诣 60 排版 · 角色资料未连接' : '真实目录 · 未连接角色资料';
    $$('[data-page]').forEach(b => { b.classList.toggle('active', b.dataset.page === state.page); b.setAttribute('aria-pressed', b.dataset.page === state.page); });
    let mode = $('#record-mode');
    if (!mode) {
      mode = document.createElement('select'); mode.id = 'record-mode'; mode.setAttribute('aria-label', '炼器记录展示状态');
      mode.innerHTML = '<option value="sample">示例存录</option><option value="empty">空记录</option>';
      $('#sample-label').before(mode);
      mode.onchange = () => { state.recordEmpty = mode.value === 'empty'; render(); };
    }
    mode.hidden = state.page !== 'records'; mode.value = state.recordEmpty ? 'empty' : 'sample';
    ({recipes: renderRecipes, records: renderRecords, alchemy: renderAlchemy, forging: renderForging})[state.page]();
    fit();
  }
  function navigate(page) {
    if (!pageNames[page]) return;
    if (!$('#overlay').hidden) closeModal();
    state.page = page;
    lastPage = page;
    history.replaceState(null, '', '#' + page);
    $('#toast').hidden = true;
    render();
  }
  $$('[data-page]').forEach(b => b.onclick = () => navigate(b.dataset.page));
  $('#back').onclick = () => $('#overlay').hidden ? backToParent() : closeModal();
  $('#notes-open').onclick = () => { $('#notes').hidden = !$('#notes').hidden; $('#notes-close').focus(); };
  $('#notes-close').onclick = () => { $('#notes').hidden = true; $('#notes-open').focus(); };
  function immersive(on) { document.body.classList.toggle('immersive', on); $('#review-restore').hidden = !on; fit(); }
  $('#immersive').onclick = () => immersive(true);
  $('#review-restore').onclick = () => immersive(false);
  window.addEventListener('resize', fit);
  window.addEventListener('hashchange', () => { const p = location.hash.slice(1); if (pageNames[p] && p !== lastPage) navigate(p); });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
      if (!$('#notes').hidden) { $('#notes').hidden = true; $('#notes-open').focus(); }
      else if (!$('#overlay').hidden) closeModal();
      else if (document.body.classList.contains('immersive')) immersive(false);
      else backToParent();
      e.preventDefault();
    }
    if (e.key === 'Tab' && !$('#overlay').hidden) {
      const nodes = $$('button:not(:disabled),input,select,[tabindex="0"]', $('#modal')).filter(n => !n.hidden && n.getClientRects().length);
      if (!nodes.length) return;
      if (e.shiftKey && (document.activeElement === nodes[0] || document.activeElement === $('#modal'))) { nodes.at(-1).focus(); e.preventDefault(); }
      else if (!e.shiftKey && document.activeElement === nodes.at(-1)) { nodes[0].focus(); e.preventDefault(); }
    }
  });
  render();
  document.fonts.ready.then(fit);
})();
