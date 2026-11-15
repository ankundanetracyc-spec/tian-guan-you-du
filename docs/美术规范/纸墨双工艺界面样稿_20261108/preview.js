(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const catalog = window.PAPER_INK_CATALOG;
  if (!catalog?.materials?.length) throw new Error('材料目录未载入');
  const materials = catalog.materials;
  const byId = new Map(materials.map(m => [m.id, m]));
  const utilityOrder = ['固','聚','通','化','引','破','护','镇','生','明','藏','行'];
  const gradeNames = ['','九品','八品','七品','六品','五品','四品','三品','二品','一品'];
  const definitions = {
    paper: {
      allowed: ['纤维','液体','黏胶'],
      spiritLabel: '承灵品阶',
      spiritBrief: '承受灵纹负担的能力',
      fields: ['承墨量','渗墨性','纹面精细度','承纹稳定','封存适用'],
      specs: [
        {id:'paper-basic',name:'素纹符纸',brief:'成页均衡，便于基础成纹',values:['适中','适中','适中','适中','基础'],retained:[],methods:['paper-open','paper-fine'],copy:'以足量纤维形成均匀纸面，兼顾承墨与基础成纹。'},
        {id:'paper-path',name:'通络符纸',brief:'灵脉通达，便于灵意流转',values:['较高','易渗','适中','适中','基础'],retained:['通'],methods:['paper-open','paper-layer'],copy:'保留可用的内部连接。墨能进入有效纹层，但需留意过度渗散与串染。'},
        {id:'paper-fine',name:'细纹符纸',brief:'纹面细整，便于精细成纹',values:['适中','较低','精细','较稳','适中'],retained:[],methods:['paper-fine'],copy:'精整纹面，分清细小笔画；更细处理也会带来额外用度与损失。'},
        {id:'paper-load',name:'厚承符纸',brief:'厚承稳纹，承担实际墨负载',values:['较高','适中','适中','稳固','适中'],retained:['固'],methods:['paper-fine','paper-layer'],copy:'为每张分配足够纸基与结合支持，侧重承墨和成纹稳定。'},
        {id:'paper-seal',name:'封藏符纸',brief:'保持边界，利于收束封符',values:['适中','较低','适中','较稳','较好'],retained:['藏'],methods:['paper-fine','paper-layer'],copy:'以完整纸面和实际留存支持保持边界；过度施胶也会影响落墨。'},
        {id:'paper-layer',name:'复层符纸',brief:'分层承托，连接有效纹层',values:['较高','适中','适中','稳固','较好'],retained:['固','通'],methods:['paper-layer'],copy:'不同层次分别分走真实纸料与连接来源，层间处理更复杂。'}
      ],
      methods: [
        {id:'paper-open',name:'养脉成纸',copy:'养护材料灵脉，保留适当开放的内部连接。',cost:'细面能力有限，过度渗入仍可能洇散。'},
        {id:'paper-fine',name:'凝纹成纸',copy:'凝合纸质、精整纹面，便于细小符纹成形。',cost:'处理用度与损失增加，可用深层连接减少。'},
        {id:'paper-layer',name:'叠脉成纸',copy:'把纸基与实际来源分层相接，承托较重纹势。',cost:'各层分别用料，层间结合与连通要求更高。'}
      ],
      fieldHelp: [
        ['能接住多少墨。','本张实际纤维、结合支持和成页完整共同决定。胶多或压得过密，可能限制可用承墨。'],
        ['墨有多容易渗进纸里。','纸内灵脉与结合方式影响渗入。易渗可能洇开，难渗则更挑墨性。'],
        ['细小笔画能否分清。','由材料调和、纹面精整与凝合形成；细整也有用度和损失。'],
        ['画符时是否容易断纹、散纹。','纸基结合、胶结、均匀度与剩余损伤共同起作用；缺料不能靠稳定功用补齐。'],
        ['封符时能否稳住已写入的效果。','需要完整纸面、边界保持和真实留存来源。封存适用本身不会额外生成藏。']
      ]
    },
    ink: {
      allowed: ['致密','颗粒','纤维','液体','黏胶'],
      spiritLabel: '灵蕴品阶',
      spiritBrief: '墨中留存的灵性层次',
      fields: ['每份墨量','浓度','流展性','附着性','凝定性'],
      specs: [
        {id:'ink-guide',name:'清润引通墨',brief:'清润行墨，留存引通来源',values:['—','较低','易铺展','适中','缓定'],retained:['引','通'],methods:['ink-even','ink-fine'],copy:'书写偏清润，目标保留引、通。实际来源与书写墨性分别满足。'},
        {id:'ink-fine',name:'细匀固藏墨',brief:'匀细附着，保持定纹边界',values:['—','适中','受控','较稳','易定形'],retained:['固','藏'],methods:['ink-even','ink-fine'],copy:'以精研和匀调形成受控墨性，目标保留固、藏；澄清可能连带损失有效来源。'},
        {id:'ink-dense',name:'浓载聚藏墨',brief:'集中有限来源，更挑承载',values:['—','较高','较缓','适中','适中'],retained:['聚','藏'],methods:['ink-fine','ink-concentrate'],copy:'把已有负载集中到可用墨量中。浓度高不等于附着强、凝定快或总功用更多。'}
      ],
      methods: [
        {id:'ink-even',name:'融灵调墨',copy:'化开墨质，调和实际来源，兼顾可用墨量与匀融。',cost:'不自动达到高细整或浓配要求。'},
        {id:'ink-fine',name:'澄灵成墨',copy:'澄去未融杂余，使墨性匀整、来源相合。',cost:'增加用度，也可能连带损失实际墨料和功用。'},
        {id:'ink-concentrate',name:'聚蕴成墨',copy:'收束已有墨质与配液，使本批有限来源更集中。',cost:'可用墨量或份数可能减少，后续承接要求提高。'}
      ],
      fieldHelp: [
        ['一标准份墨能提供多少可用墨。','由完整墨规格规定。本轮未填定量，显示为—；加液不能复制墨料和功用。'],
        ['单位墨量中的实际墨体负载。','包括实际留下的研取墨料与结合部分。浓不等于功用强、纯度高或附着好。'],
        ['墨有多容易铺开和接续。','由液胶配比、分散和实际连续送达条件形成。易铺开也可能串纹。'],
        ['落到载体后能否留住。','需要真实结合、分散与固支持，画符时还要核对纸材和落墨。'],
        ['墨迹是否容易稳定成形。','易定形有利于保持边界，也可能增加长段接续的工作；不由浓度单独决定。']
      ]
    }
  };
  const seedNames = {
    paper: [['风络藤丝',3],['山蚕长丝',2],['灵泉露',2],['凝露松脂',1]],
    ink: [['灵石',2],['雾铃花',2],['灵泉露',2],['凝露松脂',1]]
  };
  function seed(craft) {
    const selection = {};
    for (const [name,qty] of seedNames[craft]) { const m=materials.find(m=>m.name===name); if(m) selection[m.id]=Math.min(qty,m.stock); }
    return {spec:craft==='paper'?'paper-path':'ink-guide',method:craft==='paper'?'paper-open':'ink-even',selection};
  }
  const state = {craft: new URLSearchParams(location.search).get('craft')==='ink'?'ink':'paper',phase:'prepare',drafts:{paper:seed('paper'),ink:seed('ink')}};
  let picker = {query:'',texture:'',element:'',utilities:new Set(),showAll:false};
  let focusBeforeDialog = null, toastTimer = null, processTimer = null, processIndex = 0, processSteps = [];
  const storageKey = 'paper-ink-ui-preview-20261108-v1';
  const current = () => definitions[state.craft];
  const draft = () => state.drafts[state.craft];
  const spec = () => current().specs.find(s=>s.id===draft().spec) || current().specs[0];
  const method = () => current().methods.find(m=>m.id===draft().method) || current().methods[0];
  const entries = () => Object.entries(draft().selection).map(([id,qty])=>({m:byId.get(id),qty})).filter(e=>e.m);
  const total = () => entries().reduce((sum,e)=>sum+e.qty,0);
  const byTexture = texture => entries().filter(e=>e.m.texture===texture).reduce((sum,e)=>sum+e.qty,0);
  const availableUtilities = () => new Set(entries().flatMap(e=>Object.keys(e.m.utilities)));
  const utilityText = m => utilityOrder.filter(u=>m.utilities[u]).map(u=>u+(m.utilityDisplay?.[u]||gradeNames[m.utilities[u]])).join(' · ') || '无';
  const allowed = m => current().allowed.includes(m.texture);
  function issues() {
    const result=[];
    if(state.craft==='paper'&&!byTexture('纤维')) result.push('缺少纤维纸基');
    if(state.craft==='ink'&&!['致密','颗粒','纤维'].some(t=>byTexture(t))) result.push('缺少可研取墨料');
    if(!byTexture('液体')) result.push(state.craft==='paper'?'缺少制浆介质':'缺少调墨液');
    for(const u of spec().retained) if(!availableUtilities().has(u)) result.push('缺少'+u+'来源');
    return result;
  }
  function toast(copy) { clearTimeout(toastTimer);$('toast').textContent=copy;$('toast').hidden=false;toastTimer=setTimeout(()=>$('toast').hidden=true,2600); }
  function scale() {
    const box=$('viewport').getBoundingClientRect(),factor=Math.min(box.width/1672,box.height/941);
    $('stage').style.transform='scale('+factor+')';$('stage').style.left=(box.width-1672*factor)/2+'px';$('stage').style.top=(box.height-941*factor)/2+'px';
  }
  const generate = {木:'火',火:'土',土:'金',金:'水',水:'木'};
  const overcome = {木:'土',土:'水',水:'火',火:'金',金:'木'};
  function elements(m) {return [...new Set(String(m.element||'').split(/[、,，|\s]+/).filter(e=>generate[e]))];}
  function relationships() {
    const list=entries(),pairs=[];
    if(state.craft==='paper') {
      for(const a of list.filter(e=>['液体','黏胶'].includes(e.m.texture))) for(const b of list.filter(e=>e.m.texture==='纤维')) pairs.push([a,b,a.m.texture==='液体'?'蕴养':'凝纸']);
    } else {
      for(let i=0;i<list.length;i++) for(let j=i+1;j<list.length;j++) {
        const blending=[list[i].m.texture,list[j].m.texture].some(t=>['液体','黏胶'].includes(t));
        pairs.push([list[i],list[j],blending?'融灵':'化材']);
      }
    }
    const unique=new Map();
    for(const [a,b,step] of pairs) for(const ea of elements(a.m)) for(const eb of elements(b.m)) {
      let from,to,kind;
      if(generate[ea]===eb){from={e:ea,m:a.m};to={e:eb,m:b.m};kind='生';}
      else if(generate[eb]===ea){from={e:eb,m:b.m};to={e:ea,m:a.m};kind='生';}
      else if(overcome[ea]===eb){from={e:ea,m:a.m};to={e:eb,m:b.m};kind='克';}
      else if(overcome[eb]===ea){from={e:eb,m:b.m};to={e:ea,m:a.m};kind='克';}
      else continue;
      const text=from.e+kind+to.e,key=text+'-'+step;
      if(!unique.has(key)) unique.set(key,{text,step,kind,from:from.m.name,to:to.m.name});
    }
    return [...unique.values()];
  }
  function render() {
    const def=current(),s=spec(),d=draft();
    document.body.dataset.craft=state.craft;document.body.dataset.phase=state.phase;
    document.querySelectorAll('[data-craft]').forEach(button=>{if(button.tagName==='BUTTON'){button.setAttribute('aria-selected',String(button.dataset.craft===state.craft));button.disabled=state.phase!=='prepare';}});
    document.querySelectorAll('.game-header button[data-action]').forEach(button=>button.disabled=state.phase!=='prepare');
    document.querySelectorAll('[data-preview]').forEach(button=>button.classList.toggle('active',button.dataset.preview===state.phase));
    $('prepare-layer').hidden=state.phase!=='prepare';$('process-layer').hidden=state.phase!=='process';$('result-layer').hidden=state.phase!=='result';
    $('target-heading').textContent=state.craft==='paper'?'目标纸材':'目标符墨';
    document.querySelector('.target .section-head .text-button').textContent=state.craft==='paper'?'纸材册 ›':'墨谱 ›';
    $('work-label').textContent=state.craft==='paper'?'凝 纸':'融 墨';$('work-title').textContent=s.name;$('work-brief').textContent=s.brief;
    $('workbench').src='assets/'+(state.craft==='paper'?'paper':'ink')+'-spirit.svg';$('workbench').alt=state.craft==='paper'?'灵机交织，悬纸渐成':'墨气回旋，诸蕴渐融';
    $('scene-caption').textContent=state.craft==='paper'?'灵机交织 · 蕴养成纸':'诸材融灵 · 聚蕴成墨';
    $('spirit-label').textContent=def.spiritLabel;$('spirit-brief').textContent=def.spiritBrief;
    $('target-name').textContent=s.name;$('field-heading').textContent=state.craft==='paper'?'目标纸性':'目标墨性';
    $('field-list').innerHTML=def.fields.map((name,i)=>'<div><dt><button class="field-button" data-field="'+i+'">'+name+'</button></dt><dd>'+s.values[i]+'</dd></div>').join('');
    $('retained-section').hidden=state.craft!=='ink';
    $('retained-list').innerHTML=s.retained.map(u=>'<span class="utility-chip '+(availableUtilities().has(u)?'':'missing')+'">'+u+'<small>'+(availableUtilities().has(u)?'有来源':'缺来源')+'</small></span>').join('');
    $('method-name').textContent=method().name;$('total-input').textContent='投入 '+total()+' 份';
    const missing=issues();$('requirements').hidden=!missing.length;$('requirements').textContent=missing.join(' · ');
    $('start-button').disabled=!!missing.length;$('start-button').textContent=state.craft==='paper'?'开 始 制 纸':'开 始 研 墨';
    renderMaterials();renderSummary();
  }
  function renderMaterials() {
    const list=$('material-list'),scroll=list.scrollTop,selected=entries();
    list.innerHTML=selected.length?selected.map(({m,qty})=>'<article class="material-card" data-material="'+escape(m.id)+'"><button class="material-info" data-detail="'+escape(m.id)+'"><img src="'+escape(m.icon)+'" alt=""><div><span class="material-name">'+escape(m.name)+' ›</span><div class="material-meta"><span>'+escape(m.element||'无五行')+'</span><span>'+escape(m.texture)+'</span><span>可用 '+(m.stock-qty)+'</span></div><div class="material-utilities">'+escape(utilityText(m))+'</div></div></button><div class="dose-row"><label for="dose-'+escape(m.id)+'">份数</label><button class="btn" data-dose="'+escape(m.id)+'" data-delta="-1" aria-label="减少'+escape(m.name)+'" '+(qty<=1?'disabled':'')+'>−</button><input id="dose-'+escape(m.id)+'" data-quantity="'+escape(m.id)+'" inputmode="numeric" value="'+qty+'" aria-label="'+escape(m.name)+'份数"><button class="btn" data-dose="'+escape(m.id)+'" data-delta="1" aria-label="增加'+escape(m.name)+'" '+(qty>=m.stock?'disabled':'')+'>＋</button><button class="btn remove" data-remove="'+escape(m.id)+'">移除</button></div></article>').join(''):'<div class="empty-materials"><span class="empty-glyph">材</span><p>尚未选入材料</p><button class="btn" data-action="materials">＋ 选择材料</button></div>';
    list.scrollTop=scroll;$('material-count').textContent=selected.length+'种 · '+total()+'份';
  }
  function renderSummary() {
    $('batch-title').textContent=state.craft==='paper'?'凝纸配伍':'融墨配伍';
    const items=state.craft==='paper'?[['纸基',byTexture('纤维')],['调和',byTexture('液体')],['固形',byTexture('黏胶')]]:[['墨质',byTexture('致密')+byTexture('颗粒')],['取蕴',byTexture('纤维')],['调和',byTexture('液体')],['定性',byTexture('黏胶')]];
    $('batch-summary').className='batch-summary'+(state.craft==='ink'?' ink-summary':'');
    $('batch-summary').innerHTML=items.map(([label,n])=>'<dl><dt>'+label+'</dt><dd>'+n+'<small>份</small></dd></dl>').join('');
    const rel=relationships();$('relation-strip').innerHTML=rel.length?rel.slice(0,2).map(r=>'<span class="relation-tag '+(r.kind==='克'?'clash':'')+'">'+r.text+'<small>· '+r.step+'</small></span>').join(''):'<span class="relation-empty">添加材料后查看实际配伍关系</span>';
  }
  function setQuantity(id,raw) {
    const m=byId.get(id),old=draft().selection[id];if(!m||!old)return;
    if(!/^[1-9]\d*$/.test(String(raw))||Number(raw)>m.stock){toast('份数需为 1—'+m.stock+' 间的整数');render();return;}
    draft().selection[id]=Number(raw);render();
  }
  function openDialog(title,content,extraClass='') {
    if($('overlay').hidden)focusBeforeDialog=document.activeElement;
    $('overlay').innerHTML='<section class="dialog skin tall '+extraClass+'" role="dialog" aria-modal="true" aria-labelledby="dialog-title"><header class="dialog-header"><h2 id="dialog-title">'+escape(title)+'</h2><button class="btn" data-action="close">关闭</button></header><div class="dialog-content scroll-area">'+content+'</div></section>';
    $('overlay').hidden=false;for(const id of ['prepare-layer','process-layer','result-layer'])$(id).inert=true;document.querySelector('.game-header').inert=true;
    $('overlay').querySelector('button').focus();
  }
  function closeDialog() {
    $('overlay').hidden=true;$('overlay').innerHTML='';for(const id of ['prepare-layer','process-layer','result-layer'])$(id).inert=false;document.querySelector('.game-header').inert=false;
    if(focusBeforeDialog?.isConnected)focusBeforeDialog.focus();
  }
  function openMaterials() {
    picker={query:'',texture:'',element:'',utilities:new Set(),showAll:false};
    openDialog('选择'+(state.craft==='paper'?'制纸材料':'研墨材料'),'<div class="picker-filters"><input class="text-input" id="material-search" placeholder="搜索材料名称" aria-label="搜索材料名称"><select id="texture-filter" aria-label="按质地筛选"><option value="">全部质地</option>'+['纤维','液体','黏胶','致密','颗粒','气体'].map(t=>'<option>'+t+'</option>').join('')+'</select><select id="element-filter" aria-label="按五行筛选"><option value="">全部五行</option>'+['金','木','水','火','土'].map(t=>'<option>'+t+'</option>').join('')+'</select><label class="switch-label"><input type="checkbox" id="show-all">显示不适用</label></div><div class="utility-filters" aria-label="按功用筛选"><span class="muted">功用</span>'+utilityOrder.map(u=>'<button class="btn" data-filter-utility="'+u+'" aria-pressed="false">'+u+'</button>').join('')+'<button class="text-button" data-action="reset-filters">重置</button></div><div id="picker-grid" class="picker-grid scroll-area"></div><footer class="picker-footer"><span id="picker-count"></span><button class="btn primary" data-action="close">完成选材</button></footer>');
    renderPicker();
  }
  function renderPicker(restoreId) {
    const grid=$('picker-grid');if(!grid)return;const scroll=grid.scrollTop;
    const filtered=materials.filter(m=>(picker.showAll||allowed(m))&&(!picker.query||m.name.includes(picker.query))&&(!picker.texture||m.texture===picker.texture)&&(!picker.element||elements(m).includes(picker.element))&&(!picker.utilities.size||[...picker.utilities].some(u=>m.utilities[u])));
    grid.innerHTML=filtered.length?filtered.map(m=>{
      const q=draft().selection[m.id]||0,ok=allowed(m),left=m.stock-q;
      return '<button class="btn picker-card '+(q?'added':'')+'" data-pick="'+escape(m.id)+'" '+(!ok||left<=0?'disabled':'')+'><img src="'+escape(m.icon)+'" alt=""><span><strong>'+escape(m.name)+'</strong><small>'+escape(m.element||'无五行')+' · '+escape(m.texture)+'</small><small>'+escape(utilityText(m))+'</small>'+(!ok?'<small>'+(m.texture==='气体'?'暂不接收':'用于研墨，本页不接收')+'</small>':'')+'</span><span class="stock">可用 '+left+'</span>'+(q?'<span class="checkmark">已选 '+q+'</span>':'')+'</button>';
    }).join(''):'<div class="no-results">没有符合当前筛选的材料</div>';
    grid.scrollTop=scroll;$('picker-count').textContent='找到 '+filtered.length+' 种 · 本批已选 '+entries().length+' 种';
    $('overlay').querySelectorAll('[data-filter-utility]').forEach(b=>{const active=picker.utilities.has(b.dataset.filterUtility);b.classList.toggle('selected',active);b.setAttribute('aria-pressed',String(active));});
    if(restoreId)grid.querySelector('[data-pick="'+CSS.escape(restoreId)+'"]')?.focus({preventScroll:true});
  }
  let candidateSpec = null;
  function openSpecs() {
    candidateSpec=spec().id;
    openDialog(state.craft==='paper'?'纸材册':'符墨规格','<div id="spec-layout" class="spec-layout"></div>');
    renderSpecs();
  }
  function renderSpecs() {
    const s=current().specs.find(s=>s.id===candidateSpec)||spec();
    $('spec-layout').innerHTML='<nav class="spec-list" aria-label="规格列表">'+current().specs.map(item=>'<button class="btn spec-option '+(item.id===s.id?'selected':'')+'" data-spec="'+item.id+'" aria-pressed="'+(item.id===s.id)+'"><strong>'+item.name+'</strong><small>'+item.brief+'</small></button>').join('')+'</nav><article class="spec-detail"><h3>'+s.name+'</h3><p>'+s.copy+'</p><dl>'+current().fields.map((f,i)=>'<div><dt>'+f+'</dt><dd>'+s.values[i]+'</dd></div>').join('')+'</dl>'+(s.retained.length?'<div>'+s.retained.map(u=>'<span class="utility-chip">'+u+'</span>').join(' ')+'</div>':'')+'<div class="dialog-actions"><button class="btn primary" data-action="use-spec">使用此规格</button></div></article>';
  }
  function openMethods() {
    const compatible=new Set(spec().methods);
    openDialog('选择工法','<div class="method-options">'+current().methods.map(m=>'<button class="btn method-option '+(m.id===draft().method?'selected':'')+'" data-method="'+m.id+'" aria-pressed="'+(m.id===draft().method)+'" '+(!compatible.has(m.id)?'disabled':'')+'><strong>'+m.name+'</strong><p>'+m.copy+'</p><small>'+m.cost+(!compatible.has(m.id)?' · 当前目标不采用此法':'')+'</small></button>').join('')+'</div>');
  }
  function openField(index) {
    const help=current().fieldHelp[index];if(!help)return;
    openDialog(current().fields[index],'<div class="field-explanation"><p><strong>'+help[0]+'</strong></p><p>'+help[1]+'</p></div>');
  }
  function openSpirit() {
    const paper=state.craft==='paper';
    openDialog(current().spiritLabel,'<div class="field-explanation"><p><strong>'+current().spiritBrief+'。</strong></p><p>'+(paper?'纸承受灵力冲击与复杂纹势的能力。负担越重，承灵较弱的纸越容易失稳、散逸；更好的纸能从容承托，实际表现仍取决于符谱、纸墨笔与技艺。':'墨中实际留存灵性的层次。配合保留功用，为当前术式供给；负担较重时，灵蕴不足更容易造成供给与留存损失。增加用量可以补量，未必消除灵性与稳定的差别。')+'</p><p>采用软性适配，不按境界或纸墨品阶直接禁用。高品纸墨不会自动把低阶符变成高阶符。</p></div>');
  }
  function openMaterialDetail(id) {
    const m=byId.get(id);if(!m)return;
    openDialog(m.name,'<div class="detail-material"><img src="'+escape(m.icon)+'" alt="'+escape(m.name)+'"><div><dl>'+[['品阶',m.grade],['五行',m.element||'无'],['质地',m.texture],['相性',m.affinity],['功用',utilityText(m)],['简介',m.description]].map(([label,value])=>'<div><dt>'+label+'</dt><dd>'+escape(value)+'</dd></div>').join('')+'</dl></div></div>');
  }
  function openRelations() {
    const rel=relationships();
    openDialog('本批配伍',rel.length?'<div class="relationship-list">'+rel.map(r=>'<article class="relationship-row '+(r.kind==='克'?'clash':'')+'"><h3>'+r.text+' · '+r.step+'</h3><p>'+escape(r.from)+' → '+escape(r.to)+'</p><p>'+(r.kind==='生'?'相生作用于本次处理与保留，不凭空增加材料或功用。':'相克作用于实际接触的来源，结合、稳定与保留仍需分别处理。')+'</p></article>').join('')+'</div>':'<p class="help-intro">当前没有需要显示的异行接触。</p>');
  }
  function openHelp() {
    const paper=state.craft==='paper';
    openDialog(paper?'制纸工艺':'研墨工艺','<p class="help-intro">'+(paper?'选定材料与工法，化材蕴养，凝合成纸。':'选定材料与工法，化材融灵，聚蕴成墨。')+'纸墨按实际术式负担软性适配，品阶不作为境界门槛。</p><div class="help-grid">'+(paper?[
      ['纸基与成页','纤维提供实际纸料。固帮助结合，聚减少散失，生需要真实余料补缺。'],
      ['纸面与纹层','调和与凝合影响纸面；通帮助利用已有连接，疏密各有适用。'],
      ['施胶与封符','胶结和边界保持影响稳定与封存。胶多也会影响渗墨，不能替代实际纸料。']
    ]:[
      ['化材与融灵','材料转为墨质、留取灵性，也可能连带损失实际墨料与来源。'],
      ['书写墨性','浓度、流展、附着和凝定分别起作用。浓不等于附着强，易流展也可能串纹。'],
      ['保留功用','引、生等可以留给画符，不必提高某条墨性。加工、结构与可调用来源不能重复计数。']
    ]).map(([h,p])=>'<article class="help-item"><h3>'+h+'</h3><p>'+p+'</p></article>').join('')+'</div>');
  }
  function readPlans() {
    try {const rows=JSON.parse(localStorage.getItem(storageKey)||'[]');return Array.isArray(rows)?rows.filter(p=>p&&typeof p.name==='string'&&['paper','ink'].includes(p.craft)&&p.draft):[];}
    catch{return [];}
  }
  function writePlans(rows) {try{localStorage.setItem(storageKey,JSON.stringify(rows));return true;}catch{toast('当前窗口无法保存配伍');return false;}}
  function openPlans() {
    const rows=readPlans().filter(p=>p.craft===state.craft);
    openDialog(state.craft==='paper'?'制纸配伍册':'研墨配伍册','<div class="plan-list">'+(rows.length?rows.map(p=>{
      const target=definitions[p.craft].specs.find(s=>s.id===p.draft.spec);
      return '<article class="plan-row"><div><strong>'+escape(p.name)+'</strong><p>'+escape(target?.name||'原目标待重选')+' · '+Object.keys(p.draft.selection||{}).length+'种材料</p></div><div><button class="btn" data-load-plan="'+escape(p.id)+'">载入</button><button class="btn" data-delete-plan="'+escape(p.id)+'">移除</button></div></article>';
    }).join(''):'<p class="plan-empty">尚无已存配伍</p>')+'</div><div class="plan-save"><input class="text-input" id="plan-name" maxlength="24" placeholder="为本次配伍取名" aria-label="配伍名称"><button class="btn primary" data-action="save-plan">保存当前配伍</button></div>');
  }
  function loadPlan(id) {
    const p=readPlans().find(p=>p.id===id&&p.craft===state.craft);if(!p)return;
    const next=seed(state.craft);next.selection={};
    const target=current().specs.find(s=>s.id===p.draft.spec);if(target)next.spec=target.id;
    const validSpec=current().specs.find(s=>s.id===next.spec);next.method=validSpec.methods.includes(p.draft.method)?p.draft.method:validSpec.methods[0];
    for(const [matId,quantity] of Object.entries(p.draft.selection||{})) {
      const m=byId.get(matId),q=Number(quantity);if(m&&allowed(m)&&Number.isInteger(q)&&q>0)next.selection[matId]=Math.min(q,m.stock);
    }
    state.drafts[state.craft]=next;closeDialog();render();toast('已载入配伍');
  }
  function savePlan() {
    const name=$('plan-name').value.trim();if(!name){toast('请先填写配伍名称');$('plan-name').focus();return;}
    const rows=readPlans(),existing=rows.find(p=>p.craft===state.craft&&p.name===name);
    const item={id:existing?.id||Date.now().toString(36)+'-'+Math.random().toString(36).slice(2,6),name,craft:state.craft,draft:JSON.parse(JSON.stringify(draft()))};
    const next=existing?rows.map(p=>p.id===existing.id?item:p):[...rows,item];
    if(writePlans(next)){openPlans();toast('已保存配伍');}
  }
  function stopProcess() {clearTimeout(processTimer);processTimer=null;}
  function buildProcessSteps() {
    return state.craft==='paper'?['化材','蕴养','凝纸']:['化材','融灵','凝墨'];
  }
  function processCopy(step) {
    const copy={
      '化材':'诸材化合，留取本批可用来源。','蕴养':'灵机交织，纸脉渐生。','凝纸':'收束纸质，灵脉成形。','融灵':'调和墨质，使诸般灵意相融。','凝墨':'聚蕴定性，收作符墨。'
    };return copy[step]||'';
  }
  function renderProcess() {
    $('process-label').textContent=state.craft==='paper'?'制 符 纸':'研 墨';$('process-name').textContent=spec().name;
    $('process-art').src='assets/'+(state.craft==='paper'?'paper':'ink')+'-spirit.svg';$('process-art').alt=state.craft==='paper'?'灵机蕴养成纸':'墨气融灵成墨';
    $('process-steps').innerHTML=processSteps.map((step,i)=>'<li class="'+(i===processIndex?'active':i<processIndex?'done':'')+'" '+(i===processIndex?'aria-current="step"':'')+'>'+step+'</li>').join('');
    $('process-copy').textContent=processCopy(processSteps[processIndex]);
  }
  function startProcess(ignoreIssues=false) {
    if(!ignoreIssues&&issues().length){toast(issues().join(' · '));return;}
    if(!$('overlay').hidden)closeDialog();stopProcess();state.phase='process';processIndex=0;processSteps=buildProcessSteps();render();renderProcess();
    const tick=()=>{if(state.phase!=='process')return;processIndex++;if(processIndex>=processSteps.length){showResult();return;}renderProcess();processTimer=setTimeout(tick,1200);};
    processTimer=setTimeout(tick,1200);
  }
  function showResult() {
    stopProcess();state.phase='result';render();const s=spec();
    $('result-heading').textContent=state.craft==='paper'?'纸面鉴看':'符墨鉴看';$('result-name').textContent=s.name;$('result-brief').textContent=s.brief;
    $('result-spirit').textContent=current().spiritLabel+'　—';
    $('result-quantity').textContent=state.craft==='paper'?'张数　—':'份数　—';
    $('result-visual').className=state.craft==='paper'?'paper-stack':'ink-vessel';
    $('result-fields').innerHTML=current().fields.map((f,i)=>'<div><dt>'+f+'</dt><dd>'+s.values[i]+'</dd></div>').join('');
    $('result-utilities').innerHTML=state.craft==='ink'?'<span class="muted">目标留存</span>'+s.retained.map(u=>'<span class="utility-chip">'+u+'</span>').join(''):'';
  }
  function prepare() {stopProcess();if(!$('overlay').hidden)closeDialog();state.phase='prepare';render();}
  function changeCraft(craft) {if(!definitions[craft]||state.phase!=='prepare')return;if(!$('overlay').hidden)closeDialog();state.craft=craft;render();}
  document.addEventListener('click',event=>{
    const button=event.target.closest('button');if(!button||button.disabled)return;
    if(button.dataset.craft){changeCraft(button.dataset.craft);return;}
    if(button.dataset.preview){const p=button.dataset.preview;if(p==='prepare')prepare();else if(p==='process')startProcess(true);else{if(!$('overlay').hidden)closeDialog();showResult();}return;}
    if(button.dataset.dose){const id=button.dataset.dose;setQuantity(id,draft().selection[id]+Number(button.dataset.delta));return;}
    if(button.dataset.remove){delete draft().selection[button.dataset.remove];render();return;}
    if(button.dataset.detail){openMaterialDetail(button.dataset.detail);return;}
    if(button.dataset.field!==undefined){openField(Number(button.dataset.field));return;}
    if(button.dataset.pick){const m=byId.get(button.dataset.pick),q=draft().selection[m.id]||0;if(allowed(m)&&q<m.stock){draft().selection[m.id]=q+1;render();renderPicker(m.id);}return;}
    if(button.dataset.filterUtility){const u=button.dataset.filterUtility;if(picker.utilities.has(u))picker.utilities.delete(u);else picker.utilities.add(u);renderPicker();return;}
    if(button.dataset.spec){candidateSpec=button.dataset.spec;renderSpecs();return;}
    if(button.dataset.method){draft().method=button.dataset.method;closeDialog();render();return;}
    if(button.dataset.loadPlan){loadPlan(button.dataset.loadPlan);return;}
    if(button.dataset.deletePlan){if(writePlans(readPlans().filter(p=>p.id!==button.dataset.deletePlan)))openPlans();return;}
    const action=button.dataset.action;
    if(!action)return;
    if(state.phase!=='prepare'&&!['skip','finish','close'].includes(action))return;
    switch(action){
      case 'materials':openMaterials();break;
      case 'clear':draft().selection={};render();break;
      case 'specs':openSpecs();break;
      case 'methods':openMethods();break;
      case 'relations':openRelations();break;
      case 'help':openHelp();break;
      case 'spirit':openSpirit();break;
      case 'plans':openPlans();break;
      case 'close':closeDialog();break;
      case 'start':startProcess();break;
      case 'skip':showResult();break;
      case 'finish':prepare();break;
      case 'back':toast('可在顶部切换制符纸与研墨');break;
      case 'save-plan':savePlan();break;
      case 'use-spec':{
        const chosen=current().specs.find(s=>s.id===candidateSpec);if(chosen){draft().spec=chosen.id;if(!chosen.methods.includes(draft().method))draft().method=chosen.methods[0];}closeDialog();render();break;
      }
      case 'reset-filters':picker={query:'',texture:'',element:'',utilities:new Set(),showAll:false};$('material-search').value='';$('texture-filter').value='';$('element-filter').value='';$('show-all').checked=false;renderPicker();break;
    }
  });
  document.addEventListener('input',event=>{if(event.target.id==='material-search'){picker.query=event.target.value.trim();renderPicker();}});
  document.addEventListener('change',event=>{
    const el=event.target;
    if(el.dataset.quantity){setQuantity(el.dataset.quantity,el.value.trim());return;}
    if(el.id==='texture-filter'){picker.texture=el.value;renderPicker();}
    if(el.id==='element-filter'){picker.element=el.value;renderPicker();}
    if(el.id==='show-all'){picker.showAll=el.checked;renderPicker();}
  });
  document.addEventListener('keydown',event=>{
    if(event.key==='Enter'&&event.target.dataset.quantity){event.target.blur();return;}
    if(event.key==='Escape'&&!$('overlay').hidden){event.preventDefault();closeDialog();return;}
    if(event.key==='Tab'&&!$('overlay').hidden){
      const focusables=[...$('overlay').querySelectorAll('button:not(:disabled),input,select,[tabindex="0"]')].filter(e=>e.offsetParent!==null),first=focusables[0],last=focusables.at(-1);
      if(event.shiftKey&&document.activeElement===first){event.preventDefault();last?.focus();}
      else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first?.focus();}
    }
  });
  $('overlay').addEventListener('click',event=>{if(event.target===$('overlay'))closeDialog();});
  window.addEventListener('resize',scale);
  window.addEventListener('pagehide',stopProcess);
  window.PAPER_INK_PREVIEW={
    getState:()=>JSON.parse(JSON.stringify(state)),
    getIssues:()=>issues().slice(),
    getCatalogSummary:()=>({total:materials.length,eligible:materials.filter(allowed).length,source:catalog.metadata}),
    reset:()=>{stopProcess();if(!$('overlay').hidden)closeDialog();state.drafts={paper:seed('paper'),ink:seed('ink')};state.craft='paper';state.phase='prepare';render();}
  };
  render();scale();document.fonts.ready.then(scale);
})();
