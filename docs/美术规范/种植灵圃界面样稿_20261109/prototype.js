"use strict";
(() => {
  const C=window.PLANTING_CATALOG;
  const $=s=>document.querySelector(s);
  const clone=value=>JSON.parse(JSON.stringify(value));
  const esc=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const image=(path,alt='')=>`<img src="../../../${esc(path)}" alt="${esc(alt)}" draggable="false">`;
  const utility=id=>C.utilities.find(u=>u.id===id);
  const material=id=>C.materials.find(m=>m.id===id);
  const gradeName=grade=>['','九品','八品','七品','六品','五品','四品','三品','二品','一品'][grade]||'未定';
  const utilityText=m=>(m.utilities||[]).map(u=>u.id+'·'+gradeName(u.grade)).join('、');
  const initialFields=window.PLANTING_SCENE_FIXTURE.fields;
  const pages={overview:'田地概况',substrates:'基质材料',growth:'培育详览'};
  const state={page:'overview',fieldIndex:0,fields:clone(initialFields),drafts:{}};
  let modalCallback=null,modalReturn=null,toastTimer,formationEntry=null,dragPayload=null,dragSequence=0;
  const field=()=>state.fields[state.fieldIndex];
  const plant=()=>C.plants.find(p=>p.id===(field().crop?.id||field().target));
  function draft(){
    const f=field();
    if(!state.drafts[f.id])state.drafts[f.id]={selectedMaterial:null,quantity:'1',search:'',previewMode:'environment'};
    return state.drafts[f.id];
  }
  const quantity=list=>list.reduce((sum,m)=>sum+m.qty,0);
  const environment=()=>Object.fromEntries(C.dimensions.map(d=>[d.key,field().environmentDemo?.[d.key]||d.baseline]));
  const gaps=()=>plant().needs?C.dimensions.filter(d=>plant().needs[d.key]!==environment()[d.key]):null;
  const eligible = id => C.demoSubstrateEligibility[id] === true;
  function available(id){
    const m=material(id);if(!m)return 0;
    const used=state.fields.reduce((sum,f)=>sum+f.substrates.filter(x=>x.id===id).reduce((n,x)=>n+x.qty,0),0);
    return Math.max(0,m.stock-used);
  }
  function filteredMaterials(){
    const query=draft().search.trim().toLowerCase();
    return C.materials.filter(m=>eligible(m.id)&&available(m.id)>0&&(m.name+utilityText(m)).toLowerCase().includes(query));
  }
  function ensureVisibleMaterial(){
    if(!filteredMaterials().some(m=>m.id===draft().selectedMaterial)){draft().selectedMaterial=null;draft().quantity='1';}
  }
  function notify(message){
    $('#toast').textContent=message;$('#toast').hidden=false;
    clearTimeout(toastTimer);toastTimer=setTimeout(()=>{$('#toast').hidden=true;},2400);
  }
  function closeModal(){
    if($('#modal-layer').hidden)return;
    $('#modal-layer').hidden=true;modalCallback=null;
    if(modalReturn&&document.contains(modalReturn))modalReturn.focus();
    modalReturn=null;
  }
  function openModal(title,body,actions='',compact=false){
    if($('#modal-layer').hidden)modalReturn=document.activeElement;
    $('#modal').classList.toggle('compact-modal',compact);
    delete $('#modal').dataset.skinned;
    $('#modal').innerHTML=`<div class="modal-head"><h2 id="modal-title">${esc(title)}</h2><button class="button icon-button" data-skin="buttonIcon" data-action="close-modal" aria-label="关闭弹窗"><span class="button-content">${Materials.sprite('close')}</span></button></div><div class="modal-content">${body}</div>${actions?'<div class="modal-actions">'+actions+'</div>':''}`;
    $('#modal-layer').hidden=false;decorate($('#modal'));$('#modal').focus();
  }
  function confirm(title,body,label,callback){
    openModal(title,body,`<button class="button" data-action="close-modal">取消</button><button class="button primary" data-action="confirm-modal">${esc(label)}</button>`,true);
    modalCallback=callback;
  }
  function navigate(page){if(!pages[page])return;clearDrag();closeModal();state.page=page;history.pushState(null,'','#'+page);render();}
  function setField(index){if(index<0||index>=state.fields.length)return;clearDrag();closeModal();state.fieldIndex=index;render();}
  const heading=(title,extra='')=>`<div class="screen-head"><h1>${title}</h1><div class="head-controls">${extra}</div></div>`;
  function phaseLine(){
    const f=field();if(!f.crop)return '<p class="empty-phase">待环境适宜后，再择种定植。</p>';
    return `<ol class="phase-line" aria-label="生长阶段">${f.phaseSteps.map((name,i)=>`<li class="${i<f.phaseIndex?'complete':i===f.phaseIndex?'current':''}" ${i===f.phaseIndex?'aria-current="step"':''}><span class="phase-dot"></span><span>${esc(name)}</span></li>`).join('')}</ol>`;
  }
  function conditionTitle(){
    const missing=gaps();return missing===null?'尚待察明习性':missing.length?(field().crop?'长势迟缓':'尚待调理生境'):(field().crop?'安稳蕴养':'已知需求符合');
  }
  function primaryReason(){
    const missing=gaps();
    if(missing===null)return '完整培育需求尚未配置。';
    if(missing.length)return missing[0].issue+'，'+(field().crop?'未达当前培育需求。':'应先调整适生条件。');
    return field().crop?'已知环境需求符合，继续维持本田生境。':'已知环境需求符合，仍需按正式条件定植。';
  }
  function overviewEnvironment(){
    const p=plant(),env=environment();
    return C.dimensions.map(d=>{
      const need=p.needs?.[d.key],match=need===env[d.key];
      return `<tr data-dimension="${d.key}"><th scope="row">${d.label}</th><td class="environment-current">${env[d.key]}</td><td class="env-check ${need?(match?'is-suitable':'is-mismatch'):'is-unknown'}">${need?(match?'相宜':'需'+need):'需求待明'}</td></tr>`;
    }).join('');
  }
  function overview(){
    const f=field(),p=plant(),list=f.substrates;
    return heading(f.name,`<span class="state-label">${f.crop?'已定植':'空田 · 待定植'}</span>`)+`<div class="overview-grid">
      <section class="environment-summary" data-skin="paper" aria-labelledby="environment-title"><div class="section-head"><h2 id="environment-title">此地生境</h2><button class="link-button" data-route="growth">需求对照</button></div>
      <table class="overview-environment"><thead><tr><th>条件</th><th>本田环境</th><th>需求判定</th></tr></thead><tbody>${overviewEnvironment()}</tbody></table></section>
      <section class="plant-sheet" data-skin="paper"><div class="plant-overview">${image(p.image,p.name)}<div><p class="eyebrow">${f.crop?'本田灵植':'目标灵草'}</p><h2>${p.name}</h2><p class="plant-line">${esc(f.sceneDescription)}</p></div>${f.crop?'<span class="phase-seal">'+esc(f.crop.stage)+'</span>':'<button class="link-button choose-plant-action" data-action="choose-plant">选择灵草</button>'}</div>
      <div class="phase-section">${phaseLine()}</div>
      <div class="condition-brief"><div class="section-head"><h3>当前长势</h3><button class="link-button" data-route="growth">培育详览</button></div><p class="condition-title">${conditionTitle()}</p><p class="primary-reason">${primaryReason()}</p></div>
      <div class="field-operations">
        <div class="field-operation"><div><h3>基质材料</h3><span class="operation-summary">${list.length?list.length+' 种 · '+quantity(list)+' 件':'尚未加入材料'}</span></div><button class="button" data-route="substrates">添加材料</button></div>
        <div class="field-operation formation-operation"><div><h3>田地阵法</h3><span class="operation-summary">待接入</span></div><button id="open-formation" class="button" data-action="open-formation">查看阵法</button></div>
      </div></section></div>`;
  }
  function materialRows(){
    const d=draft(),rows=filteredMaterials();
    return rows.length?rows.map(m=>`<button class="inventory-row ${m.id===d.selectedMaterial?'selected':''}" data-skin="paper" draggable="true" data-action="select-material" data-id="${m.id}" aria-pressed="${m.id===d.selectedMaterial}"><span class="row-item">${image(m.image)}<strong>${m.name}</strong><span class="selection-mark" aria-hidden="true">${m.id===d.selectedMaterial?'✓':''}</span></span><span class="row-utilities">${esc(utilityText(m))}</span><span class="stock">${available(m.id)}</span></button>`).join(''):`<div class="empty-list"><p>${d.search?'没有找到材料':'行囊中暂无可用基质'}</p>${d.search?'<button class="link-button" data-action="clear-filter">清除搜索</button>':''}</div>`;
  }
  function joinedMaterials(){
    const list=field().substrates;
    return list.length?`<div class="joined-list">${list.map(x=>`<div class="joined-row"><span class="row-item">${image(material(x.id).image)}<button data-action="joined-material-info" data-id="${x.id}">${esc(material(x.id).name)}</button></span><span>${x.qty} 件</span></div>`).join('')}</div><div class="drop-tail"><span>＋</span> 拖入材料，继续添加</div>`:'<div class="drop-empty"><span aria-hidden="true">＋</span><p>将材料拖到这里</p><small>也可以选中左侧材料后，点击下方“加入”</small></div>';
  }
  function materialBar(){
    const d=draft(),m=material(d.selectedMaterial),enabled=m&&eligible(m.id)&&available(m.id)>0;
    return `<div class="selected-summary">${m?image(m.image):''}<span>${m?'已选：'+esc(m.name):'请选择行囊中的材料'}</span>${m?'<button class="link-button" data-action="material-utility-info">详情</button>':''}</div><div class="join-controls"><label for="material-quantity">数量</label><div class="stepper" data-skin="input"><input id="material-quantity" type="number" min="1" step="1" max="${enabled?available(m.id):0}" value="${esc(d.quantity)}" aria-label="加入数量" aria-describedby="quantity-error" ${enabled?'':'disabled'}><span class="step-buttons"><button class="step-arrow" data-material-exception="stepper-triangle" data-action="qty-plus" aria-label="增加件数" ${enabled?'':'disabled'}><span class="triangle-glyph triangle-up" aria-hidden="true"></span></button><button class="step-arrow" data-material-exception="stepper-triangle" data-action="qty-minus" aria-label="减少件数" ${enabled?'':'disabled'}><span class="triangle-glyph triangle-down" aria-hidden="true"></span></button></span></div><button id="add-material" class="button primary" data-action="add-material" ${enabled?'':'disabled'}>加入</button></div>`;
  }
  function materialPage(){
    return heading('添加基质',`<button class="link-button" data-route="growth">培育详览</button>`)+`<div class="substrate-layout">
      <section class="inventory-panel"><div class="list-heading"><h2>行囊</h2><span class="search-well" data-skin="input"><input id="material-search" type="search" placeholder="搜索材料" aria-label="搜索材料" value="${esc(draft().search)}"></span></div><div class="inventory-columns"><span>可作基质的材料</span><span>功用</span><span>持有</span></div><div id="material-list" class="inventory-list scroll">${materialRows()}</div></section>
      <section id="substrate-dropzone" class="substrate-panel" aria-label="拖入材料加入当前田地"><div class="list-heading"><h2>${esc(field().name)} · 基质</h2><button class="link-button" data-action="show-utility-sources">查看功用</button></div><div class="joined-columns"><span>已加入材料</span><span id="joined-count">${field().substrates.length} 种</span></div><div id="joined-materials" class="joined-content scroll">${joinedMaterials()}</div></section></div>
      <div id="substrate-actionbar" class="substrate-actionbar">${materialBar()}</div><p id="quantity-error" class="inline-error" aria-live="polite"></p>`;
  }
  function needsRows(){
    const p=plant(),env=environment();
    return C.dimensions.map(d=>{const need=p.needs?.[d.key],matches=need===env[d.key];return `<tr><td>${d.label}</td><td>${env[d.key]}</td><td>${need||'资料未齐'}</td><td class="${need?(matches?'good':'bad'):'muted'}">${need?(matches?'✓ 相宜':'× 不足或不适'):'—'}</td></tr>`;}).join('');
  }
  function needsTable(){return `<table class="needs-table"><thead><tr><th>条件</th><th>本田环境</th><th>灵植需求</th><th>判定</th></tr></thead><tbody>${needsRows()}</tbody></table>`;}
  function growthPage(){
    const f=field(),p=plant(),missing=gaps();
    return heading('培育详览',`<label for="growth-target">${f.crop?'本田灵植':'目标灵草'}</label><span class="selector-well" data-skin="input"><select id="growth-target" aria-label="预览灵草" ${f.crop?'disabled':''}>${C.plants.map(x=>`<option value="${x.id}" ${x.id===p.id?'selected':''}>${x.name}</option>`).join('')}</select></span>`)+`
      <div class="growth-grid"><section class="environment-sheet" data-skin="paper"><div class="section-head"><h2>生境与需求</h2><span class="state-label">${missing===null?'尚待察明':missing.length?missing.length+' 项待调理':'已知条件相宜'}</span></div>${needsTable()}<div class="source-note"><span>本田基质</span><button class="link-button" data-action="show-utility-sources">查看功用来源</button></div><p class="table-note">${f.substrates.length?'已加入 '+f.substrates.length+' 种材料，共 '+quantity(f.substrates)+' 件。具体培育作用待配置。':'加入材料后，可在此查看其功用与来源。'}</p></section>
      <section class="forecast-sheet"><p class="eyebrow">${p.name} · ${f.crop?esc(f.crop.stage):'待定植'}</p><h2 class="forecast-title">${conditionTitle()}</h2><p class="forecast-subtitle">${primaryReason()}</p><div class="detail-stage">${phaseLine()}</div>
      <div class="culture-reading"><span>植株状况</span><strong>${f.injuryDemo?esc(f.injuryDemo):f.crop?'尚待察明':'尚未定植'}</strong></div>
      <div class="risk-note"><h3>持续不适</h3><p>长势迟缓 → 生长停滞<br>持续严重不适，才会受损或枯死。</p></div>
      <div class="time-answer"><span>可采时机</span><strong>${f.crop?'尚未到可采阶段':'尚未定植'}</strong><small>周期与耐受时长待定</small></div>
      <div class="forecast-links"><button class="link-button" data-action="show-plan">查看已加基质</button><button class="link-button" data-route="substrates">添加材料</button></div></section></div>`;
  }
  function bindQuantity(){
    if(!$('#material-quantity'))return;
    $('#material-quantity').addEventListener('input',e=>{draft().quantity=e.target.value;updateQuantity();});updateQuantity();
  }
  function updateMaterialParts(){
    ensureVisibleMaterial();$('#material-list').innerHTML=materialRows();$('#substrate-actionbar').innerHTML=materialBar();delete $('#substrate-actionbar').dataset.skinned;decorate($('#screen'));bindQuantity();
  }
  function render(){
    const f=field();draft();
    $('#field-select').innerHTML=state.fields.map(x=>`<option value="${x.id}" ${x.id===f.id?'selected':''}>${x.name}</option>`).join('');
    $('#field-prev').disabled=state.fieldIndex===0;$('#field-next').disabled=state.fieldIndex===state.fields.length-1;
    $('#field-prev').title=state.fieldIndex===0?'已是第一块田地':'';$('#field-next').title=state.fieldIndex===state.fields.length-1?'已是最后一块田地':'';
    document.querySelectorAll('.page-tabs [data-route]').forEach(b=>{b.classList.toggle('active',b.dataset.route===state.page);if(b.dataset.route===state.page)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');});
    $('#screen').innerHTML=({overview,substrates:materialPage,growth:growthPage})[state.page]();decorate($('#app'));
    if(state.page==='substrates'){
      $('#material-search').addEventListener('input',e=>{draft().search=e.target.value;updateMaterialParts();});bindQuantity();
    }
    if(state.page==='growth')$('#growth-target').addEventListener('change',e=>{if(!field().crop){field().target=e.target.value;render();}});
  }
  function validAmount(id,raw){
    const n=Number(raw);
    return eligible(id)&&/^\d+$/.test(String(raw))&&Number.isSafeInteger(n)&&n>0&&n<=available(id);
  }
  function validQuantity(){return validAmount(draft().selectedMaterial,draft().quantity);}
  function updateQuantity(){
    const d=draft(),selected=!!d.selectedMaterial,valid=validQuantity();
    $('#add-material').disabled=!valid;
    $('#add-material').title=valid?'':selected?'请填写有效的加入数量':'请先选择可用材料';
    const n=Number(d.quantity),remaining=available(d.selectedMaterial);
    $('[data-action="qty-minus"]').disabled=!valid||n<=1;
    $('[data-action="qty-plus"]').disabled=!valid||n>=remaining;
    $('[data-action="qty-minus"]').title=!selected?'请先选择可用材料':n<=1?'最少加入一件':'';
    $('[data-action="qty-plus"]').title=!selected?'请先选择可用材料':n>=remaining?'已达到剩余持有量':'';
    $('#quantity-error').textContent=!selected||valid?'':'请输入 1 至 '+available(d.selectedMaterial)+' 的整件数量。';
    $('#material-quantity').setAttribute('aria-invalid',String(selected&&!valid));
  }
  function joinMaterial(id,raw,ownerId=field().id){
    if(ownerId!==field().id||!validAmount(id,raw))return false;
    const n=Number(raw),list=field().substrates,existing=list.find(x=>x.id===id);
    if(existing)existing.qty+=n;else list.push({id,qty:n});
    draft().quantity='1';ensureVisibleMaterial();render();notify('已加入'+material(id).name+' ×'+n+'。');return true;
  }
  function choosePlant(){openModal('选择目标灵草',C.plants.map(p=>`<button class="plant-choice" data-action="set-plant" data-id="${p.id}">${image(p.image,p.name)}<div><strong>${p.name}</strong><p>${esc(p.habitat)}</p></div></button>`).join(''));}
  function materialInfo(id=draft().selectedMaterial){
    const m=material(id);if(!m)return;
    openModal(m.name+' · 基质功用',`<table class="needs-table"><thead><tr><th>功用</th><th>单项品级</th><th>语义</th></tr></thead><tbody>${m.utilities.map(u=>`<tr><td>${u.id}</td><td>${gradeName(u.grade)}</td><td>${esc(utility(u.id)?.meaning||'')}</td></tr>`).join('')}</tbody></table><div class="detail-note"><h3>培育用途候选</h3>${m.utilities.map(u=>'<p>'+esc(u.id)+'：'+esc(utility(u.id)?.plantingIntent||'待定义')+'</p>').join('')}<p class="muted">功用通过加入田地的基质材料发挥。具体作用与适用条件待配置，品级不直接换算为生长倍数。</p></div>`);
  }
  function utilitySources(){
    const grouped=new Map();
    field().substrates.forEach(entry=>material(entry.id).utilities.forEach(u=>{if(!grouped.has(u.id))grouped.set(u.id,[]);grouped.get(u.id).push({name:material(entry.id).name,qty:entry.qty,grade:u.grade});}));
    const rows=C.utilities.filter(u=>grouped.has(u.id)).map(u=>`<tr><td>${u.id}</td><td>${grouped.get(u.id).map(x=>esc(x.name)+' ×'+x.qty+' · '+gradeName(x.grade)).join('<br>')}</td><td>${u.meaning}</td></tr>`).join('');
    openModal('本田基质 · 功用来源',rows?`<table class="needs-table"><thead><tr><th>功用</th><th>材料与单项品级</th><th>语义</th></tr></thead><tbody>${rows}</tbody></table><p class="detail-note muted">保留各材料的来源与品级；件数和品级不相加为培育总分。实际培育效果待配置。</p>`:'<p class="empty-note">本田尚未加入基质材料。</p>');
  }
  function showPlan(){
    const list=field().substrates;
    openModal('本田已加基质',list.length?`<div class="selection-summary">${list.map(x=>'<p>'+esc(material(x.id).name)+' ×'+x.qty+'</p>').join('')}</div><p class="muted">合计 ${list.length} 种、${quantity(list)} 件。</p>`:'<p class="empty-note">尚未加入材料。</p>','',true);
  }
  function openFormation(){
    if(formationEntry){formationEntry(Object.freeze({fieldId:field().id}));return;}
    openModal('田地阵法',`<p class="eyebrow">${esc(field().name)}</p><div class="formation-placeholder"><span aria-hidden="true">阵</span><div><h3>阵法系统待接入</h3><p class="muted">后续从此处配置本田阵法。</p></div></div>`,'',true);
  }
  function showNotes(){
    openModal('样稿说明','<p>本版暂不展示田面图，概况按生境与灵植两栏排版，基质和阵法入口置于页面正文。基质仍为行囊列表，可拖入或选中数量后直接加入；阵法只留独立入口。</p><p>一号田是改造后的月露苔培育情境示意，阶段、植株状况和当前环境为界面夹具；所有田地仍共享同批基础条件。情境不代表正式物种参数、先天地利或已定义的材料效果。</p><p>材料名称、图标及单项功用品级来自项目，资格与持有量为演示数据。加入只更新本页记录，刷新恢复，不写真实行囊、人物或存档；未定义的培育作用不自动改变环境或长势。</p><p>田面插图暂不显示，控件引用项目共用木纸素材。当前没有连续生长结算、真实投料、费用或采收事务。</p>');
  }
  function pickField(){
    openModal('选择田地',state.fields.map((f,i)=>'<button class="field-choice" data-action="pick-field" data-index="'+i+'"><strong>'+esc(f.name)+'</strong><span>'+(f.crop?esc(C.plants.find(p=>p.id===f.crop.id).name)+' · '+esc(f.crop.stage):'空田 · 待定植')+'</span><b>›</b></button>').join(''),'',true);
  }
  function decorate(root){
    root.querySelectorAll('button.link-button,.joined-row .row-item button').forEach(e=>{e.classList.add('button','compact-button');e.dataset.skin='button';});
    root.querySelectorAll('.inventory-panel,.substrate-panel,.substrate-actionbar,.environment-sheet').forEach(e=>{if(!e.dataset.skin)e.dataset.skin='paper';});
    if(root.id==='modal'&&!root.dataset.skin){root.dataset.skin='panel';}
    root.querySelectorAll('button.button,button.page-return,.modal-head button').forEach(e=>{if(!e.dataset.skin)e.dataset.skin=e.classList.contains('primary')?'buttonPrimary':e.classList.contains('page-return')?'button':'button';});
    root.querySelectorAll('.stepper').forEach(e=>{if(!e.dataset.skin)e.dataset.skin='input';});
    root.querySelectorAll('.search-well').forEach(e=>{if(!e.dataset.skin)e.dataset.skin='input';});
    root.querySelectorAll('button[data-skin]').forEach(e=>{
      if(e.querySelector(':scope > .button-content'))return;
      const content=document.createElement('span');content.className='button-content';
      [...e.childNodes].filter(n=>!n.classList?.contains('skin-layer')&&!n.classList?.contains('nav-binding')).forEach(n=>content.append(n));
      e.append(content);
    });
    Materials.apply(root);
    root.querySelectorAll('.page-tabs button').forEach(e=>e.classList.toggle('is-selected',e.classList.contains('active')));
  }

  function perform(action,el){
    const d=draft(),f=field();
    switch(action){
      case 'close-modal':closeModal();break;
      case 'confirm-modal':{if(!modalCallback)return;const callback=modalCallback;modalCallback=null;el.disabled=true;closeModal();callback();break;}
      case 'choose-plant':choosePlant();break;
      case 'open-formation':openFormation();break;
      case 'pick-field':closeModal();setField(Number(el.dataset.index));break;
      case 'set-plant':if(!f.crop&&C.plants.some(p=>p.id===el.dataset.id)){f.target=el.dataset.id;closeModal();render();}break;
      case 'select-material':if(eligible(el.dataset.id)&&available(el.dataset.id)>0){d.selectedMaterial=el.dataset.id;d.quantity='1';render();}break;
      case 'material-utility-info':materialInfo();break;
      case 'joined-material-info':materialInfo(el.dataset.id);break;
      case 'show-utility-sources':utilitySources();break;
      case 'show-plan':showPlan();break;
      case 'clear-filter':d.search='';ensureVisibleMaterial();render();break;
      case 'qty-minus':
      case 'qty-plus':{if(!d.selectedMaterial)return;const n=Number(d.quantity),delta=action==='qty-plus'?1:-1;d.quantity=String(Math.min(Math.max(1,(Number.isFinite(n)?Math.trunc(n):1)+delta),Math.max(1,available(d.selectedMaterial))));render();break;}
      case 'add-material':joinMaterial(d.selectedMaterial,d.quantity);break;
      case 'preview-environment':d.previewMode='environment';render();break;
      case 'preview-plan':d.previewMode='plan';render();break;
    }
  }
  function clearDrag(){
    dragPayload=null;document.querySelectorAll('.drag-over,.dragging').forEach(e=>e.classList.remove('drag-over','dragging'));
    document.querySelectorAll('.material-drag-ghost').forEach(e=>e.remove());
  }
  document.addEventListener('dragstart',e=>{
    const row=e.target.closest('.inventory-row');
    if(!row||!eligible(row.dataset.id)||available(row.dataset.id)<=0){e.preventDefault();return;}
    const d=draft();
    if(d.selectedMaterial!==row.dataset.id){d.selectedMaterial=row.dataset.id;d.quantity='1';}
    if(!validQuantity()){e.preventDefault();notify('请先设置有效的加入数量。');return;}
    dragPayload={token:++dragSequence,fieldId:field().id,id:row.dataset.id,qty:Number(d.quantity)};
    e.dataTransfer.effectAllowed='copy';e.dataTransfer.setData('application/x-planting-material',JSON.stringify(dragPayload));
    const ghost=document.createElement('div');ghost.className='material-drag-ghost';ghost.textContent=material(row.dataset.id).name+' ×'+dragPayload.qty;document.body.append(ghost);e.dataTransfer.setDragImage(ghost,12,18);
    row.classList.add('dragging');
  });
  document.addEventListener('dragover',e=>{
    const zone=e.target.closest('#substrate-dropzone');if(!zone)return;
    e.preventDefault();
    const valid=dragPayload&&dragPayload.fieldId===field().id&&validAmount(dragPayload.id,dragPayload.qty);
    e.dataTransfer.dropEffect=valid?'copy':'none';zone.classList.toggle('drag-over',!!valid);
  });
  document.addEventListener('dragleave',e=>{
    const zone=e.target.closest('#substrate-dropzone');
    if(zone&&!zone.contains(e.relatedTarget))zone.classList.remove('drag-over');
  });
  document.addEventListener('drop',e=>{
    const zone=e.target.closest('#substrate-dropzone');if(!zone)return;e.preventDefault();
    const payload=dragPayload;clearDrag();
    if(!payload||payload.fieldId!==field().id)return;
    let data;try{data=JSON.parse(e.dataTransfer.getData('application/x-planting-material'));}catch{return;}
    if(data.token!==payload.token||data.id!==payload.id||data.qty!==payload.qty||data.fieldId!==payload.fieldId)return;
    joinMaterial(payload.id,payload.qty,payload.fieldId);
  });
  document.addEventListener('dragend',()=>{clearDrag();if(state.page==='substrates')render();});

  document.addEventListener('click',e=>{
    const action=e.target.closest('[data-action]');
    if(action&&!action.disabled){perform(action.dataset.action,action);return;}
    const route=e.target.closest('[data-route]');if(route&&!route.disabled)navigate(route.dataset.route);
  });
  $('#field-select').addEventListener('change',e=>setField(state.fields.findIndex(x=>x.id===e.target.value)));
  $('#field-prev').addEventListener('click',()=>setField(state.fieldIndex-1));
  $('#field-next').addEventListener('click',()=>setField(state.fieldIndex+1));
  $('#preview-back').addEventListener('click',()=>{if(state.page==='overview')pickField();else navigate('overview');});
  $('#open-notes').addEventListener('click',showNotes);
  $('#reset-demo').addEventListener('click',()=>confirm('重置演示','<p>清除本页加入记录，恢复示例田地。</p>','重置演示',()=>{state.fields=clone(initialFields);state.drafts={};state.fieldIndex=0;state.page='overview';history.replaceState(null,'','#overview');render();notify('演示已重置。');}));
  $('.modal-shade').addEventListener('click',closeModal);
  document.addEventListener('keydown',e=>{
    if(e.key==='Escape'){if(!$('#modal-layer').hidden)closeModal();else if(state.page!=='overview')navigate('overview');}
    if(e.key==='Tab'&&!$('#modal-layer').hidden){
      const elements=[...$('#modal').querySelectorAll('button:not(:disabled),input:not(:disabled),select:not(:disabled),a[href]')],first=elements[0],last=elements.at(-1);if(!first)return;
      if(e.shiftKey&&(document.activeElement===first||document.activeElement===$('#modal'))){last.focus();e.preventDefault();}
      else if(!e.shiftKey&&(document.activeElement===last||document.activeElement===$('#modal'))){first.focus();e.preventDefault();}
    }
  });
  function fit(){const v=$('#viewport');$('#app').style.setProperty('--scale',Math.min(v.clientWidth/1664,v.clientHeight/936));}
  window.addEventListener('resize',fit);
  window.addEventListener('popstate',()=>{const page=location.hash.slice(1);closeModal();state.page=pages[page]?page:'overview';render();if(page==='formations')openFormation();});
  window.PlantingPrototype={version:'2.2',snapshot:()=>clone(state),scope:C.scope,
    connectFormationSystem(handler){if(handler!==null&&typeof handler!=='function')throw new TypeError('阵法入口应为函数或null');formationEntry=handler;}
  };
  const initial=location.hash.slice(1);state.page=pages[initial]?initial:'overview';
  history.replaceState(null,'','#'+state.page);fit();render();if(initial==='formations')openFormation();
})();

