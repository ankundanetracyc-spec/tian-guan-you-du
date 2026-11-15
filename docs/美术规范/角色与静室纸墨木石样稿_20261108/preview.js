(() => {
  'use strict';
  const M = window.Materials, D = window.PREVIEW_DATA, E = M.escape;
  const stage = document.querySelector('#stage');
  const state = { app: 'character', character: 'overview', quiet: 'practice', book: 0, studyBook: 0, mode: '指定天数', skill: '', attributeFilter: '全部', options: {}, queries: {}, inputs: { days: 5, strands: 1, year: 1, month: 3, date: 20 }, details: false };
  const pages = {
    character: [['overview', '概览', 5], ['attributes', '属性', 0], ['battle', '战斗', 1], ['manuals', '功法', 2], ['proficiency', '造诣', 4], ['equipment', '装备', 3], ['relations', '关系', 5]],
    quiet: [['practice', '吐纳修行', 0], ['study', '功法研读', 2], ['breakthrough', '闭关突破', 4]]
  };
  const portraitPath = '../../../mods/appearance/portrait_male_regenerated_20261104_v3/preview/style01_transparent.png';
  const manualIcon = '../../../textures/items/inventory_density_20261029/manual_generic_2x3.png';
  const tooltipContent = new Map();
  const menus = new Map();
  let tipNumber = 0;
  const tip = (title, text) => { const id = 'tip-' + ++tipNumber; tooltipContent.set(id, { title, text }); return `data-tip="${id}"`; };
  const btn = M.button;
  const title = text => `<h2 class="section-title">${text}</h2>`;
  const text = (content, className = '') => `<p class="${className}">${content}</p>`;
  const rows = entries => `<dl class="fact-list">${entries.map(([name, value, details]) => `<div ${details ? tip(name, details) + ' tabindex="0" data-material-source="../../../textures/ui/yuxu_library_20261107/library_frame.png"' : ''}><dt>${name}</dt><dd${String(name).includes('灵力') || name === '下层所需' || String(value).includes('缕') ? ' class="spirit-value"' : ''}>${value}</dd></div>`).join('')}</dl>`;
  const tags = items => `<div class="tags">${items.map(item => `<span data-skin="paper">${item}</span>`).join('')}</div>`;
  const bookImage = (className = '') => `<img src="${manualIcon}" class="book-icon ${className}" alt="秘籍书册">`;
  function drop(name, label, options, placeholder = '') {
    const value = state.options[name] ?? placeholder ?? options[0];
    menus.set(name, { label, options, defaultValue: value });
    return btn(`<span>${E(value || label)}</span>${M.sprite('down')}`, `data-menu="${name}" aria-haspopup="listbox" aria-label="${E(label)}"`, 'dropdown');
  }
  function stepper(name, label, min = 1, max = 999) {
    return `<span class="stepper ${name === 'strands' ? 'spirit-field' : ''}" data-skin="button"><input type="number" inputmode="numeric" aria-label="${label}" min="${min}" max="${max}" step="1" value="${state.inputs[name]}" data-number="${name}" data-material-source="../../../textures/ui/yuxu_library_20261107/library_frame.png"><span class="step-buttons"><button class="step-arrow" data-step="${name}" data-delta="1" data-material-exception="stepper-triangle" aria-label="${label}增加"><span class="triangle-glyph triangle-up" aria-hidden="true"></span></button><button class="step-arrow" data-step="${name}" data-delta="-1" data-material-exception="stepper-triangle" aria-label="${label}减少"><span class="triangle-glyph triangle-down" aria-hidden="true"></span></button></span></span>`;
  }
  function search(placeholder, group) {
    return `<label class="search-well" data-skin="button">${M.sprite('search')}<input type="search" autocomplete="off" placeholder="${placeholder}" aria-label="${placeholder}" data-search="${group}" data-material-source="../../../textures/ui/yuxu_library_20261107/library_frame.png">${M.iconButton('close', `清除${placeholder}`, `data-clear-search="${group}"`)}</label>`;
  }
  function checkbox(label, name, checked = false) {
    return `<label class="check-control" data-material-source="assets/控件补充图集_v1.png"><input type="checkbox" data-check="${name}" ${checked ? 'checked' : ''}><span class="check-off">${M.sprite('checkOff')}</span><span class="check-on">${M.sprite('checkOn')}</span><span>${label}</span></label>`;
  }
  const portrait = (className = '') => `<div class="portrait ${className}"><img src="${portraitPath}" alt="现有男修立绘素材，供界面排版展示"></div>`;
  const navIcon = index => M.atlasView('../../../textures/ui/infopanel_nav_icons_ink_v1.png', [1536, 1024], [index % 3 * 512, Math.floor(index / 3) * 512, 512, 512], 'nav-icon');
  function shell(body, app = state.app) {
    const character = app === 'character';
    const current = pages[app].find(page => page[0] === state[app]);
    const headerButtons = character ? btn('行囊', 'data-action="inventory"') : '';
    const returnControl = btn('<span>返回</span><span class="return-triangle" aria-hidden="true"></span>', `${character ? 'data-action="character-return"' : 'data-action="leave"'} aria-label="返回"`, 'page-return');
    return `<div class="game-shell ${character ? 'character-shell' : 'quiet-shell'}" ${character ? 'data-material-source="../../../textures/ui/yuxu_library_20261107/library_frame.png"' : 'data-skin="shell"'}>
      <header class="game-header"><div class="page-plaque" data-skin="${character ? 'titleCompact' : 'title'}"><h1>${character ? '人物' : '静室'}<span>·</span>${current[1]}</h1></div>${character && state.character === 'overview' ? '' : `<div class="header-identity"><strong>${D.character.name}</strong><span>${D.character.realm}　${character ? D.character.root : '元年三月十五'}</span></div>`}<div class="header-actions">${headerButtons}</div></header>
      ${returnControl}
      <aside class="side-rail" data-skin="panel"><nav aria-label="${character ? '角色分页' : '静室分页'}">${pages[app].map(([id, label, icon]) => btn(`${navIcon(icon)}<span>${label}</span>`, `data-page="${id}" aria-pressed="${state[app] === id}"`, 'nav-button ' + (state[app] === id ? 'is-selected' : ''))).join('')}</nav></aside>
      <div class="page-body" id="page-content">${body}</div><footer class="game-footer"><span>Esc 关闭浮层</span></footer></div>`;
  }

  function moodDial() {
    const moods = ['狂喜', '渴求', '焦灼', '惊惧', '悲恸', '暴怒', '怨恨', '绝望'];
    const labels = moods.map((label, index) => {
      const angle = (index * 45 - 90) * Math.PI / 180;
      return `<text x="${180 + Math.cos(angle) * 148}" y="${180 + Math.sin(angle) * 148 + 7}" text-anchor="middle">${label}</text>`;
    }).join('');
    return `<div class="mood-dial"><svg viewBox="0 0 360 360" aria-label="八种长期心绪圆盘"><defs><pattern id="moodPaper" patternUnits="userSpaceOnUse" width="360" height="360"><image href="../../../textures/ui/forging_20261021/material_panel_v3.png" x="0" y="0" width="360" height="360" preserveAspectRatio="xMidYMid slice"></image></pattern></defs><circle cx="180" cy="180" r="126" fill="url(#moodPaper)" stroke="#9a8a71" stroke-width="2"></circle><circle cx="180" cy="180" r="96" fill="none" stroke="#8a927c" stroke-width="1" opacity=".45"></circle><circle cx="180" cy="180" r="64" fill="none" stroke="#8a927c" stroke-width="1" opacity=".45"></circle>${moods.map((_, index) => { const angle = (index * 45 - 67.5) * Math.PI / 180; return `<path d="M180 180 L${180 + Math.cos(angle) * 126} ${180 + Math.sin(angle) * 126}" stroke="#9a8a71" opacity=".32"></path>`; }).join('')}${labels}</svg><span class="mood-point">${M.sprite('nodeSelected')}</span></div>`;
  }
  function worldStatuses() {
    const records = Array.isArray(D.worldStatuses) ? D.worldStatuses : [];
    const content = records.length ? `<div class="world-status-grid">${records.map(record => {
      const details = [record.effect, record.recovery, record.source ? `来源：${record.source}` : ''].filter(Boolean).join('\n');
      return `<article class="world-status-card" data-skin="paper" tabindex="0" ${tip(record.name, details)}>${M.sprite('node')}<div class="world-status-content"><h3>${E(record.name)}</h3>${record.effect ? `<p>${E(record.effect)}</p>` : ''}<div class="world-status-meta">${record.recovery ? `<span>${E(record.recovery)}</span>` : ''}${record.source ? `<span>来源：${E(record.source)}</span>` : ''}</div></div></article>`;
    }).join('')}</div>` : `<div class="empty-status">${M.sprite('node')}<span>暂无特殊状态</span></div>`;
    return M.scroll(content, 'world-status-scroll');
  }
  function reputationDetails() {
    const character = D.character;
    const breakdown = character.renownBreakdown || {};
    const renown = character.regionalRenown ?? '—';
    const karma = typeof character.karma === 'number' ? `${character.karma > 0 ? '+' : ''}${character.karma}` : '—';
    return `<div class="reputation-summary"><div class="reputation-heading"><h2>声望</h2><strong>百川 ${E(renown)}</strong></div><div class="reputation-breakdown"><span>玉虚基础声名<b>${E(breakdown.base ?? '—')}</b></span><span>身份倍率<b>${E(breakdown.identityMultiplier ?? '—')}</b></span><span>公开事迹<b>${E(breakdown.publicDeeds ?? '—')}</b></span></div></div>${M.divider('')}${rows([['业力', karma], ['所在', E(character.location ?? '未详')]])}`;
  }
  function overview() {
    return `<div class="overview-layout">${M.paper(`<div class="portrait-top"><span>形貌</span>${btn('查看立绘', 'data-action="portrait"', 'small-button')}</div>${portrait()}<div class="portrait-caption identity-caption"><strong>${E(D.character.name)}</strong><span class="identity-affiliation">${E(D.character.faction)} · ${E(D.character.identity)}</span><span class="identity-cultivation">${E(D.character.realm)} · ${E(D.character.root)}</span><span class="identity-demographics">男 · ${E(D.character.age)} · 人族</span></div>`, 'portrait-paper')}${M.paper(reputationDetails(), 'profile-facts-paper')}${M.paper(`<div class="section-head">${title('心境')}${M.iconButton('help', '心境说明', `data-action="mood"`)}</div><div class="mood-reading">${moodDial()}<div class="mood-caption"><h3>心绪平稳</h3></div></div>`, 'mood-paper')}${M.paper(`${title('当前状态')}${worldStatuses()}`, 'overview-status')}</div>`;
  }
  const proficiency = [['剑道', '武艺', '6.0'], ['刀道', '武艺', '5.0'], ['枪道', '武艺', '4.0'], ['体道', '武艺', '6.0'], ['术法', '术法', '6.0'], ['器道', '百工', '4.0'], ['符道', '百工', '2.0'], ['阵道', '百工', '3.0'], ['丹道', '百工', '4.0']];
  function attributes() {
    const basic = [['体魄', '8', '8（基础值[8]＝8）'], ['灵慧', '8', '8（基础值[8]＝8）'], ['神魂', '8', '8（基础值[8]＝8）'], ['福缘', '5', '5（基础值[5]＝5）'], ['悟性', '8', '8（基础值[8]＝8）']];
    const battle = [['生命', '100／100'], ['体力', '40／100'], ['法力', '20／20']].map(([name, value], index) => `<div class="resource-stat"><span>${name}</span><b>${value}</b>${M.progress([100, 40, 100][index], name, ['health', 'stamina', 'mana'][index])}</div>`).join('');
    return `<div class="attributes-layout">${M.paper(`${title('基础属性')}<div class="basic-stats">${basic.map(([name, value, detail], index) => `<div class="attribute-row" tabindex="0" ${tip(name, detail)} data-material-source="../../../textures/ui/yuxu_library_20261107/library_frame.png">${M.atlasView('../../../textures/ui/infopanel_attribute_icons_ink_v2.png', [1536, 1024], [index % 3 * 512, Math.floor(index / 3) * 512, 512, 512], 'attribute-icon')}<span>${name}</span><b>${value}</b></div>`).join('')}</div>${M.divider('社交属性')}${rows([['魅力', '5.0'], ['交际', '5.0'], ['治事', '5.0']])}`, 'attribute-paper')}${M.paper(`${title('战斗属性')}${battle}<div class="two-column-facts">${rows([['速度', '—'], ['灵识', '—'], ['物理防御', '—'], ['精准', '—'], ['身法', '—']])}${rows([['会心', '—'], ['护心', '—'], ['会心伤害', '—'], ['物理减伤', '—'], ['遁速', '—']])}</div>`, 'attribute-paper')}${M.paper(`${title('修行属性')}${rows([['境界', D.character.realm], ['灵根', D.character.root], ['灵力', '— 缕'], ['学习效率', '—／时'], ['修炼效率', '—／时']])}${M.divider('灵根权重')}<div class="root-weights">${['金', '木', '水', '火', '土'].map((name, index) => `<div><span>${name}</span>${M.progress([34, 33, 33, 0, 0][index], name)}<b>${[34, 33, 33, 0, 0][index]}</b></div>`).join('')}</div>`, 'attribute-paper')}${M.paper(`<div class="section-head">${title('造诣')}<div class="filter-tabs">${['全部', '武艺', '术法', '百工'].map(name => btn(name, `data-attribute-filter="${name}" aria-pressed="${state.attributeFilter === name}"`, 'small-button ' + (state.attributeFilter === name ? 'is-selected' : ''))).join('')}</div>${search('搜索造诣', 'proficiency')}</div><div class="proficiency-grid">${proficiency.map(([name, group, value]) => `<div data-filter-group="proficiency" data-query="${name}" data-category="${group}" ${state.attributeFilter !== '全部' && group !== state.attributeFilter ? 'hidden' : ''}><span>${name}</span><b>${value}</b></div>`).join('')}</div>`, 'proficiency-paper')}</div>`;
  }
  function skillLibrary() {
    return window.RoleBattle.render({ tip, modal, notify, hideTip });
  }
  function proficiencyPage() {
    {
      return `<div class="battle-layout node-layout rb-proficiency-page">${M.paper(`<div class="node-network">${D.skills.slice(0, 9).map((skill, index) => `<div class="network-place place-${index}">${btn(`${M.sprite(index === 0 ? 'nodeSelected' : 'node')}<span>${skill.name}</span>`, `data-select-skill="${index}" ${tip(skill.name, '查看当前节点的参悟条件与可习得招式。')}`, 'node-button')}</div>`).join('')}</div>`, 'node-map-paper')}${M.paper(`${title('参悟详情')}${state.skill === '' ? text('选择图谱节点，查看对应招式。', 'empty-copy') : `<h3>${D.skills[Number(state.skill)].name}</h3>${M.divider('来源')}${text(D.skills[Number(state.skill)].source)}${rows([['当前状态', '—'], ['参悟条件', '—']])}${btn('查看说明', `data-action="skill-detail"`, 'wide-button')}`}`, 'node-detail-paper')}</div>`;
    }
  }
  function manuals() {
    const book = D.books[state.book];
    return `<div class="manual-layout">${M.paper(`<div class="manual-loadout">${['主修', '辅修'].map((label, index) => btn(`${bookImage()}<span><small>${label}</small><strong>${D.books[index].name}</strong></span>${M.sprite('right')}`, `data-action="manual-picker"`, 'manual-equipped')).join('')}</div>`, 'manual-loadout-paper')}${M.paper(`<div class="section-head">${search('检索秘籍名、关键词', 'books')}${drop('bookKeyword', '秘籍关键词', ['全部关键词', '气血', '周天', '养形'], '全部关键词')}${btn('重置', 'data-reset-filter="books"', 'small-button')}</div>`, 'manual-filter-paper')}${M.paper(`${title('秘籍库')}${M.scroll(D.books.map((entry, index) => btn(`${bookImage()}<span><strong>${entry.name}</strong><small>${index === 0 ? '主修' : index === 1 ? '辅修' : '未装配'}</small></span>`, `data-select-book="${index}" data-filter-group="books" data-query="${entry.name}" aria-pressed="${index === state.book}"`, 'item-row ' + (index === state.book ? 'is-selected' : ''))).join(''))}`, 'manual-library-paper')}${M.paper(`<div class="manual-heading">${bookImage('large-book')}<div><h2>${book.name}</h2><span class="muted">卷章目录 · 修持与化法</span></div>${btn('设为主修', 'data-action="equip-manual"', 'small-button')}${btn('设为辅修', 'data-action="equip-manual"', 'small-button')}</div>${M.divider('心法')}${btn(`心法 · ${book.name}`, `data-action="heart" ${tip(book.name, '主修与辅修心法按实际装配生效。')}`, 'heart-button')}${M.scroll(book.chapters.map((chapter, index) => `<div class="chapter-row"><span class="chapter-order">${['一', '二', '三', '四', '五', '六'][index] || index + 1}</span><div><strong>${chapter}</strong><span>第${['一', '二', '三', '四', '五', '六'][index] || index + 1}卷</span></div><div class="chapter-branches">${btn('修持', `data-action="chapter"`, 'small-button ' + (index === 0 ? 'is-selected' : ''))}${btn('化法', 'data-action="chapter"', 'small-button')}${btn('习得', 'data-action="chapter"', 'small-button')}</div></div>`).join(''), 'chapter-scroll')}`, 'manual-detail-paper')}</div>`;
  }
  function equipment() {
    const slots = [['手持', '武器', 1], ['衣甲', '甲或袍', 3], ['其他护具', '非甲袍类护具', 3], ['衣着', '外观衣着', 3]];
    return `<div class="equipment-layout">${M.paper(`${title('穿戴')}${portrait('equipment-portrait')}<div class="equipment-slots">${slots.map(([name, sub, icon], index) => btn(`${navIcon(icon)}<strong>${name}</strong><span>${sub}</span><small>未选择</small>`, `data-equip-slot="${index}" aria-label="选择${name}"`, 'equipment-slot slot-' + index)).join('')}</div>`, 'equipment-figure-paper')}${M.paper(`${title('穿戴汇总')}${rows([['生命上限', '—'], ['体魄', '—'], ['神魂', '—'], ['物理防御', '—'], ['护心', '—']])}${M.divider('器物')}${text('当前没有配置器物。', 'empty-copy')}${btn('查看器物', 'data-action="artifact"', 'wide-button')}`, 'equipment-totals-paper')}${M.paper(`${title('器蕴与关联')}${text('选择已有器物，查看其关联。', 'muted')}<div class="empty-illustration">${navIcon(3)}<span>暂无器物记录</span></div>`, 'equipment-bonds-paper')}</div>`;
  }
  function relations() {
    return `<div class="relations-layout">${M.paper(`${title('人物名录')}${search('查找人物', 'people')}<div class="filter-tabs">${btn('全部', 'data-relation-filter="全部"', 'is-selected')}${btn('亲近', 'data-relation-filter="亲近"')}</div><div class="empty-illustration">${navIcon(5)}<span>尚无往来人物</span></div>`, 'people-paper')}${M.paper(`${title('关系与往来')}${rows([['当前人物', '未选择'], ['亲近', '—'], ['信任', '—']])}${M.divider('往来纪事')}<div class="relations-empty"><img src="../../../textures/ui/character_20261012/relations_bridge_v1.png" alt="已有山水桥景素材"><p>日久识人，行中结缘。</p><span class="muted">暂无往来纪事</span></div>`, 'relation-detail-paper')}</div>`;
  }
  function modeFields() {
    if (state.mode === '指定天数') return `<div class="number-line"><label>修炼时长</label>${stepper('days', '修炼天数')}<span>日</span></div>`;
    if (state.mode === '指定灵力') return `<div class="number-line"><label>目标灵力</label>${stepper('strands', '目标灵力')}<span class="spirit-unit">缕</span></div>`;
    if (state.mode === '指定日期') return `<span class="field-caption">结束日期</span><div class="date-line">${stepper('year', '年', 1, 9999)}<span>年</span>${stepper('month', '月', 1, 12)}<span>月</span>${stepper('date', '日', 1, 30)}<span>日</span></div>${btn('按最近应邀日期修炼', 'data-action="invitation"', 'wide-button')}`;
    return rows([['下层所需', '— 缕'], ['预计时长', '—']]);
  }
  function practice() {
    return `<div class="practice-layout"><div class="practice-scenery">${M.paper(`${title('吐纳修行')}`, 'scenery-heading')}<div class="scenery-space"></div>${M.paper(`<div class="section-head">${title('当前灵力')}<strong class="power-number spirit-value">— <small>缕</small></strong></div>${M.progress(35, '灵力积累', 'spirit')}${rows([['修炼速率', '— 缕／时']])}`, 'practice-summary')}</div>${M.paper(`${title('本次修炼')}<div class="mode-grid">${['指定灵力', '指定天数', '指定日期', '下层所需'].map(name => btn(name, `data-mode="${name}" aria-pressed="${state.mode === name}"`, state.mode === name ? 'is-selected' : '')).join('')}</div><div class="mode-fields">${modeFields()}</div>${M.divider('修炼安排')}${rows([['当前体力', '40／100'], ['预期结束', '—']])}<div class="practice-actions-bottom">${btn('查看归元九息', 'data-action="nine-breaths"', 'wide-button')}${btn('开始修炼', 'data-action="practice-confirm"', 'primary wide-button')}</div>`, 'practice-action-paper')}</div>`;
  }

  function study() {
    const book = D.books[state.studyBook];
    return `<div class="study-layout">${M.paper(`${title('可研读功法')}${M.scroll(D.books.map((entry, index) => btn(`${bookImage()}<span><strong>${entry.name}</strong><small>${index === state.studyBook ? '当前查看' : '查看卷章'}</small></span>`, `data-study-book="${index}" aria-pressed="${index === state.studyBook}"`, 'item-row ' + (index === state.studyBook ? 'is-selected' : ''))).join(''))}`, 'study-list-paper')}${M.paper(`<div class="manual-heading">${bookImage('large-book')}<div><h2>${book.name}</h2><span class="muted">功法研读</span></div></div>${M.divider('本卷研读')}<h3>第一卷 · ${book.chapters[0] || '卷章'}</h3>${M.progress(28, '本卷研读进度')}${rows([['研读进度', '—'], ['本卷条件', '首卷可直接研读']])}${M.divider('卷章目录')}<div class="study-chapters">${book.chapters.map((name, index) => btn(`<small>第${['一', '二', '三', '四'][index] || index + 1}卷</small><span>${name}</span>`, `data-action="study-chapter"`, 'is-selected-' + (index === 0))).join('')}</div><div class="study-bottom">${btn('研读一日', 'data-action="study-confirm"', 'primary')}</div>`, 'study-detail-paper')}</div>`;
  }
  function breakthrough() {
    return `<div class="breakthrough-layout"><div class="breakthrough-scenery">${M.paper(`${title('闭关突破')}`, 'scenery-heading')}<div class="scenery-space"></div>${M.paper(`${title(D.character.realm)}${M.progress(35, '当前灵力', 'spirit')}${rows([['当前灵力', '—'], ['下层所需', '—']])}`, 'breakthrough-summary')}</div>${M.paper(`${title('闭关准备')}${rows([['当前境界', D.character.realm], ['当前体力', '40／100']])}${M.divider('突破条件')}<div class="condition-line">${M.sprite('radioOff')}<span>灵力达到本层所需</span><b>—</b></div><div class="condition-line">${M.sprite('radioOff')}<span>满足当前突破条件</span><b>—</b></div>${M.divider('当前安排')}${text('确认准备后，方可闭关突破。', 'muted')}<div class="push-bottom">${btn('闭关突破', 'data-action="breakthrough-confirm"', 'primary wide-button')}</div>`, 'breakthrough-action-paper')}</div>`;
  }
  function materialBook() {
    const glyphNames = ['checkOff', 'checkOn', 'radioOff', 'radioOn', 'left', 'right', 'up', 'down', 'search', 'close', 'help', 'reset', 'node', 'nodeSelected', 'grip'];
    return `<div class="material-book" data-skin="shell"><div class="material-book-header" data-skin="title"><h1>公共控件素材册</h1><span>本样稿实际使用的素材与状态</span></div><div class="material-book-grid">${M.paper(`${title('按钮分型')}<div class="button-family-demos">${btn('普通操作')}${btn('主要操作', '', 'primary')}${btn('短页签', '', 'small-button')}${btn('侧栏分页', '', 'nav-button')}${M.iconButton('help', '图标底座示例')}${stepper('days', '分型输入示例')}</div>${text('按用途分别制作底图，数字增减只用三角。', 'muted')}`, 'material-demo')}${M.paper(`${title('勾选与单选')}${checkbox('显示详细信息', 'sample', state.details)}<div class="radio-samples">${[0, 1].map((value, index) => `<label class="check-control" data-material-source="assets/控件补充图集_v1.png"><input type="radio" name="sample-radio" ${index === 0 ? 'checked' : ''}><span class="radio-off">${M.sprite('radioOff')}</span><span class="radio-on">${M.sprite('radioOn')}</span><span>${index === 0 ? '按天数' : '按日期'}</span></label>`).join('')}</div>${text('方框、圆钮与勾选标记均引用图集。', 'muted')}`, 'material-demo')}${M.paper(`${title('输入与选择')}${search('查找内容', 'sample')}<div class="number-line"><span>时长</span>${stepper('days', '示例天数')}<span>日</span></div>${drop('sampleChoice', '示例选择', ['选项一', '选项二', '选项三'], '选项一')}`, 'material-demo')}${M.paper(`${title('进度与滚动')}${M.progress(64, '素材示例进度')}${M.scroll(Array.from({ length: 14 }, (_, index) => btn(`清册条目 ${index + 1}`, 'data-action="sample"', 'wide-button')).join(''), 'material-scroll')}`, 'material-demo')}${M.paper(`${title('图标与节点')}<div class="glyph-grid">${glyphNames.map(name => `<span>${M.sprite(name)}</span>`).join('')}</div>`, 'material-demo')}${M.paper(`${title('提示与确认')}${btn('查看深墨提示', 'data-action="sample-tooltip"', 'wide-button')}${btn('查看确认弹窗', 'data-action="sample-dialog"', 'primary wide-button')}${M.divider('素材覆盖')}${text('页框、题签、导航、按钮、输入、下拉、勾选、单选、进度、滚动、节点、槽位、提示与弹窗。', 'muted')}`, 'material-demo')}</div></div>`;
  }
  const renderers = { overview, attributes, battle: skillLibrary, manuals, proficiency: proficiencyPage, equipment, relations, practice, study, breakthrough };
  function fit() {
    const viewport = document.querySelector('#viewport');
    viewport.dataset.previewApp = state.app;
    const scale = Math.min((viewport.clientWidth - (state.app === 'character' ? 0 : 24)) / 1672, (viewport.clientHeight - (state.app === 'character' ? 0 : 16)) / 941);
    stage.style.width = state.app === 'character' ? `${viewport.clientWidth / Math.max(.25, scale)}px` : '1672px';
    stage.style.transform = `scale(${Math.max(.25, scale)})`;
    document.documentElement.style.setProperty('--stage-scale', Math.max(.25, scale));
  }
  function render() {
    hideTip();
    window.RoleBattle.suspend();
    const currentShell = stage.querySelector('.game-shell');
    const sameApp = state.app !== 'materials' && currentShell?.classList.contains(state.app + '-shell');
    const content = state.app === 'materials' ? materialBook() : shell(renderers[state[state.app]]());
    if (sameApp) {
      const next = document.createElement('template');
      next.innerHTML = content;
      currentShell.querySelector('.game-header').replaceWith(next.content.querySelector('.game-header'));
      currentShell.querySelector('.page-body').replaceWith(next.content.querySelector('.page-body'));
      currentShell.querySelector(':scope > .page-return')?.remove();
      const nextReturn = next.content.querySelector('.game-shell > .page-return');
      if (nextReturn) currentShell.append(nextReturn);
      currentShell.querySelectorAll('.side-rail [data-page]').forEach(button => {
        const selected = button.dataset.page === state[state.app];
        button.classList.toggle('is-selected', selected);
        button.setAttribute('aria-pressed', String(selected));
      });
    } else {
      stage.innerHTML = content;
    }
    document.querySelectorAll('[data-preview]').forEach(button => {
      button.classList.toggle('is-selected', button.dataset.preview === state.app);
      button.setAttribute('aria-pressed', String(button.dataset.preview === state.app));
    });
    M.apply(document); M.attachScroll(stage); fit(); window.RoleBattle.mount(stage);
    stage.querySelectorAll('[data-search]').forEach(input => {
      input.value = state.queries[input.dataset.search] || '';
      input.dispatchEvent(new Event('input', { bubbles: true }));
    });
    history.replaceState(null, '', `#${state.app}/${state.app === 'materials' ? 'controls' : state[state.app]}`);
  }
  let toastTimer;
  function notify(message) { const toast = document.querySelector('#toast'); toast.textContent = message; delete toast.dataset.skinned; M.apply(toast); toast.hidden = false; clearTimeout(toastTimer); toastTimer = setTimeout(() => { toast.hidden = true; }, 3000); }
  function modal(titleText, content, footer = '') {
    hideTip();
    document.querySelectorAll('dialog').forEach(dialog => dialog.close());
    const trigger = document.activeElement;
    const host = document.querySelector('#overlay-root');
    host.innerHTML = `<dialog class="preview-dialog" aria-label="${E(titleText)}" data-skin="panel"><div class="dialog-heading"><h2>${titleText}</h2>${M.iconButton('close', '关闭弹窗', 'data-close-modal')}</div><div class="dialog-content">${content}</div><div class="dialog-footer">${footer || btn('返回', 'data-close-modal')}</div></dialog>`;
    const dialog = host.querySelector('dialog'); M.apply(dialog); M.attachScroll(dialog);
    dialog.addEventListener('close', () => { if (trigger?.isConnected) trigger.focus(); });
    dialog.addEventListener('click', event => { const bounds = dialog.getBoundingClientRect(); if (event.target === dialog && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) dialog.close(); });
    dialog.showModal();
  }
  function confirm(titleText, arrangement) {
    modal(titleText, `${text(arrangement, 'confirmation-copy')}${M.divider('本次用度')}${rows([['所需时间', '—'], ['其他用度', '—']])}`, btn('返回', 'data-close-modal') + btn('确认', 'data-preview-confirm', 'primary'));
  }
  const tooltip = document.querySelector('#tooltip');
  let activeTip;
  function hideTip() { tooltip.hidden = true; activeTip = null; }
  function showTip(anchor, content) {
    activeTip = anchor;
    tooltip.classList.toggle('rb-card-tooltip', anchor.matches('[data-rb-source],[data-rb-placement]'));
    tooltip.innerHTML = `<h3>${E(content.title)}</h3>${M.divider('')}<p>${E(content.text).replace(/\n/g, '<br>')}</p>`;
    delete tooltip.dataset.skinned;
    M.apply(tooltip); tooltip.hidden = false;
    const anchorBox = anchor.getBoundingClientRect(), box = tooltip.getBoundingClientRect();
    let left = anchorBox.right + 12, top = anchorBox.top;
    if (left + box.width > innerWidth - 12) left = anchorBox.left - box.width - 12;
    left = Math.max(12, Math.min(left, innerWidth - box.width - 12));
    top = Math.max(12, Math.min(top, innerHeight - box.height - 12));
    tooltip.style.left = `${left}px`; tooltip.style.top = `${top}px`;
  }
  document.addEventListener('pointerover', event => { const anchor = event.target.closest('[data-tip]'); if (anchor && anchor !== activeTip) showTip(anchor, tooltipContent.get(anchor.dataset.tip)); else if (!anchor && !tooltip.contains(event.target)) hideTip(); });
  document.addEventListener('focusin', event => { const anchor = event.target.closest('[data-tip]'); if (anchor) showTip(anchor, tooltipContent.get(anchor.dataset.tip)); });
  document.addEventListener('focusout', () => hideTip());
  document.addEventListener('click', event => {
    const target = event.target.closest('button');
    if (!target || target.disabled) return;
    if (target.dataset.preview) { state.app = target.dataset.preview; render(); return; }
    if (target.dataset.page) { state[state.app] = target.dataset.page; render(); return; }
    if (target.dataset.mode) { state.mode = target.dataset.mode; render(); return; }
    if (target.dataset.step) {
      const name = target.dataset.step, input = stage.querySelector(`[data-number="${name}"]`);
      state.inputs[name] = Math.max(Number(input.min), Math.min(Number(input.max), Number(input.value) + Number(target.dataset.delta)));
      input.value = state.inputs[name]; return;
    }
    if (target.dataset.attributeFilter) { state.attributeFilter = target.dataset.attributeFilter; render(); return; }
    if (target.hasAttribute('data-select-book')) { state.book = Number(target.dataset.selectBook); render(); return; }
    if (target.hasAttribute('data-study-book')) { state.studyBook = Number(target.dataset.studyBook); render(); return; }
    if (target.hasAttribute('data-select-skill')) { state.skill = target.dataset.selectSkill; render(); return; }
    if (target.dataset.relationFilter) { target.parentElement.querySelectorAll('button').forEach(button => button.classList.toggle('is-selected', button === target)); return; }
    if (target.hasAttribute('data-close-modal')) { target.closest('dialog').close(); return; }
    if (target.hasAttribute('data-preview-confirm')) { target.closest('dialog').close(); notify('已查看确认样式。当前是独立界面样稿。'); return; }
    if (target.hasAttribute('data-slot')) { modal('装配位', `${text('此处可查看与调整兽战装配。', 'muted')}${btn('卸下', 'data-preview-confirm', 'wide-button')}`); return; }
    if (target.hasAttribute('data-equip-slot')) { modal('选择穿戴', `${text(['手持武器', '衣甲 · 甲或袍', '其他护具 · 非甲袍', '外观衣着'][Number(target.dataset.equipSlot)], 'confirmation-copy')}<div class="equipment-choice">${navIcon(3)}<span>暂无选中的实物</span></div>`, btn('返回', 'data-close-modal') + btn('查看空位', 'data-preview-confirm', 'primary')); return; }
    if (target.dataset.clearSearch) { const input = stage.querySelector(`[data-search="${target.dataset.clearSearch}"]`); input.value = ''; input.dispatchEvent(new Event('input', { bubbles: true })); input.focus(); return; }
    if (target.dataset.resetFilter) { state.queries[target.dataset.resetFilter] = ''; if (target.dataset.resetFilter === 'skills') state.options.skillSource = '全部来源'; if (target.dataset.resetFilter === 'books') state.options.bookKeyword = '全部关键词'; render(); return; }
    if (target.dataset.menu) {
      const name = target.dataset.menu, menu = menus.get(name);
      const selected = state.options[name] ?? menu.defaultValue;
      modal(menu.label, `<div class="choice-list" role="listbox" aria-label="${menu.label}">${menu.options.map((option, index) => btn(`${M.sprite(selected === option ? 'radioOn' : 'radioOff')}<span>${E(option)}</span>`, `data-choice-menu="${name}" data-choice-index="${index}" role="option" aria-selected="${selected === option}"`, 'choice-option')).join('')}</div>`); return;
    }
    if (target.dataset.choiceMenu) {
      const menu = menus.get(target.dataset.choiceMenu); state.options[target.dataset.choiceMenu] = menu.options[Number(target.dataset.choiceIndex)]; target.closest('dialog').close(); render();
      if (target.dataset.choiceMenu === 'skillSource') {
        const source = state.options.skillSource;
        stage.querySelectorAll('[data-filter-group="skills"]').forEach((row, index) => row.hidden = source !== '全部来源' && D.skills[index].source !== source);
      }
      return;
    }
    const action = target.dataset.action;
    if (!action) return;
    if (action === 'mood') return modal('心境', `${moodDial()}${M.divider('最近持久变化')}${text('暂无记录。', 'muted')}`);
    if (action === 'portrait') return modal('形貌', portrait('portrait-modal'));
    if (action === 'character-return') return modal('返回', text('返回当前场景。', 'confirmation-copy'), btn('留在此页', 'data-close-modal') + btn('返回', 'data-preview-confirm', 'primary'));
    if (action === 'practice-confirm') return confirm('开始修炼', state.mode === '指定天数' ? `本次安排：修炼 ${state.inputs.days} 日。` : `本次安排：${state.mode}。`);
    if (action === 'study-confirm') return confirm('研读一日', `研读《${D.books[state.studyBook].name}》。`);
    if (action === 'breakthrough-confirm') return confirm('闭关突破', '静候突破，确认后进入闭关。');
    if (action === 'sample-dialog') return confirm('确认安排', '纸面、木边、用度与操作分层。');
    if (action === 'sample-tooltip') return showTip(target, { title: '深墨悬停', text: '完整展开，米白字与细角边。\n主悬停不设内部滚动条。' });
    if (action === 'board') return modal('人类招式盘', `${text('人类对战在战前及每轮结束配置招式盘。', 'confirmation-copy')}${text('在此查看当前招式盘。', 'muted')}${M.divider('当前装配')}${text('—', 'empty-copy')}`);
    if (action === 'nine-breaths') return modal('归元九息', `${text('查看当前法门与修行安排。', 'confirmation-copy')}${rows([['当前状态', '—'], ['所需时长', '—']])}`);
    if (action === 'leave') return modal('离开静室', text('离开当前静室。', 'confirmation-copy'), btn('返回', 'data-close-modal') + btn('确认', 'data-preview-confirm', 'primary'));
    const names = { inventory: '行囊入口', 'manual-picker': '主辅修装配', 'equip-manual': '装配秘籍', heart: '心法', chapter: '卷章详情', 'study-chapter': '卷章目录', artifact: '器物', rewrite: '兽战化法', invitation: '应邀日期', 'skill-detail': '招式详情', sample: '清册条目' };
    modal(names[action] || '查看', text('暂无可查看的记录。', 'empty-copy'));
  });
  document.addEventListener('input', event => {
    if (event.target.dataset.number) {
      const input = event.target; if (input.value !== '') state.inputs[input.dataset.number] = Math.max(Number(input.min), Math.min(Number(input.max), Number(input.value)));
    }
    if (event.target.dataset.search) {
      const input = event.target, query = input.value.trim().toLowerCase();
      state.queries[input.dataset.search] = input.value;
      stage.querySelectorAll(`[data-filter-group="${input.dataset.search}"]`).forEach((item, index) => { const category = state.attributeFilter === '全部' || input.dataset.search !== 'proficiency' || item.dataset.category === state.attributeFilter; const source = input.dataset.search !== 'skills' || !state.options.skillSource || state.options.skillSource === '全部来源' || D.skills[index].source === state.options.skillSource; item.hidden = !category || !source || !item.dataset.query.toLowerCase().includes(query); });
    }
  });
  document.addEventListener('change', event => { if (event.target.dataset.check === 'sample') state.details = event.target.checked; });
  document.addEventListener('keydown', event => { if (event.key === 'Escape') hideTip(); });
  document.querySelector('#reset-preview').addEventListener('click', () => { state.options = {}; state.queries = {}; state.details = false; state.mode = '指定天数'; state.inputs = { days: 5, strands: 1, year: 1, month: 3, date: 20 }; state.attributeFilter = '全部'; state.skill = ''; document.querySelectorAll('dialog').forEach(dialog => dialog.close()); render(); notify('已重置当前样稿。'); });
  document.querySelector('#reset-preview').addEventListener('click', () => { window.RoleBattle.reset(); if (state.app === 'character' && state.character === 'battle') render(); });
  window.addEventListener('resize', () => { hideTip(); fit(); });
  function fromHash() { const [app, page] = location.hash.slice(1).split('/'); if (['character', 'quiet', 'materials'].includes(app)) state.app = app; if (pages[state.app]?.some(item => item[0] === page)) state[state.app] = page; }
  window.addEventListener('hashchange', () => { fromHash(); render(); });
  fromHash(); render();
  window.PREVIEW_READY = document.fonts.load('22px ZhenKai', '纸墨木石人物静室').then(() => document.fonts.ready).then(() => { fit(); return true; });
})();
