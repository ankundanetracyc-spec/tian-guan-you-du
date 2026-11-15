(() => {
  'use strict';
  const M=window.Materials,E=M.escape,stage=document.querySelector('#stage'),tipData=new Map(),tip=document.querySelector('#tooltip');
  let serial=0,toastTimer,dragging=false;
  function hideTip(){tip.hidden=true;}
  function showTip(anchor){
    if(dragging||window.RoleBattle.isDragging())return;
    const data=tipData.get(anchor.dataset.tip);if(!data)return;
    tip.innerHTML=`<h3>${E(data.title)}</h3>${M.divider('')}<p>${E(data.text).replace(/\n/g,'<br>')}</p>`;
    M.apply(tip);tip.hidden=false;
    const r=anchor.getBoundingClientRect(),b=tip.getBoundingClientRect();let x=r.right+12;if(x+b.width>innerWidth-12)x=r.left-b.width-12;
    tip.style.left=`${Math.max(12,Math.min(x,innerWidth-b.width-12))}px`;tip.style.top=`${Math.max(12,Math.min(r.top,innerHeight-b.height-12))}px`;
  }
  function notify(message){const toast=document.querySelector('#toast');toast.textContent=message;toast.hidden=false;M.apply(toast);clearTimeout(toastTimer);toastTimer=setTimeout(()=>toast.hidden=true,2500);}
  function modal(title,content,footer=''){
    hideTip();document.querySelectorAll('dialog[open]').forEach(d=>d.close());
    const host=document.querySelector('#overlay-root');host.innerHTML=`<dialog class="preview-dialog" data-skin="panel" aria-label="${E(title)}"><div class="dialog-heading"><h2>${E(title)}</h2>${M.iconButton('close','关闭弹窗','data-close-modal')}</div><div class="dialog-content">${content}</div><div class="dialog-footer">${footer||M.button('返回','data-close-modal')}</div></dialog>`;
    const dialog=host.querySelector('dialog');M.apply(dialog);M.attachScroll(dialog);dialog.showModal();
  }
  const context={tip:(title,text)=>{const id='battle-tip-'+ ++serial;tipData.set(id,{title,text});return `data-tip="${id}"`;},modal,notify,hideTip};
  function fit(){const viewport=document.querySelector('#viewport'),scale=Math.min(viewport.clientWidth/1560,viewport.clientHeight/877);stage.style.width=`${viewport.clientWidth/scale-32}px`;stage.style.transform=`scale(${scale})`;}
  function render(){stage.innerHTML=window.RoleBattle.render(context);M.apply(document);M.attachScroll(stage);window.RoleBattle.mount(stage);fit();}
  document.addEventListener('click',event=>{const target=event.target.closest('[data-close-modal]');if(target)target.closest('dialog')?.close();});
  document.addEventListener('pointerover',event=>{const anchor=event.target.closest('[data-tip]');if(anchor)showTip(anchor);else hideTip();});
  document.addEventListener('focusin',event=>{const anchor=event.target.closest('[data-tip]');if(anchor)showTip(anchor);});
  document.addEventListener('focusout',hideTip);document.addEventListener('dragstart',()=>{dragging=true;hideTip();},true);document.addEventListener('dragend',()=>{dragging=false;hideTip();},true);document.addEventListener('keydown',event=>{if(event.key==='Escape')hideTip();});
  window.addEventListener('resize',()=>{hideTip();fit();});render();
  window.BATTLE_WORKSPACE_READY=document.fonts.load('22px ZhenKai','战斗配招组合方案').then(()=>document.fonts.ready).then(()=>{fit();return true;});
})();
