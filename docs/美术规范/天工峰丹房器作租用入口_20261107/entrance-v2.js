(() => {
  'use strict';
  const $ = (s, root = document) => root.querySelector(s);
  const $$ = (s, root = document) => [...root.querySelectorAll(s)];
  const root = '../../../';
  const facilities = {
    alchemy: {name:'丹房', purpose:'炼丹', title:'丹房租用', icon:root+'textures/items/alchemy_20261024/porcelain_bottle_closed.png', desc:'可携自备药材入内炼丹。', enter:'进入丹房', intro:'宗门炼丹场所'},
    forging: {name:'器作', purpose:'炼器', title:'器作租用', icon:root+'textures/ui/qingxi/bottom_nav_craft_ink_v1.png', desc:'可携自备材料入内炼器。', enter:'进入器作', intro:'宗门炼器场所'}
  };
  const state = {page:facilities[location.hash.slice(1)]?location.hash.slice(1):'alchemy', forms:{alchemy:'',forging:''}, views:{alchemy:'new',forging:'new'}};
  const esc = s => String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const statusName = {new:'尚未租用',active:'租期内可用',expired:'租期已结束',poor:'尚未租用',visitor:'暂不可用'};
  const price = (name, val, cls='') => `<div class="price-line ${cls}"><dt>${name}</dt><dd>${val}</dd></div>`;
  let returnFocus;
  function fit(){const v=$('#viewport');$('#stage').style.setProperty('--scale',Math.min(v.clientWidth/1664,v.clientHeight/936));}
  function validDuration(){const text=state.forms[state.page].trim();return /^\d+(?:\.\d+)?$/.test(text)&&Number.isFinite(Number(text))&&Number(text)>0;}
  function closeDialog(){
    $('#overlay').hidden=true;$('#dialog').replaceChildren();
    if(returnFocus?.isConnected)returnFocus.focus({preventScroll:true});
  }
  function openDialog(title,content,after){
    returnFocus=document.activeElement;
    $('#dialog').innerHTML=`<div class="dialog-head"><h2 id="dialog-title">${title}</h2><button data-close>返回</button></div>${content}`;
    $('#overlay').hidden=false;$('[data-close]',$('#dialog')).onclick=closeDialog;$('#dialog').focus();
    if(after)after();
  }
  function updateForm(){
    const view=state.views[state.page], f=facilities[state.page], valid=validDuration();
    const text=state.forms[state.page].trim();
    const invalid=text.length>0&&!valid;
    const error=$('#duration-error');
    if(error){error.hidden=!invalid;error.textContent=invalid?'请填写大于零的租用时长。':'';}
    const primary=$('#primary');
    primary.textContent=view==='active'?f.enter:'租用并进入';
    primary.disabled=view==='active'?false:!valid||view==='visitor'||view==='poor';
    primary.title=primary.disabled?(view==='visitor'?'仅限玉虚仙宗成员':view==='poor'?'可用贡献不足':'请填写租用时长'):view==='active'?f.enter:'查看租用确认';
    const copy={new:valid?`租用${f.name} ${text} 时辰　｜　按租用时长收取贡献`:'选择租用时长，核对租金后进入。', active:`${f.name}租期内可使用　｜　${f.enter}继续${f.purpose}`, expired:valid?`重新租用${f.name} ${text} 时辰　｜　按租用时长收取贡献`:'租期已结束，请重新选择租用时长。',poor:'可用贡献不足，请调整租期或补足贡献。',visitor:'仅限玉虚仙宗成员使用。'};
    $('#footer-copy').textContent=copy[view];
  }
  function render(){
    const f=facilities[state.page],view=state.views[state.page];
    document.title=`天工峰 · ${f.title}入口样稿`;
    $('#page-title').textContent=`天工峰 · ${f.name}`;
    $('#state-select').value=view;
    $('#review-status').textContent='按时长收贡献 · 单价待定 · 状态为排版示例';
    $$('[data-page]').forEach(b=>{b.classList.toggle('active',b.dataset.page===state.page);b.setAttribute('aria-pressed',b.dataset.page===state.page);});
    $('#facility-switch').innerHTML=Object.entries(facilities).map(([id,x])=>`<button class="facility-tab ${id===state.page?'selected':''}" data-facility="${id}" aria-pressed="${id===state.page}">${x.name}</button>`).join('');
    const lease=view==='active'?`租期内可进入${f.name}。`:view==='expired'?'租期已结束，重新租用后可进入。':view==='visitor'?'加入玉虚仙宗后可使用此处设施。':'尚未租用此处场所。';
    $('#facility-info').innerHTML=`<div class="intro-head"><h2>入内须知</h2><span class="status ${view}">${statusName[view]}</span></div><div class="info-details"><div><p class="description">${f.desc}</p><p class="state-note">${lease}</p></div><p class="member-check ${view==='visitor'?'unmet':''}"><span>${view==='visitor'?'✕':'✓'}</span> 玉虚仙宗成员</p></div>`;
    $('#rental-title').textContent=view==='active'?f.name+' · 当前租用':'租用'+f.name;
    $('#billing-note').textContent=view==='active'?'租期内可用':'按租用时长收贡献';
    let right;
    if(view==='active'){
      right=`<dl class="active-lease">${price('租用场所','天工峰 · '+f.name)}${price('剩余时长','—')}${price('到期时间','—')}</dl><p class="description active-copy">可在租期内进入${f.name}${f.purpose}。</p><div class="notice">${f.purpose==='炼丹'?'药材':'材料'}请自行准备。</div>`;
    }else{
      right=`<label class="duration-field" for="duration">租用时长</label><div class="duration-input"><input id="duration" inputmode="decimal" autocomplete="off" aria-label="租用时长" placeholder="填写租用时长" value="${esc(state.forms[state.page])}" ${view==='visitor'?'disabled':''}><span>时辰</span></div><p class="unit-hint">一时辰为两游戏小时</p><p id="duration-error" class="form-error" role="alert" hidden></p><dl class="price-lines">${price('租用单价','待定 贡献／时辰')}${price('本次租金','待定 贡献','total')}</dl><div class="notice ${view==='poor'||view==='visitor'?'error':''}">${view==='poor'?'可用贡献不足，请调整租期或补足贡献。':view==='visitor'?'仅限玉虚仙宗成员。':'填写租期，核对租金后确认租用。'}</div>`;
    }
    $('#rental-body').innerHTML=right;
    $$('[data-facility]').forEach(b=>b.onclick=()=>navigate(b.dataset.facility));
    if($('#duration'))$('#duration').oninput=e=>{state.forms[state.page]=e.target.value;updateForm();};
    updateForm();fit();
  }
  function navigate(page){if(!facilities[page])return;if(!$('#overlay').hidden)closeDialog();state.page=page;history.replaceState(null,'','#'+page);render();}
  function confirmRental(){
    const f=facilities[state.page];
    openDialog(`确认租用${f.name}`,`<p class="dialog-facility">玉虚仙宗 · 天工峰 · ${f.name}</p><dl class="dialog-cost">${price('租用时长',esc(state.forms[state.page].trim())+' 时辰')}${price('租用单价','待定 贡献／时辰')}${price('可用贡献','—')}${price('本次租金','待定 贡献','total')}</dl><p class="notice">租用后可进入${f.name}${f.purpose}。</p><div class="dialog-foot"><button id="revise-duration">返回修改</button><button class="confirm" id="pay-and-enter" disabled title="租用单价待定">确认租用并进入</button></div>`,()=>{$('#revise-duration').onclick=closeDialog;});
  }
  $('#primary').onclick=()=>{
    if(state.views[state.page]==='active'){
      $('#review-status').textContent=`已预览：${facilities[state.page].enter}，沿用现有${facilities[state.page].purpose}界面`;
      return;
    }
    if(validDuration())confirmRental();
  };
  $('#state-select').onchange=e=>{if(!$('#overlay').hidden)closeDialog();state.views[state.page]=e.target.value;render();};
  $$('[data-page]').forEach(b=>b.onclick=()=>navigate(b.dataset.page));
  $('#back').onclick=()=>openDialog('玉虚仙宗 · 天工峰','<div class="parent-actions"><button data-parent="alchemy">丹房</button><button data-parent="forging">器作</button></div>',()=>{$$('[data-parent]').forEach(b=>b.onclick=()=>{closeDialog();navigate(b.dataset.parent);});});
  $('.veil').onclick=closeDialog;
  $('#notes-open').onclick=()=>{$('#notes').hidden=!$('#notes').hidden;$('#notes-close').focus();};
  $('#notes-close').onclick=()=>{$('#notes').hidden=true;$('#notes-open').focus();};
  function immersive(on){document.body.classList.toggle('immersive',on);$('#restore').hidden=!on;fit();}
  $('#immersive').onclick=()=>immersive(true);$('#restore').onclick=()=>immersive(false);
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'){
      if(!$('#notes').hidden){$('#notes').hidden=true;$('#notes-open').focus();}
      else if(!$('#overlay').hidden)closeDialog();
      else if(document.body.classList.contains('immersive'))immersive(false);
      e.preventDefault();
    }
    if(e.key==='Tab'&&!$('#overlay').hidden){
      const nodes=$$('button:not(:disabled),input,select',$('#dialog')).filter(n=>n.getClientRects().length);
      if(e.shiftKey&&(document.activeElement===nodes[0]||document.activeElement===$('#dialog'))){nodes.at(-1).focus();e.preventDefault();}
      else if(!e.shiftKey&&document.activeElement===nodes.at(-1)){nodes[0].focus();e.preventDefault();}
    }
  });
  window.addEventListener('resize',fit);
  window.addEventListener('hashchange',()=>{const id=location.hash.slice(1);if(facilities[id]&&id!==state.page)navigate(id);});
  render();document.fonts.ready.then(fit);
})();
