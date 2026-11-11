(() => {
  'use strict';
  const ROOT='../../../';
  const ART=ROOT+'textures/ui/forging_20261021/';
  const TOOLS=ROOT+'textures/ui/talisman_crafting_20261108/tools_v1.png';
  const CONTROL=ROOT+'docs/美术规范/角色与静室纸墨木石样稿_20261108/assets/控件补充图集_v1.png';
  const el=id=>document.getElementById(id);
  const escape=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  let roles={paper:'符纸',ink1:'第一砚'};
  const tiers=['薄效','良效','优效','极效','满效'];
  const brushes={mixed:['兼毫符笔','均衡承接常规笔段，适合通用配制。'],hard:['硬毫符笔','细纹和接点定位利落，回环需放缓。'],soft:['柔毫符笔','回环连写较顺，急折与细接点需调整。'],long:['长锋符笔','较长连续段少回蘸，精细收口仍需处理。']};
  const utilities={
    固:['定纹与收口','支撑真实落墨的结构，不替代主纹来源。'],聚:['符首会聚','将有限来源集中送向主纹，不给各支复制满额。'],通:['纹路接续','建立可达连接；连接之后还需实际送达。'],化:['研墨与调墨','处理原料中允许炼化、分散的部分，与破共享真实残余。'],引:['分流与引向','安排主纹、辅纹和封口的去向，分流不增加总来源。'],破:['研料与主纹','研取致密料；留给主纹的另一份额才参与术式。'],护:['隔离纹域','限制真实局部干扰波及邻纹，不自动附送战斗护盾。'],镇:['稳定配伍','压住相激与过度会聚波动，不要求所有材料归和。'],生:['余墨补纹','动用真实余墨修补允许的细缺，不凭空重画。'],明:['澄墨与辨线','减少夹带和串染，不替代接续，也不限制查阅信息。'],藏:['暂存与封符','减少已送达来源的封存散失，不生成额外效力。'],行:['铺展与送达','沿已连通路线输送已有墨量，仍受笔与接点约束。']
  };
  const recipeData={
    flame:{name:'炎矢符',role:'远程 · 单体敌人',description:'固定术式 · 远程单体伤害',effect:'一次物理伤害，数值待定',intro:'以长直与转折构成主纹，封存一次远程攻击。配墨、行笔与封存影响符效及成符张数。'},
    guard:{name:'护身符',role:'自身',description:'固定术式 · 自身护盾',effect:'获得公共护盾，数值待定',intro:'以首尾相接的环纹封存护身术式。配墨、接续与封口影响符效及成符张数。'}
  };

  const strokeData={
    flame:[
      {name:'贯纹',need:'起笔与长段',tip:'贯穿主干，连续承墨与末端送达都要兼顾。',before:[]},
      {name:'折纹',need:'接到主纹',tip:'转折须清楚，并在指定接头与主纹相接。',before:[0]},
      {name:'旁纹',need:'与交叉纹相隔',tip:'同湿写入需要隔纹；候底纹定形后叠写仍须保持边界。',before:[0,1]},
      {name:'接纹',need:'接到主纹',tip:'可研究趁湿接续，或候定形后另用实际墨处理搭接。',before:[0]},
      {name:'收口',need:'收锋封符',tip:'收束已经形成的纹路，可以继续用原砚。',before:[0,1,2,3]}
    ],
    guard:[
      {name:'外环',need:'回环闭合',tip:'回到预留接头；长回环需有真实墨量连续承接。',before:[]},
      {name:'内环',need:'与外环相隔',tip:'内外纹保持边界，接点按符谱规定相接。',before:[0]},
      {name:'贯接',need:'连接指定接头',tip:'先完成需要湿接的接头，可减少后续搭接工作。',before:[0]},
      {name:'收口',need:'收锋封符',tip:'在实际收口处完成定纹与封存。',before:[0,1,2]}
    ]
  };
  const makeDraft=(kind)=>({
    paper:[{name:'基础符纸',quantity:1}],
    ink1:(kind==='flame'?['赤纹燧石','朱阳花籽','灵石','灵泉露','石蜂蜡']:['玄甲薄片','石蜂蜡','青木芯','清音玉片','雾心菌核']).map(name=>({name,quantity:1})),
    inks:['ink1'],nextInk:2,brush:kind==='flame'?'hard':'soft',
    order:kind==='flame'?[0,1,3,2,4]:[0,2,1,3],
    strokePlan:strokeData[kind].map((s,index)=>({ink:'ink1',reload:index===0,timing:s.need.includes('相隔')?'set':'wet'}))
  });
  const initial={flame:makeDraft('flame'),guard:makeDraft('guard')};
  const drafts=structuredClone(initial);
  const state={recipe:'flame',role:'ink1',mode:'prepare',saved:[],focus:null,stroke:0};
  let materials=[],materialMap=new Map(),frame=0,animationToken=0,toastTimer=0,sceneProgress=0;
  const draft=()=>drafts[state.recipe];
  const inkLabel=id=>'第'+({1:'一',2:'二',3:'三',4:'四',5:'五',6:'六'}[Number(id.slice(3))]||id.slice(3))+'砚';
  function syncRoles(){roles={paper:'符纸',...Object.fromEntries(draft().inks.map(id=>[id,inkLabel(id)]))};if(!Object.hasOwn(roles,state.role))state.role=draft().inks[0];if(!strokeData[state.recipe][state.stroke])state.stroke=0;}
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
  function updateAmounts(){
    const d=draft();
    const list=allInputs();el('material-kinds').textContent=new Set(list.map(i=>i.name)).size+'种';
    el('material-total').textContent=list.reduce((n,i)=>n+i.quantity,0)+'份';
    el('pool-total').textContent=d.inks.length+'砚';el('paper-total').textContent=d.paper.reduce((n,i)=>n+i.quantity,0)+'张';
    el('cost-list').innerHTML=list.map(i=>`<div><span><span class="cost-role">${roles[i.role]} / </span>${escape(i.name)} ×${i.quantity}</span><span>—</span></div>`).join('')||'<p>尚无选材</p>';
  }
  function render(){
    syncRoles();const d=draft(),recipe=recipeData[state.recipe],paper=state.role==='paper';
    document.querySelector('.recipe-current').textContent='符谱：'+recipe.name;
    el('work-name').textContent=recipe.name;el('work-description').textContent=recipe.role;
    el('brush-button').textContent='符笔：'+brushes[d.brush][0]+'　›';
    el('input-heading').textContent=paper?'选纸':'配墨';
    el('input-subheading').textContent=paper?'纸面承纹，张数是本次投入':'一砚可写全符，按笔段安排用墨';
    el('role-tabs').innerHTML=[['paper','符纸',paper],['ink','墨砚',!paper]].map(([id,name,on])=>btn(name,'role',`data-role="${id}" aria-pressed="${on}"`)).join('');
    el('ink-controls').hidden=paper;
    el('ink-select').innerHTML=d.inks.map(id=>`<option value="${id}" ${id===state.role?'selected':''}>${inkLabel(id)}</option>`).join('');
    el('ink-controls').querySelector('.down').innerHTML=sprite('down');
    el('input-list').innerHTML=d[state.role].length?d[state.role].map((item,index)=>{
      const m=sourceFor(item.name);
      const icon=m?`<img src="${escape(ROOT+m.icon)}" alt="">`:`<span style="width:62px;height:68px;display:block">${imageView(ART+'material_panel_v3.png',[2172,724],[600,180,972,340])}</span>`;
      return `<article class="material-card skin" data-skin="slip"><button class="material-select" data-action="replace" data-index="${index}" aria-label="更换${escape(item.name)}">${icon}<span><span class="material-name">${escape(item.name)}　›</span><span class="material-meta">${m?escape(m.element+' · '+m.texture+' · '+m.affinity+' · 库存—'):'承墨与纹面规格待定'}</span></span></button><div class="material-utilities">${m?escape(utilityString(m)):'本次符纸投入'}</div><div class="dose-row"><span>${paper?'张数':'份数'}</span>${btn('−','less',`data-index="${index}" aria-label="减少${escape(item.name)}"`)}<span class="number-field"><input type="number" inputmode="numeric" min="1" step="1" value="${item.quantity}" data-quantity="${index}" aria-label="${escape(item.name)}${paper?'张数':'份数'}"></span>${btn('＋','more',`data-index="${index}" aria-label="增加${escape(item.name)}"`)}${btn('移除','remove',`data-index="${index}"`)}</div>${!paper&&d.inks.length>1?`<div class="move-material">${btn('移入别砚','transfer',`data-index="${index}" aria-label="将${escape(item.name)}移入别砚"`)}</div>`:''}</article>`;
    }).join(''):'<div class="empty-slot">尚未选材<br><small>下方可添加材料</small></div>';
    updateAmounts();
    el('stroke-list').innerHTML=d.order.map((id,pos)=>btn((pos+1)+' '+strokeData[state.recipe][id].name,'stroke',`data-stroke="${id}" aria-pressed="${state.stroke===id}"`)).join('');
    const reload=d.strokePlan.filter((s,id)=>s.reload&&id!==d.order[0]).length;
    const waits=d.strokePlan.filter((s,id)=>s.timing==='set'&&id!==d.order[0]).length;
    el('pattern-summary').textContent=d.inks.length+'砚 · '+d.order.length+'笔 · 提笔补墨'+reload+'处 · 候干'+waits+'处';
    el('stone-label-text').textContent=paper?inkLabel(d.inks[0]):inkLabel(state.role);
    el('result-name').textContent=recipe.name;el('result-role').textContent=recipe.role;el('result-effect').textContent=recipe.effect;
    document.querySelectorAll('[data-preview]').forEach(b=>b.classList.toggle('active',b.dataset.preview===state.mode));
    skinAll();paintScene(state.mode==='prepare'?0:sceneProgress);paintResult();paintPattern();
  }
  function modal(title,content,footer='',narrow=false){clearTimeout(toastTimer);el('toast').hidden=true;closeModal();state.focus=document.activeElement;el('prepare-layer').inert=true;const overlay=el('overlay-layer');overlay.hidden=false;overlay.innerHTML=`<section class="dialog skin${narrow?' narrow':''}" data-skin="wide" role="dialog" aria-modal="true" aria-label="${escape(title)}"><header class="dialog-heading"><h2>${escape(title)}</h2>${btn(sprite('close'),'close','aria-label="关闭"')}</header><div class="dialog-content scroll-area">${content}</div>${footer?`<footer class="dialog-footer">${footer}</footer>`:''}</section>`;skinAll(overlay);overlay.querySelector('button,input,select')?.focus();}
  function closeModal(){const overlay=el('overlay-layer');overlay.hidden=true;overlay.innerHTML='';el('prepare-layer').inert=false;const previous=state.focus;state.focus=null;if(previous&&document.contains(previous))previous.focus();}
  function showRecipes(selected=state.recipe){const recipe=recipeData[selected];modal('符谱册',`<div class="recipe-list"><nav>${Object.entries(recipeData).map(([id,data])=>btn(escape(data.name),'recipe-info',`data-recipe="${id}" aria-pressed="${id===selected}"`)).join('')}</nav><article class="recipe-info"><h3>${recipe.name}</h3><p>${recipe.role}</p><p class="effect-line">${recipe.effect}</p><p>${recipe.intro}</p><div class="efficacy-tiers">${tiers.map(t=>`<span>${t}</span>`).join('')}</div><p class="recipe-note">固定符种，有限符效档。档名与数值为候选，制作后才鉴看档位；这里不选择成品品质。</p></article></div>`,btn('选用此符谱','choose-recipe',`data-recipe="${selected}"`,true));}
  function showBrushes(){modal('选用符笔',`<div class="tool-options">${Object.entries(brushes).map(([id,[name,note]])=>btn(`<strong>${name}</strong><small>${note}</small>`,'choose-brush',`data-brush="${id}" aria-pressed="${draft().brush===id}"`)).join('')}</div><p class="muted" style="margin-top:20px">选笔须兼顾墨性、连续笔段和接点；承墨不足时需要回蘸。</p>`,'',true);}

  function sequenceLegal(order){return strokeData[state.recipe].every((s,id)=>s.before.every(before=>order.indexOf(before)<order.indexOf(id)));}
  function movedOrder(id,delta){const order=[...draft().order],a=order.indexOf(id),b=a+delta;if(b<0||b>=order.length)return null;[order[a],order[b]]=[order[b],order[a]];return sequenceLegal(order)?order:null;}
  function showPattern(id=state.stroke){
    state.stroke=id;const d=draft(),p=d.strokePlan[id],info=strokeData[state.recipe][id],pos=d.order.indexOf(id),mustDip=pos===0||p.ink!==d.strokePlan[d.order[pos-1]].ink;
    modal('笔序安排',`<div class="sequence-editor"><div class="sequence-paper"><canvas id="pattern-canvas" width="340" height="480" aria-label="放大的符纸笔段定位"></canvas><p>朱线：当前笔段　青圈：蘸墨／补墨点<br>按符谱保留主干与接点</p></div><div class="sequence-config"><nav class="sequence-menu" aria-label="选择笔段">${d.order.map((key,i)=>btn((i+1)+' '+strokeData[state.recipe][key].name,'stroke',`data-stroke="${key}" aria-pressed="${key===id}"`)).join('')}</nav><h3>第${pos+1}笔 · ${info.name}</h3><p class="stroke-requirement">符谱要求：${info.need}</p><p class="stroke-note">${info.tip}</p><div class="setting-row"><label for="stroke-ink">蘸取墨砚</label><span class="field-skin"><select id="stroke-ink">${d.inks.map(ink=>`<option value="${ink}" ${p.ink===ink?'selected':''}>${inkLabel(ink)}</option>`).join('')}</select><span class="down">${sprite('down')}</span></span></div><div class="setting-row"><label for="stroke-timing">行笔时机</label><span class="field-skin"><select id="stroke-timing"><option value="wet" ${p.timing==='wet'?'selected':''}>趁湿行笔</option><option value="set" ${p.timing==='set'?'selected':''}>候底纹定形</option></select><span class="down">${sprite('down')}</span></span></div><label class="check-line"><input id="stroke-reload" type="checkbox" ${p.reload||mustDip?'checked':''} ${mustDip?'disabled':''}><span class="check-art">${sprite(p.reload||mustDip?'checkOn':'checkOff')}</span><span>${mustDip?'起笔或换砚时蘸墨':'此笔前提笔补墨'}</span></label><div class="sequence-order">${btn('〈 前移','move-stroke',`data-step="-1" ${movedOrder(id,-1)?'':'disabled'}`)}${btn('后移 〉','move-stroke',`data-step="1" ${movedOrder(id,1)?'':'disabled'}`)}</div><p class="sequence-footnote">候干会改变接续条件并增加工时；相接与相隔还需纸、墨和笔法支持。换砚时自动安排蘸墨。</p></div></div>`,btn('返回案面','close','',true));
    paintPattern();render();
  }
  function paintPattern(){const canvas=el('pattern-canvas');if(!canvas)return;const ctx=canvas.getContext('2d');ctx.clearRect(0,0,340,480);drawPaper(ctx,[[62,14],[278,14],[300,458],[40,458]],0,true);}
  function showInkInfo(){
    const d=draft(),ink=state.role==='paper'?d.inks[0]:state.role,items=d[ink],present=new Set(items.flatMap(i=>Object.keys(sourceFor(i.name)?.utilities||{})));
    modal(inkLabel(ink)+' · 墨性与功用',`<p>这一砚可以用于主纹、接续或收口，具体去处由笔序安排。另开一砚用于不同的实际配制需要。</p><div class="ink-info-grid"><div class="skin" data-skin="plain"><h3>铺展与附着</h3><p>偏润利于长段，偏凝便于控细线；表现随纸笔而变。</p></div><div class="skin" data-skin="plain"><h3>干湿与接续</h3><p>定形快便于后续叠写，留给湿接的时间也更短。</p></div><div class="skin" data-skin="plain"><h3>收锋与封存</h3><p>收口可继续用原砚，藏与固保留已经形成的纹路。</p></div></div><p class="muted" style="margin-top:16px">具体墨性与用度尚未标定，样稿只显示材料来源。</p><div class="ink-info-list">${items.length?items.map(i=>escape(i.name)+' ×'+i.quantity+'　'+escape(utilityString(sourceFor(i.name)))).join('<br>'):'本砚尚未放入材料。'}</div><div class="ink-utility-buttons">${Object.keys(utilities).filter(u=>present.has(u)).map(u=>btn(u,'utility',`data-utility="${u}"`)).join('')}</div>`);
  }
  let transferIndex=0;
  function showTransfer(index){const d=draft(),item=d[state.role][index];if(!item||d.inks.length<2)return;transferIndex=index;modal('移入别砚',`<div class="transfer-form"><p>${escape(item.name)} · ${inkLabel(state.role)}现有${item.quantity}份</p><label><span>移入</span><span class="field-skin"><select id="transfer-ink">${d.inks.filter(i=>i!==state.role).map(i=>`<option value="${i}">${inkLabel(i)}</option>`).join('')}</select><span class="down">${sprite('down')}</span></span></label><label><span>份数</span><span class="number-field"><input id="transfer-quantity" type="number" min="1" max="${item.quantity}" value="${item.quantity}" aria-label="移入份数"></span></label><p class="muted">从原砚移出相同份数，整批投入保持不变。</p></div>`,btn('移入此砚','apply-transfer','',true),true);}
  let pickerIndex=null;
  function showPicker(index=null){pickerIndex=index;if(state.role==='paper'){modal('选择符纸','<h3>基础符纸</h3><p>当前样稿用共用符纸展示张数和案面；正式纸材的承载、纹面、封存及物品定义仍待定稿。</p>',btn('放入样稿符纸','choose-paper','',true),true);return;}
    modal('选择材料',`<div class="filter-row"><label>名称<span class="field-skin"><input id="material-search" type="search" placeholder="搜索材料"></span></label><label>功用<span class="field-skin"><select id="material-utility"><option value="">全部功用</option>${Object.keys(utilities).map(u=>`<option value="${u}">${u}</option>`).join('')}</select><span class="down">${sprite('down')}</span></span></label></div><div class="filter-row"><label>五行<span class="field-skin"><select id="material-element"><option value="">全部五行</option>${["木","火","土","金","水"].map(v=>`<option value="${v}">${v}</option>`).join("")}</select><span class="down">${sprite("down")}</span></span></label><label>质地<span class="field-skin"><select id="material-texture"><option value="">全部质地</option>${[...new Set(materials.map(m=>m.texture))].map(v=>`<option value="${escape(v)}">${escape(v)}</option>`).join("")}</select><span class="down">${sprite("down")}</span></span></label><label>相性<span class="field-skin"><select id="material-affinity"><option value="">全部相性</option>${["阴","和","阳"].map(v=>`<option value="${v}">${v}</option>`).join("")}</select><span class="down">${sprite("down")}</span></span></label></div><div id="picker-grid" class="picker-grid"></div><p class="muted" style="margin-top:18px">按五行、质地、相性与功用挑选，放入后仍可调整份数。</p>`);updatePicker();}
  function updatePicker(){const query=(el('material-search')?.value||'').trim(),utility=el('material-utility')?.value||'';const element=el('material-element')?.value||'',texture=el('material-texture')?.value||'',affinity=el('material-affinity')?.value||'';const filtered=materials.filter(m=>(!query||m.name.includes(query))&&(!utility||m.utilities[utility])&&(!element||m.element===element)&&(!texture||m.texture===texture)&&(!affinity||m.affinity===affinity));const grid=el('picker-grid');if(!grid)return;grid.innerHTML=filtered.map(m=>btn(`<img src="${escape(ROOT+m.icon)}" alt=""><span><strong>${escape(m.name)}</strong><small>${escape(m.element+' · '+m.texture+' · '+m.affinity)}</small><small>${escape(utilityString(m))}</small></span>`,'choose-material',`data-material="${escape(m.name)}"`)).join('')||'<p>没有匹配材料</p>';grid.querySelectorAll('button').forEach(b=>b.classList.add('picker-card'));skinAll(grid);}
  function showUtility(u='固'){const data=utilities[u];const sources=allInputs().filter(item=>sourceFor(item.name)?.utilities[u]).map(item=>{const material=sourceFor(item.name);return `${item.name} · ${u}${material.utilities[u]}品 · ${roles[item.role]} ×${item.quantity}`;});modal('十二功用与工艺',`<div class="utility-help"><nav class="utility-nav">${Object.keys(utilities).map(key=>btn(key,'utility',`data-utility="${key}" aria-pressed="${u===key}"`)).join('')}</nav><article class="utility-copy"><h3>${u} · ${data[0]}</h3><p>${data[1]}</p><p class="muted">当前已选来源</p><p>${sources.length?sources.map(escape).join('<br>'):'当前投料没有这一项来源。'}</p><p class="muted">同一份材料分配给不同工序时守恒；功用不直接拼成额外战斗效果。</p></article></div>`,'',true);}
  function showNotebook(){modal('配伍册',`<div class="ledger-list">${btn(`<strong>当前符谱 · 参考配伍</strong><small>载入后仍可修改材料、份数、笔与纹路，不锁定符效档。</small>`,'reference')}${state.saved.map((item,index)=>btn(`<strong>${escape(item.name)}</strong><small>${recipeData[item.recipe].name} · 本次会话保存</small>`,'load-plan',`data-plan="${index}"`)).join('')}</div><div class="save-line"><label for="plan-name">当前配伍</label><span class="field-skin"><input id="plan-name" placeholder="填写方案名称"></span>${btn('保存方案','save-plan')}</div>`,'',false);}

  function setMode(mode){animationToken++;cancelAnimationFrame(frame);frame=0;clearTimeout(toastTimer);el('toast').hidden=true;state.mode=mode;sceneProgress=mode==='result'?1:0;document.body.dataset.mode=mode;el('scene-actions').hidden=mode!=='drawing';el('result-layer').hidden=mode!=='result';closeModal();restBrush();render();if(mode==='drawing')animateDrawing();}
  function restBrush(){el('brush').style.left='975px';el('brush').style.top='427px';el('brush').style.transform='rotate(20deg)';}
  function paths(){const raw=state.recipe==='flame'?[
    [[.5,.13],[.5,.83]],[[.22,.24],[.75,.29],[.31,.44],[.76,.55]],[[.76,.21],[.65,.44],[.84,.49]],[[.23,.64],[.78,.64]],[[.5,.83],[.35,.89],[.66,.89],[.58,.94]]
  ]:[
    [[.5,.13],[.31,.17],[.2,.28],[.24,.41],[.4,.48],[.61,.46],[.78,.35],[.75,.23],[.63,.15],[.5,.13]],[[.5,.3],[.33,.45],[.29,.61],[.4,.76],[.5,.8],[.66,.7],[.72,.54],[.64,.39],[.5,.3]],[[.5,.18],[.5,.82]],[[.31,.83],[.5,.91],[.69,.83],[.5,.96]]
  ];return draft().order.map(id=>raw[id]);}
  const paperCorners=[[748,480],[907,480],[938,735],[714,735]];
  function project(u,v,c=paperCorners){return [c[0][0]*(1-u)*(1-v)+c[1][0]*u*(1-v)+c[2][0]*u*v+c[3][0]*(1-u)*v,c[0][1]*(1-u)*(1-v)+c[1][1]*u*(1-v)+c[2][1]*u*v+c[3][1]*(1-u)*v];}
  function drawPaper(ctx,corners,progress,guide){ctx.save();ctx.beginPath();corners.forEach((p,i)=>i?ctx.lineTo(...p):ctx.moveTo(...p));ctx.closePath();ctx.shadowColor='#302a2144';ctx.shadowBlur=7;ctx.shadowOffsetY=5;ctx.fillStyle='#e6d5ad';ctx.fill();ctx.shadowColor='transparent';ctx.clip();const xs=corners.map(p=>p[0]),ys=corners.map(p=>p[1]),x=Math.min(...xs),y=Math.min(...ys),w=Math.max(...xs)-x,h=Math.max(...ys)-y;if(plateImage.complete&&plateImage.naturalWidth)ctx.drawImage(plateImage,600,180,972,340,x,y,w,h);ctx.fillStyle='#d8bd7630';ctx.fillRect(x,y,w,h);const strokes=paths();strokes.forEach((stroke,index)=>{if(guide){ctx.beginPath();ctx.strokeStyle=draft().order[index]===state.stroke?'#8f4439':'#9b5a4666';ctx.lineWidth=draft().order[index]===state.stroke?3.2:1.6;stroke.forEach((point,i)=>i?ctx.lineTo(...project(...point,corners)):ctx.moveTo(...project(...point,corners)));ctx.stroke();const start=project(...stroke[0],corners);ctx.fillStyle='#4f6759';ctx.font='17px ZhenKai,serif';ctx.fillText(String(index+1),start[0]-17,start[1]-6);if(index===0||draft().strokePlan[draft().order[index]].reload||draft().strokePlan[draft().order[index]].ink!==draft().strokePlan[draft().order[index-1]].ink){ctx.beginPath();ctx.strokeStyle='#527b68';ctx.lineWidth=1.8;ctx.arc(start[0],start[1],6,0,Math.PI*2);ctx.stroke();}}const part=Math.max(0,Math.min(1,progress*strokes.length-index));if(!part)return;ctx.beginPath();ctx.lineWidth=3.8;ctx.lineCap='round';ctx.lineJoin='round';ctx.strokeStyle='#8f4439';ctx.moveTo(...project(...stroke[0],corners));for(let i=1;i<stroke.length;i++){const step=Math.max(0,Math.min(1,part*(stroke.length-1)-(i-1)));if(!step)break;const a=stroke[i-1],b=stroke[i];ctx.lineTo(...project(a[0]+(b[0]-a[0])*step,a[1]+(b[1]-a[1])*step,corners));}ctx.stroke();});ctx.restore();}
  function paintScene(progress=0){const ctx=el('scene-canvas').getContext('2d');ctx.clearRect(0,0,1672,941);drawPaper(ctx,paperCorners,progress,state.mode==='prepare');}
  function paintResult(){const ctx=el('result-canvas').getContext('2d');ctx.clearRect(0,0,280,420);drawPaper(ctx,[[38,22],[240,22],[246,397],[32,397]],1,false);}
  function strokePoint(progress){const strokes=paths(),value=Math.min(strokes.length-.0001,progress*strokes.length),index=Math.floor(value),stroke=strokes[index],local=(value-index)*(stroke.length-1),point=Math.floor(local),t=local-point,a=stroke[point],b=stroke[Math.min(point+1,stroke.length-1)];return project(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t);}

  function drawingEvents(){
    const d=draft(),events=[{kind:'prep',label:'研墨 · 调墨',duration:450,progress:0}],count=d.order.length;let previous=null;
    d.order.forEach((id,pos)=>{const p=d.strokePlan[id],name=strokeData[state.recipe][id].name,progress=pos/count;
      if(pos>0&&p.timing==='set')events.push({kind:'wait',label:'第'+(pos+1)+'笔 '+name+' · 候底纹定形',duration:500,progress,ink:p.ink});
      if(pos===0||p.reload||previous!==p.ink)events.push({kind:'dip',label:'蘸墨 · '+inkLabel(p.ink),duration:300,progress,ink:p.ink});
      events.push({kind:'write',label:'第'+(pos+1)+'笔 '+name+' · '+inkLabel(p.ink)+' · '+(p.timing==='set'?'定形后行笔':'趁湿行笔'),duration:650,progress,end:(pos+1)/count,ink:p.ink});previous=p.ink;
    });events.push({kind:'seal',label:'收锋 · 封符',duration:450,progress:1});return events;
  }
  function animateDrawing(){
    const token=animationToken,events=drawingEvents(),start=performance.now();
    if(matchMedia('(prefers-reduced-motion: reduce)').matches){sceneProgress=1;paintScene(1);el('drawing-stage').textContent='静态笔序示意 · 可跳过进入鉴看';return;}
    const tick=now=>{
      if(token!==animationToken)return;
      let elapsed=now-start,event=null,local=0;
      for(const e of events){if(elapsed<e.duration){event=e;local=elapsed/e.duration;break;}elapsed-=e.duration;}
      if(!event){setMode('result');return;}
      el('drawing-stage').textContent=event.label;if(event.ink)el('stone-label-text').textContent=inkLabel(event.ink);
      sceneProgress=event.kind==='write'?event.progress+(event.end-event.progress)*local:event.progress;paintScene(sceneProgress);
      if(event.kind==='write'){const point=strokePoint(Math.min(.99999,sceneProgress));el('brush').style.left=(point[0]-11)+'px';el('brush').style.top=(point[1]-254)+'px';el('brush').style.transform='rotate(13deg)';}
      else if(event.kind==='dip'){el('brush').style.left='1033px';el('brush').style.top='458px';el('brush').style.transform='rotate(7deg)';}
      else restBrush();
      frame=requestAnimationFrame(tick);
    };frame=requestAnimationFrame(tick);
  }

  function act(button){
    const action=button.dataset.action,index=Number(button.dataset.index),d=draft();
    if(action==='close')return closeModal();
    if(action==='role'){state.role=button.dataset.role==='paper'?'paper':d.inks[0];return render();}
    if(action==='recipes')return showRecipes();if(action==='recipe-info')return showRecipes(button.dataset.recipe);
    if(action==='choose-recipe'){state.recipe=button.dataset.recipe;state.role='ink1';state.stroke=0;closeModal();return render();}
    if(action==='brushes')return showBrushes();if(action==='choose-brush'){d.brush=button.dataset.brush;closeModal();return render();}
    if(action==='pattern')return showPattern();if(action==='stroke')return showPattern(Number(button.dataset.stroke));
    if(action==='move-stroke'){const next=movedOrder(state.stroke,Number(button.dataset.step));if(next){d.order=next;return showPattern(state.stroke);}return;}
    if(action==='ink-info')return showInkInfo();
    if(action==='new-ink'){const id='ink'+d.nextInk++;d.inks.push(id);d[id]=[];state.role=id;return render();}
    if(action==='delete-ink'){
      if(d.inks.length===1)return notify('至少保留一砚。');
      if(d.strokePlan.some(p=>p.ink===state.role))return notify('还有笔段选用此砚，请先在笔序中换砚。');
      d.inks=d.inks.filter(id=>id!==state.role);delete d[state.role];state.role=d.inks[0];return render();
    }
    if(action==='transfer')return showTransfer(index);
    if(action==='apply-transfer'){
      const item=d[state.role][transferIndex],target=el('transfer-ink').value,n=Number(el('transfer-quantity').value);
      if(!item||!d.inks.includes(target)||target===state.role||!Number.isSafeInteger(n)||n<1||n>item.quantity)return notify('请填写原砚可移出的整数份数。');
      const existing=d[target].find(i=>i.name===item.name);if(existing)existing.quantity+=n;else d[target].push({name:item.name,quantity:n});
      item.quantity-=n;if(!item.quantity)d[state.role].splice(transferIndex,1);closeModal();render();return notify('已按实际份数移入，整批投入不变。');
    }
    if(action==='add')return showPicker(null);if(action==='replace')return showPicker(index);
    if(action==='choose-paper'){const paper={name:'基础符纸',quantity:1};if(pickerIndex===null){if(d.paper.length)d.paper[0].quantity++;else d.paper.push(paper);}else d.paper[pickerIndex]=paper;closeModal();return render();}
    if(action==='choose-material'){
      const name=button.dataset.material;if(!sourceFor(name))return;
      if(pickerIndex===null){const existing=d[state.role].find(i=>i.name===name);if(existing)existing.quantity++;else d[state.role].push({name,quantity:1});}
      else{const old=d[state.role][pickerIndex];if(old)old.name=name;const merged=new Map();d[state.role].forEach(i=>merged.set(i.name,(merged.get(i.name)||0)+i.quantity));d[state.role]=[...merged].map(([name,quantity])=>({name,quantity}));}
      closeModal();return render();
    }
    if(action==='less'||action==='more'){const item=d[state.role][index];if(item){const n=item.quantity+(action==='more'?1:-1);if(Number.isSafeInteger(n))item.quantity=Math.max(1,n);}return render();}
    if(action==='remove'){d[state.role].splice(index,1);return render();}if(action==='clear'){d[state.role]=[];return render();}
    if(action==='utility')return showUtility(button.dataset.utility);if(action==='help')return showUtility('固');if(action==='notebook')return showNotebook();
    if(action==='reference'){drafts[state.recipe]=structuredClone(initial[state.recipe]);state.role='ink1';state.stroke=0;closeModal();render();return notify('已载入一砚参考草稿，可继续安排笔序。');}
    if(action==='save-plan'){const name=el('plan-name').value.trim();if(!name)return notify('请填写方案名称。');state.saved.push({name,recipe:state.recipe,draft:structuredClone(d)});showNotebook();return notify('已保存在本次样稿会话。');}
    if(action==='load-plan'){const item=state.saved[Number(button.dataset.plan)];if(!item)return;state.recipe=item.recipe;drafts[item.recipe]=structuredClone(item.draft);state.role='ink1';state.stroke=0;closeModal();render();return notify('已载入配砚与笔序。');}
    if(action==='start'){
      if(!d.paper.length)return notify('请先放入符纸。');
      if(!sequenceLegal(d.order))return notify('笔序需符合符谱先后约束。');
      const missing=d.order.find(id=>!d[d.strokePlan[id].ink]?.length);
      if(missing!==undefined)return notify(strokeData[state.recipe][missing].name+'所用墨砚尚无选材。');
      return setMode('drawing');
    }
    if(action==='skip')return setMode('result');if(action==='finish')return setMode('prepare');
    if(action==='back')return notify('独立界面样稿，正式入口将回到符室。');
  }
  document.addEventListener('click',event=>{const button=event.target.closest('[data-action]');if(button)act(button);const mode=event.target.closest('[data-preview]');if(mode)setMode(mode.dataset.preview);if(event.target===el('overlay-layer'))closeModal();});
  const filterIds=['material-search','material-utility','material-element','material-texture','material-affinity'];
  document.addEventListener('input',event=>{if(filterIds.includes(event.target.id))updatePicker();});
  document.addEventListener('change',event=>{
    const d=draft(),node=event.target;
    if(node.matches('[data-quantity]')){const index=Number(node.dataset.quantity),value=Number(node.value),item=d[state.role][index];if(item){if(Number.isSafeInteger(value)&&value>=1)item.quantity=value;else node.value=item.quantity;}updateAmounts();}
    if(filterIds.includes(node.id)&&node.id!=='material-search')updatePicker();
    if(node.id==='ink-select'){state.role=node.value;render();}
    if(['stroke-ink','stroke-timing','stroke-reload'].includes(node.id)){
      const p=d.strokePlan[state.stroke];if(node.id==='stroke-ink'){p.ink=node.value;return showPattern(state.stroke);}if(node.id==='stroke-timing')p.timing=node.value;
      if(node.id==='stroke-reload'){p.reload=node.checked;node.nextElementSibling.innerHTML=sprite(node.checked?'checkOn':'checkOff');}
      render();
    }
  });
  document.addEventListener('keydown',event=>{
    if(event.key==='Escape'&&!el('overlay-layer').hidden){event.preventDefault();closeModal();}
    if(event.key==='Tab'&&!el('overlay-layer').hidden){const controls=[...el('overlay-layer').querySelectorAll('button,input,select')].filter(n=>!n.disabled);const first=controls[0],last=controls[controls.length-1];if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}
  });
  window.addEventListener('resize',fit);plateImage.addEventListener('load',()=>{paintScene(sceneProgress);paintResult();paintPattern();});
  el('stone').innerHTML=imageView(TOOLS,[1774,887],[193,363,582,308],false);el('brush').innerHTML=imageView(TOOLS,[1774,887],[1322,15,71,854],false);fit();skinAll();document.body.dataset.mode='prepare';
  const catalog=window.TALISMAN_UI_CATALOG;
  if(catalog&&Array.isArray(catalog.materials)){materials=catalog.materials;materialMap=new Map(materials.map(item=>[item.name,item]));render();}else{console.error('Missing bundled material catalog');notify('材料目录未能载入。');}
})();
