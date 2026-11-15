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
  const scene = root+'textures/sect_overview/repaint_20261103/live_composition_item_style_20261104/tiangong.png';
  const statusName = {new:'尚未租用',active:'租期内可用',expired:'租期已结束',poor:'尚未租用',visitor:'暂不可用'};
  const column = (title, body) => `<section class="column"><h2>${title}</h2><div class="body">${body}</div></section>`;
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
    const left=`${Object.entries(facilities).map(([id,x])=>`<button class="facility-card ${id===state.page?'selected':''}" data-facility="${id}" aria-pressed="${id===state.page}"><img src="${x.icon}" alt=""><div><h3>${x.name}</h3><p>${x.intro}</p></div></button>`).join('')}<h3 class="mini-heading">所在场所</h3><div class="fact"><span>宗门</span><span>玉虚仙宗</span></div><div class="fact"><span>所在峰</span><span>天工峰</span></div>`;
    const lease=view==='active'?`<p class="state-note">租期内可由右下入口进入${f.name}。</p>`:`<p class="state-note">${view==='expired'?'租期已结束，重新租用后可进入。':view==='visitor'?'加入玉虚仙宗后可使用此处设施。':'尚未租用此处场所。'}</p>`;
    const middle=`<div class="scene"><img src="${scene}" alt="当前天工峰场景"><span class="scene-label">天工峰</span></div><div class="intro-head"><h3>${f.name}</h3><span class="status ${view}">${statusName[view]}</span></div><p class="description">${f.desc}</p><div class="fact"><span>使用条件</span><span>${view==='visitor'?'✕':'✓'} 玉虚仙宗成员</span></div><div class="lease"><h3>我的租用</h3>${lease}</div>`;
    let right;
    if(view==='active'){
      right=`<h3 class="status-heading">${f.name}租期内可用</h3><div class="active-lease"><h3>当前租用</h3><div class="fact"><span>租用场所</span><span>天工峰 · ${f.name}</span></div><div class="fact"><span>剩余时长</span><span>—</span></div><div class="fact"><span>到期时间</span><span>—</span></div></div><p class="description">可在租期内进入${f.name}${f.purpose}。</p><div class="notice">${f.purpose==='炼丹'?'药材':'材料'}请自行准备。</div>`;
    }else{
      right=`<div class="rental-tag">按租用时长收取贡献</div><label class="duration-field" for="duration">租用时长</label><div class="duration-input"><input id="duration" inputmode="decimal" autocomplete="off" aria-label="租用时长" placeholder="填写租用时长" value="${esc(state.forms[state.page])}" ${view==='visitor'?'disabled':''}><span>时辰</span></div><p class="unit-hint">一时辰为两游戏小时</p><p id="duration-error" class="form-error" role="alert" hidden></p><dl class="price-lines">${price('租用单价','待定 贡献／时辰')}${price('可用贡献','—')}${price('本次租金','待定 贡献','total')}</dl><div class="notice ${view==='poor'||view==='visitor'?'error':''}">${view==='poor'?'可用贡献不足，请调整租期或补足贡献。':view==='visitor'?'仅限玉虚仙宗成员。':'填写租期，核对租金后确认租用。'}</div>`;
    }
    $('#columns').innerHTML=column('宗门设施',left)+column('场所与使用条件',middle)+column(view==='active'?'租用状态':'租用办理',right);
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
