/*
 * BookFlip.turn({ book, direction: 'next' | 'previous', scope: 'spread' | 'reading',
 *                 commit, reducedMotion?, duration? }) -> Promise<{ status }>
 *
 * commit is synchronous and runs once at the midpoint of an accepted turn.
 * The caller owns its navigation queue and focus restoration after commit.
 * A concurrent call resolves with { status: 'busy' } without accepting its commit.
 * settle() / cancel() synchronously commit and clean up the accepted turn.
 *
 * For a still frame, pause the animations returned by the layer's getAnimations
 * ({ subtree: true }), set every currentTime to duration * progress, and allow
 * one animation frame. The master clock drives the midpoint from the same time.
 */
(() => {
  'use strict';

  const STYLE_ID = 'baigong-book-flip-style';
  const DEFAULT_DURATION = 500;
  const PAGE = { left: .058, right: .942, top: .043, bottom: .927, gutter: .5 };
  const FROZEN_PROPERTIES = (`
    box-sizing display position inset top right bottom left z-index float clear
    width height min-width min-height max-width max-height aspect-ratio
    margin-top margin-right margin-bottom margin-left
    padding-top padding-right padding-bottom padding-left
    flex flex-grow flex-shrink flex-basis flex-direction flex-wrap
    align-content align-items align-self justify-content justify-items justify-self
    grid-template-columns grid-template-rows grid-auto-flow grid-auto-columns grid-auto-rows
    grid-column grid-row gap row-gap column-gap order
    overflow overflow-x overflow-y overflow-wrap text-overflow white-space
    font-family font-size font-style font-weight font-stretch line-height
    letter-spacing word-spacing color text-align text-indent text-decoration
    text-transform text-shadow writing-mode text-orientation vertical-align
    background-color background-image background-position background-size background-repeat
    border-top border-right border-bottom border-left border-radius
    border-collapse border-spacing box-shadow opacity visibility
    transform transform-origin object-fit object-position filter clip-path
  `).trim().split(/\s+/);
  let active = null;

  function installStyles(doc) {
    if (doc.getElementById(STYLE_ID)) return;
    const style = doc.createElement('style');
    style.id = STYLE_ID;
    style.textContent = `
      [data-bookflip-layer] {
        position:absolute;inset:0;z-index:12;overflow:visible;
        pointer-events:none!important;perspective-origin:50% 44%;
        transform-style:preserve-3d;
      }
      [data-bookflip-layer], [data-bookflip-layer] * {
        pointer-events:none!important;user-select:none!important;
        -webkit-user-select:none!important;caret-color:transparent!important;
        transition:none!important;animation:none!important;
      }
      [data-bookflip-sheet] {
        position:absolute;transform-style:preserve-3d;will-change:transform;
      }
      [data-bookflip-face] {
        position:absolute;inset:0;overflow:hidden;box-sizing:border-box;
        backface-visibility:hidden;-webkit-backface-visibility:hidden;
        background:#eee0c7;border-radius:1.4% 1.8% 1.7% 1.2%;
      }
      [data-bookflip-face="front"] { transform:translateZ(.35px) }
      [data-bookflip-face="back"] { transform:rotateY(180deg) translateZ(.35px) }
      [data-bookflip-paper] {
        position:absolute;max-width:none!important;max-height:none!important;
        object-fit:fill;user-select:none;-webkit-user-drag:none;
      }
      [data-bookflip-shade] {
        position:absolute;inset:0;z-index:3;pointer-events:none;
        background:linear-gradient(90deg,#493c2338,transparent 24%,#4e3d2520);
      }
      [data-bookflip-shadow] {
        position:absolute;z-index:2;opacity:0;pointer-events:none;
        background:linear-gradient(90deg,#30281d38,#30281d0a 70%,transparent);
        filter:blur(8px);border-radius:3%;transform-origin:0 50%;
      }
      [data-bookflip-clock] { position:absolute;width:0;height:0;visibility:hidden }
    `;
    (doc.head || doc.documentElement).appendChild(style);
  }

  const leafFor = (book, side) => book.querySelector(side === 'left' ? '.bg-catalog' : '.bg-reading');
  const rectangle = (job, side) => ({
    x: job.width * (side === 'left' ? PAGE.left : PAGE.gutter),
    y: job.height * PAGE.top,
    width: job.width * (PAGE.gutter - PAGE.left),
    height: job.height * (PAGE.bottom - PAGE.top),
  });

  function putBox(node, rect) {
    Object.assign(node.style, {
      left: `${rect.x}px`, top: `${rect.y}px`, width: `${rect.width}px`, height: `${rect.height}px`,
    });
  }

  function freezeStyle(source, clone, win) {
    const computed = win.getComputedStyle(source);
    for (const property of FROZEN_PROPERTIES) {
      const value = computed.getPropertyValue(property);
      if (value) clone.style.setProperty(property, value);
    }
    clone.style.setProperty('animation', 'none', 'important');
    clone.style.setProperty('transition', 'none', 'important');
    clone.style.setProperty('pointer-events', 'none', 'important');
  }

  function snapshot(job, source, crop) {
    if (!source) return null;
    const clone = source.cloneNode(true);
    const originals = [source, ...source.querySelectorAll('*')];
    const copies = [clone, ...clone.querySelectorAll('*')];
    const scrollPositions = [];
    for (let index = 0; index < originals.length; index++) {
      const original = originals[index];
      let copy = copies[index];
      if (/^(SCRIPT|STYLE|LINK|IFRAME|OBJECT|EMBED)$/.test(original.tagName)) {
        copy.remove();
        continue;
      }
      // Copy the visible field text, never a second live form control or focus.
      if (/^(INPUT|TEXTAREA|SELECT)$/.test(original.tagName)) {
        const visual = job.doc.createElement('span');
        if (original.tagName === 'SELECT') {
          visual.textContent = original.selectedOptions?.[0]?.textContent || '';
        } else if (original.type === 'checkbox' || original.type === 'radio') {
          visual.textContent = original.checked ? '✓' : '';
        } else {
          visual.textContent = original.value || original.placeholder || '';
        }
        freezeStyle(original, visual, job.win);
        visual.style.whiteSpace = 'pre';
        visual.style.textOverflow = 'ellipsis';
        visual.style.overflow = 'hidden';
        if (!original.value && original.placeholder) {
          visual.style.color = job.win.getComputedStyle(original, '::placeholder').color;
        }
        copy.replaceWith(visual);
        copy = visual;
      } else if (original.namespaceURI === 'http://www.w3.org/1999/xhtml'
        && !original.closest('.skin-layer')) {
        freezeStyle(original, copy, job.win);
      }
      for (const attribute of [...copy.attributes]) {
        if (attribute.name === 'id' || attribute.name === 'name' || attribute.name === 'for'
          || attribute.name === 'form' || attribute.name === 'autofocus'
          || attribute.name.startsWith('on') || attribute.name.startsWith('data-')
          || attribute.name.startsWith('aria-')) copy.removeAttribute(attribute.name);
      }
      copy.removeAttribute('contenteditable');
      if (original.tagName === 'A') copy.removeAttribute('href');
      if (original.tagName === 'IMG') {
        copy.src = original.currentSrc || original.src;
        copy.removeAttribute('srcset');
        copy.draggable = false;
      }
      if (original.tagName === 'CANVAS') {
        try { copy.getContext('2d')?.drawImage(original, 0, 0); } catch (_) { /* Keep its paper backing. */ }
      }
      if (/^(BUTTON|A|INPUT|TEXTAREA|SELECT)$/.test(copy.tagName) || copy.hasAttribute('tabindex')) {
        copy.setAttribute('tabindex', '-1');
        if (copy.tagName === 'BUTTON') copy.disabled = true;
      }
      // Materials.attachScroll must only bind the real controls during commit.
      for (const name of ['scroll-box', 'scroll-view', 'scroll-content', 'scrollbar', 'scroll-track', 'scroll-thumb']) {
        copy.classList.remove(name);
      }
      if (original.scrollTop || original.scrollLeft) {
        scrollPositions.push([copy, original.scrollTop, original.scrollLeft]);
      }
    }
    clone.classList.remove('bg-leaf', 'bg-catalog', 'bg-reading', 'paper-panel');
    clone.setAttribute('data-bookflip-snapshot', '');
    clone.setAttribute('aria-hidden', 'true');
    clone.inert = true;
    const bounds = source.getBoundingClientRect();
    const bookBounds = job.book.getBoundingClientRect();
    const scaleX = job.width / bookBounds.width;
    const scaleY = job.height / bookBounds.height;
    Object.assign(clone.style, {
      position: 'absolute',
      left: `${(bounds.left - bookBounds.left) * scaleX - crop.x}px`,
      top: `${(bounds.top - bookBounds.top) * scaleY - crop.y}px`,
      right: 'auto', bottom: 'auto',
      width: `${bounds.width * scaleX}px`, height: `${bounds.height * scaleY}px`,
      margin: '0', transform: 'none', visibility: 'visible',
    });
    job.scrollRestorers.push(() => {
      for (const [element, top, left] of scrollPositions) { element.scrollTop = top; element.scrollLeft = left; }
    });
    return clone;
  }

  function makeFace(job, side, label = 'front', source = null) {
    const crop = rectangle(job, side);
    const face = job.doc.createElement('div');
    face.dataset.bookflipFace = label;
    // This is a viewport into the existing PNG, not a stretched new paper swatch.
    const paper = job.doc.createElement('img');
    paper.dataset.bookflipPaper = side;
    paper.src = job.paperURL;
    paper.alt = '';
    paper.draggable = false;
    Object.assign(paper.style, {
      left: `${-crop.x}px`, top: `${-crop.y}px`, width: `${job.width}px`, height: `${job.height}px`,
    });
    face.appendChild(paper);
    if (source) {
      const copy = snapshot(job, source, crop);
      if (copy) face.appendChild(copy);
    }
    const shade = job.doc.createElement('div');
    shade.dataset.bookflipShade = '';
    face.appendChild(shade);
    return face;
  }

  function makeSheet(job, side, label) {
    const sheet = job.doc.createElement('div');
    sheet.dataset.bookflipSheet = label;
    putBox(sheet, rectangle(job, side));
    sheet.style.transformOrigin = side === 'right' ? '0% 50%' : '100% 50%';
    sheet.style.zIndex = '3';
    job.layer.appendChild(sheet);
    return sheet;
  }

  function animate(job, target, frames, options = {}) {
    const animation = target.animate(frames, {
      duration: job.duration, easing: 'linear', fill: 'both', ...options,
    });
    animation.finished.catch(() => {});
    job.animations.push(animation);
    return animation;
  }

  function restoreScroll(job) {
    for (const restore of job.scrollRestorers.splice(0)) restore();
  }

  function addShadows(job, side, sheet) {
    const shadow = job.doc.createElement('div');
    shadow.dataset.bookflipShadow = '';
    putBox(shadow, rectangle(job, side));
    if (side === 'left') shadow.style.transform = 'scaleX(-1)';
    job.layer.prepend(shadow);
    animate(job, shadow, [
      { opacity: 0, offset: 0 }, { opacity: .52, offset: .28 },
      { opacity: .58, offset: .56 }, { opacity: 0, offset: 1 },
    ]);
    for (const shade of sheet.querySelectorAll('[data-bookflip-shade]')) {
      animate(job, shade, [
        { opacity: .12, offset: 0 }, { opacity: .8, offset: .46 },
        { opacity: .64, offset: .57 }, { opacity: .1, offset: 1 },
      ]);
    }
  }

  function setUpSpread(job) {
    const origin = job.direction === 'next' ? 'right' : 'left';
    const destination = origin === 'right' ? 'left' : 'right';
    const base = makeSheet(job, destination, 'stationary');
    base.style.zIndex = '1';
    base.style.transformStyle = 'flat';
    base.appendChild(makeFace(job, destination, 'front', leafFor(job.book, destination)));
    const underlay = makeSheet(job, origin, 'underlay');
    underlay.style.zIndex = '0';
    underlay.appendChild(makeFace(job, origin));
    const sheet = makeSheet(job, origin, 'turning');
    sheet.appendChild(makeFace(job, origin, 'front', leafFor(job.book, origin)));
    const back = makeFace(job, destination, 'back');
    sheet.appendChild(back);
    const degrees = job.direction === 'next' ? -180 : 180;
    animate(job, sheet, [
      { transform: 'rotateY(0deg)', offset: 0, easing: 'cubic-bezier(.38,0,.55,1)' },
      { transform: `rotateY(${degrees / 2}deg)`, offset: .5, easing: 'cubic-bezier(.15,.55,.42,1)' },
      { transform: `rotateY(${degrees}deg)`, offset: 1 },
    ]);
    addShadows(job, destination, sheet);
    job.reveal = () => {
      const copy = snapshot(job, leafFor(job.book, destination), rectangle(job, destination));
      if (copy) back.insertBefore(copy, back.lastChild);
      underlay.remove();
    };
  }

  function setUpReading(job) {
    const sign = job.direction === 'next' ? -1 : 1;
    const outgoing = makeSheet(job, 'right', 'outgoing');
    outgoing.appendChild(makeFace(job, 'right', 'front', leafFor(job.book, 'right')));
    outgoing.appendChild(makeFace(job, 'right', 'back'));
    const incoming = makeSheet(job, 'right', 'incoming');
    incoming.style.zIndex = '4';
    const front = makeFace(job, 'right');
    incoming.appendChild(front);
    incoming.appendChild(makeFace(job, 'right', 'back'));
    const underlay = makeSheet(job, 'right', 'underlay');
    underlay.style.zIndex = '0';
    underlay.appendChild(makeFace(job, 'right'));
    animate(job, outgoing, [
      { transform: 'rotateY(0deg)', opacity: 1, offset: 0, easing: 'ease-in' },
      { transform: `rotateY(${sign * 96}deg)`, opacity: 1, offset: .49 },
      { transform: `rotateY(${sign * 96}deg)`, opacity: 0, offset: .5 },
      { transform: `rotateY(${sign * 96}deg)`, opacity: 0, offset: 1 },
    ]);
    animate(job, incoming, [
      { transform: `rotateY(${-sign * 96}deg)`, opacity: 0, offset: 0 },
      { transform: `rotateY(${-sign * 96}deg)`, opacity: 0, offset: .49 },
      { transform: `rotateY(${-sign * 96}deg)`, opacity: 1, offset: .5, easing: 'ease-out' },
      { transform: 'rotateY(0deg)', opacity: 1, offset: 1 },
    ]);
    addShadows(job, 'right', outgoing);
    addShadows(job, 'right', incoming);
    job.reveal = () => {
      const copy = snapshot(job, leafFor(job.book, 'right'), rectangle(job, 'right'));
      if (copy) front.insertBefore(copy, front.lastChild);
      underlay.remove();
    };
  }

  function commit(job) {
    if (job.committed || job.finished) return;
    job.committed = true;
    job.commit();
    if (job.finished || job.finishing) return;
    if (job.book.isConnected) {
      job.book.dataset.bookflipPhase = 'incoming';
      job.reveal?.();
      restoreScroll(job);
    }
  }

  function finish(job, status = 'finished', failure = null) {
    if (job.finished || job.finishing) return job.promise;
    job.finishing = true;
    let error = failure;
    try { commit(job); } catch (caught) { error = error || caught; }
    job.finished = true;
    job.win.cancelAnimationFrame(job.frame);
    job.win.clearTimeout(job.watchdog);
    for (const dispose of job.disposers) { try { dispose(); } catch (_) { /* Continue cleanup. */ } }
    for (const animation of job.animations) { try { animation.cancel(); } catch (_) { /* Continue cleanup. */ } }
    job.layer?.remove();
    for (const [name, value] of job.savedAttributes) {
      if (value === null) job.book.removeAttribute(name); else job.book.setAttribute(name, value);
    }
    if (job.restorePosition) {
      if (job.positionValue) job.book.style.setProperty('position', job.positionValue, job.positionPriority);
      else job.book.style.removeProperty('position');
    }
    if (active === job) active = null;
    if (error) job.reject(error); else job.resolve({ status });
    return job.promise;
  }

  function on(job, target, type, listener) {
    target.addEventListener(type, listener);
    job.disposers.push(() => target.removeEventListener(type, listener));
  }

  function tick(job) {
    if (job.finished) return;
    if (!job.book.isConnected) { finish(job, 'detached'); return; }
    const time = Number(job.clock.currentTime) || 0;
    if (time >= job.duration / 2) {
      try { commit(job); } catch (error) { finish(job, 'failed', error); return; }
    }
    if (job.finished) return;
    if (time >= job.duration) { finish(job); return; }
    job.frame = job.win.requestAnimationFrame(() => tick(job));
  }

  function turn(options = {}) {
    const { book, commit: update } = options;
    if (!book || book.nodeType !== 1 || typeof update !== 'function') {
      return Promise.reject(new TypeError('BookFlip.turn requires a book element and a synchronous commit function.'));
    }
    if (active) return Promise.resolve({ status: 'busy' });
    const direction = options.direction || 'next';
    const scope = options.scope || 'spread';
    if (!['next', 'previous'].includes(direction) || !['spread', 'reading'].includes(scope)) {
      return Promise.reject(new TypeError('Unknown book turn direction or scope.'));
    }
    const doc = book.ownerDocument;
    const win = doc.defaultView || window;
    const duration = Number.isFinite(options.duration) ? Math.max(160, Math.min(5000, options.duration)) : DEFAULT_DURATION;
    const job = {
      book, doc, win, direction, scope, duration, commit: update,
      committed: false, finished: false, finishing: false,
      animations: [], disposers: [], scrollRestorers: [],
      savedAttributes: ['aria-busy', 'data-bookflip-phase'].map(name => [name, book.getAttribute(name)]),
    };
    job.promise = new Promise((resolve, reject) => { job.resolve = resolve; job.reject = reject; });
    active = job;
    const preference = win.matchMedia?.('(prefers-reduced-motion: reduce)');
    const reduce = options.reducedMotion ?? preference?.matches ?? false;
    const box = book.getBoundingClientRect();
    if (reduce || !book.isConnected || box.width <= 0 || box.height <= 0 || typeof book.animate !== 'function') {
      finish(job, reduce ? 'reduced-motion' : 'immediate');
      return job.promise;
    }
    try {
      installStyles(doc);
      job.width = book.clientWidth || book.offsetWidth;
      job.height = book.clientHeight || book.offsetHeight;
      const art = book.querySelector('.bg-book-art');
      job.paperURL = art?.currentSrc || art?.src || new URL('assets/展开书册_v2.png', doc.baseURI).href;
      job.positionValue = book.style.getPropertyValue('position');
      job.positionPriority = book.style.getPropertyPriority('position');
      job.restorePosition = win.getComputedStyle(book).position === 'static';
      if (job.restorePosition) book.style.position = 'relative';
      const layer = doc.createElement('div');
      layer.dataset.bookflipLayer = scope;
      layer.dataset.bookflipDirection = direction;
      layer.setAttribute('aria-hidden', 'true');
      layer.setAttribute('role', 'presentation');
      layer.inert = true;
      layer.style.perspective = `${job.width * 2.3}px`;
      job.layer = layer;
      book.appendChild(layer);
      book.setAttribute('aria-busy', 'true');
      book.dataset.bookflipPhase = 'outgoing';
      if (scope === 'reading') setUpReading(job); else setUpSpread(job);
      restoreScroll(job);
      const clock = doc.createElement('span');
      clock.dataset.bookflipClock = '';
      layer.appendChild(clock);
      job.clock = animate(job, clock, [{ opacity: 0 }, { opacity: 1 }], { easing: 'linear' });
      // All visible sheets and this clock share exactly one timeline origin.
      const startedAt = doc.timeline.currentTime;
      if (typeof startedAt === 'number') {
        for (const animation of job.animations) animation.startTime = startedAt;
      }
      job.clock.finished.then(() => { finish(job); }, () => {
        if (!job.finished) finish(job, 'cancelled');
      });
      on(job, win, 'resize', () => finish(job, 'settled'));
      on(job, win, 'pagehide', () => finish(job, 'settled'));
      on(job, doc, 'visibilitychange', () => { if (doc.hidden) finish(job, 'settled'); });
      if (options.reducedMotion === undefined && preference?.addEventListener) {
        on(job, preference, 'change', event => { if (event.matches) finish(job, 'reduced-motion'); });
      }
      if (win.MutationObserver) {
        const observer = new win.MutationObserver(() => { if (!book.isConnected) finish(job, 'detached'); });
        observer.observe(doc.documentElement, { childList: true, subtree: true });
        job.disposers.push(() => observer.disconnect());
      }
      const watchdog = () => {
        if (job.finished) return;
        if (job.clock.playState === 'paused') {
          job.watchdog = win.setTimeout(watchdog, job.duration + 200);
        } else finish(job, 'settled');
      };
      job.watchdog = win.setTimeout(watchdog, job.duration + 200);
      job.frame = win.requestAnimationFrame(() => tick(job));
    } catch (_) {
      // A missing animation capability must not discard an accepted navigation.
      finish(job, 'immediate');
    }
    return job.promise;
  }

  const settle = () => active ? finish(active, 'settled') : Promise.resolve({ status: 'idle' });
  window.BookFlip = Object.freeze({ turn, settle, cancel: settle, get isTurning() { return active !== null; } });
})();
