(() => {
"use strict";
const W=1664,H=936;
const books=window.LIBRARY_BOOKS;
const state={view:new URLSearchParams(location.search).get("view")==="shop"?"shop":"library",keyword:"全部",query:"",selected:"taihe",tab:"summary"};
const stage=document.getElementById("stage");
const slots={xuanshu:[235,272],taihe:[663,272],baihai:[381,467],zhuoji:[858,467],shujuan:[554,663]};
const attributeNames={physique:"体魄",intelligence:"灵慧",soul:"神魂",body_proficiency:"体道造诣",magic_proficiency:"术法造诣",sword_proficiency:"剑道造诣",formation_proficiency:"阵法造诣",alchemy_proficiency:"丹道造诣",forging_proficiency:"器道造诣"};
const reader=window.LIBRARY_PREVIEW_PROFILE;
let stageScale=1;
function node(tag,cls,text){const n=document.createElement(tag);if(cls)n.className=cls;if(text!==undefined)n.textContent=text;return n;}
function realmIndex(realm){
 if(!realm||realm==="凡人")return 0;
 const names=["一","二","三","四","五","六","七","八","九","十"];
 const match=realm.match(/^云息([一二三四五六七八九十]|\d+)层$/);
 return match?(names.includes(match[1])?names.indexOf(match[1])+1:Number(match[1])):null;
}
function requirementRows(book,chapter,includeRealm=false){
 const rows=[],root=book.requirements||{};
 if(root.root==="NONE"||!root.root)rows.push({met:true,label:"灵根：不限",current:""});
 else if(root.root==="ALL")rows.push({met:reader.roots.length>0,label:"具有灵根",current:reader.roots.length?reader.roots.join("、"):"无灵根"});
 else{
  const roots=root.root.split("_"),met=root.rootMode==="AND"?roots.every(v=>reader.roots.includes(v)):roots.some(v=>reader.roots.includes(v));
  rows.push({met,label:roots.join(root.rootMode==="AND"?"、":"或")+"灵根",current:reader.roots.join("、")||"无灵根"});
 }
 const realmCandidates=[root.realm,chapter?.realm].filter(Boolean);
 const realm=realmCandidates.reduce((best,next)=>!best||(realmIndex(next)??-1)>(realmIndex(best)??-1)?next:best,"");
 if(realm&&(includeRealm||realm!=="凡人")){const current=realmIndex(reader.realm),required=realmIndex(realm);rows.push({met:current!==null&&required!==null&&current>=required,label:"境界 ≥ "+realm,current:"当前 "+reader.realm});}
 const attrs={...(root.attributes||{})};
 for(const [key,threshold]of Object.entries(chapter?.attributes||{}))attrs[key]=Math.max(attrs[key]??-Infinity,threshold);
 for(const [key,threshold]of Object.entries(attrs)){const current=reader.attributes[key]??0;rows.push({met:current>=threshold,label:(attributeNames[key]||key)+" ≥ "+threshold,current:"当前 "+current});}
 for(const id of chapter?.prerequisites||[]){
  const index=book.chapters.findIndex(c=>c.id===id),name=index<0?"未标注卷章":"第"+(index+1)+"卷·"+book.chapters[index].name;
  const met=(reader.learned[book.id]||[]).includes(id);rows.push({met,label:"已研习"+name,current:met?"已研习":"未研习"});
 }
 if(chapter?.requiresMain)rows.push({met:reader.mainBook===book.id,label:"需主修本秘籍",current:reader.mainBook===book.id?"已设为主修":"未设为主修"});
 return rows;
}
function appendRequirements(parent,book,chapter,includeRealm=false){
 const list=node("ul","requirement-list");list.setAttribute("aria-label","逐项修习条件");
 for(const result of requirementRows(book,chapter,includeRealm)){
  const line=node("li","requirement-row "+(result.met?"met":"unmet"));
  line.setAttribute("aria-label",(result.met?"满足：":"不满足：")+result.label+(result.current?"，"+result.current:""));
  const mark=node("span","requirement-mark",result.met?"✓":"✕");mark.setAttribute("aria-hidden","true");
  line.append(mark,node("span","requirement-label",result.label));if(result.current)line.append(node("span","requirement-current",result.current));list.append(line);
 }
 parent.append(list);
}
function updateSize(){stageScale=Math.min(innerWidth/W,innerHeight/H);stage.style.setProperty("--scale",stageScale);fitTitles();}
function fitTitles(){
 for(const book of books){
  const button=document.querySelector('[data-book="'+book.id+'"]');if(!button)continue;
  const label=button.querySelector(".spine-label"),chars=Array.from(book.name);
  const availableH=label.clientHeight-8,availableW=label.clientWidth-4;
  const size=Math.max(10,Math.min(18,Math.floor(availableH/(chars.length*1.005)),availableW));
  const column=node("span","spine-column",book.name);column.style.fontSize=size+"px";label.replaceChildren(column);
  button.dataset.titleSize=String(size);button.dataset.titleColumns="1";
 }
}
function matches(book){return(state.keyword==="全部"||book.tags.includes(state.keyword))&&(!state.query||book.name.includes(state.query));}
function updateFocus(){
 for(const book of books){const button=document.querySelector('[data-book="'+book.id+'"]');button.classList.toggle("is-dim",!matches(book));button.classList.toggle("selected",state.selected===book.id);button.setAttribute("aria-pressed",String(state.selected===book.id));}
 for(const button of document.querySelectorAll("[data-keyword]")){const active=button.dataset.keyword===state.keyword;button.classList.toggle("active",active);button.setAttribute("aria-pressed",String(active));}
}
function addSection(parent,title,text){const section=node("section","detail-section");section.append(node("h3","",title),node("p","",text));parent.append(section);}
function renderDetail(){
 const book=books.find(b=>b.id===state.selected);
 document.getElementById("detail-title").textContent=book.name;
 const tags=document.getElementById("detail-tags");tags.replaceChildren(...book.tags.map(tag=>node("span","detail-tag",tag)));
 const body=document.getElementById("detail-body");body.replaceChildren();body.scrollTop=0;
 for(const button of document.querySelectorAll("[data-tab]"))button.classList.toggle("active",button.dataset.tab===state.tab);
 if(state.tab==="summary"){
  addSection(body,"修习境界",book.start+"至"+book.end);
  const eligibility=node("section","detail-section");eligibility.append(node("h3","","修习要求（入门）"));appendRequirements(eligibility,book,book.chapters[0]);body.append(eligibility);
  addSection(body,"简介",book.description);
 }else{
  book.chapters.forEach((chapter,index)=>{const line=node("section","chapter-line");line.append(node("h3","","第"+(index+1)+"卷 · "+chapter.name));appendRequirements(line,book,chapter,true);body.append(line);});
 }
}
function selectBook(id){if(!books.some(b=>b.id===id))return;state.selected=id;updateFocus();renderDetail();}
const bookRoot=document.getElementById("books");
for(const book of books){
 const button=node("button","book-slot");button.type="button";button.dataset.book=book.id;button.setAttribute("aria-label",book.name);
 const [x,y]=slots[book.id];button.style.left=x+"px";button.style.top=y+"px";
 const visual=node("span","book-visual");visual.setAttribute("aria-hidden","true");
 const body=node("span","book-body");body.append(node("span","fore-edge"),node("span","tie top"),node("span","tie bottom"),node("span","spine-label"));visual.append(body);button.append(visual);
 button.addEventListener("click",()=>selectBook(book.id));bookRoot.append(button);
}
const keywordRoot=document.getElementById("keywords");
const keywordList=["全部",...new Set(books.flatMap(book=>book.tags))];
for(const keyword of keywordList){const button=node("button","keyword",keyword);button.type="button";button.dataset.keyword=keyword;button.addEventListener("click",()=>{state.keyword=keyword;updateFocus();});keywordRoot.append(button);}
document.getElementById("book-search").addEventListener("input",event=>{state.query=event.target.value.trim();updateFocus();});
for(const button of document.querySelectorAll("[data-tab]"))button.addEventListener("click",()=>{state.tab=button.dataset.tab;renderDetail();});
for(const button of document.querySelectorAll('[aria-disabled="true"]'))button.addEventListener("click",event=>event.preventDefault());
const categories=["材料","器物","丹药","装备"];
let selectedShop=window.SHOP_ITEMS.find(item=>item.name==="回春丹·良效");
function renderShop(){
 const query=document.getElementById("shop-search").value.trim();const root=document.getElementById("shop-columns");root.replaceChildren();
 for(const category of categories){
  const column=node("section","shop-column");column.append(node("h2","",category));
  const fields=node("div","shop-fields");for(const label of ["名称","库存","贡献"])fields.append(node("span","",label));column.append(fields);
  const list=node("div","shop-list");
  for(const item of window.SHOP_ITEMS.filter(item=>item.category===category&&(!query||item.name.includes(query)))){
   const row=node("button","shop-row"+(selectedShop.id===item.id?" selected":""));row.type="button";row.dataset.item=item.id;row.append(node("span","",item.name),node("span","","—"),node("span","",String(item.cost)));row.addEventListener("click",()=>{selectedShop=item;renderShop();});list.append(row);
  }
  column.append(list);root.append(column);
 }
 document.getElementById("shop-summary").textContent="已选："+selectedShop.name+"  |  数量 1  |  所需贡献 "+selectedShop.cost;
}
document.getElementById("shop-search").addEventListener("input",renderShop);
document.getElementById("library").hidden=state.view!=="library";document.getElementById("shop").hidden=state.view!=="shop";
document.title=state.view==="shop"?"善功堂 · 原布局素材预览":"玄览峰 · 关键词与抽拉预览";
window.inspectLibraryPreview=()=>({view:state.view,keyword:state.keyword,selected:state.selected,scale:stageScale,books:books.map(book=>{const slot=document.querySelector('[data-book="'+book.id+'"]');const rect=slot.getBoundingClientRect();const visual=slot.querySelector(".book-body").getBoundingClientRect();return{id:book.id,name:book.name,tags:book.tags,x:rect.x,y:rect.y,width:rect.width,height:rect.height,visualX:visual.x,visualY:visual.y,visualWidth:visual.width,visualHeight:visual.height,dim:slot.classList.contains("is-dim"),fontSize:Number(slot.dataset.titleSize),columns:Number(slot.dataset.titleColumns),labelFits:[...slot.querySelectorAll(".spine-column")].every(col=>col.getBoundingClientRect().height<slot.querySelector(".spine-label").getBoundingClientRect().height)};})});
addEventListener("resize",updateSize);updateFocus();renderDetail();renderShop();updateSize();document.fonts.ready.then(updateSize);
})();
