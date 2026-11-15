(() => {
  'use strict';
  const definitions = {
    yingdi:{name:'应敌', kind:'skill', w:1, h:1, learned:true, maxExample:2, depletionDemo:true, mark:'应', description:'对一名合法敌人造成物理伤害。', cost:'体力 1', target:'自动随机选择合法敌人', source:'公共基础招式'},
    jiazhao:{name:'架招', kind:'skill', w:1, h:1, learned:true, maxExample:1, mark:'架', description:'应对物理来袭并化解。读到后待命，所有人走完本轮后失效；多个合格应对按盘面先后。', cost:'触发应对时支付原费用', target:'自身应对', source:'公共基础招式'},
    tongji:{name:'痛击', kind:'skill', w:1, h:2, learned:true, maxExample:2, depletionDemo:true, mark:'痛', description:'对一名合法敌人造成物理伤害。', cost:'体力 2', target:'自动随机选择合法敌人', source:'公共基础招式'},
    cuixue:{name:'催血', kind:'skill', w:1, h:1, learned:true, maxExample:1, mark:'血', description:'获得气血；若满足已附化法的条件，按原效果追加。', cost:'体力 1', target:'自身', source:'已参悟技能示例'},
    juqi:{name:'聚炁', kind:'skill', w:1, h:1, learned:true, maxExample:1, mark:'炁', description:'获得炁；达到周天条件时按原规则回满法力。', cost:'体力 1', target:'自身', source:'已参悟技能示例'},
    lingji:{name:'灵击', kind:'skill', w:2, h:1, learned:false, maxExample:1, mark:'灵', description:'尚未参悟，不能放入。', cost:'体力 2', target:'一名敌方', source:'未参悟示例'},
    yangluo:{name:'百骸·养络', kind:'rewrite', w:1, h:1, learned:true, mark:'化', description:'适用催血；施展前气血不高于 3 层时，额外获得 1 层气血。', cost:'依原化法条件', target:'紧邻的催血', source:'已启用化法示例'},
    huichun:{name:'回春丹', kind:'pill', w:1, h:2, learned:true, mark:'丹', description:'本人生命有缺口时可用，实际生效才消耗一颗。', cost:'消耗真实丹药', target:'自身', source:'行囊库存示例', limit:2},
    ninggang:{name:'凝罡丹', kind:'pill', w:1, h:2, learned:true, mark:'丹', description:'按原丹药规则为本人获得护盾。示例行囊中没有该丹药。', cost:'消耗真实丹药', target:'自身', source:'库存不足示例', limit:0},
    fuzhi:{name:'符箓', kind:'talisman', w:1, h:1, learned:false, mark:'符', description:'此处只预留符箓类别，具体战斗符箓尚未开放。', cost:'待确定', target:'按符箓原规则', source:'未开放示例'}
  };
  const initial = [
    {uid:'p1',id:'cuixue',x:0,y:0,rot:false},
    {uid:'p2',id:'yangluo',x:1,y:0,rot:false,dir:2},
    {uid:'p3',id:'huichun',x:2,y:0,rot:false},
    {uid:'p4',id:'yingdi',x:0,y:1,rot:false},
    {uid:'p5',id:'jiazhao',x:0,y:2,rot:false},
    {uid:'p6',id:'tongji',x:1,y:1,rot:false}
  ];
  const copy = data => JSON.parse(JSON.stringify(data));
  let pieces=copy(initial), saved=copy(initial), demoExhausted={}, savedDemo={}, selected={type:'board',key:'p1'}, pending=null, dragging=null, tab='skill', editing=true, requested=false, counter=7, paused=true, speed=1;
  const dirVectors=[[1,0],[0,1],[-1,0],[0,-1]], dirSigns=['→','↓','←','↑'];
  const board=document.getElementById('board'), list=document.getElementById('library-list'), info=document.getElementById('detail-body'), actions=document.getElementById('details-actions'), demoTools=document.getElementById('prototype-tools'), demoActions=document.getElementById('demo-actions'), feedback=document.getElementById('feedback'), search=document.getElementById('search');
  const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  function shape(item){const d=definitions[item.id];return item.rot?{w:d.h,h:d.w}:{w:d.w,h:d.h};}
  function occupied(x,y,except=''){return pieces.find(p=>{if(p.uid===except)return false;const s=shape(p);return x>=p.x&&x<p.x+s.w&&y>=p.y&&y<p.y+s.h;});}
  function fits(id,x,y,rot=false,except=''){const d=definitions[id],w=rot?d.h:d.w,h=rot?d.w:d.h;if(x<0||y<0||x+w>3||y+h>3)return false;for(let dy=0;dy<h;dy++)for(let dx=0;dx<w;dx++)if(occupied(x+dx,y+dy,except))return false;return true;}
  function linkFor(piece){const [dx,dy]=dirVectors[piece.dir??0],target=occupied(piece.x+dx,piece.y+dy,piece.uid);return target?.id==='cuixue'?target:null;}
  function current(){if(selected.type==='board'){const p=pieces.find(i=>i.uid===selected.key);return p?{piece:p,def:definitions[p.id]}:null;}return definitions[selected.key]?{piece:null,def:definitions[selected.key],id:selected.key}:null;}
  function uses(id){return pieces.filter(p=>p.id===id).length;}
  function isAvailable(id){const d=definitions[id];return d.learned && (d.kind==='skill'?uses(id)<d.maxExample:Number.isInteger(d.limit)?uses(id)<d.limit:uses(id)===0);}
  function glyph(def,rot=false){
    const rotateStyle=rot?` style="width:${100*def.w/def.h}%;height:${100*def.h/def.w}%;transform:rotate(90deg)"`:'';
    if(def.kind==='pill')return `<img src="../../../textures/items/inventory_density_20261029/pill_container_empty_porcelain.png" alt=""${rotateStyle}>`;
    if(def===definitions.yingdi)return `<img src="招式样张_应敌_1x1.png" alt=""${rotateStyle}>`;
    if(def===definitions.tongji)return `<img src="招式样张_痛击_1x2.png" alt=""${rotateStyle}>`;
    const forms={
      yingdi:'<path d="M21 61 Q15 51 22 42 L36 30 Q40 27 43 31 L46 35 L51 27 Q54 23 58 27 L61 32 L65 28 Q69 25 72 29 L74 35 Q79 33 81 39 L78 58 Q76 69 66 74 L49 82 L34 74Z" fill="#658d72" stroke="#41614b" stroke-width="3"/><path d="M45 38 L38 47 M59 35 L52 44 M72 38 L65 47" fill="none" stroke="#d8e8cd" stroke-width="3" stroke-linecap="round"/>',
      tongji:'<path d="M24 37 Q20 29 26 25 Q31 23 36 28 L40 32 L44 23 Q47 19 52 23 L56 29 L61 24 Q65 21 69 26 L72 33 Q78 30 81 37 L79 56 Q77 68 66 72 L50 79 L31 67Z" fill="#9a6655" stroke="#765446" stroke-width="3"/><path d="M25 80 Q49 70 74 77 M20 87 Q50 77 80 84" stroke="#b68e72" fill="none" stroke-width="4" stroke-linecap="round"/>',
      jiazhao:'<path d="M33 78 Q22 66 24 51 L27 26 Q28 19 33 20 Q38 21 37 28 L36 43 L42 17 Q44 11 49 13 Q54 15 51 22 L47 42 L56 19 Q59 14 63 17 Q67 20 64 26 L58 44 L66 27 Q69 22 73 25 Q77 28 74 35 L68 54 Q74 48 78 49 Q84 52 77 60 L63 77 Q49 84 33 78Z" fill="#98b299" stroke="#58795f" stroke-width="3"/>',
      cuixue:'<path d="M50 16 C42 31 29 40 30 55 C31 69 39 77 50 81 C61 77 69 69 70 55 C71 40 58 30 50 16Z" fill="#a3655b" stroke="#83564f" stroke-width="3"/><path d="M37 62 Q47 39 59 43 Q68 49 58 61 Q50 70 39 67" fill="none" stroke="#e5c0a3" stroke-width="4" stroke-linecap="round"/>',
      juqi:'<path d="M73 31 C63 17 37 18 27 33 C18 47 25 67 42 72 C57 77 72 66 70 53 C68 42 54 38 47 44 C40 49 44 57 51 58" fill="none" stroke="#719b85" stroke-width="8" stroke-linecap="round"/><path d="M77 68 Q63 84 42 80 M20 34 Q29 15 49 15" fill="none" stroke="#9dbca3" stroke-width="4" stroke-linecap="round"/>',
      lingji:'<path d="M16 61 Q48 35 82 30 Q61 46 52 68 Q43 58 16 61Z" fill="#89a893" stroke="#5f856e" stroke-width="3"/><circle cx="76" cy="26" r="6" fill="#c9dfcb"/>',
      yangluo:'<path d="M28 57 Q19 37 36 26 Q49 17 64 25 Q79 33 73 50 Q68 64 54 66 Q45 67 42 59 Q39 52 46 48 Q52 44 57 50" fill="none" stroke="#71977a" stroke-width="7" stroke-linecap="round"/><path d="M29 66 Q50 84 72 66 M30 34 Q50 13 71 35" fill="none" stroke="#acc7a7" stroke-width="3" stroke-linecap="round"/>',
      fuzhi:'<path d="M23 28 Q50 16 77 28 L73 72 Q50 81 27 72Z" fill="#c5b692" stroke="#8a8669" stroke-width="3"/><path d="M38 37 L60 36 M48 30 L45 67 M35 51 L63 48" fill="none" stroke="#8c6857" stroke-width="4" stroke-linecap="round"/>'
    };
    const key=Object.entries(definitions).find(([,candidate])=>candidate===def)?.[0];
    return `<svg viewBox="0 0 100 100" aria-hidden="true"${rotateStyle}>${forms[key]||forms.lingji}</svg>`;
  }
  function notice(text,bad=false){feedback.textContent=text;feedback.classList.toggle('bad',bad);}
  function cellFromEvent(event){const rect=board.getBoundingClientRect(),x=Math.floor((event.clientX-rect.left)/(rect.width/3)),y=Math.floor((event.clientY-rect.top)/(rect.height/3));return {x,y};}
  function selectLibrary(id){selected={type:'library',key:id};pending=null;render();}
  function selectBoard(uid){selected={type:'board',key:uid};pending=null;render();}
  function place(id,x,y,rot=false){if(!editing)return;if(!isAvailable(id)){notice('该配置尚不可用，或摆放数量已满',true);return;}if(!fits(id,x,y,rot)){notice('空间不足或已有对象，请换一个位置',true);return;}const p={uid:`p${counter++}`,id,x,y,rot};if(definitions[id].kind==='rewrite')p.dir=0;pieces.push(p);selected={type:'board',key:p.uid};pending=null;render();notice('已放入；确认布阵后用于下一回合');}
  function move(uid,x,y){const p=pieces.find(v=>v.uid===uid);if(!p)return;if(!fits(p.id,x,y,p.rot,uid)){notice('目标位置无法放下，已保留原位置',true);return;}p.x=x;p.y=y;selected={type:'board',key:uid};render();notice('已调整位置');}
  function rotate(){const c=current();if(!c||!c.piece||!editing)return;const p=c.piece,d=c.def;if(d.kind==='rewrite'){p.dir=((p.dir??0)+1)%4;render();notice(linkFor(p)?'化法已指向催血':'化法尚未指向符合条件的招式');return;}if(d.w===d.h){notice('这个对象的占格方向无需旋转');return;}if(!fits(p.id,p.x,p.y,!p.rot,p.uid)){notice('旋转后空间不足，已保留原位置',true);return;}p.rot=!p.rot;render();notice('已旋转占格方向');}
  function remove(){const c=current();if(!c?.piece||!editing)return;const id=c.piece.id;pieces=pieces.filter(p=>p.uid!==c.piece.uid);selected={type:'library',key:id};render();notice('已从盘面取下');}
  function renderBoard(){board.innerHTML='';for(let y=0;y<3;y++)for(let x=0;x<3;x++){const cell=document.createElement('button');cell.type='button';cell.className='cell'+(occupied(x,y)?' occupied':'');cell.style.left=`calc(var(--cell)*${x})`;cell.style.top=`calc(var(--cell)*${y})`;cell.setAttribute('aria-label',`第${y+1}行第${x+1}格`);cell.dataset.x=x;cell.dataset.y=y;cell.addEventListener('click',()=>{if(pending)place(pending,x,y);else notice('选择左侧已学配置，再点空格摆入');});board.append(cell);}
    for(const p of pieces){const d=definitions[p.id],s=shape(p),button=document.createElement('button'),linked=d.kind==='skill'&&pieces.some(item=>definitions[item.id].kind==='rewrite'&&linkFor(item)?.uid===p.uid);button.type='button';button.title=d.name;button.dataset.id=p.id;button.dataset.uid=p.uid;button.className='board-item'+(demoExhausted[p.id]?' demo-exhausted':'')+(selected.type==='board'&&selected.key===p.uid?' selected':'')+(d.kind==='rewrite'&&!linkFor(p)?' invalid-link':'')+(linked?' target-linked':'');button.style.left=`calc(var(--cell)*${p.x})`;button.style.top=`calc(var(--cell)*${p.y})`;button.style.width=`calc(var(--cell)*${s.w})`;button.style.height=`calc(var(--cell)*${s.h})`;button.setAttribute('aria-label',`${d.name}，第${p.y+1}行第${p.x+1}格${demoExhausted[p.id]?'，次数耗尽外观示例':''}`);button.draggable=editing;button.innerHTML=glyph(d,p.rot)+(demoExhausted[p.id]?'<span class="exhausted-badge">耗尽示例</span>':'')+(d.kind==='rewrite'?`<span class="mini-status"></span><span class="item-arrow">${dirSigns[p.dir??0]}</span>`:'')+`<span class="item-name">${esc(d.name)}</span>`;button.addEventListener('click',e=>{if(pending){const {x,y}=cellFromEvent(e);place(pending,x,y);}else selectBoard(p.uid);});button.addEventListener('dragstart',e=>{if(!editing)return;dragging={id:p.id,uid:p.uid,rot:p.rot};e.dataTransfer.setData('text/plain',`board:${p.uid}`);e.dataTransfer.effectAllowed='move';});button.addEventListener('dragend',()=>{dragging=null;clearHighlights();});board.append(button);}
  }
  function renderLibrary(){
    const query=search.value.trim();
    const entries=Object.entries(definitions).filter(([,d])=>d.kind===tab&&(!query||d.name.includes(query)));
    list.innerHTML='';
    document.getElementById('library-tip').textContent=tab==='artifact'?'暂不提供摆放对象':'数量上限为示例；选择或拖入格子';
    if(tab==='artifact'){list.innerHTML='<p class="empty">法宝参战方式待定</p>';return;}
    if(!entries.length){list.innerHTML='<p class="empty">没有匹配的配置</p>';return;}
    for(const [id,d] of entries){
      const row=document.createElement('button');
      row.type='button';row.dataset.id=id;
      row.className='lib-row'+(!d.learned||(d.kind!=='skill'&&Number.isInteger(d.limit)&&!isAvailable(id))?' unavailable':'')+(selected.type==='library'&&selected.key===id?' selected':'');
      const availability=d.kind==='skill'
        ? `${!d.learned?'未参悟 · ':''}已放入 ${uses(id)} / ${d.maxExample}（示例）`
        : !d.learned?(d.kind==='talisman'?'未开放':'未参悟')
        : Number.isInteger(d.limit)?d.limit-uses(id)<=0?'库存不足':`可用 ${d.limit-uses(id)} / ${d.limit}`
        : uses(id)?'已放入':'已学';
      row.innerHTML=`<span class="thumb">${glyph(d)}</span><span class="name">${esc(d.name)}</span><span class="state">${esc(availability)}</span>`;
      row.addEventListener('click',()=>selectLibrary(id));row.draggable=editing&&isAvailable(id);
      row.addEventListener('dragstart',e=>{if(!isAvailable(id)){e.preventDefault();return;}dragging={id,uid:'',rot:false};e.dataTransfer.setData('text/plain',`library:${id}`);e.dataTransfer.effectAllowed='copy';});
      row.addEventListener('dragend',()=>{dragging=null;clearHighlights();});list.append(row);
    }
  }
  function renderDetails(){
    const c=current();
    if(!c){info.innerHTML='<p>选择一项配置以查看详情。</p>';actions.innerHTML='';demoTools.hidden=true;demoActions.innerHTML='';return;}
    const {def:d,piece:p}=c,id=c.id??p?.id,s=p?shape(p):{w:d.w,h:d.h};
    document.getElementById('source-label').textContent=d.source;
    let state='';
    if(p&&d.kind==='rewrite')state=linkFor(p)?'已作用：催血':'未作用：请指向紧邻的催血';
    else if(!d.learned)state=d.kind==='talisman'?'示例类别尚未开放':'尚未参悟，不能摆入';
    else if(p)state=demoExhausted[id]?'次数耗尽（仅外观示例）':'已放入招式盘';
    else if(!isAvailable(id))state=d.kind==='skill'?'已达摆放上限（示例）':Number.isInteger(d.limit)?'库存不足，不能摆入':'示例中已放入，不能重复摆入';
    else state='可选择空格摆入';
    const skillInfo=d.kind==='skill'?`<p><strong>盘面检查</strong>每格检查，共 ${s.w*s.h} 次</p><p><strong>本轮上限</strong>这一份最多成功发动 1 次</p><p><strong>可摆数量</strong>已放入 ${uses(id)} / ${d.maxExample}（示例）</p>`:'';
    info.innerHTML=`<div class="detail-top"><div class="detail-icon${demoExhausted[id]?' demo-exhausted':''}">${glyph(d)}</div><div><div class="detail-title">${esc(d.name)}</div><div class="detail-sub">${esc(d.source)} · ${s.w} × ${s.h} 格示意</div></div></div><div class="detail-list"><p>${esc(d.description)}</p><p><strong>消耗</strong>${esc(d.cost)}</p><p><strong>目标</strong>${esc(d.target)}</p>${skillInfo}</div><div class="link-state${state.startsWith('未')||state.startsWith('尚')||state.startsWith('库存')?' warn':''}">${esc(state)}</div>`;
    actions.innerHTML='';demoActions.innerHTML='';demoTools.hidden=!(p&&d.depletionDemo);
    if(p){
      const r=document.createElement('button');r.type='button';r.textContent=d.kind==='rewrite'?'旋转指向':'旋转占格';r.disabled=!editing||(d.kind!=='rewrite'&&d.w===d.h);r.addEventListener('click',rotate);actions.append(r);
      const removeButton=document.createElement('button');removeButton.type='button';removeButton.textContent='取下';removeButton.disabled=!editing;removeButton.addEventListener('click',remove);actions.append(removeButton);
      if(d.depletionDemo){
        const demo=document.createElement('button');demo.type='button';demo.className='demo-toggle';demo.textContent=demoExhausted[id]?'恢复外观（示例）':'演示次数耗尽';
        demo.title='纯界面演示：同角色同招所有图标同步变暗；不计算次数或恢复';demo.disabled=!editing;
        demo.addEventListener('click',()=>{demoExhausted[id]=!demoExhausted[id];render();notice(demoExhausted[id]?'同招图标已变暗：仅外观示例':'同招图标已恢复：仅外观示例');});demoActions.append(demo);
      }
    }else{
      const add=document.createElement('button');add.type='button';add.className='primary';add.textContent='选择位置';add.disabled=!editing||!isAvailable(id);add.addEventListener('click',()=>{pending=id;notice(`请选择 ${d.name} 的放置位置`);});actions.append(add);
    }
  }
  function render(){renderBoard();renderLibrary();renderDetails();document.getElementById('workbench').classList.toggle('running',!editing);document.getElementById('confirm').textContent=editing?'确认布阵':requested?'取消整理预约':'回合结束后整理';document.getElementById('reset').hidden=!editing;document.getElementById('cancel').hidden=!editing;document.getElementById('demo-next').hidden=editing;document.getElementById('pause').textContent=paused?'继续':'暂停';document.getElementById('rule-mark').textContent=editing?'本轮遍历完成':'自动战斗示意';document.getElementById('phase-suffix').textContent=editing?'第三回合 · 间歇':'自动战斗 · 示意';document.getElementById('library-phase').textContent=editing?'本轮间歇':'战斗中';}
  function clearHighlights(){board.querySelectorAll('.cell').forEach(c=>c.classList.remove('valid','invalid'));}
  function paintPlacement(id,x,y,rot=false,except=''){clearHighlights();const d=definitions[id],w=rot?d.h:d.w,h=rot?d.w:d.h,valid=fits(id,x,y,rot,except);for(let dy=0;dy<h;dy++)for(let dx=0;dx<w;dx++){const cell=board.querySelector(`.cell[data-x="${x+dx}"][data-y="${y+dy}"]`);cell?.classList.add(valid?'valid':'invalid');}}
  board.addEventListener('dragover',e=>{if(!editing)return;e.preventDefault();const {x,y}=cellFromEvent(e);if(dragging)paintPlacement(dragging.id,x,y,dragging.rot,dragging.uid);});
  board.addEventListener('mousemove',e=>{if(!pending||!editing)return;const {x,y}=cellFromEvent(e);paintPlacement(pending,x,y);});
  board.addEventListener('mouseleave',()=>{if(pending)clearHighlights();});
  board.addEventListener('dragleave',e=>{if(!board.contains(e.relatedTarget))clearHighlights();});
  board.addEventListener('drop',e=>{e.preventDefault();clearHighlights();const {x,y}=cellFromEvent(e),data=e.dataTransfer.getData('text/plain');if(data.startsWith('library:'))place(data.slice(8),x,y);else if(data.startsWith('board:'))move(data.slice(6),x,y);});
  document.querySelectorAll('.tab').forEach(button=>button.addEventListener('click',()=>{tab=button.dataset.tab;document.querySelectorAll('.tab').forEach(b=>b.classList.toggle('active',b===button));search.value='';search.placeholder=tab==='skill'?'查找已学招式':'查找配置';renderLibrary();}));
  search.addEventListener('input',renderLibrary);
  document.getElementById('reset').addEventListener('click',()=>{pieces=copy(initial);demoExhausted={};selected={type:'board',key:'p1'};pending=null;render();notice('已恢复初始示意');});
  document.getElementById('cancel').addEventListener('click',()=>{pieces=copy(saved);demoExhausted=copy(savedDemo);selected={type:'board',key:pieces[0]?.uid};pending=null;editing=false;paused=false;requested=false;document.getElementById('phase-label').textContent='自动战斗示意';render();notice('已撤销本次调整并返回战斗示意');});
  document.getElementById('confirm').addEventListener('click',()=>{if(editing){saved=copy(pieces);savedDemo=copy(demoExhausted);editing=false;paused=false;requested=false;document.getElementById('phase-label').textContent='自动战斗示意';notice('配置已确认；此页面只演示界面，不进行真实战斗');}else{requested=!requested;notice(requested?'已预约：所有人遍历完成后进入整理':'已取消整理预约');}render();});
  document.getElementById('demo-next').addEventListener('click',()=>{if(editing)return;if(requested){editing=true;paused=true;requested=false;document.getElementById('phase-label').textContent='调整布阵后继续';notice('所有人已遍历，进入回合间布阵示意');}else notice('本轮无人预约整理，继续自动战斗示意');render();});
  document.getElementById('pause').addEventListener('click',()=>{if(editing){notice('正在布阵，确认后继续');return;}paused=!paused;render();notice(paused?'界面示意已暂停':'界面示意继续，不计算真实战斗');});
  document.getElementById('speed').addEventListener('click',e=>{speed=speed===1?2:speed===2?3:1;e.currentTarget.textContent=`× ${speed}`;});
  document.getElementById('log').addEventListener('click',()=>notice('战斗记录入口示意，尚未接入真实数据'));
  document.getElementById('status-open').addEventListener('click',()=>{document.getElementById('status-popover').hidden=false;});
  document.getElementById('status-close').addEventListener('click',()=>{document.getElementById('status-popover').hidden=true;});
  document.addEventListener('keydown',e=>{if((e.key==='r'||e.key==='R')&&document.activeElement!==search){e.preventDefault();rotate();}if(e.key==='Escape'){pending=null;clearHighlights();notice('已取消本次选择');}});
  render();
})();
