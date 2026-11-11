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
  const initialFields=[
    {id:'field-1',name:'一号田',target:'moon',crop:null,substrates:[]},
    {id:'field-2',name:'二号田',target:'bell',crop:{id:'bell',stage:'幼苗'},substrates:[]},
    {id:'field-3',name:'三号田',target:'moon',crop:null,substrates:[]}
  ];
  const pages={overview:'田地概况',substrates:'基质材料',growth:'成长预览'};
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
  const gaps=()=>plant().needs?C.dimensions.filter(d=>plant().needs[d.key]!==d.baseline):null;
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
    $('#modal').innerHTML=`<div class="modal-head"><h2 id="modal-title">${esc(title)}</h2><button data-action="close-modal" aria-label="关闭弹窗">×</button></div><div class="modal-content">${body}</div>${actions?'<div class="modal-actions">'+actions+'</div>':''}`;
    $('#modal-layer').hidden=false;$('#modal').focus();
  }
  function confirm(title,body,label,callback){
    openModal(title,body,`<button class="button" data-action="close-modal">取消</button><button class="button primary" data-action="confirm-modal">${esc(label)}</button>`,true);
    modalCallback=callback;
  }
  function navigate(page){if(!pages[page])return;clearDrag();closeModal();state.page=page;history.pushState(null,'','#'+page);render();}
  function setField(index){if(index<0||index>=state.fields.length)return;clearDrag();closeModal();state.fieldIndex=index;render();}
  const heading=(title,extra='')=>`<div class="screen-head"><h1>${title}</h1><div class="head-controls">${extra}</div></div>`;
  function plot(){
    return `<div class="field-visual" aria-label="${esc(field().name)} · ${field().crop?'雾铃花幼苗':'尚未定植'}"><div class="plot-ground"><div class="furrows">${Array.from({length:5},()=>'<i></i>').join('')}</div>${field().crop?'<div class="crop-patch">'+Array.from({length:20},()=>image(plant().image)).join('')+'</div>':'<span class="empty-plot">尚未定植</span>'}</div></div>`;
  }
  function overview(){
    const f=field(),p=plant(),missing=gaps(),list=field().substrates;
    return heading('田地概况',`<span class="state-label">${f.crop?'生长中 · '+esc(f.crop.stage):'空田 · 待定植'}</span>`)+`
      <div class="overview-grid">
        <section class="field-column">${plot()}<div class="field-caption"><span>原有土层 · 普通土层</span><span>${esc(f.name)}</span></div>
          <div class="field-environment"><h2>当前环境</h2><div class="environment-values">${C.dimensions.map(d=>`<div><small>${d.label}</small><span>${d.baseline}</span></div>`).join('')}</div></div>
        </section>
        <section class="plant-sheet">
          <div class="plant-overview">${image(p.image,p.name)}<div><p class="eyebrow">${f.crop?'本田灵草':'目标灵草'}</p><h2>${p.name}</h2><p class="muted">${f.crop?'当前为'+esc(f.crop.stage)+'，环境需求待补齐':'先调整培育条件，再安排定植'}</p>${f.crop?'':'<button class="link-button" data-action="choose-plant">更换目标灵草 ›</button>'}</div></div>
          <div class="condition-brief"><div class="section-head"><h3>适生情况</h3><button class="link-button" data-route="growth">查看需求对照 ›</button></div><p class="condition-title">${missing===null?'培育资料待补齐':missing.length?missing.length+' 项环境尚需调整':'已知环境需求已满足'}</p><div class="brief-reasons">${missing===null?'<span>完整需求尚未配置</span>':missing.length?missing.map(d=>'<span>'+esc(d.issue)+'</span>').join(''):'<span>继续关注生长阶段与维持条件</span>'}</div></div>
          <div class="substrate-brief"><div class="section-head"><h3>本田基质</h3><span class="muted">${list.length?list.length+' 种材料 · '+quantity(list)+' 件':'尚未加入材料'}</span></div>${list.length?'<div class="material-miniatures">'+list.map(x=>'<span>'+image(material(x.id).image)+esc(material(x.id).name)+' ×'+x.qty+'</span>').join('')+'</div>':'<p class="muted">从行囊选择材料，加入本田基质。</p>'}</div>
          <div class="overview-actions"><button class="button primary" data-route="substrates">调整基质</button><button class="button" data-route="growth">预览成长</button></div>
        </section>
      </div>`;
  }
  function materialRows(){
    const d=draft(),rows=filteredMaterials();
    return rows.length?rows.map(m=>`<button class="inventory-row ${m.id===d.selectedMaterial?'selected':''}" draggable="true" data-action="select-material" data-id="${m.id}" aria-pressed="${m.id===d.selectedMaterial}"><span class="row-item">${image(m.image)}<strong>${m.name}</strong></span><span class="row-utilities">${esc(utilityText(m))}</span><span class="stock">${available(m.id)}</span></button>`).join(''):`<div class="empty-list"><p>${d.search?'没有找到材料':'行囊中暂无可用基质'}</p>${d.search?'<button class="link-button" data-action="clear-filter">清除搜索</button>':''}</div>`;
  }
  function joinedMaterials(){
    const list=field().substrates;
    return list.length?`<div class="joined-list">${list.map(x=>`<div class="joined-row"><span class="row-item">${image(material(x.id).image)}<button data-action="joined-material-info" data-id="${x.id}">${esc(material(x.id).name)}</button></span><span>${x.qty} 件</span></div>`).join('')}</div><div class="drop-tail"><span>＋</span> 拖入材料，继续添加</div>`:'<div class="drop-empty"><span aria-hidden="true">＋</span><p>将材料拖到这里</p><small>也可以选中左侧材料后，点击下方“加入”</small></div>';
  }
  function materialBar(){
    const d=draft(),m=material(d.selectedMaterial),enabled=m&&eligible(m.id)&&available(m.id)>0;
    return `<div class="selected-summary">${m?image(m.image):''}<span>${m?'已选：'+esc(m.name):'请选择行囊中的材料'}</span>${m?'<button class="link-button" data-action="material-utility-info">详情</button>':''}</div><div class="join-controls"><label for="material-quantity">数量</label><div class="stepper"><button data-action="qty-minus" aria-label="减少件数" ${enabled?'':'disabled'}>−</button><input id="material-quantity" type="number" min="1" step="1" max="${enabled?available(m.id):0}" value="${esc(d.quantity)}" aria-label="加入数量" ${enabled?'':'disabled'}><button data-action="qty-plus" aria-label="增加件数" ${enabled?'':'disabled'}>＋</button></div><button id="add-material" class="button primary" data-action="add-material" ${enabled?'':'disabled'}>加入 →</button></div>`;
  }
  function materialPage(){
    return heading('基质材料',`<button class="link-button" data-route="growth">查看成长 ›</button>`)+`<div class="substrate-layout">
      <section class="inventory-panel"><div class="list-heading"><h2>行囊</h2><input id="material-search" type="search" placeholder="搜索材料" aria-label="搜索材料" value="${esc(draft().search)}"></div><div class="inventory-columns"><span>可作基质的材料</span><span>功用</span><span>持有</span></div><div id="material-list" class="inventory-list scroll">${materialRows()}</div></section>
      <section id="substrate-dropzone" class="substrate-panel" aria-label="拖入材料加入当前田地"><div class="list-heading"><h2>${esc(field().name)} · 基质</h2><button class="link-button" data-action="show-utility-sources">查看功用 ›</button></div><div class="joined-columns"><span>已加入材料</span><span id="joined-count">${field().substrates.length} 种</span></div><div id="joined-materials" class="joined-content scroll">${joinedMaterials()}</div></section></div>
      <div id="substrate-actionbar" class="substrate-actionbar">${materialBar()}</div><p id="quantity-error" class="inline-error" aria-live="polite"></p>`;
  }
  function needsRows(){
    const p=plant();
    return C.dimensions.map(d=>{const need=p.needs?.[d.key],matches=need===d.baseline;return `<tr><td>${d.label}</td><td>${d.baseline}</td><td>${need||'待配置'}</td><td class="${need?(matches?'good':'bad'):'muted'}">${need?(matches?'✓ 满足':'× 不符'):'—'}</td></tr>`;}).join('');
  }
  function needsTable(){return `<table class="needs-table"><thead><tr><th>条件</th><th>本田环境</th><th>灵草需求</th><th>判定</th></tr></thead><tbody>${needsRows()}</tbody></table>`;}
  function growthPage(){
    const d=draft(),p=plant(),missing=gaps(),proposed=d.previewMode==='plan',hasPlan=field().substrates.length>0;
    const title=missing===null?'培育资料待补齐':proposed&&hasPlan?'基质作用待配置':missing.length?'当前尚不适合定植':'已知适生条件符合';
    return heading('成长预览',`<label for="growth-target">${field().crop?'本田灵草':'目标灵草'}</label><select id="growth-target" aria-label="预览灵草" ${field().crop?'disabled':''}>${C.plants.map(x=>`<option value="${x.id}" ${x.id===p.id?'selected':''}>${x.name}</option>`).join('')}</select>`)+`
      <div class="growth-toolbar"><div class="segmented" aria-label="预览依据"><button data-action="preview-environment" class="${proposed?'':'active'}" aria-pressed="${!proposed}">现有环境</button><button data-action="preview-plan" class="${proposed?'active':''}" aria-pressed="${proposed}">已加基质</button></div><button class="link-button" data-action="show-utility-sources">查看基质功用 ›</button></div>
      <div class="growth-grid"><section class="environment-sheet"><div class="section-head"><h2>环境与需求</h2><span class="state-label">${missing===null?'资料待补齐':missing.length?missing.length+' 项不符':'已知需求符合'}</span></div>${needsTable()}<p class="table-note">${proposed&&hasPlan?'已加入 '+field().substrates.length+' 种材料，共 '+quantity(field().substrates)+' 件；未配置的培育作用尚未计入环境。':'当前按田地已有环境对照。'}</p></section>
      <section class="forecast-sheet"><p class="eyebrow">${p.name} · ${proposed?'已加基质':'当前环境'}</p><h2 class="forecast-title">${title}</h2><p class="forecast-subtitle">${missing===null?'完整环境需求尚未配置。':proposed&&hasPlan?'材料功用已记录，具体培育效果待配置。':missing.length?missing.map(x=>x.issue).join('、')+'。':'仍需按物种周期与持续环境判断长势。'}</p>
      <div class="time-answer"><span>预计可采收时间</span><strong>暂无法估算</strong></div>
      <div class="growth-stages"><div><i></i><h3>定植</h3><p>${missing===null?'需求资料待补齐':missing.length?missing.length+' 项条件待调整':'已知需求符合'}</p></div><div><i></i><h3>生长</h3><p>根据持续环境与基质作用判断</p></div><div><i></i><h3>采收</h3><p>依据物种周期与采收方式</p></div></div>
      <div class="risk-note"><h3>持续不适</h3><p>生长减慢 → 停长<br>持续严重不适才受损或枯死。</p></div><button class="link-button" data-action="show-plan">查看已加基质 ›</button></section></div>`;
  }
  function bindQuantity(){
    if(!$('#material-quantity'))return;
    $('#material-quantity').addEventListener('input',e=>{draft().quantity=e.target.value;updateQuantity();});updateQuantity();
  }
  function updateMaterialParts(){
    ensureVisibleMaterial();$('#material-list').innerHTML=materialRows();$('#substrate-actionbar').innerHTML=materialBar();bindQuantity();
  }
  function render(){
    const f=field();draft();
    $('#field-select').innerHTML=state.fields.map(x=>`<option value="${x.id}" ${x.id===f.id?'selected':''}>${x.name}</option>`).join('');
    $('#field-prev').disabled=state.fieldIndex===0;$('#field-next').disabled=state.fieldIndex===state.fields.length-1;
    document.querySelectorAll('.page-tabs [data-route]').forEach(b=>{b.classList.toggle('active',b.dataset.route===state.page);if(b.dataset.route===state.page)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');});
    $('#screen').innerHTML=({overview,substrates:materialPage,growth:growthPage})[state.page]();
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
    openModal('样稿说明','<p>基质页只列行囊中持有且可作基质的材料。拖入右侧或选中后点击加入，立即更新本页材料记录，不再保存方案。</p><p>物品资料与功用品级来自项目；持有数量和可添加标识为独立演示夹具，不是正式入田白名单。当前不读写真实行囊、人物或存档，刷新恢复。</p><p>功用通过基质材料发挥，具体培育作用、费用、时间与返还规则未定。阵法仍是独立系统，本页只留入口。</p>');
  }
  function perform(action,el){
    const d=draft(),f=field();
    switch(action){
      case 'close-modal':closeModal();break;
      case 'confirm-modal':{if(!modalCallback)return;const callback=modalCallback;modalCallback=null;el.disabled=true;closeModal();callback();break;}
      case 'choose-plant':choosePlant();break;
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
  $('#open-formation').addEventListener('click',openFormation);
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
  window.PlantingPrototype={version:'1.3',snapshot:()=>clone(state),scope:C.scope,
    connectFormationSystem(handler){if(handler!==null&&typeof handler!=='function')throw new TypeError('阵法入口应为函数或null');formationEntry=handler;}
  };
  const initial=location.hash.slice(1);state.page=pages[initial]?initial:'overview';
  history.replaceState(null,'','#'+state.page);fit();render();if(initial==='formations')openFormation();
})();

