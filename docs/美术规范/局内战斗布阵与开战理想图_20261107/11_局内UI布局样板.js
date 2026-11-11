/* 独立布局样板：只保存页面内的示意状态，不读取存档或执行战斗结算。 */
(() => {
  'use strict';

  const iconRoot = '../九格招式盘与战斗界面_20261106/';
  const items = {
    yingdi: { name: '应敌', category: 'skill', width: 1, height: 1, image: `${iconRoot}招式样张_应敌_1x1.png` },
    jiazhao: { name: '架招', category: 'skill', width: 1, height: 1, placeholder: '架' },
    tongji: { name: '痛击', category: 'skill', width: 1, height: 2, image: `${iconRoot}招式样张_痛击_1x2.png` },
    cuixue: { name: '催血', category: 'skill', width: 1, height: 1, placeholder: '血', warm: true },
    juqi: { name: '聚炁', category: 'skill', width: 1, height: 1, placeholder: '炁' },
    lingji: { name: '灵击', category: 'skill', width: 2, height: 1, placeholder: '灵' },
    huichun: { name: '回春丹', category: 'pill', width: 1, height: 2, image: '../../../textures/items/inventory_density_20261029/pill_container_empty_porcelain.png' }
  };
  const sourceOrder = ['yingdi', 'jiazhao', 'tongji', 'cuixue', 'juqi', 'lingji', 'huichun'];
  const presets = {
    one: [
      { id: 'yingdi', x: 0, y: 0 }, { id: 'jiazhao', x: 1, y: 0 },
      { id: 'tongji', x: 0, y: 1 }, { id: 'juqi', x: 1, y: 1 }
    ],
    two: [
      { id: 'lingji', x: 0, y: 0 }, { id: 'cuixue', x: 2, y: 0 },
      { id: 'jiazhao', x: 0, y: 1 }, { id: 'tongji', x: 1, y: 1 }
    ]
  };
  const copy = value => JSON.parse(JSON.stringify(value));
  const state = {
    mode: 'edit', category: 'skill', search: '', selected: null, selectedSource: null,
    scheme: 'one', board: copy(presets.one), roundComplete: false, paused: false, speed: 1,
    sourcePage: 'items'
  };

  const $ = id => document.getElementById(id);
  const battle = $('battle');
  const board = $('boardGrid');
  const list = $('sourceList');
  const tooltip = $('tooltip');
  const feedback = $('boardFeedback');
  const phase = $('phaseNote');
  const sourcePanel = $('sourcePanel');
  const sourceView = $('sourceView');
  const schemeView = $('schemeView');

  function visual(item) {
    const wrap = document.createElement('span');
    wrap.className = 'piece-visual';
    wrap.style.setProperty('--piece-w', item.width);
    wrap.style.setProperty('--piece-h', item.height);
    wrap.dataset.shape = `${item.width}x${item.height}`;
    appendArt(wrap, item);
    appendShapeLine(wrap, item);
    return wrap;
  }

  function appendArt(element, item) {
    let art;
    if (item.image) {
      art = document.createElement('img');
      art.src = item.image;
      art.alt = '';
      art.draggable = false;
      art.className = 'move-art';
    } else {
      art = document.createElement('span');
      art.className = `move-art placeholder-icon${item.warm ? ' placeholder-icon--warm' : ''}`;
      art.textContent = item.placeholder;
      art.setAttribute('aria-hidden', 'true');
    }
    element.appendChild(art);
  }

  function appendShapeLine(element, item) {
    if (item.width === 2) {
      const line = document.createElement('span');
      line.className = 'shape-line shape-line--vertical';
      line.setAttribute('aria-hidden', 'true');
      element.appendChild(line);
    }
    if (item.height === 2) {
      const line = document.createElement('span');
      line.className = 'shape-line shape-line--horizontal';
      line.setAttribute('aria-hidden', 'true');
      element.appendChild(line);
    }
  }

  function setFeedback(message) {
    feedback.textContent = message;
  }

  function renderSource() {
    list.replaceChildren();
    const query = state.search.trim().toLocaleLowerCase();
    const ids = sourceOrder.filter(id => items[id].category === state.category &&
      (!query || items[id].name.toLocaleLowerCase().includes(query)));
    if (!ids.length) {
      const empty = document.createElement('p');
      empty.className = 'source-empty';
      empty.textContent = query ? '没有匹配项' : '暂无示例';
      list.appendChild(empty);
      return;
    }
    for (const id of ids) {
      const item = items[id];
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'source-item';
      button.dataset.move = id;
      button.dataset.shape = `${item.width}x${item.height}`;
      button.style.setProperty('--piece-h', item.height);
      button.setAttribute('aria-pressed', String(state.selectedSource === id));
      button.setAttribute('aria-label', `${item.name}，${item.width}乘${item.height}格，选择后可放入空格`);
      button.draggable = true;
      const visualBox = document.createElement('span');
      visualBox.className = 'source-visual';
      visualBox.appendChild(visual(item));
      const label = document.createElement('span');
      label.className = 'source-copy';
      const name = document.createElement('span');
      name.className = 'source-name';
      name.textContent = item.name;
      const meta = document.createElement('span');
      meta.className = 'source-meta';
      meta.textContent = `${item.width} × ${item.height}${item.placeholder ? ' · 图形示意' : ''}`;
      label.append(name, meta);
      button.append(visualBox, label);
      list.appendChild(button);
    }
  }

  function renderBoard() {
    board.replaceChildren();
    for (let y = 0; y < 3; y += 1) {
      for (let x = 0; x < 3; x += 1) {
        const cell = document.createElement('button');
        cell.type = 'button';
        cell.className = 'grid-cell';
        cell.dataset.x = String(x);
        cell.dataset.y = String(y);
        cell.style.gridColumn = String(x + 1);
        cell.style.gridRow = String(y + 1);
        cell.setAttribute('aria-label', `第${y + 1}行，第${x + 1}格`);
        cell.tabIndex = state.mode === 'edit' ? 0 : -1;
        board.appendChild(cell);
      }
    }
    state.board.forEach((piece, index) => {
      const item = items[piece.id];
      if (!item) return;
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'board-piece';
      button.dataset.boardIndex = String(index);
      button.dataset.move = piece.id;
      button.dataset.shape = `${item.width}x${item.height}`;
      button.style.gridColumn = `${piece.x + 1} / span ${item.width}`;
      button.style.gridRow = `${piece.y + 1} / span ${item.height}`;
      button.setAttribute('aria-label', `${item.name}，占${item.width}乘${item.height}格${state.mode === 'edit' ? '，可选择移动' : ''}`);
      button.setAttribute('aria-pressed', String(state.selected?.kind === 'board' && state.selected.index === index));
      button.draggable = state.mode === 'edit';
      appendArt(button, item);
      appendShapeLine(button, item);
      board.appendChild(button);
    });
  }

  function showPage(page) {
    state.sourcePage = page;
    sourceView.hidden = page !== 'items';
    schemeView.hidden = page !== 'schemes';
    hideTooltip();
  }

  function renderMode() {
    battle.dataset.mode = state.mode;
    sourcePanel.hidden = state.mode !== 'edit';
    $('battleControls').hidden = state.mode !== 'combat';
    $('editButton').disabled = !state.roundComplete;
    $('roundButton').hidden = state.roundComplete;
    $('boardCaption').textContent = state.mode === 'edit' ? '布阵中' : '战斗中';
    phase.textContent = state.mode === 'edit' ? '布阵中' : (state.roundComplete ? '共同回合结束' : '自动战斗');
    if (state.mode === 'combat') showPage('items');
    renderBoard();
  }

  function selectSource(id) {
    if (state.mode !== 'edit' || !items[id]) return;
    state.selected = { kind: 'source', id };
    state.selectedSource = id;
    setFeedback(`已选${items[id].name}，点空格摆放`);
    renderSource();
    renderBoard();
    board.querySelector('.grid-cell')?.focus();
  }

  function selectBoard(index) {
    const piece = state.board[index];
    if (state.mode !== 'edit' || !piece) return;
    if (state.selected?.kind === 'source') {
      setFeedback('目标位置已有对象');
      return;
    }
    state.selected = { kind: 'board', index, id: piece.id };
    state.selectedSource = null;
    setFeedback(`已选${items[piece.id].name}，点空格移动`);
    renderSource();
    renderBoard();
    board.querySelector(`[data-board-index="${index}"]`)?.focus();
  }

  function covers(piece, x, y) {
    const item = items[piece.id];
    return x >= piece.x && x < piece.x + item.width && y >= piece.y && y < piece.y + item.height;
  }

  function attemptPlacement(x, y, selection = state.selected) {
    if (state.mode !== 'edit' || !selection || !items[selection.id]) return;
    const item = items[selection.id];
    if (x < 0 || y < 0 || x + item.width > 3 || y + item.height > 3) {
      setFeedback('放置超出盘面');
      return;
    }
    for (let yy = y; yy < y + item.height; yy += 1) {
      for (let xx = x; xx < x + item.width; xx += 1) {
        if (state.board.some((piece, index) => !(selection.kind === 'board' && index === selection.index) && covers(piece, xx, yy))) {
          setFeedback('目标位置已有对象');
          return;
        }
      }
    }
    if (selection.kind === 'board') {
      const piece = state.board[selection.index];
      if (!piece || piece.id !== selection.id) return;
      piece.x = x;
      piece.y = y;
    } else {
      state.board.push({ id: selection.id, x, y });
    }
    state.selected = null;
    state.selectedSource = null;
    state.scheme = 'custom';
    setFeedback(`${item.name}已放入`);
    renderSource();
    renderBoard();
    board.querySelector('.grid-cell')?.focus();
  }

  function tooltipContent(node) {
    if (node.dataset.status) {
      const title = node.dataset.status === 'shield' ? '护盾' : '气血';
      return { title, description: '状态位置示意；实际状态与数值由战斗读取。' };
    }
    const item = items[node.dataset.move];
    if (item) return { title: item.name, description: `占格 ${item.width} × ${item.height}。效果、费用与使用条件以实际数据为准。${item.placeholder ? '当前图形为示意。' : ''}` };
    if (node.id === 'handSlot') return { title: '手持槽', description: '手持器物位置示意；具体效果以实际装备为准。' };
    return null;
  }

  function showTooltip(node) {
    const content = tooltipContent(node);
    if (!content) return;
    const title = document.createElement('strong');
    title.textContent = content.title;
    const description = document.createElement('p');
    description.textContent = content.description;
    tooltip.replaceChildren(title, description);
    tooltip.hidden = false;
    const rect = node.getBoundingClientRect();
    const tip = tooltip.getBoundingClientRect();
    let left = rect.right + 10;
    if (left + tip.width > innerWidth - 10) left = rect.left - tip.width - 10;
    tooltip.style.left = `${Math.max(10, left)}px`;
    tooltip.style.top = `${Math.max(10, Math.min(rect.top, innerHeight - tip.height - 10))}px`;
  }

  function hideTooltip() { tooltip.hidden = true; }

  list.addEventListener('click', event => {
    const button = event.target.closest('.source-item');
    if (button) selectSource(button.dataset.move);
  });
  board.addEventListener('click', event => {
    const piece = event.target.closest('.board-piece');
    if (piece) { selectBoard(Number(piece.dataset.boardIndex)); return; }
    const cell = event.target.closest('.grid-cell');
    if (cell) attemptPlacement(Number(cell.dataset.x), Number(cell.dataset.y));
  });
  list.addEventListener('dragstart', event => {
    const button = event.target.closest('.source-item');
    if (!button) return;
    state.selected = { kind: 'source', id: button.dataset.move };
    state.selectedSource = button.dataset.move;
    event.dataTransfer.setData('text/plain', button.dataset.move);
    event.dataTransfer.effectAllowed = 'copy';
    hideTooltip();
  });
  board.addEventListener('dragstart', event => {
    const button = event.target.closest('.board-piece');
    if (!button || state.mode !== 'edit') return;
    const index = Number(button.dataset.boardIndex);
    state.selected = { kind: 'board', index, id: button.dataset.move };
    state.selectedSource = null;
    event.dataTransfer.setData('text/plain', button.dataset.move);
    event.dataTransfer.effectAllowed = 'move';
    hideTooltip();
  });
  board.addEventListener('dragover', event => {
    if (state.mode === 'edit' && event.target.closest('.grid-cell')) event.preventDefault();
  });
  board.addEventListener('drop', event => {
    const cell = event.target.closest('.grid-cell');
    if (!cell) return;
    event.preventDefault();
    attemptPlacement(Number(cell.dataset.x), Number(cell.dataset.y));
  });

  $('category').addEventListener('change', event => {
    state.category = event.target.value;
    state.selected = null;
    state.selectedSource = null;
    hideTooltip();
    renderSource();
    renderBoard();
  });
  $('search').addEventListener('input', event => {
    state.search = event.target.value;
    hideTooltip();
    renderSource();
  });
  $('startButton').addEventListener('click', () => {
    state.mode = 'combat';
    state.roundComplete = false;
    state.selected = null;
    state.selectedSource = null;
    hideTooltip();
    renderMode();
  });
  $('roundButton').addEventListener('click', () => {
    state.roundComplete = true;
    renderMode();
    $('editButton').focus();
  });
  $('editButton').addEventListener('click', () => {
    if (!state.roundComplete) return;
    state.mode = 'edit';
    state.roundComplete = false;
    setFeedback('选择招式，再点空格摆放');
    renderMode();
    $('category').focus();
  });
  $('schemeButton').addEventListener('click', () => { showPage('schemes'); $('schemeBack').focus(); });
  $('schemeBack').addEventListener('click', () => { showPage('items'); $('schemeButton').focus(); });
  schemeView.addEventListener('click', event => {
    const option = event.target.closest('[data-scheme]');
    if (!option || !presets[option.dataset.scheme]) return;
    state.scheme = option.dataset.scheme;
    state.board = copy(presets[state.scheme]);
    state.selected = null;
    state.selectedSource = null;
    showPage('items');
    setFeedback('方案已替换当前盘面');
    renderSource();
    renderBoard();
    $('schemeButton').focus();
  });
  $('pauseButton').addEventListener('click', event => {
    state.paused = !state.paused;
    event.currentTarget.setAttribute('aria-pressed', String(state.paused));
    event.currentTarget.textContent = state.paused ? '继续' : '暂停';
  });
  $('speedButton').addEventListener('click', event => {
    state.speed = state.speed === 1 ? 2 : state.speed === 2 ? 3 : 1;
    event.currentTarget.textContent = `×${state.speed}`;
  });
  $('characterButton').addEventListener('click', () => {
    hideTooltip();
    const dialog = $('characterDialog');
    if (typeof dialog.showModal === 'function') dialog.showModal();
    else dialog.setAttribute('open', '');
  });
  $('closeDialog').addEventListener('click', () => {
    const dialog = $('characterDialog');
    if (typeof dialog.close === 'function') dialog.close();
    else dialog.removeAttribute('open');
    $('characterButton').focus();
  });
  document.addEventListener('pointerover', event => {
    const node = event.target.closest('[data-move], [data-status], #handSlot');
    if (node && !node.contains(event.relatedTarget)) showTooltip(node);
  });
  document.addEventListener('pointerout', event => {
    const node = event.target.closest('[data-move], [data-status], #handSlot');
    if (node && !node.contains(event.relatedTarget)) hideTooltip();
  });
  document.addEventListener('focusin', event => {
    const node = event.target.closest('[data-move], [data-status], #handSlot');
    if (node) showTooltip(node);
  });
  document.addEventListener('focusout', hideTooltip);
  document.addEventListener('keydown', event => { if (event.key === 'Escape') hideTooltip(); });

  window.layoutPreview = {
    getState: () => copy(state),
    getItems: () => copy(items),
    selectSource,
    placeAt: (x, y) => attemptPlacement(x, y)
  };

  renderSource();
  renderMode();
})();
