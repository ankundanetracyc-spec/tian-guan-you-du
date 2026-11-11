/* Isolated UI fixture. No game calls, combat execution, storage, or resource mutation. */
(() => {
  'use strict';
  const M = window.Materials, E = M.escape, btn = M.button;
  const cards = window.ROLE_BATTLE_DATA.cards, byKey = new Map(cards.map(card => [card.key, card]));
  const skin = window.BATTLE_UI_SKIN_MANIFEST, art = '../../../textures/ui/combat_board_20261107_v1/';
  const initialCells = Array.from({length: 9}, (_, i) => [i % 3, Math.floor(i / 3)]);
  const copy = value => JSON.parse(JSON.stringify(value));
  const placement = (key, x, y, turns = 0) => ({id: crypto.randomUUID(), key, x, y, turns});
  const layout = placements => ({cells: copy(initialCells), placements, hand: null});
  const identity = p => [p.key, p.x, p.y, p.turns || 0];
  const signature = board => JSON.stringify([board.cells, board.placements.map(identity), board.hand]);
  let acquired=copy(window.ROLE_BATTLE_DATA.acquired||{});
  const ownedCount=card=>Math.max(0,Number(acquired.item_counts?.[card.key]||0));
  const isAcquired=card=>!!card&&(card.kind==='skill'?(acquired.skill_keys||[]).includes(card.key):card.kind==='rewrite'?(acquired.rewrite_keys||[]).includes(card.key):card.kind==='indicator'?(acquired.indicator_keys||[]).includes(card.key):ownedCount(card)>0);
  let ctx, root, serial = 3, state, drag = null, combat = null, editorView = null, editingGate = () => true;

  function reset() {
    acquired=copy(window.ROLE_BATTLE_DATA.acquired||{});
    const sword = layout([placement('养锋',0,0),placement('舒卷天章·云舒',1,0,2),placement('架招',2,0),placement('斩剑',0,1),placement('尽锋',0,2)]);
    const guard = layout([placement('架招',0,0),placement('应敌',1,0),placement('应敌',2,0),placement('催血',0,1),placement('痛击',0,2)]);
    const medicine = layout([placement('huichun_1',0,0),placement('huichun_1',1,0),placement('huichun_1',2,0),placement('架招',0,2)]);
    state = {draft: copy(sword), plans:[{id:'sword',name:'养锋连剑',board:sword},{id:'guard',name:'临敌守御',board:guard},{id:'pill',name:'服丹备用',board:medicine}],
      active:'sword', name:'养锋连剑', selected:'', picked:'', turns:0, tab:'skill', query:'', source:'全部来源', plansPage:false, details:true, notice:'', scroll:0};
    serial = 3; combat = null;
  }
  reset();
  function footprint(p) {
    const shape = byKey.get(p.key)?.shape || [0,0];
    return p.turns % 2 && ['skill','pill'].includes(byKey.get(p.key)?.kind) ? [shape[1], shape[0]] : shape;
  }
  function cellsOf(p) {
    const [w,h] = footprint(p);
    return Array.from({length:w*h}, (_,i) => [p.x + i % w, p.y + Math.floor(i/w)]);
  }
  const at = (board,x,y) => board.placements.find(p => cellsOf(p).some(c => c[0] === x && c[1] === y));
  function fits(board,p,except='') {
    const available = new Set(board.cells.map(c=>c.join(',')));
    return cellsOf(p).length > 0 && cellsOf(p).every(([x,y]) => available.has(`${x},${y}`) && !board.placements.some(other=>other.id !== except && cellsOf(other).some(c=>c[0]===x && c[1]===y)));
  }
  function issues(board) {
    const errors=[], seen = new Set(), count = new Map();
    for(const p of board.placements) {
      const card = byKey.get(p.key);
      if(!card || !card.available || !isAcquired(card)) { errors.push(`${card?.name || p.key}来源不可用`); continue; }
      if(card.kind==='indicator') errors.push('当前示例主辅修未提供周天指示');
      if(seen.has(p.id)) errors.push('摆放编号重复');
      seen.add(p.id);
      if(!fits(board,p,p.id)) errors.push(`${card.name}重叠或超出可用格`);
      const n=(count.get(p.key)||0)+1; count.set(p.key,n);
      if(card.kind==='pill') { if(n>ownedCount(card)) errors.push(`缺少${card.name}${n-ownedCount(card)}颗`); }
      else if(n>card.max) errors.push(`${card.name}超出可摆份数`);
    }
    return [...new Set(errors)];
  }
  function targetOf(p,board=state.draft) {
    const card=byKey.get(p.key), offsets=[[1,0],[0,1],[-1,0],[0,-1]], [dx,dy]=offsets[((p.turns||0)%4+4)%4];
    const target=at(board,p.x+dx,p.y+dy), targetCard=byKey.get(target?.key);
    const valid=card.kind==='indicator' ? targetCard?.kind==='skill' && !targetCard.tags.includes('应对') : targetCard?.key===card.target;
    return {target,valid:!!valid};
  }
  function part(name,x,y,w,h,turn=0) {
    const p=skin.parts[name], imageSize=p.image==='grid_parts_atlas.png'?[1254,1254]:[1,1];
    return `<g${turn ? ` transform="translate(${x} ${y}) rotate(${turn*90})"` : ''}><svg x="${turn?0:x}" y="${turn?0:y}" width="${w}" height="${h}" viewBox="${p.rect.join(' ')}" preserveAspectRatio="none"><image href="${art+p.image}" width="${imageSize[0]}" height="${imageSize[1]}"></image></svg></g>`;
  }
  function itemFrame(x,y,w,h,id,edge) {
    const [sx,sy,sw,sh]=skin.parts.focus_outline.rect,cut=20;
    const xs=[sx,sx+cut,sx+sw-cut,sx+sw],ys=[sy,sy+cut,sy+sh-cut,sy+sh];
    const dx=[x,x+edge,x+w-edge,x+w],dy=[y,y+edge,y+h-edge,y+h];
    let out=`<g class="rb-item-frame" data-item-frame="${E(id)}" data-material-source="${art}grid_parts_atlas.png">`;
    for(let row=0;row<3;row++)for(let col=0;col<3;col++){
      if(row===1&&col===1)continue;
      out+=`<svg x="${dx[col]}" y="${dy[row]}" width="${dx[col+1]-dx[col]}" height="${dy[row+1]-dy[row]}" viewBox="${xs[col]} ${ys[row]} ${xs[col+1]-xs[col]} ${ys[row+1]-ys[row]}" preserveAspectRatio="none"><image href="${art}grid_parts_atlas.png" width="1254" height="1254"></image></svg>`;
    }
    return out+'</g>';
  }
  function boardArt(board,cell=110,ox=260,oy=38,width=720,height=430,showHand=true) {
    const present=new Set(board.cells.map(c=>c.join(','))), has=(x,y)=>present.has(`${x},${y}`);
    const owner=new Map(); board.placements.forEach(p=>cellsOf(p).forEach(c=>owner.set(c.join(','),p.id)));
    const same=(x,y,xx,yy)=>owner.has(`${x},${y}`)&&owner.get(`${x},${y}`)===owner.get(`${xx},${yy}`);
    let out=`<svg class="rb-board-art" viewBox="0 0 ${width} ${height}" aria-hidden="true" data-material-source="${art}grid_parts_atlas.png">`;
    for(const [x,y] of board.cells) out+=part('cell_face',ox+x*cell,oy+y*cell,cell,cell);
    const edge=(x,y,vertical=false,weak=false)=>`<g opacity="${weak?.34:1}">${part('outer_edge',x,y,cell,weak?1.1:3,vertical?1:0)}</g>`;
    for(const [x,y] of board.cells) {
      const px=ox+x*cell,py=oy+y*cell;
      if(!has(x,y-1))out+=edge(px,py);
      if(!has(x-1,y))out+=edge(px,py,true);
      if(!has(x+1,y))out+=edge(px+cell,py,true);else if(!same(x,y,x+1,y))out+=edge(px+cell,py,true,true);
      if(!has(x,y+1))out+=edge(px,py+cell);else if(!same(x,y,x,y+1))out+=edge(px,py+cell,false,true);
    }
    const vertices=new Set();for(const [x,y]of board.cells)for(const [dx,dy]of[[0,0],[1,0],[0,1],[1,1]])vertices.add(`${x+dx},${y+dy}`);
    for(const v of vertices){const [x,y]=v.split(',').map(Number),q=[has(x,y),has(x-1,y),has(x-1,y-1),has(x,y-1)],n=q.filter(Boolean).length;
      if(n===1||n===3){const name=n===1?'outer_corner':'inner_corner',turn=n===1?q.indexOf(true):[1,2,3,0][q.indexOf(false)],a=skin.parts[name].anchor_fraction;
        out+=`<g transform="translate(${ox+x*cell} ${oy+y*cell}) rotate(${turn*90})">${part(name,-16*a[0],-16*a[1],16,16)}</g>`;}}
    if(showHand)out+=part('hand_connector',ox-cell*.28,oy+cell*.4,cell*.32,cell*.2)+part('hand_socket',ox-cell*1.3,oy,cell,cell)+`<text x="${ox-cell*.8}" y="${oy+cell+28}" text-anchor="middle" class="rb-hand-label">手持</text>`;
    for(const p of board.placements) {
      const card=byKey.get(p.key);if(!card)continue;const [w,h]=footprint(p),x=ox+p.x*cell,y=oy+p.y*cell;
      const rotated=p.turns%2 && ['skill','pill'].includes(card.kind);
      out+=`<g data-placement-art="${E(p.id)}">`;
      out+=rotated?`<image href="${E(card.icon)}" x="${-h*cell/2}" y="${-w*cell/2}" width="${h*cell}" height="${w*cell}" preserveAspectRatio="xMidYMid meet" transform="translate(${x+w*cell/2} ${y+h*cell/2}) rotate(90)"></image>`:`<image href="${E(card.icon)}" x="${x}" y="${y}" width="${w*cell}" height="${h*cell}" preserveAspectRatio="xMidYMid meet"></image>`;
      if(['rewrite','indicator'].includes(card.kind)) {
        const valid=targetOf(p,board).valid,angle=(p.turns||0)*90;
        out+=`<g transform="translate(${x+cell/2} ${y+cell/2}) rotate(${angle})" class="rb-direction ${valid?'valid':'invalid'}"><path d="M${cell*.12} 0 H${cell*.43} M${cell*.33} -6 L${cell*.43} 0 L${cell*.33} 6"></path></g>`;
      }
      out+=itemFrame(x+2,y+2,w*cell-4,h*cell-4,p.id,cell*7/110)+'</g>';
    }
    return out+'</svg>';
  }
  const displayName = card => card.name;
  function face(card,p) {
    let text=card.face;
    if(['skill','rewrite'].includes(card.kind))text=text.split('\n').slice(1).join('\n');
    if(!state.details)text=text.replace(/（[^（）]*）/g,match=>/[=＝]/.test(match)?'':match);
    if(card.kind==='pill')text+='\n一块一颗；仅预留，实际服用时消耗。';
    if(p&&['rewrite','indicator'].includes(card.kind)) { const t=targetOf(p); text+='\n\n'+(t.valid?`已强化：${byKey.get(t.target.key).name}`:'未生效：请指向共边相邻的适用招式。'); }
    return text;
  }
  function boardView(board=state.draft,interactive=true) {
    const c=110,ox=260,oy=38;
    const style=(x,y,w,h)=>`left:${(ox+x*c)/720*100}%;top:${(oy+y*c)/430*100}%;width:${w*c/720*100}%;height:${h*c/430*100}%`;
    const emptyTip=ctx.tip('空格','扫到真正空格时回复体力1。');
    return `<div class="rb-board-plane ${interactive?'rb-editable':''}" ${interactive?'data-rb-board':''}>${boardArt(board)}${interactive?board.cells.map(([x,y])=>`<button class="rb-cell" style="${style(x,y,1,1)}" data-rb-cell="${x},${y}" aria-label="第${y+1}行第${x+1}格" ${emptyTip}></button>`).join('')+board.placements.map(p=>{const card=byKey.get(p.key),[w,h]=footprint(p);return `<button class="rb-placement ${state.selected===p.id?'is-active':''} ${['rewrite','indicator'].includes(card.kind)&&!targetOf(p,board).valid?'has-invalid-target':''}" style="${style(p.x,p.y,w,h)}" data-rb-placement="${p.id}" draggable="true" aria-label="${E(displayName(card))}，第${p.y+1}行第${p.x+1}格" aria-pressed="${state.selected===p.id}" ${ctx.tip(displayName(card),face(card,p))}></button>`;}).join('')+`<button class="rb-hand-hit" style="left:${(ox-c*1.3)/720*100}%;top:${oy/430*100}%;width:${c/720*100}%;height:${c/430*100}%" data-rb="hand" aria-label="手持空位" ${ctx.tip('手持','唯一外围手持位。\n当前未选手持器物；不占主盘格。')}></button>`:''}<div class="rb-drop-ghost" hidden></div></div>`;
  }
  function mini(board) {return `<span class="rb-mini">${boardArt(board,24,6,6,84,84,false)}</span>`;}
  function dirty(){return !state.plans.find(p=>p.id===state.active)||signature(state.draft)!==signature(state.plans.find(p=>p.id===state.active).board);}
  function summary(){const used=new Set(state.draft.placements.flatMap(cellsOf).map(c=>c.join(','))).size;return `${used} / ${state.draft.cells.length}格 · 留空${state.draft.cells.length-used}格`;}
  function sourceShape(card) {
    const b={cells:Array.from({length:card.shape[0]*card.shape[1]},(_,i)=>[i%card.shape[0],Math.floor(i/card.shape[0])]),placements:[{key:card.key,x:0,y:0,turns:0,id:'source'}]};
    return `<span class="rb-shape" style="width:${card.shape[0]*40}px;height:${card.shape[1]*40}px">${boardArt(b,40,0,0,card.shape[0]*40,card.shape[1]*40,false)}</span>`;
  }
  function sources(){
    if(['artifact','talisman'].includes(state.tab))return `<div class="rb-empty-source">${M.sprite('node')}<h3>暂无可用${state.tab==='talisman'?'符箓':'法宝'}</h3></div>`;
    const pool=cards.filter(card=>isAcquired(card)&&(state.tab==='rewrite'?['rewrite','indicator'].includes(card.kind):card.kind===state.tab)&&
      (state.source==='全部来源'||card.source===state.source)&&`${card.name} ${card.source} ${card.face}`.toLowerCase().includes(state.query.toLowerCase()));
    return pool.length?pool.map(card=>{
      const n=state.draft.placements.filter(p=>p.key===card.key).length,available=card.available&&card.kind!=='indicator';
      const reason=!card.available?'当前版本暂未启用。':card.kind==='indicator'?'当前示例主辅修未提供周天指示。':n>=(card.kind==='pill'?ownedCount(card):card.max)?'已达可摆份数或可用库存。':'';
      return `<button class="rb-source ${state.picked===card.key?'is-picked':''}" data-rb-source="${E(card.key)}" data-rb-available="${!reason}" draggable="${!reason}" aria-disabled="${!!reason}" aria-pressed="${state.picked===card.key}" style="--shape-height:${Math.max(72,card.shape[1]*40+18)}px" ${ctx.tip(displayName(card),face(card)+(reason?'\n\n'+reason:''))}>${sourceShape(card)}<span class="rb-source-name">${E(displayName(card))}</span></button>`;
    }).join(''):`<p class="rb-list-empty">没有符合检索的来源</p>`;
  }
  function plansList(inCombat=false){return state.plans.map(plan=>{const problems=issues(plan.board),selected=(inCombat?combat.plan:state.active)===plan.id;return btn(`${mini(plan.board)}<span><strong>${E(plan.name)}</strong><small class="${problems.length?'rb-missing':''}">${E(problems[0]||'可以载入')}</small></span>`, `data-rb-plan="${plan.id}" ${inCombat?'data-rb-combat-plan':''} aria-pressed="${selected}" aria-disabled="${problems.length>0}" ${ctx.tip(plan.name,problems.length?problems.join('\n'):'载入此方案；完整替换当前草稿。')}`,`rb-plan-row ${selected?'is-selected':''}`);}).join('');}
  function sidebar(){
    return M.paper(state.plansPage?`<div class="rb-source-heading">${btn('返回来源','data-rb="sources"','small-button')}<h2>我的方案</h2><span>${state.plans.length}套</span></div>${M.scroll(plansList(),'rb-source-scroll')}<div class="rb-plan-actions">${btn('新建方案','data-rb="new"','primary')}</div>${btn('删除当前方案','data-rb="delete"','rb-delete')}`:
      `<div class="rb-tabs">${[['skill','招式'],['rewrite','化法'],['pill','丹药'],['talisman','符箓'],['artifact','法宝']].map(([key,label])=>btn(label,`data-rb-tab="${key}" aria-pressed="${state.tab===key}"`,'small-button '+(state.tab===key?'is-selected':''))).join('')}</div><label class="search-well rb-search" data-skin="button">${M.sprite('search')}<input type="search" aria-label="搜索招式、效果或来源" placeholder="搜索名称、效果或来源" value="${E(state.query)}" data-rb-search>${M.iconButton('close','清除招式检索','data-rb="clear-search"')}</label><div class="rb-filter">${btn(`${E(state.source)} ▾`,'data-rb="source-filter"','small-button')}${btn('重置','data-rb="reset-filter"','small-button')}</div>${M.scroll(sources(),'rb-source-scroll')}<div class="rb-sidebar-footer">${btn('清空盘面','data-rb="clear"')}${btn('方案','data-rb="plans"','primary')}</div>`,'rb-source-paper');
  }
  function render(context){ctx=context;return `<div class="role-battle-root"><div class="rb-toolbar">${M.paper(`<h2 class="rb-toolbar-title">招式盘</h2><span class="rb-context">局外预装配</span>${btn('局内载入预览','data-rb="combat-preview"','small-button')}${M.iconButton('help','配招说明','data-rb="help"')}`,'rb-toolbar-paper')}</div><div class="rb-workspace">${M.paper(`<div class="rb-board-heading"><div><h2>${E(state.name)}</h2><span class="rb-dirty">${dirty()?'已调整':'已保存'}</span></div>${btn('改名','data-rb="rename"','small-button')}<span class="rb-summary">${summary()}</span></div><div class="rb-stone-field">${boardView()}<p class="rb-order">从左到右 · 逐行出招</p></div><div class="rb-selection"><span>${selectionText()}</span>${btn('旋转 R','data-rb="rotate"','small-button')}${btn('移出','data-rb="remove"','small-button')}</div><p class="rb-notice" role="status" aria-live="polite">${E(state.notice)}</p><div class="rb-board-footer"><span>拖入或点选摆放 · 右键移出</span>${btn('另存方案','data-rb="save-as"')}${btn('保存方案','data-rb="save"','primary')}</div>`,'rb-main-paper')}${sidebar()}</div></div>`;}
  function selectionText(){const p=state.draft.placements.find(p=>p.id===state.selected),card=byKey.get(p?.key||state.picked);return card?displayName(card)+(p&&['rewrite','indicator'].includes(card.kind)?' · '+(targetOf(p).valid?'指向生效':'未指向适用招式'):''):'选择盘中招式或右侧来源';}

  function refresh(focus='') {
    if(!root?.isConnected)return;
    ctx.hideTip();
    const scroll=root.querySelector('.rb-source-scroll .scroll-view'); if(scroll)state.scroll=scroll.scrollTop;
    const template=document.createElement('template');template.innerHTML=editorView?editorView(ctx):render(ctx);
    const next=template.content.firstElementChild;root.replaceWith(next);root=next;
    M.apply(root);M.attachScroll(root);
    const nextScroll=root.querySelector('.rb-source-scroll .scroll-view');if(nextScroll)nextScroll.scrollTop=state.scroll;
    if(focus)root.querySelector(focus)?.focus({preventScroll:true});
  }
  function notice(message){state.notice=message;refresh();}
  function chooseSource(key){
    const card=byKey.get(key);if(!card)return;
    if(!isAcquired(card))return notice('当前尚未学会或没有持有此项。');
    if(!card.available)return notice('寄念当前暂未启用，保留查看。');
    if(card.kind==='indicator')return notice('当前示例主辅修未提供周天指示。');
    const n=state.draft.placements.filter(p=>p.key===key).length;
    if(n>=(card.kind==='pill'?ownedCount(card):card.max))return notice(card.kind==='pill'?'行囊中的同种丹药已全部预留。':`${card.name}已达可摆份数。`);
    state.picked=key;state.selected='';state.turns=0;state.notice=`已选${card.name}，点击盘面摆放；R转向。`;refresh();
  }
  function place(key,x,y,turns=0,moving=''){
    const card=byKey.get(key);if(!card?.available||!isAcquired(card))return false;
    const original=state.draft.placements.find(p=>p.id===moving);
    if(!original&&(card.kind==='indicator'||state.draft.placements.filter(p=>p.key===key).length>=(card.kind==='pill'?ownedCount(card):card.max))){notice('已达可摆份数或可用库存。');return false;}
    const candidate={id:original?.id||crypto.randomUUID(),key,x,y,turns:card.rotate?((turns%4)+4)%4:0};
    if(!fits(state.draft,candidate,moving)){notice('此处放不下，请调整位置或朝向。');return false;}
    if(original)state.draft.placements=state.draft.placements.map(p=>p.id===moving?candidate:p);else state.draft.placements.push(candidate);
    state.selected=candidate.id;state.picked='';state.notice=['rewrite','indicator'].includes(card.kind)&&!targetOf(candidate).valid?'未生效：请将箭头指向紧邻的适用招式。':'';
    refresh(`[data-rb-placement="${candidate.id}"]`);return true;
  }
  function rotate(){
    if(state.picked){const card=byKey.get(state.picked);if(!card.rotate)return notice('此招式不需要转向。');state.turns=(state.turns+1)%4;return notice(`待摆${card.name}已转向。`);}
    const p=state.draft.placements.find(p=>p.id===state.selected);if(!p)return notice('先选择要转向的招式或化法。');
    if(!byKey.get(p.key).rotate)return notice('此招式不需要转向。');
    place(p.key,p.x,p.y,p.turns+1,p.id);
  }
  function remove(){const p=state.draft.placements.find(p=>p.id===state.selected);if(!p)return notice('先选择盘中要移出的对象。');
    if(byKey.get(p.key).kind==='indicator')return notice('周天指示可以移动和转向，不能移出。');
    state.draft.placements=state.draft.placements.filter(p=>p.id!==state.selected);state.selected='';notice('已移出盘面。');}
  function clear(){state.draft.placements=state.draft.placements.filter(p=>byKey.get(p.key)?.kind==='indicator');state.selected=state.picked='';notice('已清空盘面，已保存的方案保持。');}
  function loadPlan(id,inCombat=false,keepLibrary=false){
    const plan=state.plans.find(p=>p.id===id);if(!plan)return false;
    const problems=issues(plan.board), available=new Set((inCombat?combat.board:state.draft).cells.map(c=>c.join(',')));
    if(!plan.board.cells.every(c=>available.has(c.join(','))))problems.push('当前盘面尚未解锁所需格');
    if(problems.length){if(inCombat){combat.notice=problems.join('；')+'。当前盘面保持。';paintCombat();}else notice(problems.join('；')+'。当前盘面保持。');return false;}
    const candidate=copy(plan.board);candidate.cells=copy(inCombat?combat.board.cells:state.draft.cells);
    if(inCombat){combat.board=candidate;combat.plan=plan.id;combat.notice=`已载入「${plan.name}」；当前资源、次数与冷却保持。`;paintCombat();return true;}
    state.draft=candidate;state.active=plan.id;state.name=plan.name;state.selected=state.picked='';if(!keepLibrary)state.plansPage=false;state.scroll=0;state.notice=`已载入「${plan.name}」。`;refresh();return true;
  }
  function saveNamedPlan(name,overwriteId=''){
    name=String(name).trim();if(!name){notice('请填写方案名称。');return false;}
    const problems=issues(state.draft);if(problems.length){notice(problems.join('；'));return false;}
    const existing=state.plans.find(p=>p.name===name);
    if(existing&&existing.id!==overwriteId){notice('已有同名方案，请确认覆盖。');return false;}
    const plan=existing||{id:'custom-'+ ++serial,name,board:null};plan.board=copy(state.draft);
    if(!existing)state.plans.push(plan);state.active=plan.id;state.name=plan.name;
    state.notice=existing?'方案已覆盖。':'方案已保存。';refresh();return true;
  }
  function renameNamedPlan(id,name){
    name=String(name).trim();const plan=state.plans.find(p=>p.id===id);if(!plan)return false;
    if(!name){notice('请填写方案名称。');return false;}
    if(state.plans.some(p=>p.id!==id&&p.name===name)){notice('已有同名方案，请换一个名称。');return false;}
    plan.name=name;if(state.active===id)state.name=name;state.notice='方案名称已更新。';refresh();return true;
  }
  function nameDialog(mode){
    const label=mode==='rename'?'修改方案名称':mode==='new'?'新建方案':'另存方案';
    ctx.modal(label,`<label class="field-group"><span>方案名称</span><span class="field-well" data-skin="button"><input id="rb-plan-name" aria-label="方案名称" autocomplete="off" value="${E(mode==='rename'?state.name:'')}" placeholder="为这套搭配取个名字"></span></label><p class="rb-form-error" role="alert"></p><p class="muted">${mode==='new'?'新建空白招式盘，已有方案保留。':mode==='rename'?'只修改名称，盘面保持。':'保存当前盘面的位置、朝向与来源需求。'}</p>`,btn('返回','data-close-modal')+btn(label,`data-rb-name-submit="${mode}"`,'primary'));
    const input=document.querySelector('#rb-plan-name');input.focus();if(mode==='rename')input.select();
  }
  function submitName(mode){
    const name=document.querySelector('#rb-plan-name').value.trim(),error=document.querySelector('.rb-form-error');
    if(!name){error.textContent='请填写方案名称。';return;}
    if(state.plans.some(p=>p.name===name&&(mode!=='rename'||p.id!==state.active))){error.textContent='已有同名方案，请换一个名称或返回后覆盖保存。';return;}
    if(mode==='rename'){
      const plan=state.plans.find(p=>p.id===state.active);if(plan)plan.name=name;state.name=name;
    }else{
      const board=mode==='new'?layout(state.draft.placements.filter(p=>byKey.get(p.key)?.kind==='indicator').map(copy)):copy(state.draft);
      const id='custom-'+ ++serial;state.plans.push({id,name,board:copy(board)});state.draft=board;state.active=id;state.name=name;state.selected=state.picked='';
    }
    document.querySelector('dialog[open]')?.close();state.notice='方案已在本次样稿中保存。';refresh();
  }
  function save(){
    const plan=state.plans.find(p=>p.id===state.active);if(!plan)return nameDialog('save-as');
    const problems=issues(state.draft);if(problems.length)return notice(problems.join('；'));
    plan.board=copy(state.draft);notice(`已保存「${plan.name}」。`);
  }
  function deleteDialog(){
    const plan=state.plans.find(p=>p.id===state.active);if(!plan)return notice('当前草稿还没有保存为方案。');
    ctx.modal('删除方案',`<p class="confirmation-copy">删除「${E(plan.name)}」？</p><p class="muted">当前盘面保留为未保存草稿。</p>`,btn('返回','data-close-modal')+btn('删除方案','data-rb="confirm-delete"','primary'));
  }
  function help(){ctx.modal('配招说明',`<div class="rb-rules"><p>拖入来源，或先点选来源，再点盘面摆放。多格招式与丹药可按R转向；选中后按Delete或右键移出。</p>${M.divider('出招与应对')}<p>从左到右逐行读取。每份招式首次检查后变暗，本轮失败不重试；应对扫到后待命。每共同回合末可以重新配招。</p><p>轮初体力+3，基础上限6；扫到真正空格回体1。</p>${M.divider('化法与方案')}<p>化法只强化箭头指向、共边相邻的一份适用招式。无效指向可以保留，但不生效。</p><p>*N是可摆份数；限N才是整场共享次数。切换方案不恢复资源、次数、冷却或库存。</p><p>丹药一块一颗，摆放只预留，实际服用后留下空瓶占格。</p>${M.divider('查看样稿')}<p>当前展示的是示例来源与库存；方案只保留在本次查看中，刷新或重置恢复初始示例。</p><label class="check-control"><input type="checkbox" data-rb-details ${state.details?'checked':''}><span class="check-off">${M.sprite('checkOff')}</span><span class="check-on">${M.sprite('checkOn')}</span><span>显示详细公式</span></label></div>`);}
  function combatContent(){return `<div class="rb-combat-preview"><div class="rb-combat-field">${boardView(combat.board,false)}<div class="rb-combat-resources"><span>体力 <b>— / —</b></span><span>法力 <b>— / —</b></span><span>灵识占用 <b>— / —</b></span></div><p>载入只更换配招，资源与次数保持。</p></div><div class="rb-combat-plans"><h3>我的方案</h3>${M.scroll(plansList(true),'rb-combat-scroll')}</div><p class="rb-combat-notice" role="status">${E(combat.notice)}</p></div>`;}
  function showCombat(){combat={board:copy(state.draft),plan:state.active,notice:'点击方案即可载入；此处只预览局内配招。'};
    ctx.modal('局内载入预览',combatContent(),btn('<span>返回</span><span class="return-triangle" aria-hidden="true"></span>','data-close-modal aria-label="返回"'));
    document.querySelector('dialog[open]').classList.add('rb-combat-dialog');}
  function paintCombat(){const host=document.querySelector('.rb-combat-preview');if(!host)return;host.outerHTML=combatContent();const dialog=document.querySelector('dialog[open]');M.apply(dialog);M.attachScroll(dialog);ctx.hideTip();}
  function filterSources(){const kinds=state.tab==='rewrite'?['rewrite','indicator']:[state.tab];
    const options=['全部来源',...new Set(cards.filter(c=>kinds.includes(c.kind)).map(c=>c.source))];
    ctx.modal('筛选来源',`<div class="choice-list">${options.map(name=>btn(E(name),`data-rb-source-filter="${E(name)}"`,'choice-option')).join('')}</div>`);}
  const actions={rotate,remove,clear,save,help,hand:()=>ctx.modal('外围手持',`<p class="confirmation-copy">主盘外只保留一个手持位。</p><p>手持不占主盘格；具体器物的占格、启用与能力沿当前器物定义。本次样稿未接入实际器物装配。</p>`),
    plans:()=>{state.plansPage=true;state.scroll=0;state.notice='';refresh();},sources:()=>{state.plansPage=false;state.scroll=0;refresh();},
    new:()=>nameDialog('new'),rename:()=>nameDialog('rename'),'save-as':()=>nameDialog('save-as'),delete:deleteDialog,
    'confirm-delete':()=>{state.plans=state.plans.filter(p=>p.id!==state.active);state.active='';state.name='未命名方案';document.querySelector('dialog[open]')?.close();notice('已删除方案，当前草稿保留。');},
    'combat-preview':showCombat,'source-filter':filterSources,
    'reset-filter':()=>{state.query='';state.source='全部来源';state.scroll=0;refresh();},
    'clear-search':()=>{state.query='';refresh();root.querySelector('[data-rb-search]')?.focus();}};
  document.addEventListener('click',event=>{
    const target=event.target.closest('button');if(!target)return;
    if(target.dataset.rbNameSubmit)return submitName(target.dataset.rbNameSubmit);
    if(target.dataset.rbSourceFilter){state.source=target.dataset.rbSourceFilter;state.scroll=0;document.querySelector('dialog[open]')?.close();refresh();return;}
    if(target.dataset.rbPlan)return loadPlan(target.dataset.rbPlan,target.hasAttribute('data-rb-combat-plan'));
    if(target.dataset.rbSource)return chooseSource(target.dataset.rbSource);
    if(target.dataset.rbTab){state.tab=target.dataset.rbTab;state.source='全部来源';state.scroll=0;state.plansPage=false;refresh();return;}
    if(target.dataset.rbCell){const [x,y]=target.dataset.rbCell.split(',').map(Number);if(state.picked)place(state.picked,x,y,state.turns);else{state.selected='';refresh();}return;}
    if(target.dataset.rbPlacement){const p=state.draft.placements.find(p=>p.id===target.dataset.rbPlacement);if(state.picked)return place(state.picked,p.x,p.y,state.turns);state.selected=p.id;state.notice='';refresh(`[data-rb-placement="${p.id}"]`);return;}
    if(target.dataset.rb)actions[target.dataset.rb]?.();
  });
  document.addEventListener('input',event=>{if(!event.target.hasAttribute('data-rb-search'))return;state.query=event.target.value;state.scroll=0;const host=root.querySelector('.rb-source-scroll .scroll-content');host.innerHTML=sources();M.apply(host);});
  document.addEventListener('change',event=>{if(event.target.hasAttribute('data-rb-details')){state.details=event.target.checked;refresh();}});
  document.addEventListener('contextmenu',event=>{const p=event.target.closest('[data-rb-placement]');if(p){event.preventDefault();state.selected=p.dataset.rbPlacement;remove();}});
  document.addEventListener('keydown',event=>{
    if(event.target.id==='rb-plan-name'&&event.key==='Enter'){event.preventDefault();return submitName(document.querySelector('[data-rb-name-submit]').dataset.rbNameSubmit);}
    if(!editingGate()||!root?.isConnected||document.querySelector('dialog[open]')||event.target.matches('input,textarea,select')||event.altKey||event.metaKey)return;
    if(event.ctrlKey){if(event.key.toLowerCase()==='s'){event.preventDefault();save();}return;}
    if(event.key.toLowerCase()==='r'){event.preventDefault();rotate();}else if(event.key==='Delete'){event.preventDefault();remove();}else if(event.key==='Escape'){state.picked='';state.selected='';state.notice='';refresh();}
    else if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key)&&state.selected){event.preventDefault();const p=state.draft.placements.find(p=>p.id===state.selected),d={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]}[event.key];place(p.key,p.x+d[0],p.y+d[1],p.turns,p.id);}
  });
  document.addEventListener('dragstart',event=>{
    const source=event.target.closest('[data-rb-source]'),placed=event.target.closest('[data-rb-placement]');if(!source&&!placed)return;
    if(source?.dataset.rbAvailable==='false'){event.preventDefault();return;}
    ctx.hideTip();drag=source?{key:source.dataset.rbSource,turns:state.picked===source.dataset.rbSource?state.turns:0,offset:[0,0]}:(()=>{const p=state.draft.placements.find(p=>p.id===placed.dataset.rbPlacement),r=placed.getBoundingClientRect(),[w,h]=footprint(p);return {key:p.key,turns:p.turns,id:p.id,offset:[Math.min(w-1,Math.floor((event.clientX-r.left)/r.width*w)),Math.min(h-1,Math.floor((event.clientY-r.top)/r.height*h))]};})();
    event.dataTransfer.setData('application/x-role-board',JSON.stringify(drag));event.dataTransfer.effectAllowed=source?'copy':'move';
  });
  function dropPosition(event,plane){const r=plane.getBoundingClientRect();return [Math.floor(((event.clientX-r.left)/r.width*720-260)/110)-drag.offset[0],Math.floor(((event.clientY-r.top)/r.height*430-38)/110)-drag.offset[1]];}
  document.addEventListener('dragover',event=>{const plane=event.target.closest('[data-rb-board]');if(!plane||!drag)return;event.preventDefault();const [x,y]=dropPosition(event,plane),p={key:drag.key,x,y,turns:drag.turns},[w,h]=footprint(p),ghost=plane.querySelector('.rb-drop-ghost');ghost.hidden=false;ghost.classList.toggle('is-invalid',!fits(state.draft,p,drag.id));ghost.style.cssText=`left:${(260+x*110)/720*100}%;top:${(38+y*110)/430*100}%;width:${w*110/720*100}%;height:${h*110/430*100}%`;});
  document.addEventListener('drop',event=>{const plane=event.target.closest('[data-rb-board]');if(!plane||!drag)return;event.preventDefault();const [x,y]=dropPosition(event,plane),payload=drag;drag=null;place(payload.key,x,y,payload.turns,payload.id);});
  document.addEventListener('dragend',()=>{drag=null;root?.querySelectorAll('.rb-drop-ghost').forEach(g=>g.hidden=true);});
  document.addEventListener('dragleave',event=>{const plane=event.target.closest('[data-rb-board]');if(plane&&!plane.contains(event.relatedTarget))plane.querySelector('.rb-drop-ghost').hidden=true;});
  window.RoleBattle={render, reset,
    suspend:()=>{if(root?.isConnected){const view=root.querySelector('.rb-source-scroll .scroll-view');if(view)state.scroll=view.scrollTop;}},
    mount:host=>{root=host.querySelector('.role-battle-root');const view=root?.querySelector('.rb-source-scroll .scroll-view');if(view)view.scrollTop=state.scroll;},
    inspect:()=>copy({draft:state.draft,plans:state.plans,active:state.active,name:state.name,selected:state.selected,picked:state.picked,turns:state.turns,plansPage:state.plansPage,dirty:dirty(),notice:state.notice,combat}),
    configure:context=>{ctx=context;},
    useEditorView:(view,gate)=>{editorView=view;editingGate=gate;},
    redraw:()=>refresh(),
    notify:notice,
    validate:board=>issues(board),
    adoptDraft:(board,selected='',message='')=>{const errors=issues(board);if(errors.length){notice(errors.join('；'));return false;}state.draft=copy(board);state.selected=selected;state.picked='';state.notice=message;refresh();return true;},
    openLibrary:page=>{state.plansPage=page==='plans';state.scroll=0;refresh();},
    clearPicked:()=>{state.picked='';state.turns=0;},
    selectPlacement:id=>{state.selected=id;state.picked='';state.turns=0;},
    loadNamedPlan:id=>loadPlan(id,false,true),
    saveNamedPlan,renameNamedPlan,
    sourceSnapshot:()=>copy(acquired),
    setSourceSnapshot:value=>{acquired=copy(value||{});},
    ownedCards:()=>copy(cards.filter(isAcquired).map(card=>({...card,owned_count:ownedCount(card)}))),
    card:key=>copy(byKey.get(key)),
    geometry:{footprint,cellsOf,targetOf:(p,board)=>copy(targetOf(p,board))},
    drawing:{boardArt,boardView,mini},
    signature};
})();

