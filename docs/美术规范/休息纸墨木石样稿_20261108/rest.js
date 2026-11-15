(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const fixtures = window.REST_FIXTURES;
  const rules = window.REST_RULES;
  const state = { case: 'day', mode: 'hours', hours: 8, date: [1, 3, 16], fixture: fixtures.day, closed: false, lastPreview: null };
  const dayIndex = date => (date[0] - 1) * 360 + (date[1] - 1) * 30 + date[2] - 1;
  const dateFromIndex = index => [Math.floor(index / 360) + 1, Math.floor(index % 360 / 30) + 1, index % 30 + 1];
  const dateText = date => date ? `${date[0]}年${date[1]}月${date[2]}日` : '—';
  const validInteger = (value, min, max) => Number.isInteger(value) && value >= min && value <= max;
  const validDate = date => Array.isArray(date) && validInteger(date[0], 1, 9999) && validInteger(date[1], 1, 12) && validInteger(date[2], 1, 30);
  const hasFixedHours = fixture => fixture.fixed !== undefined && fixture.fixed !== null;
  const hasKnownStats = fixture => Number.isFinite(fixture.current) && Number.isFinite(fixture.maximum) && fixture.maximum >= 0;
  const hasKnownClock = fixture => validDate(fixture.date) && validInteger(fixture.hour, 0, 23);
  const hasRecoveryRule = () => Number.isFinite(rules?.recoveryPerHour) && rules.recoveryPerHour > 0;
  // Match the current RestPanel's one-decimal presentation without exposing float residue.
  const amountText = value => Number.isFinite(value) ? String(Math.round((value + Number.EPSILON) * 10) / 10) : '—';
  function evaluate() {
    const f = state.fixture;
    const mode = hasFixedHours(f) ? 'fixed' : state.mode;
    if (!hasKnownStats(f) || !hasKnownClock(f)) return { valid: false, mode, unknown: true, hours: null, recovered: null, after: null, date: null, reason: '尚未绑定人物与日期。' };
    if (!hasRecoveryRule()) return { valid: false, mode, unknown: true, hours: null, recovered: null, after: null, date: null, reason: '恢复规则尚未就绪。' };
    if (mode === 'date' && !validDate(state.date)) return { valid: false, mode, reason: '请填写完整的醒来日期。' };
    const missing = Math.max(0, f.maximum - f.current);
    if (mode === 'full' && missing === 0)
      return { valid: false, mode, alreadyFull: true, hours: 0, recovered: 0, after: f.current, date: [...f.date], reason: '精力已满，无需休息。' };
    // Full recovery is independent of the manual 720-hour input limit.
    const hours = mode === 'fixed' ? f.fixed : mode === 'full' ? Math.ceil(missing / rules.recoveryPerHour) : mode === 'date' ? (dayIndex(state.date) - dayIndex(f.date)) * 24 : state.hours;
    const maximumHours = mode === 'hours' ? 720 : Number.MAX_SAFE_INTEGER;
    if (!validInteger(hours, 1, maximumHours))
      return { valid: false, mode, reason: mode === 'date' ? '请选择晚于当前日期的醒来日期。' : mode === 'hours' ? '休息时长须为 1—720 小时。' : '休息安排暂时无法计算。' };
    const recovered = Math.max(0, Math.min(missing, hours * rules.recoveryPerHour));
    const endDate = dateFromIndex(Math.floor((dayIndex(f.date) * 24 + f.hour + hours) / 24));
    return { valid: true, mode, hours, recovered, after: f.current + recovered, date: endDate };
  }
  function render() {
    const f = state.fixture, result = evaluate(), knownStats = hasKnownStats(f), knownClock = hasKnownClock(f), fixed = hasFixedHours(f);
    const isDay = !knownClock || f.hour >= 6 && f.hour < 18;
    const canArrange = knownStats && knownClock && hasRecoveryRule() && !fixed;
    state.lastPreview = result;
    $('stage').dataset.case = state.case;
    const targetBackground = `../../../textures/ui/cultivation/quiet_room_20261010/rest_${isDay ? 'day' : 'night'}.png`;
    if (!$('room-background').getAttribute('src').endsWith(targetBackground)) $('room-background').src = targetBackground;
    $('current-date').textContent = knownClock ? dateText(f.date) : '—';
    $('current-stamina').innerHTML = !knownStats ? '— <small>/ —</small>' : `${amountText(f.current)} <small>/ ${amountText(f.maximum)}</small>`;
    $('current-progress').innerHTML = Materials.progress(!knownStats ? 0 : Math.max(0, Math.min(100, f.current / Math.max(1, f.maximum) * 100)), '当前精力');
    $('current-progress').firstElementChild.dataset.unknown = String(!knownStats);
    $('current-progress').firstElementChild.setAttribute('aria-valuetext', !knownStats ? '未绑定' : `${amountText(f.current)} / ${amountText(f.maximum)}`);
    $('mode-tabs').hidden = fixed;
    $('hours-section').hidden = fixed || state.mode !== 'hours';
    $('date-section').hidden = fixed || state.mode !== 'date';
    $('full-section').hidden = fixed || state.mode !== 'full';
    $('full-hours').textContent = Number.isSafeInteger(result.hours) ? result.hours : '—';
    $('fixed-section').hidden = !fixed;
    $('fixed-mark').hidden = !fixed;
    for (const [id, mode] of [['mode-hours', 'hours'], ['mode-date', 'date'], ['mode-full', 'full']]) {
      $(id).setAttribute('aria-pressed', String(state.mode === mode));
      $(id).classList.toggle('is-selected', state.mode === mode);
      $(id).disabled = !canArrange;
    }
    document.querySelectorAll('.number-well input,.step-arrow').forEach(input => input.disabled = !canArrange);
    $('after-stamina').innerHTML = result.valid || result.alreadyFull ? `${amountText(result.after)} <small>/ ${amountText(f.maximum)}</small>` : !knownStats ? '—' : `${amountText(f.current)} <small>/ ${amountText(f.maximum)}</small>`;
    $('recovered-stamina').textContent = result.valid || result.alreadyFull ? `${amountText(result.recovered)} 点` : '—';
    $('wake-date').textContent = result.valid || result.alreadyFull ? dateText(result.date) : '—';
    $('health-result').textContent = !knownStats || result.alreadyFull ? '—' : '恢复至上限';
    $('recovery-rate').textContent = hasRecoveryRule() ? amountText(rules.recoveryPerHour) : '—';
    $('validation-message').textContent = result.valid ? '' : result.reason;
    $('start-rest').disabled = !result.valid;
    $('closed-view').hidden = !state.closed;
    for (const button of document.querySelectorAll('[data-case]')) if (button.tagName === 'BUTTON') {
      button.classList.toggle('is-selected', button.dataset.case === state.case);
      button.setAttribute('aria-pressed', String(button.dataset.case === state.case));
    }
    Materials.apply(document);
  }
  function chooseCase(name) {
    if (!fixtures[name]) return;
    if ($('arrangement-dialog').open) $('arrangement-dialog').close();
    state.case = name; state.fixture = fixtures[name]; state.mode = state.fixture.mode; state.hours = 8; state.closed = false;
    state.date = state.fixture.date ? dateFromIndex(dayIndex(state.fixture.date) + 1) : [1, 3, 16];
    $('hours').value = 8;
    ['year', 'month', 'day'].forEach((id, index) => $(id).value = state.date[index]);
    render();
  }
  function inputChanged(id, commit = false) {
    const input = $(id);
    let value = input.value.trim() === '' ? NaN : Number(input.value);
    if (commit && Number.isFinite(value)) { value = Math.max(Number(input.min), Math.min(Number(input.max), Math.round(value))); input.value = value; }
    if (id === 'hours') state.hours = value;
    else state.date[['year', 'month', 'day'].indexOf(id)] = value;
    render();
  }
  function fit() {
    const viewport = $('viewport');
    const scale = Math.min(viewport.clientWidth / 1672, viewport.clientHeight / 941);
    $('stage').style.transform = `scale(${scale})`;
    document.documentElement.style.setProperty('--stage-scale', scale);
  }
  function closeRest() { state.closed = true; render(); $('reopen').focus(); }
  function chooseMode(mode) {
    if (hasFixedHours(state.fixture) || !hasKnownStats(state.fixture) || !hasKnownClock(state.fixture) || !hasRecoveryRule()) return;
    state.mode = mode; render();
  }
  document.querySelectorAll('[data-case]').forEach(button => button.addEventListener('click', () => chooseCase(button.dataset.case)));
  $('mode-hours').addEventListener('click', () => chooseMode('hours'));
  $('mode-date').addEventListener('click', () => chooseMode('date'));
  $('mode-full').addEventListener('click', () => chooseMode('full'));
  for (const id of ['hours', 'year', 'month', 'day']) {
    $(id).addEventListener('input', () => inputChanged(id));
    $(id).addEventListener('change', () => inputChanged(id, true));
  }
  document.querySelectorAll('.step-arrow').forEach(button => button.addEventListener('click', () => {
    const input = $(button.dataset.field), oldValue = Number(input.value);
    input.value = Math.max(Number(input.min), Math.min(Number(input.max), (Number.isFinite(oldValue) ? oldValue : Number(input.min)) + Number(button.dataset.delta)));
    inputChanged(input.id, true);
  }));
  $('leave').addEventListener('click', closeRest);
  $('reopen').addEventListener('click', () => { state.closed = false; render(); $('start-rest').focus(); });
  $('start-rest').addEventListener('click', () => {
    const result = evaluate(); if (!result.valid) return;
    $('arrangement-copy').textContent = `你将在观复峰居所休息 ${result.hours} 小时，醒来日期：${dateText(result.date)}。`;
    $('arrangement-dialog').showModal(); $('return-arrangement').focus();
  });
  $('return-arrangement').addEventListener('click', () => $('arrangement-dialog').close());
  $('arrangement-dialog').addEventListener('close', () => $('start-rest').focus());
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && !$('arrangement-dialog').open) { event.preventDefault(); if (!state.closed) closeRest(); }
  });
  window.addEventListener('resize', fit);
  // Read-only inspection seam; the preview has no persistence or game bridge.
  window.RestReview = Object.freeze({ snapshot: () => {
    const result = evaluate();
    return structuredClone({ case: state.case, mode: state.mode, fixture: state.fixture, rules, hours: state.hours, date: state.date, closed: state.closed, preview: result, result });
  } });
  chooseCase(new URLSearchParams(location.search).get('case') || 'day');
  fit(); Materials.apply(document);
})();
