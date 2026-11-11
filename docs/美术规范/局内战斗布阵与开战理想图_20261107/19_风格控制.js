/* 仅供风格评审：皮肤与版面切换不执行正式战斗。 */
(() => {
  const allowed = new Set(['paper','silk','wood']);
  const params = new URLSearchParams(location.search);
  const skin = allowed.has(params.get('skin')) ? params.get('skin') : 'wood';
  const apply = value => {
    if (allowed.has(value)) document.documentElement.dataset.skin = value;
  };
  function phase(mode) {
    const state = window.layoutPreview?.getState();
    if (!state) return;
    if (mode === 'combat' && state.mode !== 'combat') document.getElementById('startButton').click();
    if (mode === 'edit' && state.mode !== 'edit') {
      document.getElementById('roundButton').click();
      document.getElementById('editButton').click();
    }
  }
  apply(skin);
  phase(params.get('view'));
  function notifyPhase() {
    if (parent !== window) parent.postMessage({ type: 'combat-style-state', view: window.layoutPreview?.getState().mode }, '*');
  }
  new MutationObserver(notifyPhase).observe(document.getElementById('battle'), { attributes: true, attributeFilter: ['data-mode'] });
  window.addEventListener('message', event => {
    if (event.source !== parent || event.data?.type !== 'combat-style-preview') return;
    apply(event.data.skin);
    if (event.data.view === 'edit' || event.data.view === 'combat') phase(event.data.view);
    notifyPhase();
  });
})();