/* Battle-only flow. The character shell belongs to a separate workstream. */
(() => {
  'use strict';
  const api=window.RoleBattle,M=window.Materials,E=M.escape,btn=M.button,clone=x=>JSON.parse(JSON.stringify(x));
  const base={render:api.render,mount:api.mount,reset:api.reset,inspect:api.inspect,setSources:api.setSourceSnapshot};
  let context,overviewRoot,overlay,flow;
  const item=(key,x,y,turns=0)=>({id:crypto.randomUUID(),key,x,y,turns});
  function normalize(items){
    const x=Math.min(...items.map(p=>p.x)),y=Math.min(...items.map(p=>p.y));
    const placements=items.map(p=>({...clone(p),x:p.x-x,y:p.y-y}));
    const cells=[...new Map(placements.flatMap(api.geometry.cellsOf).map(c=>[c.join(','),c])).values()];
    return {placements,cells,hand:null};
  }
  function resetFlow(){
    const current=base.inspect();
    flow={open:false,applied:clone(current.draft),appliedName:current.name,appliedPlanId:current.active,planDialog:null,library:'sources',ownedTab:'skill',ownedQuery:'',selected:[],picked:'',turns:0,pointer:null,suppressClickUntil:0,backgrounds:[],
      combos:[{id:'sword-pair',name:'养锋·云舒',board:normalize([item('养锋',0,0),item('舒卷天章·云舒',1,0,2)])},
        {id:'blood-pair',name:'催血·养络',board:normalize([item('催血',0,0),item('百骸衍枝化生宝册·养络',2,0,2)])}]};
  }
  resetFlow();
  const snapshot=()=>base.inspect();
  function thumb(board,className=''){
    const w=Math.max(1,...board.cells.map(c=>c[0]+1)),h=Math.max(1,...board.cells.map(c=>c[1]+1));
    return `<span class="rf-thumb ${className}">${api.drawing.boardArt(board,40,4,4,w*40+8,h*40+8,false)}</span>`;
  }
  function overview(){
    const pool=api.ownedCards(),tabs=[['skill','招式'],['rewrite','化法'],['pill','丹药'],['talisman','符箓'],['artifact','法宝']];
    return `<div class="role-battle-root rf-overview rf-owned-catalog">${M.paper(`<div class="rf-owned-heading"><h2>已学与持有</h2>${btn('配招','data-rf="open"','primary')}</div><div class="rf-owned-tools"><div class="rf-owned-tabs">${tabs.map(([key,label])=>btn(`${label}<small>${pool.filter(c=>c.kind===key).length}</small>`,`data-rf-owned-tab="${key}" aria-pressed="${flow.ownedTab===key}"`,'primary '+(flow.ownedTab===key?'is-selected':''))).join('')}</div><label class="search-well rf-owned-search" data-skin="button">${M.sprite('search')}<input type="search" data-rf-owned-search aria-label="检索已学与持有" placeholder="检索名称、来源或关键词" value="${E(flow.ownedQuery)}">${M.iconButton('close','清除检索','data-rf-clear-owned')}</label></div>${M.scroll(ownedEntries(),'rf-owned-scroll')}`, 'rf-owned-paper')}</div>`;
  }
  function ownedEntries(){
    const query=flow.ownedQuery.trim().toLowerCase(),pool=api.ownedCards().filter(card=>card.kind===flow.ownedTab&&`${card.name} ${card.source} ${(card.tags||[]).join(' ')}`.toLowerCase().includes(query));
    if(!pool.length)return `<p class="rf-owned-empty">${query?'没有符合检索的内容':['skill','rewrite'].includes(flow.ownedTab)?'暂无已学会的'+(flow.ownedTab==='skill'?'招式':'化法'):'暂无持有的'+({pill:'丹药',talisman:'符箓',artifact:'法宝'}[flow.ownedTab]||'物品')}</p>`;
    return `<div class="rf-owned-grid">${pool.map(card=>{
      const sample=normalize([item(card.key,0,0)]),[w,h]=card.shape,learned=['skill','rewrite'].includes(card.kind);
      const detail=card.kind==='skill'?`消耗：体力${card.energy}${card.mana?'、法力'+card.mana:''}`:card.kind==='rewrite'?`适用：${card.target||'对应招式'}`:`持有${card.owned_count}${card.kind==='pill'?'颗':'份'}`;
      return `<article class="rf-owned-entry ${card.available?'':'is-unavailable'}" data-rf-owned-key="${E(card.key)}"><span class="rf-owned-art" style="width:${w*40+6}px;height:${h*40+6}px">${api.drawing.boardArt(sample,40,3,3,w*40+6,h*40+6,false)}</span><div><small>${E(card.source||'')}</small><h3>${E(card.name)}</h3><p>${E(detail)}</p><span class="rf-owned-state">${learned?'已学会':`${w}×${h}格`}${card.available?'':' · 暂不可配招'}</span></div></article>`;
    }).join('')}</div>`;
  }
  function paintOverview(){
    if(!overviewRoot?.isConnected)return;
    const t=document.createElement('template');t.innerHTML=overview();const next=t.content.firstElementChild;overviewRoot.replaceWith(next);overviewRoot=next;M.apply(next);M.attachScroll(next);
    if(!flow.open)base.mount(next.parentElement);
  }
  function selection(){const board=snapshot().draft;return board.placements.filter(p=>flow.selected.includes(p.id));}
  function comboList(){
    return flow.combos.length?flow.combos.map(c=>`<article class="rf-combo-card ${flow.picked===c.id?'is-picked':''}"><button class="rf-combo-pick" data-rf-combo="${c.id}" draggable="true" aria-label="组合：${E(c.name)}" aria-pressed="${flow.picked===c.id}">${thumb(c.board)}<span>${E(c.name)}</span></button><div class="rf-combo-tools">${btn('改名',`data-rf-combo-rename="${c.id}"`,'small-button')}${btn('删除',`data-rf-combo-delete="${c.id}"`,'small-button')}</div></article>`).join(''):'<p class="rb-list-empty">在盘面多选招式后，可保存为组合。</p>';
  }
  function planList(){
    return snapshot().plans.map(plan=>{
      const problems=api.validate(plan.board),applied=flow.appliedPlanId===plan.id&&api.signature(plan.board)===api.signature(flow.applied);
      return `<article class="rf-scheme-row ${applied?'is-applied':''}" data-rf-plan="${plan.id}">${nativeMaterial('secondary_normal')}${thumb(plan.board)}<div class="rf-scheme-copy"><h3>${E(plan.name)}</h3><p class="${problems.length?'rb-missing':''}">${E(problems[0]||(applied?'当前应用':`${plan.board.placements.length}项`))}</p></div><div class="rf-scheme-actions">${btn('应用',`data-rf-plan-apply="${plan.id}" aria-label="应用方案：${E(plan.name)}" ${problems.length?'disabled':''}`,'small-button')}${btn('改名',`data-rf-plan-rename="${plan.id}" aria-label="改名方案：${E(plan.name)}"`,'small-button')}</div></article>`;
    }).join('')||'<p class="rb-list-empty">暂无方案，可在盘面下保存。</p>';
  }
  function nativeMaterial(name){
    const skin=window.BATTLE_UI_SKIN_MANIFEST,part=skin.parts[name],size=skin.images[part.image];
    const [x,y,w,h]=part.rect,[l,t,r,b]=part.source_insets,[dl,dt,dr,db]=part.display_insets;
    const xs=[x,x+l,x+w-r,x+w],ys=[y,y+t,y+h-b,y+h];
    let html=`<span class="rf-native-material" aria-hidden="true" data-native-part="${name}" style="grid-template-columns:${dl}px 1fr ${dr}px;grid-template-rows:${dt}px 1fr ${db}px">`;
    for(let row=0;row<3;row++)for(let col=0;col<3;col++)html+=`<svg style="grid-area:${row+1}/${col+1}" viewBox="${xs[col]} ${ys[row]} ${xs[col+1]-xs[col]} ${ys[row+1]-ys[row]}" preserveAspectRatio="none"><image href="../../../textures/ui/combat_board_20261107_v1/${part.image}" width="${size.width}" height="${size.height}"></image></svg>`;
    return html+'</span>';
  }
  function nativeControls(root){
    root.querySelectorAll('button.ui-button[data-skin="button"],.rb-search[data-skin="button"]').forEach(control=>{
      const primary=control.classList.contains('primary')||control.matches('.rf-library-tabs .is-selected,.rb-tabs .is-selected');
      control.removeAttribute('data-skin');control.classList.add('rf-native-control');
      if(primary)control.classList.add('rf-native-primary');
      control.insertAdjacentHTML('afterbegin',nativeMaterial(primary?'primary_normal':'secondary_normal'));
    });
  }
  function editorMarkup(ctx){
    const caption=overlay?.querySelector('[data-rf-current-applied]');if(caption)caption.innerHTML=`当前应用：${E(flow.appliedName)}${api.signature(snapshot().draft)!==api.signature(flow.applied)?'<small>有未应用的更改</small>':''}`;
    const t=document.createElement('template');t.innerHTML=base.render(ctx);const root=t.content.firstElementChild;
    root.classList.add('rf-editor-layout');root.querySelector('.rb-toolbar')?.remove();
    const current=snapshot();flow.selected=flow.selected.filter(id=>current.draft.placements.some(p=>p.id===id));
    const tab=flow.library==='combos'?'combos':current.plansPage?'plans':'sources';flow.library=tab;
    if(tab==='combos'){
      const panel=M.paper(`<div class="rf-library-heading"><h2>我的组合</h2><span>${flow.combos.length}组</span></div><p class="rf-copy">拖入整组搭配；Shift点选盘中多项后保存。</p>${M.scroll(comboList(),'rb-source-scroll rf-combo-scroll')}${btn(`保存当前选择${flow.selected.length?' · '+flow.selected.length+'项':''}`,'data-rf="save-combo"','primary wide-button')}`, 'rb-source-paper');
      root.querySelector('.rb-source-paper').outerHTML=panel;
    }
    if(tab==='plans'){
      root.querySelector('.rb-source-paper').outerHTML=M.paper(`<div class="rf-library-heading"><h2>我的方案</h2><span>${current.plans.length}套</span></div>${M.scroll(planList(),'rb-source-scroll rf-scheme-scroll')}`,'rb-source-paper');
    }
    const content=root.querySelector('.rb-source-paper>.panel-content');
    content.insertAdjacentHTML('afterbegin',`<div class="rf-library-tabs">${[['sources','来源'],['combos','组合'],['plans','方案']].map(([id,label])=>btn(label,`data-rf-library="${id}" aria-pressed="${tab===id}"`,'small-button '+(tab===id?'is-selected':''))).join('')}</div>`);
    root.querySelector('.rb-sidebar-footer [data-rb="plans"]')?.remove();root.querySelector('.rb-source-heading [data-rb="sources"]')?.remove();
    root.querySelector('.rb-filter')?.remove();
    root.querySelectorAll('[data-rb-source]').forEach(source=>{const card=api.card(source.dataset.rbSource),shape=source.querySelector('.rb-shape');shape.style.width=`${card.shape[0]*52}px`;shape.style.height=`${card.shape[1]*52}px`;source.style.setProperty('--shape-height',`${Math.max(72,card.shape[1]*52+18)}px`);});
    const plane=root.querySelector('[data-rb-board]');
    plane.insertAdjacentHTML('beforeend',`<div class="rf-board-commit"><div class="rf-board-actions">${btn('保存为方案','data-rf="save-plan"')}${btn('应用','data-rf="apply"','primary')}</div><p class="rb-notice" role="status" aria-live="polite">${E(current.notice)}</p></div>`);
    root.querySelector('.rb-main-paper').outerHTML=`<section class="rb-stone-field rf-board-field">${plane.outerHTML}</section>`;
    const panel=root.querySelector('.rb-source-paper');panel.removeAttribute('data-skin');panel.classList.add('rf-native-panel');panel.insertAdjacentHTML('afterbegin',nativeMaterial('source_panel'));
    root.querySelectorAll('[data-rb-placement]').forEach(button=>{const selected=flow.selected.includes(button.dataset.rbPlacement);button.classList.toggle('is-active',selected);button.setAttribute('aria-pressed',selected);if(selected)button.classList.add('rf-group-selected');});
    nativeControls(root);
    return root.outerHTML;
  }
  function fitEditor(){
    const stage=overlay?.querySelector('.rf-editor-stage');if(!stage)return;
    const scale=Math.min(innerWidth/1560,innerHeight/900);
    stage.style.width=`${innerWidth/scale}px`;stage.style.height=`${innerHeight/scale}px`;stage.style.transform=`translate(-50%,-50%) scale(${scale})`;
  }
  function openEditor(library='sources'){
    context.hideTip();flow.open=true;flow.library=library;flow.picked='';flow.selected=[];
    if(!overlay){overlay=document.createElement('div');overlay.id='battle-loadout-editor';overlay.className='rf-fullscreen';overlay.setAttribute('role','dialog');overlay.setAttribute('aria-modal','true');overlay.setAttribute('aria-label','配招');document.body.append(overlay);}
    flow.backgrounds=[...document.body.children].filter(e=>e!==overlay&&!['overlay-root','tooltip','toast'].includes(e.id)).map(element=>({element,inert:element.inert}));
    flow.backgrounds.forEach(({element})=>element.inert=true);
    const pending=api.signature(snapshot().draft)!==api.signature(flow.applied);
    overlay.innerHTML=`<div class="rf-editor-backdrop" aria-hidden="true"></div><div class="rf-editor-stage"><header class="rf-editor-heading"><span class="rf-current-applied" data-rf-current-applied>当前应用：${E(flow.appliedName)}${pending?'<small>有未应用的更改</small>':''}</span>${btn('<span>返回</span><span class="return-triangle" aria-hidden="true"></span>','data-rf="close" aria-label="返回"','rf-editor-return')}</header><main class="rf-editor-body">${editorMarkup(context)}</main></div>`;
    nativeControls(overlay);M.apply(overlay);M.attachScroll(overlay);base.mount(overlay);api.openLibrary(library==='plans'?'plans':'sources');fitEditor();
    overlay.querySelector('[data-rf="close"]').focus({preventScroll:true});
  }
  function closeEditor(){
    if(!flow.open)return;cancelDrag();api.clearPicked();context.hideTip();api.suspend();flow.open=false;flow.picked='';overlay?.remove();overlay=null;restoreBackground();paintOverview();
    overviewRoot?.querySelector('[data-rf="open"]')?.focus({preventScroll:true});
  }
  function restoreBackground(){flow.backgrounds.forEach(({element,inert})=>element.inert=inert);flow.backgrounds=[];}
  function apply(){const data=snapshot(),errors=api.validate(data.draft);if(errors.length)return api.notify(errors.join('；'));
    flow.applied=clone(data.draft);flow.appliedName=data.name;flow.appliedPlanId=data.plans.find(p=>p.id===data.active&&api.signature(p.board)===api.signature(data.draft))?.id||'';closeEditor();context.notify('已应用，开始战斗时使用此布局。');}
  function applyNamedPlan(id){
    const plan=snapshot().plans.find(p=>p.id===id);if(!plan||!api.loadNamedPlan(id))return;
    flow.applied=clone(snapshot().draft);flow.appliedName=plan.name;flow.appliedPlanId=id;flow.selected=[];flow.picked='';api.clearPicked();api.notify('方案已应用。');
  }
  function planNameDialog(id=''){
    const existing=snapshot().plans.find(p=>p.id===id);if(id&&!existing)return;
    if(!id){const errors=api.validate(snapshot().draft);if(errors.length)return api.notify(errors.join('；'));}
    flow.planDialog={id,overwriteId:''};
    context.modal(id?'方案改名':'保存为方案',`<label class="field-group rf-plan-field"><span class="rf-plan-label">方案名称</span><span class="field-well rf-plan-input-well" data-skin="button"><input id="rf-plan-name" aria-label="方案名称" autocomplete="off" value="${E(existing?.name||'')}" placeholder="为这套配招取个名字"></span></label><p class="rf-form-error rf-plan-error" role="alert"></p><p class="muted" data-rf-plan-hint>${id?'只修改这套方案的名称。':'保存当前整盘的位置、朝向与来源。'}</p>`,btn('返回','data-close-modal')+btn(id?'确认改名':'确认保存','data-rf="confirm-plan"','primary'));
    document.querySelector('dialog[open]').classList.add('rf-plan-dialog');
    const field=document.querySelector('#rf-plan-name');field.focus();if(id)field.select();
  }
  function updatePlanNameHint(){
    if(!flow.planDialog||flow.planDialog.id)return;
    const name=document.querySelector('#rf-plan-name').value.trim(),existing=snapshot().plans.find(p=>p.name===name);
    flow.planDialog.overwriteId=existing?.id||'';
    document.querySelector('[data-rf-plan-hint]').textContent=existing?`已有同名方案「${name}」，确认后将覆盖它。`:'保存当前整盘的位置、朝向与来源。';
    document.querySelector('[data-rf="confirm-plan"] .button-content').textContent=existing?'确认覆盖':'确认保存';
  }
  function submitPlanName(){
    if(!flow.planDialog)return;
    const field=document.querySelector('#rf-plan-name'),name=field.value.trim(),error=document.querySelector('.rf-plan-error');
    if(!name){error.textContent='请填写方案名称。';field.focus();return;}
    const {id,overwriteId}=flow.planDialog;
    const saved=id?api.renameNamedPlan(id,name):api.saveNamedPlan(name,overwriteId);
    if(!saved){error.textContent=snapshot().notice;return;}
    if(id&&flow.appliedPlanId===id)flow.appliedName=name;
    flow.planDialog=null;document.querySelector('dialog[open]')?.close();setLibrary('plans');
    const row=overlay?.querySelector(`[data-rf-plan="${id||snapshot().active}"]`);row?.scrollIntoView({block:'nearest'});row?.querySelector('button:not(:disabled)')?.focus({preventScroll:true});
    context.notify(id?'方案名称已更新。':overwriteId?'方案已覆盖。':'方案已保存。');
  }
  function setLibrary(page){flow.library=page;flow.picked='';api.clearPicked();api.openLibrary(page==='plans'?'plans':'sources');}
  function saveComboDialog(id='',rename=false){
    const existing=flow.combos.find(c=>c.id===id),chosen=selection();
    if(!rename&&!chosen.length)return api.notify('先在盘面选择招式；按住Shift可选择多项。');
    const title=rename?'组合改名':'保存组合';
    context.modal(title,`<label class="field-group"><span>组合名称</span><span class="field-well" data-skin="button"><input id="rf-combo-name" aria-label="组合名称" value="${E(existing?.name||'')}" autocomplete="off" placeholder="为这组搭配取个名字"></span></label><p class="rf-form-error" role="alert"></p><p class="muted">${rename?'只修改组合名称。':`保存所选${chosen.length}项的相对位置和朝向；不覆盖整盘。`}</p>`,btn('返回','data-close-modal')+btn(rename?'保存名称':'保存组合',`data-rf-combo-submit="${id||'new'}" data-rf-rename="${rename}"`,'primary'));
    const field=document.querySelector('#rf-combo-name');field.focus();if(rename)field.select();
  }
  function submitCombo(id,rename){
    const name=document.querySelector('#rf-combo-name').value.trim(),error=document.querySelector('.rf-form-error');
    if(!name){error.textContent='请填写组合名称。';return;}
    const same=flow.combos.find(c=>c.name===name&&c.id!==id);
    if(rename&&same){error.textContent='已有同名组合，请换一个名称。';return;}
    let target=flow.combos.find(c=>c.id===id)||same;
    if(rename){target.name=name;}
    else{
      const chosen=selection();
      if(!chosen.length){error.textContent='当前没有选中的招式。';return;}
      const board=normalize(chosen);
      if(target){target.name=name;target.board=board;}else flow.combos.push({id:crypto.randomUUID(),name,board});
    }
    document.querySelector('dialog[open]')?.close();flow.library='combos';flow.picked='';api.redraw();
  }
  function rotateBoard(board){
    const h=Math.max(...board.cells.map(c=>c[1]+1));
    const placements=board.placements.map(p=>{const [w,ph]=api.geometry.footprint(p),card=api.card(p.key);return {...clone(p),x:h-p.y-ph,y:p.x,turns:card.rotate?(p.turns+1)%4:p.turns};});
    return normalize(placements);
  }
  function groupFor(payload){
    let board=clone(payload.board||flow.combos.find(c=>c.id===payload.combo)?.board);
    for(let n=0;n<(payload.turns||0)%4;n++)board=rotateBoard(board);
    return board;
  }
  function candidate(payload,x,y){
    const board=clone(snapshot().draft),group=groupFor(payload),moving=new Set(payload.moving||[]);
    board.placements=board.placements.filter(p=>!moving.has(p.id));
    const placements=group.placements.map(p=>({...p,id:moving.size?p.id:crypto.randomUUID(),x:x+p.x,y:y+p.y}));
    board.placements.push(...placements);return {board,placements,group,errors:api.validate(board)};
  }
  function placeGroup(payload,x,y){
    const next=candidate(payload,x,y);if(next.errors.length){api.notify(next.errors.join('；')+'。盘面保持。');return false;}
    flow.selected=next.placements.map(p=>p.id);flow.picked='';
    return api.adoptDraft(next.board,next.placements[0]?.id||'','');
  }
  function groupSize(board){return [Math.max(...board.cells.map(c=>c[0]+1)),Math.max(...board.cells.map(c=>c[1]+1))];}
  function clearDragVisuals(){
    overlay?.classList.remove('rf-is-dragging');
    overlay?.querySelectorAll('.rf-drag-hidden').forEach(e=>e.classList.remove('rf-drag-hidden'));
    overlay?.querySelectorAll('.rb-drop-ghost').forEach(g=>{g.hidden=true;g.innerHTML='';g.className='rb-drop-ghost';});
    document.querySelector('.rf-drag-preview')?.remove();context?.hideTip();
  }
  function cancelDrag(){
    if(!flow.pointer)return;const active=flow.pointer.active;flow.pointer=null;clearDragVisuals();
    if(active){flow.suppressClickUntil=performance.now()+250;api.clearPicked();}
  }
  function dragLocation(pointer){
    const plane=overlay.querySelector('[data-rb-board]'),r=plane.getBoundingClientRect();
    const fx=((pointer.x-r.left)/r.width*720-260)/110,fy=((pointer.y-r.top)/r.height*430-38)/110;
    const cell=[Math.floor(fx),Math.floor(fy)],inside=snapshot().draft.cells.some(([x,y])=>x===cell[0]&&y===cell[1]);
    return {plane,r,inside,x:cell[0]-Math.floor(pointer.anchor[0]),y:cell[1]-Math.floor(pointer.anchor[1])};
  }
  function updateDrag(){
    const pointer=flow.pointer;if(!pointer?.active)return;
    const location=dragLocation(pointer),next=candidate(pointer.payload,location.x,location.y),[w,h]=groupSize(next.group);
    const ghost=location.plane.querySelector('.rb-drop-ghost');
    let preview=document.querySelector('.rf-drag-preview');
    if(!preview){preview=document.createElement('div');preview.className='rf-drag-preview';preview.setAttribute('aria-hidden','true');document.body.append(preview);}
    if(pointer.artTurns!==pointer.payload.turns){
      const art=api.drawing.boardArt(next.group,110,0,0,w*110,h*110,false);
      ghost.innerHTML=next.group.cells.map(([cx,cy])=>`<span style="left:${cx/w*100}%;top:${cy/h*100}%;width:${100/w}%;height:${100/h}%"></span>`).join('')+art;
      preview.innerHTML=art;pointer.artTurns=pointer.payload.turns;
    }
    ghost.hidden=!location.inside;preview.hidden=location.inside;
    ghost.className='rb-drop-ghost rf-combo-ghost'+(next.errors.length?' is-invalid':'');
    ghost.style.cssText=`left:${(260+location.x*110)/720*100}%;top:${(38+location.y*110)/430*100}%;width:${w*110/720*100}%;height:${h*110/430*100}%`;
    const cell=location.r.width/720*110;
    preview.style.cssText=`left:${pointer.x-pointer.anchor[0]*cell}px;top:${pointer.y-pointer.anchor[1]*cell}px;width:${w*cell}px;height:${h*cell}px`;
  }
  function rotateDrag(){
    const pointer=flow.pointer;if(!pointer?.active)return;
    const group=groupFor(pointer.payload);if(group.placements.length===1&&!api.card(group.placements[0].key).rotate)return;
    const [,h]=groupSize(group),[ax,ay]=pointer.anchor;
    pointer.anchor=[Math.max(.001,h-ay),Math.max(.001,ax)];pointer.payload.turns=(pointer.payload.turns+1)%4;updateDrag();
  }
  document.addEventListener('pointerdown',event=>{
    if(event.button===0)flow.suppressClickUntil=0;
    if(!flow.open||document.querySelector('dialog[open]'))return;
    if(event.button===2&&flow.pointer?.active){event.preventDefault();event.stopImmediatePropagation();return;}
    if(event.button!==0)return;
    const source=event.target.closest('[data-rb-source]'),placed=event.target.closest('[data-rb-placement]'),combo=event.target.closest('[data-rf-combo]');
    if(!source&&!placed&&!combo)return;
    if(source?.dataset.rbAvailable==='false')return;
    let payload,anchor=[.5,.5];
    if(source){payload={board:normalize([item(source.dataset.rbSource,0,0,snapshot().picked===source.dataset.rbSource?snapshot().turns||0:0)]),turns:0};}
    else if(combo)payload={combo:combo.dataset.rfCombo,turns:flow.picked===combo.dataset.rfCombo?flow.turns:0};
    else{
      const all=snapshot().draft.placements,p=all.find(p=>p.id===placed.dataset.rbPlacement);
      const selected=flow.selected.includes(p.id)&&flow.selected.length>1?selection():[p];
      const x=Math.min(...selected.map(p=>p.x)),y=Math.min(...selected.map(p=>p.y)),r=placed.closest('[data-rb-board]').getBoundingClientRect();
      anchor=[((event.clientX-r.left)/r.width*720-260)/110-x,((event.clientY-r.top)/r.height*430-38)/110-y];
      payload={board:normalize(selected),moving:selected.map(p=>p.id),turns:0};
    }
    flow.pointer={id:event.pointerId,payload,anchor,x:event.clientX,y:event.clientY,startX:event.clientX,startY:event.clientY,active:false};
  },true);
  // A second mouse button while holding left produces mousedown, not pointerdown.
  document.addEventListener('mousedown',event=>{if(event.button===2&&flow.pointer?.active){event.preventDefault();event.stopImmediatePropagation();rotateDrag();}},true);
  document.addEventListener('pointermove',event=>{
    const pointer=flow.pointer;if(!pointer||event.pointerId!==pointer.id)return;
    pointer.x=event.clientX;pointer.y=event.clientY;
    if(!pointer.active&&Math.hypot(pointer.x-pointer.startX,pointer.y-pointer.startY)<6)return;
    if(!pointer.active){
      pointer.active=true;api.selectPlacement(pointer.payload.moving?.[0]||'');flow.picked='';flow.selected=pointer.payload.moving||[];context.hideTip();
      document.activeElement?.blur();overlay.classList.add('rf-is-dragging');
      const plane=overlay.querySelector('[data-rb-board]');
      (pointer.payload.moving||[]).forEach(id=>{plane.querySelector(`[data-placement-art="${id}"]`)?.classList.add('rf-drag-hidden');plane.querySelector(`[data-rb-placement="${id}"]`)?.classList.add('rf-drag-hidden');});
    }
    event.preventDefault();event.stopImmediatePropagation();updateDrag();
  },true);
  document.addEventListener('pointerup',event=>{
    const pointer=flow.pointer;if(!pointer||event.button!==0||event.pointerId!==pointer.id)return;
    if(!pointer.active){flow.pointer=null;return;}
    event.preventDefault();event.stopImmediatePropagation();pointer.x=event.clientX;pointer.y=event.clientY;
    const location=dragLocation(pointer),payload=pointer.payload;flow.pointer=null;flow.suppressClickUntil=performance.now()+250;api.clearPicked();clearDragVisuals();
    if(location.inside){placeGroup(payload,location.x,location.y);return;}
    if(payload.moving?.length){
      const board=clone(snapshot().draft),moving=board.placements.filter(p=>payload.moving.includes(p.id));
      if(moving.some(p=>p.locked||api.card(p.key).kind==='indicator')){api.notify('必需对象不能移出，已回到原位。');return;}
      board.placements=board.placements.filter(p=>!payload.moving.includes(p.id));flow.selected=[];api.adoptDraft(board,'','');
    }else api.redraw();
  },true);
  document.addEventListener('pointercancel',cancelDrag,true);window.addEventListener('blur',cancelDrag);
  document.addEventListener('contextmenu',event=>{
    if(flow.open&&(flow.pointer?.active||event.target.closest('[data-rb-board],[data-rb-source],[data-rf-combo]'))){event.preventDefault();event.stopImmediatePropagation();}
  },true);
  document.addEventListener('pointerover',event=>{if(flow.pointer?.active){context.hideTip();event.stopImmediatePropagation();}},true);
  document.addEventListener('focusin',event=>{if(flow.pointer?.active){context.hideTip();event.stopImmediatePropagation();}},true);
  document.addEventListener('click',event=>{
    if(performance.now()<flow.suppressClickUntil){flow.suppressClickUntil=0;event.preventDefault();event.stopImmediatePropagation();return;}
    const target=event.target.closest('button');if(!target){
      if(flow.open&&!document.querySelector('dialog[open]')&&!event.target.closest('.rb-source-paper,.rf-board-commit'))closeEditor();
      return;
    }
    const command=target.dataset.rf;
    if(target.dataset.rfOwnedTab){event.preventDefault();event.stopImmediatePropagation();flow.ownedTab=target.dataset.rfOwnedTab;flow.ownedQuery='';paintOverview();return;}
    if(target.hasAttribute('data-rf-clear-owned')){event.preventDefault();event.stopImmediatePropagation();flow.ownedQuery='';paintOverview();overviewRoot.querySelector('[data-rf-owned-search]')?.focus();return;}
    if(command){event.preventDefault();event.stopImmediatePropagation();
      if(command==='open')openEditor();
      if(command==='close')closeEditor();if(command==='apply')apply();
      if(command==='save-combo')saveComboDialog();
      if(command==='save-plan')planNameDialog();if(command==='confirm-plan')submitPlanName();return;
    }
    if(target.dataset.rfLibrary){event.preventDefault();event.stopImmediatePropagation();setLibrary(target.dataset.rfLibrary);return;}
    if(target.dataset.rfPlanApply){event.preventDefault();event.stopImmediatePropagation();applyNamedPlan(target.dataset.rfPlanApply);return;}
    if(target.dataset.rfPlanRename){event.preventDefault();event.stopImmediatePropagation();planNameDialog(target.dataset.rfPlanRename);return;}
    if(target.dataset.rfComboSubmit){event.preventDefault();event.stopImmediatePropagation();submitCombo(target.dataset.rfComboSubmit,target.dataset.rfRename==='true');return;}
    if(target.dataset.rfComboRename){event.preventDefault();event.stopImmediatePropagation();saveComboDialog(target.dataset.rfComboRename,true);return;}
    if(target.dataset.rfComboDelete){event.preventDefault();event.stopImmediatePropagation();flow.combos=flow.combos.filter(c=>c.id!==target.dataset.rfComboDelete);if(flow.picked===target.dataset.rfComboDelete)flow.picked='';api.redraw();return;}
    if(target.dataset.rfCombo){event.preventDefault();event.stopImmediatePropagation();flow.picked=target.dataset.rfCombo;flow.turns=0;flow.selected=[];api.redraw();return;}
    if(!flow.open)return;
    const placed=target.dataset.rbPlacement;
    if(flow.picked&&(target.dataset.rbCell||placed)){
      event.preventDefault();event.stopImmediatePropagation();const p=snapshot().draft.placements.find(p=>p.id===placed),[x,y]=p?[p.x,p.y]:target.dataset.rbCell.split(',').map(Number);placeGroup({combo:flow.picked,turns:flow.turns},x,y);return;
    }
    const current=snapshot();
    if(current.picked&&(target.dataset.rbCell||placed)){
      event.preventDefault();event.stopImmediatePropagation();const p=current.draft.placements.find(p=>p.id===placed),[x,y]=p?[p.x,p.y]:target.dataset.rbCell.split(',').map(Number);
      placeGroup({board:normalize([item(current.picked,0,0,current.turns)]),turns:0},x,y);return;
    }
    if(placed&&(event.shiftKey||event.ctrlKey)){
      event.preventDefault();event.stopImmediatePropagation();flow.selected=flow.selected.includes(placed)?flow.selected.filter(id=>id!==placed):[...flow.selected,placed];api.selectPlacement(flow.selected[0]||'');api.redraw();return;
    }
    if(placed)flow.selected=[placed];
    if(target.dataset.rbCell)flow.selected=[];
    if(target.dataset.rbSource){flow.picked='';flow.selected=[];}
    if(target.dataset.rb==='clear'){flow.picked='';flow.selected=[];}
  },true);
  document.addEventListener('input',event=>{
    if(event.target.id==='rf-plan-name'){updatePlanNameHint();document.querySelector('.rf-plan-error').textContent='';return;}
    if(!event.target.hasAttribute('data-rf-owned-search'))return;flow.ownedQuery=event.target.value;
    const content=overviewRoot?.querySelector('.rf-owned-scroll .scroll-content');if(!content)return;content.innerHTML=ownedEntries();M.apply(content);overviewRoot.querySelector('.rf-owned-scroll .scroll-view').scrollTop=0;
  });
  document.addEventListener('keydown',event=>{
    if(flow.pointer?.active&&(event.key==='Escape'||event.key.toLowerCase()==='r')){
      event.preventDefault();event.stopImmediatePropagation();if(event.key==='Escape'){cancelDrag();api.redraw();}else rotateDrag();return;
    }
    if(event.target.id==='rf-combo-name'&&event.key==='Enter'){event.preventDefault();event.stopImmediatePropagation();const button=document.querySelector('[data-rf-combo-submit]');submitCombo(button.dataset.rfComboSubmit,button.dataset.rfRename==='true');return;}
    if(event.target.id==='rf-plan-name'&&event.key==='Enter'){event.preventDefault();event.stopImmediatePropagation();submitPlanName();return;}
    if(!flow.open||document.querySelector('dialog[open]')||event.target.matches('input,textarea,select'))return;
    if(event.key==='Tab'){
      const buttons=[...overlay.querySelectorAll('button,input,[tabindex="0"]')].filter(b=>!b.disabled&&b.getClientRects().length);
      const first=buttons[0],last=buttons.at(-1);
      if((event.shiftKey&&document.activeElement===first)||(!event.shiftKey&&document.activeElement===last)||!overlay.contains(document.activeElement)){
        event.preventDefault();event.stopImmediatePropagation();(event.shiftKey?last:first)?.focus();return;
      }
    }
    if(event.key==='Escape'){
      event.preventDefault();event.stopImmediatePropagation();if(flow.picked||flow.selected.length){flow.picked='';flow.selected=[];api.redraw();}else closeEditor();return;
    }
    if(event.key.toLowerCase()==='r'&&flow.picked){event.preventDefault();event.stopImmediatePropagation();flow.turns=(flow.turns+1)%4;api.notify('组合已转向。');}
    else if(flow.selected.length>1&&['r','R','Delete','ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key)){
      event.preventDefault();event.stopImmediatePropagation();const selected=selection(),board=clone(snapshot().draft);
      if(event.key==='Delete'){if(selected.some(p=>p.locked||api.card(p.key).kind==='indicator'))return api.notify('必需对象不能移出。');board.placements=board.placements.filter(p=>!flow.selected.includes(p.id));flow.selected=[];api.adoptDraft(board,'','');return;}
      const x=Math.min(...selected.map(p=>p.x)),y=Math.min(...selected.map(p=>p.y)),d={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]}[event.key]||[0,0];
      placeGroup({board:normalize(selected),moving:selected.map(p=>p.id),turns:event.key.toLowerCase()==='r'?1:0},x+d[0],y+d[1]);
    }
  },true);
  document.addEventListener('dragstart',event=>{
    if(flow.open){event.preventDefault();event.stopImmediatePropagation();}
  },true);
  window.addEventListener('resize',fitEditor);
  api.useEditorView(editorMarkup,()=>flow.open);
  api.render=ctx=>{context=ctx;api.configure(ctx);return overview();};
  api.mount=host=>{overviewRoot=host.querySelector('.rf-overview');if(flow.open&&overlay)base.mount(overlay);else base.mount(host);};
  api.reset=()=>{cancelDrag();overlay?.remove();overlay=null;restoreBackground();base.reset();resetFlow();};
  api.inspect=()=>({...base.inspect(),applied:clone(flow.applied),appliedName:flow.appliedName,appliedPlanId:flow.appliedPlanId,editorOpen:flow.open,combos:clone(flow.combos),groupSelected:[...flow.selected]});
  api.getOpeningConfiguration=()=>clone(flow.applied);
  api.setSourceSnapshot=value=>{base.setSources(value);if(flow.open)api.redraw();else paintOverview();};
  api.isDragging=()=>!!flow.pointer?.active;
})();
