(() => {
  'use strict';
  const M = window.Materials;
  const input = () => '<span class="stepper" data-skin="button"><input type="number" aria-label="示例数量" value="5" min="1" max="999"><span class="step-buttons"><button class="step-arrow" data-delta="1" data-material-exception="stepper-triangle" aria-label="数量增加"><span class="triangle-glyph triangle-up" aria-hidden="true"></span></button><button class="step-arrow" data-delta="-1" data-material-exception="stepper-triangle" aria-label="数量减少"><span class="triangle-glyph triangle-down" aria-hidden="true"></span></button></span></span>';
  const families = [
    ['侧栏分页', 'navigation', () => M.button('概览', 'aria-pressed="false"', 'nav-button')],
    ['短页签', 'tab', () => M.button('指定天数', '', 'small-button tab-demo')],
    ['普通操作', 'regular', () => M.button('查看归元九息', '', 'regular-demo')],
    ['主操作', 'primary', () => M.button('开始修炼', '', 'primary')],
    ['小图标', 'icon', () => M.iconButton('help', '查看说明')],
    ['输入槽', 'input', input],
  ];
  document.querySelector('#family-samples').innerHTML = families.map(([label, family, control]) => `<section class="sheet-row" data-family="${family}"><h2 class="sheet-label">${label}</h2>${['paper','ink','scenery'].map(background => `<div class="sample-cell sample-${background}" data-background="${background}">${control()}</div>`).join('')}</section>`).join('');
  document.querySelector('#family-states').insertAdjacentHTML('beforeend', ['常态','悬停','按下','选中','焦点','不可用'].map((name, index) => M.button(name, index === 5 ? 'disabled' : '', ['demo-normal','demo-hover','demo-pressed','is-selected','demo-focus',''][index])).join(''));
  M.apply(document);
  document.addEventListener('click', event => {
    const tab = event.target.closest('.nav-button');
    if (tab) {
      const selected = !tab.classList.contains('is-selected');
      tab.classList.toggle('is-selected', selected);
      tab.setAttribute('aria-pressed', String(selected));
      return;
    }
    const button = event.target.closest('.step-arrow');
    if (!button) return;
    const field = button.closest('.stepper').querySelector('input');
    field.value = String(Math.max(Number(field.min), Math.min(Number(field.max), Number(field.value) + Number(button.dataset.delta))));
  });
  window.BUTTON_SHEET_READY = document.fonts.load('22px ZhenKai', '纸墨木石按钮与输入').then(() => document.fonts.ready);
})();
