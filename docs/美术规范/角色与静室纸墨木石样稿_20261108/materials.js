(() => {
  'use strict';
  const catalog = window.SKIN_DATA;
  const atlas = new URL(catalog.file, document.baseURI).href;
  const frameAtlas = new URL(catalog.frameFile, document.baseURI).href;
  const escape = value => String(value).replace(/[&<>"']/g, letter => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[letter]));
  const imageView = (source, size, bounds, className = '', stretch = false) => `<svg class="asset-view ${className}" viewBox="${bounds.join(' ')}" preserveAspectRatio="${stretch ? 'none' : 'xMidYMid meet'}" aria-hidden="true" focusable="false"><image href="${escape(source)}" width="${size[0]}" height="${size[1]}"></image></svg>`;
  const sprite = (name, className = '', stretch = false) => imageView(atlas, catalog.size, catalog.regions[name], `sprite sprite-${name} ${className}`, stretch);
  const atlasView = (path, size, bounds, className = '') => imageView(new URL(path, document.baseURI).href, size, bounds, className);
  function controlType(element) {
    if (element.dataset.skin !== 'button') return element.dataset.skin;
    if (element.dataset.buttonKind) return element.dataset.buttonKind;
    if (element.matches('.stepper,.search-well,.field-well')) return 'input';
    if (element.matches('.icon-button,.equipment-slot')) return 'buttonIcon';
    if (element.classList.contains('primary')) return 'buttonPrimary';
    if (element.classList.contains('nav-button')) return 'buttonNav';
    if (element.matches('.small-button,.item-row,button[data-mode],button[data-attribute-filter],button[data-battle-mode],.preview-tools nav>button,.filter-tabs>button,.chapter-branches>button')) return 'buttonTab';
    return 'button';
  }
  function ensureFilters() {
    if (document.querySelector('#control-alpha-clean')) return;
    document.body.insertAdjacentHTML('afterbegin', '<svg class="skin-definitions" width="0" height="0" aria-hidden="true"><defs><filter id="control-alpha-clean" color-interpolation-filters="sRGB" x="0" y="0" width="100%" height="100%"><feComponentTransfer><feFuncA type="linear" slope="1.032388664" intercept="-0.032388664"></feFuncA></feComponentTransfer></filter></defs></svg>');
  }
  function frame(type) {
    if (type === 'woodFull') return `<span class="skin-layer wood-full-layer" aria-hidden="true">${imageView(frameAtlas, catalog.frameSize, [110, 285, 850, 140], '', true)}</span>`;
    const spec = type === 'titleCompact' ? { ...catalog.frames.title, rendered: [18, 12, 18, 12] } : catalog.frames[type] || catalog.frames.panel;
    const source = spec.file ? new URL(spec.file, document.baseURI).href : frameAtlas;
    const size = spec.size || catalog.frameSize;
    const [x, y, width, height] = spec.bounds;
    const [left, top, right, bottom] = spec.slices;
    const xs = [x, x + left, x + width - right, x + width];
    const ys = [y, y + top, y + height - bottom, y + height];
    let html = `<span class="skin-layer"${spec.family ? ` data-control-family="${spec.family}"` : ''} aria-hidden="true" style="grid-template-columns:${spec.rendered[0]}px 1fr ${spec.rendered[2]}px;grid-template-rows:${spec.rendered[1]}px 1fr ${spec.rendered[3]}px">`;
    for (let row = 0; row < 3; row++) for (let column = 0; column < 3; column++) {
      const center = row === 1 && column === 1;
      let bounds = [xs[column], ys[row], xs[column + 1] - xs[column], ys[row + 1] - ys[row]];
      if (center && (type === 'shell' || type === 'wood')) bounds = [110, 285, 850, 140];
      html += `<span class="skin-bit ${center ? 'skin-center' : ''}" style="grid-area:${row + 1}/${column + 1}">${imageView(source, size, bounds, '', true)}</span>`;
    }
    return html + '</span>';
  }
  function apply(root = document) {
    ensureFilters();
    const elements = [...root.querySelectorAll('[data-skin]')];
    if (root.matches?.('[data-skin]')) elements.unshift(root);
    for (const element of elements) {
      if (element.dataset.skinned) continue;
      const type = controlType(element);
      const binding = type === 'buttonNav' ? `<span class="nav-binding" aria-hidden="true">${frame('buttonNavBinding')}</span>` : '';
      element.insertAdjacentHTML('afterbegin', binding + frame(type));
      element.dataset.skinned = type;
      element.dataset.materialSource = (catalog.frames[type] || {}).file || catalog.frameFile;
      element.classList.add('material-surface');
    }
    root.querySelectorAll('[data-sprite]').forEach(element => {
      if (element.querySelector('.asset-view')) return;
      element.innerHTML = sprite(element.dataset.sprite, '', element.dataset.stretch === 'true');
      element.dataset.materialSource = catalog.file;
    });
  }
  const button = (label, attrs = '', className = '') => `<button data-skin="button" class="ui-button ${className}" ${attrs}><span class="button-content">${label}</span></button>`;
  const iconButton = (glyph, label, attrs = '') => button(sprite(glyph), `aria-label="${escape(label)}" ${attrs}`, 'icon-button');
  const paper = (content, className = '', attrs = '') => `<section data-skin="panel" class="paper-panel ${className}" ${attrs}><div class="panel-content">${content}</div></section>`;
  const divider = label => `<div class="section-divider">${label ? `<span>${label}</span>` : ''}${sprite('divider', '', true)}</div>`;
  const progress = (percent, label = '', className = '') => `<div class="progress ${className}" role="progressbar" aria-label="${escape(label)}" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${percent}" data-material-source="${catalog.file}">${sprite('progressTrack', 'progress-trough', true)}<div class="progress-fill-clip" style="width:${percent}%">${sprite('progressFill', 'progress-fill', true)}</div></div>`;
  let scrollNumber = 0;
  const scroll = (content, className = '') => {
    const id = 'scroll-' + ++scrollNumber;
    return `<div class="scroll-box ${className}"><div class="scroll-view" id="${id}" tabindex="0"><div class="scroll-content">${content}</div></div><div class="scrollbar"><div class="scroll-track" role="scrollbar" tabindex="0" aria-label="滚动列表" aria-controls="${id}" aria-orientation="vertical" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0" data-material-source="${catalog.file}">${sprite('scrollTrack', 'scroll-track-art', true)}<div class="scroll-thumb" data-material-source="${catalog.file}">${sprite('scrollThumb', '', true)}</div></div></div></div>`;
  };
  function attachScroll(root = document) {
    root.querySelectorAll('.scroll-box').forEach(box => {
      if (box.dataset.bound) return;
      box.dataset.bound = 'true';
      const view = box.querySelector('.scroll-view'), track = box.querySelector('.scroll-track'), thumb = box.querySelector('.scroll-thumb');
      const update = () => {
        const maximum = view.scrollHeight - view.clientHeight;
        const ratio = maximum > 0 ? view.scrollTop / maximum : 0;
        const thumbHeight = Math.min(track.clientHeight, Math.max(43, track.clientHeight * view.clientHeight / Math.max(1, view.scrollHeight)));
        thumb.style.height = `${thumbHeight}px`;
        thumb.style.top = `${ratio * (track.clientHeight - thumbHeight)}px`;
        track.setAttribute('aria-valuenow', String(Math.round(ratio * 100)));
        box.classList.toggle('can-scroll', maximum > 2);
      };
      view.addEventListener('scroll', update, { passive: true });
      track.addEventListener('keydown', event => {
        const changes = { ArrowDown: 50, ArrowUp: -50, PageDown: view.clientHeight * .8, PageUp: -view.clientHeight * .8 };
        if (event.key in changes) { event.preventDefault(); view.scrollTop += changes[event.key]; }
        if (event.key === 'Home') { event.preventDefault(); view.scrollTop = 0; }
        if (event.key === 'End') { event.preventDefault(); view.scrollTop = view.scrollHeight; }
        update();
      });
      track.addEventListener('pointerdown', event => {
        event.preventDefault(); track.focus();
        const scale = track.getBoundingClientRect().height / track.clientHeight;
        if (!thumb.contains(event.target)) {
          const local = (event.clientY - track.getBoundingClientRect().top) / scale;
          view.scrollTop = (local - thumb.clientHeight / 2) / Math.max(1, track.clientHeight - thumb.clientHeight) * (view.scrollHeight - view.clientHeight);
          update();
        }
        const initialY = event.clientY, initialScroll = view.scrollTop;
        track.setPointerCapture(event.pointerId);
        const move = pointer => { view.scrollTop = initialScroll + (pointer.clientY - initialY) / scale / Math.max(1, track.clientHeight - thumb.clientHeight) * (view.scrollHeight - view.clientHeight); update(); };
        const stop = () => { track.removeEventListener('pointermove', move); track.removeEventListener('pointerup', stop); track.removeEventListener('pointercancel', stop); };
        track.addEventListener('pointermove', move); track.addEventListener('pointerup', stop); track.addEventListener('pointercancel', stop);
      });
      new ResizeObserver(update).observe(view);
      new ResizeObserver(update).observe(view.querySelector('.scroll-content'));
      requestAnimationFrame(update);
    });
  }
  const field = (label, input, className = '') => `<label class="field-group ${className}"><span class="field-caption">${label}</span><span data-skin="button" class="field-well">${input}</span></label>`;
  window.Materials = { sprite, atlasView, apply, button, iconButton, paper, divider, progress, scroll, attachScroll, field, escape };
})();
