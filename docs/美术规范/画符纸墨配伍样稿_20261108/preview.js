(() => {
  'use strict';
  const ROOT='../../../';
  const ART=ROOT+'textures/ui/forging_20261021/';
  const TOOLS=ROOT+'textures/ui/talisman_crafting_20261108/tools_v1.png';
  const CONTROL=ROOT+'docs/美术规范/角色与静室纸墨木石样稿_20261108/assets/控件补充图集_v1.png';
  const el=id=>document.getElementById(id);
  const escape=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const roles={paper:'符纸',ink:'符墨'};
  const tiers=['薄效','良效','优效','极效','满效'];
  const brushes={mixed:['兼毫符笔','常规纸墨的均衡承接。'],hard:['硬毫符笔','侧重凝墨细纹与落点控制，铺展仍看纸墨。'],soft:['柔毫符笔','侧重润墨铺展与回环，细线边界需纸墨支持。'],long:['长锋符笔','较长段的承墨送达，回蘸由系统处理。']};
  const utilities={
    固:['定纹与收口','支撑真实落墨的结构，不替代主纹来源。'],聚:['符首会聚','将有限来源集中送向主纹，不给各支复制满额。'],通:['纹路接续','建立可达连接；连接之后还需实际送达。'],化:['研墨与调墨','处理原料中允许炼化、分散的部分，与破共享真实残余。'],引:['分流与引向','安排主纹、辅纹和封口的去向，分流不增加总来源。'],破:['研料与主纹','研取致密料；留给主纹的另一份额才参与术式。'],护:['隔离纹域','限制真实局部干扰波及邻纹，不自动附送战斗护盾。'],镇:['稳定配伍','压住相激与过度会聚波动，不要求所有材料归和。'],生:['余墨补纹','动用真实余墨修补允许的细缺，不凭空重画。'],明:['澄墨与辨线','减少夹带和串染，不替代接续，也不限制查阅信息。'],藏:['暂存与封符','减少已送达来源的封存散失，不生成额外效力。'],行:['铺展与送达','沿已连通路线输送已有墨量，仍受笔与接点约束。']
  };
  const recipeData={
    flame:{name:'炎矢符',role:'远程 · 单体敌人',description:'固定术式 · 远程单体伤害',effect:'一次物理伤害，数值待定',intro:'以长直与转折构成主纹，封存一次远程攻击。配墨、行笔与封存影响符效及成符张数。'},
    guard:{name:'护身符',role:'自身',description:'固定术式 · 自身护盾',effect:'获得公共护盾，数值待定',intro:'以首尾相接的环纹封存护身术式。配墨、接续与封口影响符效及成符张数。'}
  };

  const paperKinds={
    grass:{name:'草纤符纸',brief:'疏纤吸墨',note:'纤维较疏，墨容易渗入；能否成为有效纹路，要看纸内通路与配墨。',tradeoff:'吸入有效纹层可参与成符，渗入无效区域则会散失。',tone:'#d7bb762c'},
    bark:{name:'韧皮符纸',brief:'紧密承纹',note:'组织紧密，侧重表层承纹与结构保持。',tradeoff:'较凝的墨需兼顾附着与均匀铺开。',tone:'#a47d4328'},
    silk:{name:'蚕丝符纸',brief:'长纤细织',note:'长纤细织，侧重连续导通与细纹承载。',tradeoff:'仍要处理墨在织面上的附着、渗开与局部聚集。',tone:'#b9c9bc28'}
  };
  const brushBrief={mixed:'均衡承接',hard:'细纹与落点',soft:'润墨与回环',long:'长段承墨'};
  const makeDraft=kind=>({paper:[{id:'grass',name:paperKinds.grass.name,quantity:1}],ink:(kind==='flame'?['赤纹燧石','朱阳花籽','灵石','灵泉露','石蜂蜡']:['玄甲薄片','石蜂蜡','青木芯','清音玉片','雾心菌核']).map(name=>({name,quantity:1})),brush:kind==='flame'?'hard':'soft'});
  const initial={flame:makeDraft('flame'),guard:makeDraft('guard')},drafts=structuredClone(initial);
  const state={recipe:'flame',role:'ink',mode:'prepare',saved:[],focus:null};
  let materials=[],materialMap=new Map(),frame=0,animationToken=0,toastTimer=0,sceneProgress=0;
  const draft=()=>drafts[state.recipe];
  const selectedPaper=()=>paperKinds[draft().paper[0]?.id]||paperKinds.grass;
  const plateImage=new Image();plateImage.src=ART+'material_panel_v3.png';
  const textureDefs={
    tall:{file:ART+'ledger_panel_v3.png',size:[887,1774],bounds:[0,0,887,1774],slices:[12,12,12,12],rendered:[7,7,7,7]},
    wide:{file:ART+'material_panel_v3.png',size:[2172,724],bounds:[0,0,2172,724],slices:[12,12,12,12],rendered:[6,6,6,6]},
    plain:{file:ART+'material_panel_v3.png',size:[2172,724],bounds:[600,180,972,340],slices:[12,12,12,12],rendered:[5,5,5,5]},
    slip:{file:ART+'material_slip_v2.png',size:[2172,724],bounds:[60,84,2050,560],slices:[12,12,12,12],rendered:[6,6,6,6]},
    jade:{file:ART+'jade_button_v3.png',size:[2172,724],bounds:[0,0,2172,724],slices:[16,8,16,8],rendered:[12,7,12,7]}
  };
  function imageView(file,size,bounds,stretch=true){return `<svg class="asset-view" viewBox="${bounds.join(' ')}" preserveAspectRatio="${stretch?'none':'xMidYMid meet'}" aria-hidden="true" focusable="false"><image href="${escape(file)}" width="${size[0]}" height="${size[1]}"></image></svg>`;}
  function sprite(name){const bounds={close:[350,652,140,138],down:[915,407,121,91],checkOff:[68,93,155,158],checkOn:[343,93,155,159]}[name];return imageView(CONTROL,[1122,1402],bounds,false);}
  function texture(type){const spec=textureDefs[type]||textureDefs.plain,[x,y,w,h]=spec.bounds,[l,t,r,b]=spec.slices;const xs=[x,x+l,x+w-r,x+w],ys=[y,y+t,y+h-b,y+h];let html=`<span class="skin-layer" style="grid-template-columns:${spec.rendered[0]}px 1fr ${spec.rendered[2]}px;grid-template-rows:${spec.rendered[1]}px 1fr ${spec.rendered[3]}px" aria-hidden="true">`;for(let row=0;row<3;row++)for(let col=0;col<3;col++)html+=`<span class="skin-bit" style="grid-area:${row+1}/${col+1}">${imageView(spec.file,spec.size,[xs[col],ys[row],xs[col+1]-xs[col],ys[row+1]-ys[row]])}</span>`;return html+'</span>';}
  function skinAll(root=document){root.querySelectorAll('[data-skin],button.btn:not([data-skin]),.number-field,.field-skin').forEach(node=>{const type=node.dataset.skin||(node.classList.contains('primary')||node.classList.contains('is-selected')||node.getAttribute('aria-pressed')==='true'?'jade':'plain');if(node.dataset.framed===type&&node.querySelector(':scope > .skin-layer'))return;node.querySelector(':scope > .skin-layer')?.remove();node.insertAdjacentHTML('afterbegin',texture(type));node.dataset.framed=type;node.dataset.materialSource=textureDefs[type].file;});}
  const btn=(text,action,attrs='',primary=false)=>`<button class="btn${primary?' primary':''}" data-action="${action}" ${attrs}>${text}</button>`;
  function fit(){const scale=Math.min((innerWidth-24)/1672,(innerHeight-70)/941);const stage=el('stage');stage.style.transform=`scale(${scale})`;stage.style.left=((innerWidth-1672*scale)/2)+'px';stage.style.top=((innerHeight-54-941*scale)/2)+'px';}
  function notify(message){clearTimeout(toastTimer);el('toast').innerHTML=texture('jade')+escape(message);el('toast').hidden=false;toastTimer=setTimeout(()=>el('toast').hidden=true,2600);}
  function sourceFor(name){return materialMap.get(name);}
  function utilityString(material){return material?Object.entries(material.utilities).map(([u,g])=>u+g+'品').join('、'):'';}


  function allInputs(){return Object.keys(roles).flatMap(role=>draft()[role].map(item=>({...item,role})));}
  function paperThumb(paper){return `<span class="paper-thumb">${texture('plain')}<span style="position:absolute;inset:4px;background:${paper.tone}"></span></span>`;}
  function updateAmounts(){
    const d=draft(),list=allInputs(),paper=d.paper[0];
    const kinds=new Set(d.ink.map(i=>i.name)).size,total=d.ink.reduce((n,i)=>n+i.quantity,0);
    el('material-kinds').textContent=kinds+'种';el('material-total').textContent=total+'份';
    el('paper-total').textContent=paper?paper.name+' ×'+paper.quantity+'张':'尚未选纸';el('brush-total').textContent=brushes[d.brush][0];
    el('cost-list').innerHTML=list.map(i=>`<div><span><span class="cost-role">${roles[i.role]} / </span>${escape(i.name)} ×${i.quantity}</span><span>—</span></div>`).join('')||'<p>尚无选材</p>';
    el('ink-summary').textContent=kinds+'种墨料，共'+total+'份';
    el('paper-summary').textContent=paper?paper.name+' · '+selectedPaper().brief:'尚未选择纸材';
    el('brush-summary').textContent=brushes[d.brush][0]+' · '+brushBrief[d.brush];
  }
  function render(){
    const d=draft(),recipe=recipeData[state.recipe],paper=state.role==='paper';
    document.querySelector('.recipe-current').textContent='符谱：'+recipe.name;el('work-name').textContent=recipe.name;el('work-description').textContent=recipe.role;
    el('brush-button').textContent='符笔：'+brushes[d.brush][0]+'　›';
    el('input-heading').textContent=paper?'选纸':'配墨';el('input-subheading').textContent=paper?'选择纸材与投入张数':'选择墨料与份数，自动研取调墨';
    el('add-button').textContent=paper?'选择纸材':'＋ 添加墨料';
    el('role-tabs').innerHTML=[['ink','符墨'],['paper','符纸']].map(([id,name])=>btn(name,'role',`data-role="${id}" aria-pressed="${id===state.role}"`)).join('');
    el('input-list').innerHTML=d[state.role].length?d[state.role].map((item,index)=>{
      const material=sourceFor(item.name),p=paper?paperKinds[item.id]:null;
      const icon=p?paperThumb(p):`<img src="${escape(ROOT+material.icon)}" alt="">`;
      return `<article class="material-card skin" data-skin="slip"><button class="material-select" data-action="replace" data-index="${index}" aria-label="更换${escape(item.name)}">${icon}<span><span class="material-name">${escape(item.name)}　›</span><span class="material-meta">${p?escape(p.brief+' · 候选纸材'):escape(material.element+' · '+material.texture+' · '+material.affinity+' · 库存—')}</span></span></button><div class="material-utilities">${p?'承墨、纹层与功用品级待定':escape(utilityString(material))}</div>${p?`<p class="paper-detail">${p.note}</p>`:''}<div class="dose-row"><span>${paper?'张数':'份数'}</span>${btn('−','less',`data-index="${index}" aria-label="减少${escape(item.name)}"`)}<span class="number-field"><input type="number" inputmode="numeric" min="1" step="1" value="${item.quantity}" data-quantity="${index}" aria-label="${escape(item.name)}${paper?'张数':'份数'}"></span>${btn('＋','more',`data-index="${index}" aria-label="增加${escape(item.name)}"`)}${btn('移除','remove',`data-index="${index}"`)}</div></article>`;
    }).join(''):'<div class="empty-slot">尚未选材<br><small>从下方选择材料</small></div>';
    updateAmounts();el('result-name').textContent=recipe.name;el('result-role').textContent=recipe.role;el('result-effect').textContent=recipe.effect;
    document.querySelectorAll('[data-preview]').forEach(b=>b.classList.toggle('active',b.dataset.preview===state.mode));skinAll();paintScene(state.mode==='prepare'?0:sceneProgress);paintResult();
  }
  function modal(title,content,footer='',narrow=false){clearTimeout(toastTimer);el('toast').hidden=true;closeModal();state.focus=document.activeElement;el('prepare-layer').inert=true;const overlay=el('overlay-layer');overlay.hidden=false;overlay.innerHTML=`<section class="dialog skin${narrow?' narrow':''}" data-skin="wide" role="dialog" aria-modal="true" aria-label="${escape(title)}"><header class="dialog-heading"><h2>${escape(title)}</h2>${btn('关闭','close','aria-label="关闭"')}</header><div class="dialog-content scroll-area">${content}</div>${footer?`<footer class="dialog-footer">${footer}</footer>`:''}</section>`;skinAll(overlay);overlay.querySelector('button,input,select')?.focus();}
  function closeModal(){const overlay=el('overlay-layer');overlay.hidden=true;overlay.innerHTML='';el('prepare-layer').inert=false;const previous=state.focus;state.focus=null;if(previous&&document.contains(previous))previous.focus();}
  function showRecipes(selected=state.recipe){const recipe=recipeData[selected];modal('符谱册',`<div class="recipe-list"><nav>${Object.entries(recipeData).map(([id,data])=>btn(escape(data.name),'recipe-info',`data-recipe="${id}" aria-pressed="${id===selected}"`)).join('')}</nav><article class="recipe-info"><h3>${recipe.name}</h3><p>${recipe.role}</p><p class="effect-line">${recipe.effect}</p><p>${recipe.intro}</p><div class="efficacy-tiers">${tiers.map(t=>`<span>${t}</span>`).join('')}</div><p class="recipe-note">固定符种，有限符效档。档名与数值为候选，制作后才鉴看档位；这里不选择成品品质。</p></article></div>`,btn('选用此符谱','choose-recipe',`data-recipe="${selected}"`,true));}
  function showBrushes(){modal('选用符笔',`<div class="tool-options">${Object.entries(brushes).map(([id,[name,note]])=>btn(`<strong>${name}</strong><small>${note}</small>`,'choose-brush',`data-brush="${id}" aria-pressed="${draft().brush===id}"`)).join('')}</div><p class="muted" style="margin-top:20px">系统按所选纸墨处理承墨与运笔，符笔侧重工艺适用。</p>`,'',true);}

  function showPaperKinds(){const current=draft().paper[0]?.id;modal('选择符纸',`<div class="paper-options">${Object.entries(paperKinds).map(([id,p])=>btn(`${paperThumb(p)}<span><strong>${p.name}</strong><small>${p.note}</small><small class="paper-caption">${p.tradeoff}</small></span>`,'choose-paper',`data-paper="${id}" aria-pressed="${id===current}"`)).join('')}</div><p class="muted" style="margin-top:17px">三类纸材为设计候选；墨承载、功用品级与用度尚未标定。</p>`);el('overlay-layer').querySelectorAll('[data-paper]').forEach(b=>b.classList.add('paper-option'));}
  function showPairing(){
    const d=draft(),p=d.paper[0]?selectedPaper():null;
    modal('纸墨配伍',`<div class="pairing-grid"><article class="skin" data-skin="plain"><h3>墨 · 术式来源</h3><p>${d.ink.length}种材料，共${d.ink.reduce((n,i)=>n+i.quantity,0)}份。</p><p>由材料形成可用墨；铺展、附着与配伍共同影响写入。</p><p class="muted">${d.ink.map(i=>escape(i.name)).join('、')||'尚未选择墨料'}</p></article><article class="skin" data-skin="plain"><h3>纸 · 材质载体</h3><p>${p?p.name:'尚未选择纸材'}</p><p>${p?p.note:'选择纸材后查看吸附与承纹特点。'}</p><p class="muted">纸材本身的功用按加工后的定义参与。</p></article><article class="skin" data-skin="plain"><h3>笔 · 工艺衔接</h3><p>${brushes[d.brush][0]}</p><p>${brushes[d.brush][1]}</p><p class="muted">研墨、运笔、必要接续与封符自动完成。</p></article></div><div class="pairing-examples"><p>润墨与疏纤纸：既要进入可用纹层，也要处理向无效区域渗散。</p><p>凝墨与紧密纸：既要停住细纹，也要完成附着和均匀送达。</p><p class="muted">这些是配伍原理，样稿不据此预报成符档位或数量。</p></div>`);
  }
  let pickerIndex=null;
  function showPicker(index=null){pickerIndex=index;if(state.role==='paper'){showPaperKinds();return;}
    modal('选择材料',`<div class="filter-row"><label>名称<span class="field-skin"><input id="material-search" type="search" placeholder="搜索材料"></span></label><label>功用<span class="field-skin"><select id="material-utility"><option value="">全部功用</option>${Object.keys(utilities).map(u=>`<option value="${u}">${u}</option>`).join('')}</select><span class="down">${sprite('down')}</span></span></label></div><div class="filter-row"><label>五行<span class="field-skin"><select id="material-element"><option value="">全部五行</option>${["木","火","土","金","水"].map(v=>`<option value="${v}">${v}</option>`).join("")}</select><span class="down">${sprite("down")}</span></span></label><label>质地<span class="field-skin"><select id="material-texture"><option value="">全部质地</option>${[...new Set(materials.map(m=>m.texture))].map(v=>`<option value="${escape(v)}">${escape(v)}</option>`).join("")}</select><span class="down">${sprite("down")}</span></span></label><label>相性<span class="field-skin"><select id="material-affinity"><option value="">全部相性</option>${["阴","和","阳"].map(v=>`<option value="${v}">${v}</option>`).join("")}</select><span class="down">${sprite("down")}</span></span></label></div><div id="picker-grid" class="picker-grid"></div><p class="muted" style="margin-top:18px">按五行、质地、相性与功用挑选，放入后仍可调整份数。</p>`);updatePicker();}
  function updatePicker(){const query=(el('material-search')?.value||'').trim(),utility=el('material-utility')?.value||'';const element=el('material-element')?.value||'',texture=el('material-texture')?.value||'',affinity=el('material-affinity')?.value||'';const filtered=materials.filter(m=>(!query||m.name.includes(query))&&(!utility||m.utilities[utility])&&(!element||m.element===element)&&(!texture||m.texture===texture)&&(!affinity||m.affinity===affinity));const grid=el('picker-grid');if(!grid)return;grid.innerHTML=filtered.map(m=>btn(`<img src="${escape(ROOT+m.icon)}" alt=""><span><strong>${escape(m.name)}</strong><small>${escape(m.element+' · '+m.texture+' · '+m.affinity)}</small><small>${escape(utilityString(m))}</small></span>`,'choose-material',`data-material="${escape(m.name)}"`)).join('')||'<p>没有匹配材料</p>';grid.querySelectorAll('button').forEach(b=>b.classList.add('picker-card'));skinAll(grid);}
  function showUtility(u='固'){const data=utilities[u];const sources=allInputs().filter(item=>sourceFor(item.name)?.utilities[u]).map(item=>{const material=sourceFor(item.name);return `${item.name} · ${u}${material.utilities[u]}品 · ${roles[item.role]} ×${item.quantity}`;});modal('十二功用与工艺',`<div class="utility-help"><nav class="utility-nav">${Object.keys(utilities).map(key=>btn(key,'utility',`data-utility="${key}" aria-pressed="${u===key}"`)).join('')}</nav><article class="utility-copy"><h3>${u} · ${data[0]}</h3><p>${data[1]}</p><p class="muted">当前已选来源</p><p>${sources.length?sources.map(escape).join('<br>'):'当前投料没有这一项来源。'}</p><p class="muted">同一份材料分配给不同工序时守恒；功用不直接拼成额外战斗效果。</p></article></div>`,'',true);}
  function showNotebook(){modal('配伍册',`<div class="ledger-list">${btn(`<strong>当前符谱 · 参考配伍</strong><small>载入后仍可修改墨料、纸材、份数和符笔。</small>`,'reference')}${state.saved.map((item,index)=>btn(`<strong>${escape(item.name)}</strong><small>${recipeData[item.recipe].name} · 本次会话保存</small>`,'load-plan',`data-plan="${index}"`)).join('')}</div><div class="save-line"><label for="plan-name">当前配伍</label><span class="field-skin"><input id="plan-name" placeholder="填写方案名称"></span>${btn('保存方案','save-plan')}</div>`,'',false);}

  function restBrush(){el('brush').style.left='975px';el('brush').style.top='427px';el('brush').style.transform='rotate(20deg)';}
  function setMode(mode){animationToken++;cancelAnimationFrame(frame);frame=0;clearTimeout(toastTimer);el('toast').hidden=true;state.mode=mode;sceneProgress=mode==='result'?1:0;document.body.dataset.mode=mode;el('scene-actions').hidden=mode!=='drawing';el('result-layer').hidden=mode!=='result';closeModal();restBrush();render();if(mode==='drawing')animateDrawing();}
  function paths(){return state.recipe==='flame'?[
    [[.5,.13],[.5,.83]],[[.22,.24],[.75,.29],[.31,.44],[.76,.55]],[[.76,.21],[.65,.44],[.84,.49]],[[.23,.64],[.78,.64]],[[.5,.83],[.35,.89],[.66,.89],[.58,.94]]
  ]:[
    [[.5,.13],[.31,.17],[.2,.28],[.24,.41],[.4,.48],[.61,.46],[.78,.35],[.75,.23],[.63,.15],[.5,.13]],[[.5,.3],[.33,.45],[.29,.61],[.4,.76],[.5,.8],[.66,.7],[.72,.54],[.64,.39],[.5,.3]],[[.5,.18],[.5,.82]],[[.31,.83],[.5,.91],[.69,.83],[.5,.96]]
  ];}
  const paperCorners=[[748,480],[907,480],[938,735],[714,735]];
  function project(u,v,c=paperCorners){return [c[0][0]*(1-u)*(1-v)+c[1][0]*u*(1-v)+c[2][0]*u*v+c[3][0]*(1-u)*v,c[0][1]*(1-u)*(1-v)+c[1][1]*u*(1-v)+c[2][1]*u*v+c[3][1]*(1-u)*v];}
  function drawPaper(ctx,corners,progress,guide){ctx.save();ctx.beginPath();corners.forEach((p,i)=>i?ctx.lineTo(...p):ctx.moveTo(...p));ctx.closePath();ctx.shadowColor='#302a2144';ctx.shadowBlur=7;ctx.shadowOffsetY=5;ctx.fillStyle='#e6d5ad';ctx.fill();ctx.shadowColor='transparent';ctx.clip();const xs=corners.map(p=>p[0]),ys=corners.map(p=>p[1]),x=Math.min(...xs),y=Math.min(...ys),w=Math.max(...xs)-x,h=Math.max(...ys)-y;if(plateImage.complete&&plateImage.naturalWidth)ctx.drawImage(plateImage,600,180,972,340,x,y,w,h);ctx.fillStyle=selectedPaper().tone;ctx.fillRect(x,y,w,h);const strokes=paths();strokes.forEach((stroke,index)=>{if(guide){ctx.beginPath();ctx.strokeStyle='#9b5a4636';ctx.lineWidth=1.4;stroke.forEach((point,i)=>i?ctx.lineTo(...project(...point,corners)):ctx.moveTo(...project(...point,corners)));ctx.stroke();}const part=Math.max(0,Math.min(1,progress*strokes.length-index));if(!part)return;ctx.beginPath();ctx.lineWidth=3.8;ctx.lineCap='round';ctx.lineJoin='round';ctx.strokeStyle='#8f4439';ctx.moveTo(...project(...stroke[0],corners));for(let i=1;i<stroke.length;i++){const step=Math.max(0,Math.min(1,part*(stroke.length-1)-(i-1)));if(!step)break;const a=stroke[i-1],b=stroke[i];ctx.lineTo(...project(a[0]+(b[0]-a[0])*step,a[1]+(b[1]-a[1])*step,corners));}ctx.stroke();});ctx.restore();}
  function paintScene(progress=0){const ctx=el('scene-canvas').getContext('2d');ctx.clearRect(0,0,1672,941);drawPaper(ctx,paperCorners,progress,state.mode==='prepare');}
  function paintResult(){const ctx=el('result-canvas').getContext('2d');ctx.clearRect(0,0,280,420);drawPaper(ctx,[[38,22],[240,22],[246,397],[32,397]],1,false);}
  function strokePoint(progress){const strokes=paths(),value=Math.min(strokes.length-.0001,progress*strokes.length),index=Math.floor(value),stroke=strokes[index],local=(value-index)*(stroke.length-1),point=Math.floor(local),t=local-point,a=stroke[point],b=stroke[Math.min(point+1,stroke.length-1)];return project(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t);}

  function animateDrawing(){
    const token=animationToken,start=performance.now();
    if(matchMedia('(prefers-reduced-motion: reduce)').matches){sceneProgress=1;paintScene(1);el('drawing-stage').textContent='自动制作示意 · 可跳过鉴看';return;}
    const tick=now=>{if(token!==animationToken)return;const elapsed=now-start,progress=Math.max(0,Math.min(1,(elapsed-950)/3300));sceneProgress=progress;
      el('drawing-stage').textContent=elapsed<650?'研墨 · 调墨':elapsed<950?'铺纸 · 承墨':progress<1?'自动运笔 · 成纹':'收锋 · 封符';paintScene(progress);
      if(elapsed>=950){const point=strokePoint(Math.min(.999,progress));el('brush').style.left=(point[0]-11)+'px';el('brush').style.top=(point[1]-254)+'px';el('brush').style.transform='rotate(13deg)';}
      if(elapsed<5100)frame=requestAnimationFrame(tick);else setMode('result');
    };frame=requestAnimationFrame(tick);
  }

  function act(button){
    const action=button.dataset.action,index=Number(button.dataset.index),d=draft();
    if(action==='close')return closeModal();if(action==='role'){state.role=button.dataset.role;return render();}
    if(action==='recipes')return showRecipes();if(action==='recipe-info')return showRecipes(button.dataset.recipe);
    if(action==='choose-recipe'){state.recipe=button.dataset.recipe;closeModal();return render();}
    if(action==='brushes')return showBrushes();if(action==='choose-brush'){d.brush=button.dataset.brush;closeModal();return render();}
    if(action==='pairing')return showPairing();
    if(action==='add')return showPicker(null);if(action==='replace')return showPicker(index);
    if(action==='choose-paper'){
      const id=button.dataset.paper;if(!paperKinds[id])return;const quantity=d.paper[0]?.quantity||1;d.paper=[{id,name:paperKinds[id].name,quantity}];closeModal();return render();
    }
    if(action==='choose-material'){
      const name=button.dataset.material;if(!sourceFor(name))return;
      if(pickerIndex===null){const existing=d.ink.find(i=>i.name===name);if(existing)existing.quantity++;else d.ink.push({name,quantity:1});}
      else{const old=d.ink[pickerIndex];if(old)old.name=name;const merged=new Map();d.ink.forEach(i=>merged.set(i.name,(merged.get(i.name)||0)+i.quantity));d.ink=[...merged].map(([name,quantity])=>({name,quantity}));}
      closeModal();return render();
    }
    if(action==='less'||action==='more'){const item=d[state.role][index];if(item){const n=item.quantity+(action==='more'?1:-1);if(Number.isSafeInteger(n))item.quantity=Math.max(1,n);}return render();}
    if(action==='remove'){d[state.role].splice(index,1);return render();}if(action==='clear'){d[state.role]=[];return render();}
    if(action==='utility')return showUtility(button.dataset.utility);if(action==='help')return showUtility('固');if(action==='notebook')return showNotebook();
    if(action==='reference'){drafts[state.recipe]=structuredClone(initial[state.recipe]);closeModal();render();return notify('已载入纸墨参考配伍，可继续调整材料。');}
    if(action==='save-plan'){const name=el('plan-name').value.trim();if(!name)return notify('请填写方案名称。');state.saved.push({name,recipe:state.recipe,draft:structuredClone(d)});showNotebook();return notify('已保存在本次样稿会话。');}
    if(action==='load-plan'){const item=state.saved[Number(button.dataset.plan)];if(!item)return;state.recipe=item.recipe;drafts[item.recipe]=structuredClone(item.draft);closeModal();render();return notify('已载入纸墨与符笔。');}
    if(action==='start'){if(!d.paper.length||!d.ink.length)return notify('请先选择符纸与墨料。');return setMode('drawing');}
    if(action==='skip')return setMode('result');if(action==='finish')return setMode('prepare');if(action==='back')return notify('独立界面样稿，正式入口将回到符室。');
  }
  const filterIds=['material-search','material-utility','material-element','material-texture','material-affinity'];
  document.addEventListener('click',event=>{const button=event.target.closest('[data-action]');if(button)act(button);const mode=event.target.closest('[data-preview]');if(mode)setMode(mode.dataset.preview);if(event.target===el('overlay-layer'))closeModal();});
  document.addEventListener('input',event=>{if(filterIds.includes(event.target.id))updatePicker();});
  document.addEventListener('change',event=>{
    const node=event.target;if(node.matches('[data-quantity]')){const index=Number(node.dataset.quantity),value=Number(node.value),item=draft()[state.role][index];if(item){if(Number.isSafeInteger(value)&&value>=1)item.quantity=value;else node.value=item.quantity;}updateAmounts();}
    if(filterIds.includes(node.id)&&node.id!=='material-search')updatePicker();
  });
  document.addEventListener('keydown',event=>{if(event.key==='Escape'&&!el('overlay-layer').hidden){event.preventDefault();closeModal();}if(event.key==='Tab'&&!el('overlay-layer').hidden){const controls=[...el('overlay-layer').querySelectorAll('button,input,select')].filter(n=>!n.disabled);const first=controls[0],last=controls[controls.length-1];if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}});
  window.addEventListener('resize',fit);plateImage.addEventListener('load',()=>{paintScene(sceneProgress);paintResult();});
  el('stone').innerHTML=imageView(TOOLS,[1774,887],[193,363,582,308],false);el('brush').innerHTML=imageView(TOOLS,[1774,887],[1322,15,71,854],false);fit();skinAll();document.body.dataset.mode='prepare';
  const catalog=window.TALISMAN_UI_CATALOG;
  if(catalog&&Array.isArray(catalog.materials)){materials=catalog.materials;materialMap=new Map(materials.map(item=>[item.name,item]));render();}else{console.error('Missing bundled material catalog');notify('材料目录未能载入。');}
})();
