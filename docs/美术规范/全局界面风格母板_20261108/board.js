(() => {
  'use strict';
  const tabs = Array.from(document.querySelectorAll('[data-view]'));
  const panels = Array.from(document.querySelectorAll('[data-panel]'));
  const viewNames = tabs.map(tab => tab.dataset.view);

  // The frame is assembled from eight regions of the existing hand-painted atlas.
  // The center stays transparent so text, state colors and layout remain live.
  const atlas = new URL('../../../textures/ui/yuxu_library_20261107/library_frame.png', document.baseURI).href;
  function addFrame(element, bounds, slices, rendered) {
    if (element.querySelector(':scope > .hand-frame')) return;
    const [x, y, width, height] = bounds;
    const [left, top, right, bottom] = slices;
    const xs = [x, x + left, x + width - right, x + width];
    const ys = [y, y + top, y + height - bottom, y + height];
    const frame = document.createElement('span');
    frame.className = 'hand-frame';
    frame.setAttribute('aria-hidden', 'true');
    frame.style.gridTemplateColumns = `${rendered[0]}px 1fr ${rendered[2]}px`;
    frame.style.gridTemplateRows = `${rendered[1]}px 1fr ${rendered[3]}px`;
    const ns = 'http://www.w3.org/2000/svg';
    for (let row = 0; row < 3; row++) {
      for (let column = 0; column < 3; column++) {
        if (row === 1 && column === 1) continue;
        const svg = document.createElementNS(ns, 'svg');
        svg.setAttribute('viewBox', `${xs[column]} ${ys[row]} ${xs[column + 1] - xs[column]} ${ys[row + 1] - ys[row]}`);
        svg.setAttribute('preserveAspectRatio', 'none');
        svg.setAttribute('focusable', 'false');
        svg.style.gridArea = `${row + 1} / ${column + 1}`;
        const image = document.createElementNS(ns, 'image');
        image.setAttribute('href', atlas);
        image.setAttribute('width', '1672');
        image.setAttribute('height', '941');
        svg.append(image);
        frame.append(svg);
      }
    }
    element.classList.add('has-hand-frame');
    element.append(frame);
  }
  function decorate(root) {
    root.querySelectorAll('.paper-panel, .dialog-body').forEach(element => addFrame(element, [1105, 160, 537, 744], [38, 38, 38, 38], [27, 27, 27, 27]));
    root.querySelectorAll('.paper-button, .index-item, .paper-title').forEach(element => addFrame(element, [96, 131, 218, 47], [16, 8, 16, 8], [14, 7, 14, 7]));
  }

  function showView(name, focus = false) {
    const target = viewNames.includes(name) ? name : 'basis';
    tabs.forEach(tab => {
      const active = tab.dataset.view === target;
      tab.setAttribute('aria-selected', String(active));
      tab.tabIndex = active ? 0 : -1;
      if (active && focus) tab.focus();
    });
    panels.forEach(panel => { panel.hidden = panel.dataset.panel !== target; });
  }
  tabs.forEach((tab, index) => {
    tab.addEventListener('click', () => {
      showView(tab.dataset.view);
      history.replaceState(null, '', `#${tab.dataset.view}`);
    });
    tab.addEventListener('keydown', event => {
      let next = index;
      if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
      else if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
      else if (event.key === 'Home') next = 0;
      else if (event.key === 'End') next = tabs.length - 1;
      else return;
      event.preventDefault();
      showView(tabs[next].dataset.view, true);
      history.replaceState(null, '', `#${tabs[next].dataset.view}`);
    });
  });
  window.addEventListener('hashchange', () => showView(location.hash.slice(1)));
  showView(location.hash.slice(1));

  const selectOne = (items, clicked) => items.forEach(item => {
    const active = item === clicked;
    item.classList.toggle('is-current', active);
    item.setAttribute('aria-pressed', String(active));
  });
  const indexItems = Array.from(document.querySelectorAll('.index-item'));
  indexItems.forEach(item => item.addEventListener('click', () => {
    selectOne(indexItems, item);
    document.querySelector('#specimen-title').textContent = item.textContent.trim();
  }));
  const segments = Array.from(document.querySelectorAll('.segment'));
  const rows = Array.from(document.querySelectorAll('.list-row'));
  const updateSelection = () => {
    const segment = segments.find(item => item.classList.contains('is-current'));
    const row = rows.find(item => item.classList.contains('is-current'));
    document.querySelector('#selection-status').textContent = `已选：${segment.textContent.trim()} · ${row.querySelector('span').textContent.trim()}`;
  };
  segments.forEach(item => item.addEventListener('click', () => { selectOne(segments, item); updateSelection(); }));
  rows.forEach(item => item.addEventListener('click', () => { selectOne(rows, item); updateSelection(); }));

  const dialog = document.querySelector('#confirmation');
  let dialogTrigger;
  document.querySelectorAll('#open-demo, #open-demo-bottom').forEach(button => button.addEventListener('click', () => {
    dialogTrigger = button;
    if (!dialog.open) dialog.showModal();
  }));
  document.querySelectorAll('[data-close-dialog]').forEach(button => button.addEventListener('click', () => dialog.close()));
  dialog.addEventListener('click', event => {
    const bounds = dialog.getBoundingClientRect();
    if (event.target === dialog && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) dialog.close();
  });
  dialog.addEventListener('close', () => dialogTrigger?.focus());

  const applicationDescriptions = {
    character: '木边与纸册支撑人物资料，目录与详情有明确主次。',
    quiet: '共用纸木做工，保留静室自己的背景、修行图示与操作。',
    inventory: '纸面清册配浅绢内衬，物品空间继续采用已认可的 v5。',
    rental: '场所身份与租用办理分层，用度清楚，沿用既有炼制入口。'
  };
  const appButtons = Array.from(document.querySelectorAll('[data-application]'));
  function showApplication(name) {
    if (!Object.hasOwn(applicationDescriptions, name)) return;
    const template = document.querySelector(`#application-${name}`);
    document.querySelector('#application-preview').replaceChildren(template.content.cloneNode(true));
    decorate(document.querySelector('#application-preview'));
    document.querySelector('#application-description').textContent = applicationDescriptions[name];
    selectOne(appButtons, appButtons.find(button => button.dataset.application === name));
  }
  appButtons.forEach(button => button.addEventListener('click', () => showApplication(button.dataset.application)));
  showApplication('character');
  decorate(document);
})();
