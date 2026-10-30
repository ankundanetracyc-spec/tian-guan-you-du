
'use strict';
const DATA={"stone": {"definitionId": "mat_spirit_stone", "name": "灵石", "category": "材料", "file": "spirit_stone_1x1.png", "width": 1, "height": 1, "resourcePath": "resources/entities/items/灵石.tres", "hoverIdentity": "", "hoverBody": "品阶：9品上\n五行：土\n质地：致密\n相性：和\n功用：聚·九品、藏·九品、通·九品\n简介："}, "manual": {"definitionId": "manual_huangting_yangxing", "name": "胎息经", "category": "秘籍", "file": "manual_generic_2x3.png", "width": 2, "height": 3, "resourcePath": "resources/entities/items/黄庭养形篇秘籍.tres", "hoverIdentity": "", "hoverBody": "修习境界：凡人至云息十层\n修习要求：—\n关键词：—\n简介：气血未尽时回落可积身法，气血归零时转得借力；主修在零气血时消耗身法与借力，以攻击换回气血。"}, "pill": {"definitionId": "alchemy_huichun_9_1", "name": "回春丹·良效", "category": "丹药", "file": "pill_container_empty_porcelain.png", "width": 2, "height": 3, "resourcePath": "resources/entities/items/alchemy/huichun_1.tres", "hoverIdentity": "", "hoverBody": "丹阶：九品\n丹效：良效\n药效：—\n丹毒：—\n简介："}, "root": {"definitionId": "mat_bitter_moss_root", "name": "苦苔根", "category": "材料", "file": "bitter_moss_root_2x4.png", "width": 2, "height": 4, "resourcePath": "resources/entities/items/苦苔根.tres", "hoverIdentity": "", "hoverBody": "品阶：9品中\n五行：木\n质地：纤维\n相性：阴\n功用：藏·九品\n简介："}, "iron": {"definitionId": "mat_xuantie", "name": "玄铁", "category": "材料", "file": "xuantie_3x3.png", "width": 3, "height": 3, "resourcePath": "resources/entities/items/玄铁.tres", "hoverIdentity": "", "hoverBody": "品阶：8品下\n五行：金\n质地：致密\n相性：阳\n功用：固·八品\n简介："}, "sword": {"definitionId": "sect_sword_suxing", "name": "素行剑", "category": "器物", "file": "slender_sword_2x6.png", "width": 2, "height": 6, "resourcePath": "resources/entities/equipment/素行剑.tres", "hoverIdentity": "", "hoverBody": "品阶：无品\n器类：剑\n器蕴：—\n锋锐：—\n装备效果：—\n化法：—\n技能：—\n简介：宗门制式凡器。精准 +2。"}};
const ICON_ROOT='../../../textures/items/inventory_density_20261029/';
const BAG_ID='bag:player';
const REGIONS=[
 {id:'chest',name:'胸前',width:6,height:4,left:334,top:22},
 {id:'sleeve-left',name:'左袖',width:3,height:4,left:78,top:218},
 {id:'sleeve-right',name:'右袖',width:3,height:4,left:684,top:218},
 {id:'waist',name:'腰间',width:6,height:4,left:334,top:410},
 {id:'back',name:'背囊',width:6,height:8,left:884,top:122}
];
const CATEGORY_NAMES=['全部','装备','材料','秘籍','阵盘','器具','符箓','丹药','灵膳','器物'];
const CELL=48,BOARD_SCALE=48/42;
let items=[],containers=new Map(),currentContainerId=BAG_ID;
let selectedId=null,selectedGroupKey=null,locatedId=null,hoveredId=null;
let category='全部',query='',lastHoverAnchor=null;
const memberOrder=new Map(),lastLocatedByGroup=new Map();
const $=id=>document.getElementById(id);
function originalItems(){
 const a=[];
 const add=(instanceId,definitionId,regionId,cellX,cellY)=>a.push({instanceId,definitionId,containerId:BAG_ID,regionId,cellX,cellY,width:DATA[definitionId].width,height:DATA[definitionId].height});
 add('sword:01','sword','back',0,0);add('manual:01','manual','back',2,0);add('manual:02','manual','back',4,0);
 add('root:01','root','chest',4,0);add('iron:01','iron','waist',0,0);
 add('pill:01','pill','sleeve-left',0,0);add('pill:02','pill','sleeve-right',0,0);
 let n=0;for(let x=0;x<4;x++)add('stone:'+String(++n).padStart(2,'0'),'stone','chest',x,0);
 for(const region of ['sleeve-left','sleeve-right'])for(let y=0;y<4;y++)add('stone:'+String(++n).padStart(2,'0'),'stone',region,2,y);
 return a;
}
function find(id){return items.find(i=>i.instanceId===id)}
function definition(item){return DATA[item.definitionId]}
function region(item){return REGIONS.find(r=>r.id===item.regionId)}
function itemPath(item){const c=containers.get(item.containerId);return [...c.path,...(item.containerId===BAG_ID?[region(item).name]:[])].join(' / ')}
function matches(item){const d=definition(item);return(category==='全部'||d.category===category)&&(!query||d.name.includes(query))}
function quantity(item){return Number.isFinite(item.quantity)&&item.quantity>0?item.quantity:1}
function canonical(value){if(Array.isArray(value))return value.map(canonical);if(value&&typeof value==='object')return Object.fromEntries(Object.keys(value).sort().map(k=>[k,canonical(value[k])]));return value??null}
function keyFor(item,d=definition(item)){
 const variant=item.variantKey??item.variantSignature??d.variantKey??d.variantSignature;
 const distinct=item.groupingMode==='distinct'||item.isUniqueArtifact||item.artifactInstanceId||(item.isArtifact&&variant==null);
 return JSON.stringify(canonical({definition:d.definitionId??item.definitionId,path:d.resourcePath??null,variant:variant??null,tier:item.efficacy_tier??d.efficacy_tier??null,quality:item.quality??d.quality??null,instance:distinct?item.instanceId:null}));
}
function groups(){
 const map=new Map();
 for(const item of items){const key=keyFor(item);if(!map.has(key))map.set(key,{key,name:definition(item).name,members:[],count:0});const g=map.get(key);g.members.push(item);g.count+=quantity(item)}
 for(const g of map.values()){
  const live=new Map(g.members.map(i=>[i.instanceId,i]));
  const order=(memberOrder.get(g.key)||[]).filter(id=>live.has(id));
  for(const i of g.members)if(!order.includes(i.instanceId))order.push(i.instanceId);
  memberOrder.set(g.key,order);g.members=order.map(id=>live.get(id));
  if(lastLocatedByGroup.has(g.key)&&!live.has(lastLocatedByGroup.get(g.key)))lastLocatedByGroup.delete(g.key);
 }
 return [...map.values()];
}
function groupFor(id){const item=find(id);return item?groups().find(g=>g.key===keyFor(item)):null}
function representative(g){return g.members.find(i=>i.instanceId===locatedId)||g.members.find(i=>i.instanceId===selectedId)||g.members.find(i=>i.instanceId===lastLocatedByGroup.get(g.key))||g.members[0]}
function scale(){const f=Math.min(innerWidth/1920,innerHeight/1080);$('stage').style.transform=`scale(${f})`;$('stage').style.left=`${(innerWidth-1920*f)/2}px`;$('stage').style.top=`${(innerHeight-1080*f)/2}px`;hideTooltip()}
function element(tag,className,text){const e=document.createElement(tag);if(className)e.className=className;if(text!==undefined)e.textContent=text;return e}
function icon(item,className){const image=element('img',className);image.src=ICON_ROOT+definition(item).file;image.alt='';image.draggable=false;return image}
function bindHover(anchor,getItem){
 const current=()=>typeof getItem==='function'?getItem():getItem;
 anchor.addEventListener('mouseenter',()=>showTooltip(anchor,current()));
 anchor.addEventListener('mouseleave',()=>{if(lastHoverAnchor===anchor)hideTooltip()});
 anchor.addEventListener('focus',()=>showTooltip(anchor,current()));
 anchor.addEventListener('blur',()=>{if(lastHoverAnchor===anchor)hideTooltip()});
}
function renderIndex(){
 const list=$('index-list'),scroll=list.scrollTop;list.replaceChildren();
 const visible=groups().filter(g=>g.members.some(matches));
 $('index-count').textContent=`${visible.length}种 · ${visible.reduce((n,g)=>n+g.count,0)}件`;
 for(const g of visible){
  const row=element('button','index-row');row.type='button';row.dataset.groupKey=g.key;
  row.setAttribute('aria-label',`${g.name}，${g.count}件`);
  const count=element('span','row-count');count.append(element('span','row-total',String(g.count)));
  row.append(element('span','row-name',g.name),count);
  row.addEventListener('click',()=>selectGroup(g.key));
  row.addEventListener('dblclick',()=>cycleLocateGroup(g.key));
  row.addEventListener('keydown',e=>{if(e.key==='Enter'){e.preventDefault();cycleLocateGroup(g.key)}});
  bindHover(row,()=>representative(groups().find(x=>x.key===g.key)||g));list.append(row);
 }
 if(!visible.length)list.append(element('p','empty-index','没有找到对应物品'));
 list.scrollTop=scroll;updateClasses();
}
function renderBreadcrumb(){
 const box=$('breadcrumbs');box.replaceChildren();
 const bag=element('button','crumb','行囊');bag.type='button';bag.addEventListener('click',()=>openContainer(BAG_ID));box.append(bag);
 if(currentContainerId!==BAG_ID){box.append(element('span','crumb-separator','/'));box.append(element('span','crumb',containers.get(currentContainerId).name))}
 box.append(element('span','state',currentContainerId===BAG_ID?'完整随身空间':'跨容器定位演示'));
}
function renderWorkspace(){
 hideTooltip();renderBreadcrumb();const workspace=$('workspace');workspace.replaceChildren();
 if(currentContainerId===BAG_ID){
  const board=element('div','body-board');board.dataset.containerId=BAG_ID;
  for(const r of REGIONS){
   const box=element('section','region');box.id='region-'+r.id;box.dataset.regionId=r.id;box.style.left=(r.left*BOARD_SCALE)+'px';box.style.top=(r.top*BOARD_SCALE)+'px';box.append(element('h3','',r.name));
   const grid=element('div','region-grid');grid.style.width=r.width*CELL+'px';grid.style.height=r.height*CELL+'px';grid.dataset.columns=r.width;grid.dataset.rows=r.height;
   for(const item of items.filter(i=>i.containerId===BAG_ID&&i.regionId===r.id)){
    const button=element('button','bag-item');button.type='button';button.dataset.instanceId=item.instanceId;button.setAttribute('aria-label',`${definition(item).name}，${r.name}`);
    button.style.left=item.cellX*CELL+'px';button.style.top=item.cellY*CELL+'px';button.style.width=item.width*CELL+'px';button.style.height=item.height*CELL+'px';button.append(icon(item,''));
    if(typeof applyItemRotation==='function')applyItemRotation(button,item);
    button.addEventListener('click',()=>{select(item.instanceId);revealIndex(item.instanceId)});bindHover(button,item);grid.append(button);
   }
   box.append(grid);board.append(box);
  }
  workspace.append(board);const used=items.filter(i=>i.containerId===BAG_ID).reduce((s,i)=>s+i.width*i.height,0);
  $('space-count').textContent=`占用 ${used} / 120 格`;$('space-container-note').textContent='悬停查看物品详情与占格';
 }else{
  const board=element('div','external-board');board.dataset.containerId=currentContainerId;board.append(element('div','external-mode','跨容器定位演示'));
  for(const item of items.filter(i=>i.containerId===currentContainerId)){
   const target=element('button','external-target');target.type='button';target.dataset.instanceId=item.instanceId;target.append(icon(item,''));
   const copy=element('div','');copy.append(element('h3','',definition(item).name),element('p','','已找到对应物品'));target.append(copy);bindHover(target,item);board.append(target);
  }
  const back=element('button','external-return','返回行囊');back.type='button';back.addEventListener('click',()=>openContainer(BAG_ID));board.append(back);workspace.append(board);
  $('space-count').textContent='';$('space-container-note').textContent='仅验证容器定位，不定义戒指容量';
 }
 updateClasses();
}
function updateClasses(){
 const all=groups(),byKey=new Map(all.map(g=>[g.key,g]));
 document.querySelectorAll('[data-instance-id]').forEach(node=>{
  const id=node.dataset.instanceId,item=find(id);if(!item)return;
  node.classList.toggle('is-selected',id===selectedId);node.classList.toggle('is-located',id===locatedId);node.classList.toggle('is-linked',id===hoveredId);
  if(node.classList.contains('bag-item'))node.classList.toggle('is-filtered',!matches(item));
 });
 document.querySelectorAll('.index-row').forEach(row=>{
  const g=byKey.get(row.dataset.groupKey);if(!g)return;
  row.classList.toggle('is-selected',g.key===selectedGroupKey);row.classList.toggle('is-linked',g.members.some(i=>i.instanceId===hoveredId));row.setAttribute('aria-selected',String(g.key===selectedGroupKey));
  const at=g.members.findIndex(i=>i.instanceId===lastLocatedByGroup.get(g.key));let progress=row.querySelector('.row-progress');
  if(at>=0){if(!progress){progress=element('span','row-progress');row.querySelector('.row-count').append(progress)}progress.textContent=`第${at+1}/${g.members.length}件`}
  else progress?.remove();
 });
 document.querySelectorAll('.region').forEach(node=>node.classList.toggle('is-located',locatedId&&find(locatedId)?.regionId===node.dataset.regionId&&currentContainerId===BAG_ID));
}
function selectGroup(key){const g=groups().find(x=>x.key===key);if(!g)return false;selectedGroupKey=key;selectedId=representative(g).instanceId;locatedId=null;$('location-status').textContent='';updateClasses();return true}
function select(id){const item=find(id);if(!item)return false;selectedId=id;selectedGroupKey=keyFor(item);locatedId=null;$('location-status').textContent='';updateClasses();return true}
function revealIndex(id){const g=groupFor(id);if(!g)return;[...document.querySelectorAll('.index-row')].find(row=>row.dataset.groupKey===g.key)?.scrollIntoView({block:'nearest',inline:'nearest',behavior:'instant'})}
function locate(id){
 const item=find(id);if(!item)return false;hideTooltip();selectedId=id;selectedGroupKey=keyFor(item);locatedId=null;
 if(currentContainerId!==item.containerId){currentContainerId=item.containerId;renderWorkspace()}
 locatedId=id;lastLocatedByGroup.set(selectedGroupKey,id);const g=groupFor(id),at=g.members.findIndex(i=>i.instanceId===id);
 updateClasses();$('location-status').textContent=`已定位：${definition(item).name} · 第${at+1}/${g.members.length}件`;
 const target=document.querySelector(`#workspace [data-instance-id="${CSS.escape(id)}"]`);target?.scrollIntoView({block:'center',inline:'center',behavior:'instant'});target?.focus({preventScroll:true});hideTooltip();revealIndex(id);return true;
}
function cycleLocateGroup(key){const g=groups().find(x=>x.key===key);if(!g||!g.members.length)return false;const last=g.members.findIndex(i=>i.instanceId===lastLocatedByGroup.get(key));return locate(g.members[(last+1)%g.members.length].instanceId)}
function openContainer(id){if(!containers.has(id))return false;currentContainerId=id;locatedId=null;$('location-status').textContent='';renderWorkspace();return true}
function showTooltip(anchor,item){
 if(typeof inventoryDrag!=='undefined'&&inventoryDrag?.active)return;
 if(!item||!find(item.instanceId))return;hoveredId=item.instanceId;lastHoverAnchor=anchor;updateClasses();const d=definition(item),tip=$('tooltip');tip.replaceChildren();
 const head=element('div','tooltip-head');head.append(icon(item,'tooltip-icon'));const names=element('div','');names.append(element('div','tooltip-title',d.name),element('div','tooltip-sub',d.hoverIdentity||''));head.append(names);
 const footprint=element('div','tooltip-footprint');footprint.append(element('span','','占格'));const grid=element('div','mini-grid');grid.dataset.columns=item.width;grid.dataset.rows=item.height;grid.style.gridTemplateColumns=`repeat(${item.width},16px)`;
 for(let n=0;n<item.width*item.height;n++)grid.append(element('i',''));footprint.append(grid,element('span','',`${item.width*item.height}格`));head.append(footprint);tip.append(head,element('div','tooltip-description',d.hoverBody||'简介：'));
 tip.dataset.hoverInstanceId=item.instanceId;tip.classList.add('show');tip.setAttribute('aria-hidden','false');const bounds=anchor.getBoundingClientRect(),w=tip.offsetWidth,h=tip.offsetHeight;let x=bounds.right+12;
 if(x+w>innerWidth-16)x=bounds.left-w-12;if(x<16)x=Math.max(16,innerWidth-w-16);tip.style.left=x+'px';tip.style.top=Math.max(16,Math.min(bounds.top,innerHeight-h-16))+'px';
}
function hideTooltip(){hoveredId=null;lastHoverAnchor=null;const tip=$('tooltip');tip.classList.remove('show');tip.setAttribute('aria-hidden','true');delete tip.dataset.hoverInstanceId;updateClasses()}
function setFilter(next={}){if('query'in next){query=String(next.query).trim();$('search').value=query}if('category'in next&&CATEGORY_NAMES.includes(next.category))category=next.category;hideTooltip();renderCategories();renderIndex();return snapshot()}
function updateCategoryArrows(){const nav=$('categories');$('category-prev').disabled=nav.scrollLeft<=1;$('category-next').disabled=nav.scrollLeft>=nav.scrollWidth-nav.clientWidth-1}
function renderCategories(){const nav=$('categories'),scroll=nav.scrollLeft;nav.replaceChildren();for(const name of CATEGORY_NAMES){const b=element('button','category'+(category===name?' active':''),name);b.type='button';b.setAttribute('aria-pressed',String(category===name));b.addEventListener('click',()=>{setFilter({category:name});[...nav.children].find(x=>x.textContent===name)?.scrollIntoView({block:'nearest',inline:'nearest',behavior:'smooth'})});nav.append(b)}nav.scrollLeft=scroll;requestAnimationFrame(updateCategoryArrows)}
function bindCategoryRail(){const nav=$('categories');let drag=null,suppressUntil=0;nav.addEventListener('scroll',updateCategoryArrows);$('category-prev').addEventListener('click',()=>nav.scrollBy({left:-nav.clientWidth*.8,behavior:'smooth'}));$('category-next').addEventListener('click',()=>nav.scrollBy({left:nav.clientWidth*.8,behavior:'smooth'}));nav.addEventListener('wheel',e=>{if(nav.scrollWidth<=nav.clientWidth)return;const before=nav.scrollLeft;nav.scrollLeft+=Math.abs(e.deltaX)>Math.abs(e.deltaY)?e.deltaX:e.deltaY;if(nav.scrollLeft!==before)e.preventDefault()},{passive:false});nav.addEventListener('pointerdown',e=>{if(e.pointerType==='mouse'&&e.button===0)drag={x:e.clientX,start:nav.scrollLeft,id:e.pointerId,moved:false}});nav.addEventListener('pointermove',e=>{if(!drag)return;const delta=e.clientX-drag.x;if(!drag.moved&&Math.abs(delta)>6){drag.moved=true;nav.setPointerCapture(e.pointerId);nav.classList.add('is-dragging')}if(drag.moved){nav.scrollLeft=drag.start-delta;e.preventDefault()}});const finish=()=>{if(drag?.moved)suppressUntil=performance.now()+350;drag=null;nav.classList.remove('is-dragging')};nav.addEventListener('pointerup',finish);nav.addEventListener('pointercancel',finish);nav.addEventListener('click',e=>{if(performance.now()<suppressUntil){e.preventDefault();e.stopImmediatePropagation()}},true)}
function reset(){items=originalItems();containers=new Map([[BAG_ID,{id:BAG_ID,name:'行囊',path:['行囊'],kind:'body',regions:REGIONS}]]);currentContainerId=BAG_ID;selectedId=selectedGroupKey=locatedId=hoveredId=null;memberOrder.clear();lastLocatedByGroup.clear();query='';category='全部';$('search').value='';$('location-status').textContent='';$('categories').scrollTo({left:0,behavior:'instant'});renderCategories();renderIndex();renderWorkspace();$('index-list').scrollTop=0;$('workspace-scroll').scrollTop=0;return snapshot()}
function snapshot(){return JSON.parse(JSON.stringify({currentContainerId,selectedId,selectedGroupKey,locatedId,hoveredId,query,category,visibleIds:items.filter(matches).map(i=>i.instanceId),items:items.map(i=>({...i,name:definition(i).name,file:definition(i).file,path:itemPath(i)})),groups:groups().map(g=>({key:g.key,name:g.name,count:g.count,memberIds:g.members.map(i=>i.instanceId),lastLocatedId:lastLocatedByGroup.get(g.key)||null,nextIndex:(g.members.findIndex(i=>i.instanceId===lastLocatedByGroup.get(g.key))+1)%g.members.length})),containers:[...containers.values()],regionCount:REGIONS.length,totalBagCells:REGIONS.reduce((s,r)=>s+r.width*r.height,0),bagOccupiedCells:items.filter(i=>i.containerId===BAG_ID).reduce((s,i)=>s+i.width*i.height,0)}))}
function enableCrossContainerFixture(){if(!containers.has('fixture:external'))containers.set('fixture:external',{id:'fixture:external',name:'定位演示容器',path:['行囊','定位演示容器'],kind:'fixture',regions:null});const item=find('manual:02');item.containerId='fixture:external';item.regionId='fixture-content';item.cellX=0;item.cellY=0;renderIndex();renderWorkspace();return snapshot()}
function groupingSelfCheck(){
 const d={definitionId:'test:a',resourcePath:'test/a',name:'同名',file:'same.png'};
 const a={instanceId:'a',definitionId:'a'},b={instanceId:'b',definitionId:'a'};
 return {sameDefinitionSameGroup:keyFor(a,d)===keyFor(b,d),sameNameDifferentDefinition:keyFor(a,d)!==keyFor(b,{...d,definitionId:'test:b',resourcePath:'test/b'}),sameIconDifferentDefinition:keyFor(a,d)!==keyFor(b,{...d,definitionId:'test:c'}),differentVariant:keyFor({...a,variantKey:'low'},d)!==keyFor({...b,variantKey:'high'},d),variantFieldOrder:keyFor({...a,variantKey:{x:1,y:2}},d)===keyFor({...b,variantKey:{y:2,x:1}},d),unknownUniqueArtifactsStaySeparate:keyFor({...a,isArtifact:true},d)!==keyFor({...b,isArtifact:true},d)};
}
window.inventoryPrototype=Object.freeze({getSnapshot:snapshot,locate,cycleLocateGroup,locateGroup:cycleLocateGroup,openContainer,setFilter,reset,enableCrossContainerFixture,groupingSelfCheck});
$('search').addEventListener('input',e=>setFilter({query:e.target.value}));$('index-list').addEventListener('scroll',hideTooltip);$('workspace-scroll').addEventListener('scroll',hideTooltip);window.addEventListener('resize',scale);document.addEventListener('keydown',e=>{if(e.key==='Escape')hideTooltip()});bindCategoryRail();reset();scale();
