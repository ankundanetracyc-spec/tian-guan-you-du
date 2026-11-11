(() => {
  'use strict';
  const M = window.Materials, F = window.JOURNAL_FIXTURE;
  const stage = document.querySelector('#stage');
  const state = {section:'lore', selected:'all', completed:false, query:'', tracked:null, empty:false, closed:false};
  const titles = {lore:'见闻', tasks:'任务', records:'记录'};
  const captions = {lore:'峰内见闻', tasks:'任务清册', records:'行事记录'};
  const esc = M.escape;
  const button = (text, attrs='', cls='', kind='button') => `<button data-skin="button" data-button-kind="${kind}" class="${cls}" ${attrs}><span class="button-content">${text}</span></button>`;
  const crop = (path,size,bounds,cls='',stretch=false) => {
    const image=M.atlasView(path,size,bounds,cls);
    return stretch?image.replace('preserveAspectRatio="xMidYMid meet"','preserveAspectRatio="none"'):image;
  };
  const wood = () => crop(window.SKIN_DATA.frameFile,[1672,941],[110,285,850,140],'desk-wood',true);
  const paper = (cls) => `<div class="${cls}" data-skin="paper" aria-hidden="true"></div>`;
  function entries() {
    let list = state.empty ? [] : state.section==='lore' ? F.lore : state.section==='tasks' ? F.tasks.filter(e=>(e.state==='已完成')===state.completed) : F.records;
    const query=state.query.trim().toLocaleLowerCase();
    return query ? list.filter(e=>`${e.title} ${e.body} ${e.place}`.toLocaleLowerCase().includes(query)) : list;
  }
  function selected(list) {return list.find(e=>e.id===state.selected) || (state.section==='lore'&&state.selected==='all' ? null : list[0]);}
  function row(e,index) {
    const active = e.id===state.selected;
    const subtitle = state.section==='tasks' ? e.source : e.subtitle;
    return `<button class="entry-row ${active?'is-selected':''}" data-skin="paper" data-entry="${esc(e.id)}" aria-pressed="${active}"><span><span class="entry-title">${esc(e.title)}</span><span class="entry-subtitle">${esc(subtitle||e.place)}</span></span><span class="entry-index">${String(index+1).padStart(2,'0')}</span>${state.tracked===e.id?M.sprite('checkOn','tracked-mark'):''}</button>`;
  }
  function emptyBody() {
    const title=state.section==='tasks' ? state.completed?'暂无已完成任务':'暂无进行中任务' : '暂无记录';
    const isSearch=state.query.trim().length>0;
    return `<div class="empty-view">${M.sprite('grip')}<h2>${isSearch?'没有相符条目':title}</h2><div class="empty-rule">${M.sprite('divider','',true)}</div><p>${isSearch?'试试其他名称或关键词。':'此处暂未收录新的内容。'}</p>${isSearch?'':'<span class="empty-hint">见闻会随你的经历逐渐补全。</span>'}</div>`;
  }
  function content(list,e) {
    if (!list.length) return emptyBody();
    if (state.section==='tasks') return M.scroll(`<div class="reading-document"><div class="task-goal" data-skin="paper"><div class="goal-label">当前目标</div><p>${esc(e.body)}</p></div></div>`,'detail-scroll');
    if (state.section==='lore'&&state.selected==='all') return M.scroll(`<div class="reading-document continuous-reading">${list.map((item,i)=>`<section class="chapter" data-chapter="${esc(item.id)}"><span class="chapter-number">${String(i+1).padStart(2,'0')}</span><h3>${esc(item.title)}</h3><div class="chapter-subtitle">${esc(item.subtitle)}</div><p>${esc(item.body)}</p></section>`).join('')}</div>`,'detail-scroll');
    return M.scroll(`<div class="reading-document"><div class="lead">${esc(e.subtitle||'')}</div><p>${esc(e.body)}</p></div>`,'detail-scroll');
  }
  function render() {
    const list=entries();
    if (state.selected!=='all' && !list.some(e=>e.id===state.selected)) state.selected=list[0]?.id || null;
    if (state.section!=='lore' && state.selected==='all') state.selected=list[0]?.id || null;
    const e=selected(list);
    const all=state.section==='lore'&&state.selected==='all';
    const title=!list.length ? titles[state.section] : all?'八峰清册':e.title;
    const source=!list.length?'':all?'玉虚仙宗峰内见闻':e.source;
    const place=!list.length?'':all?'玉虚仙宗':e.place;
    const tag=state.section==='tasks'&&e ? e.state : all?'连续阅读':state.section==='lore'?'峰内见闻':'';
    stage.innerHTML=`${button('<span>返回</span><span class="return-triangle" aria-hidden="true"></span>','data-action="close" aria-label="返回"','page-return')}<div class="desk">${wood()}<div class="desk-wash"></div><div class="folder-backing"><span class="binding-art">${crop(window.SKIN_DATA.frames.buttonNavBinding.file,[2172,724],[122,190,82,315],'',true)}</span></div>${paper('folder-back-paper')}<header class="folio-top"><h1>见闻</h1><span class="heading-rule"></span></header><nav class="folder-tabs" role="tablist" aria-label="见闻分类">${['lore','tasks','records'].map(key=>`<button class="folder-tab" role="tab" id="tab-${key}" aria-selected="${state.section===key}" aria-controls="journal-content" tabindex="${state.section===key?'0':'-1'}" data-section="${key}" data-skin="button" data-button-kind="buttonTab"><span class="tab-caption">${titles[key]}</span></button>`).join('')}</nav><span class="fastener" aria-hidden="true">${M.sprite('grip')}</span><section class="folder-front" id="journal-content" role="tabpanel" aria-labelledby="tab-${state.section}" data-skin="paper"><aside class="index-column"><div class="index-heading"><h2>${captions[state.section]}</h2><span class="index-count">${list.length} 条</span></div><label class="search-field" data-skin="input">${M.sprite('search')}<input id="journal-search" type="search" aria-label="检索当前清册" placeholder="检索条目" value="${esc(state.query)}" autocomplete="off" spellcheck="false"></label>${state.section==='tasks'?`<div class="filter-row">${button('进行中',`data-filter="ongoing" aria-pressed="${!state.completed}"`,!state.completed?'filter-button is-selected':'filter-button','buttonTab')}${button('已完成',`data-filter="completed" aria-pressed="${state.completed}"`,state.completed?'filter-button is-selected':'filter-button','buttonTab')}</div>`:''}${M.scroll(`<div class="entry-list">${list.length?list.map(row).join(''):'<div class="list-empty">暂无条目</div>'}</div>`,'list-scroll')}<div class="index-footer">${state.section==='lore'?button(`${M.sprite('divider')}连续阅读`,`data-action="all" aria-pressed="${all}"`,all?'is-selected':'','buttonTab'):`<span class="index-caption"></span>`}</div></aside><article class="reading-column"><header class="reading-heading"><h2 class="reading-title">${esc(title)}</h2>${tag?`<span class="reading-tag" data-skin="paper">${esc(tag)}</span>`:''}</header>${list.length?`<div class="reading-meta"><span><span class="meta-name">来源</span>${esc(source)}</span><span><span class="meta-name">地点</span>${esc(place)}</span></div>`:''}<div class="reading-divider">${M.sprite('divider','',true)}</div>${content(list,e)}<footer class="reading-footer">${state.section==='tasks'&&e?`${state.tracked===e.id?`<span class="track-state">${M.sprite('checkOn')}已追踪</span>`:'<span class="reading-position">未追踪</span>'}${e.state!=='已完成'?button(state.tracked===e.id?'取消追踪':'追踪任务',`data-action="track" aria-pressed="${state.tracked===e.id}"`,'track-button',state.tracked===e.id?'button':'buttonPrimary'):''}`:`<span class="reading-position">${!list.length?'—':all?'共 '+list.length+' 条':String(list.findIndex(item=>item.id===e.id)+1).padStart(2,'0')+' / '+String(list.length).padStart(2,'0')}</span><span class="light-note"></span>`}</footer></article></section>${state.closed?`<div class="closed-card" data-skin="paper"><h2>见闻已收起</h2>${button('继续查看','data-action="open"','','buttonPrimary')}</div>`:''}</div>`;
    M.apply(stage);M.attachScroll(stage);M.apply(document.querySelector('.preview-bar'));
    document.querySelector('#demo-empty').setAttribute('aria-pressed',String(state.empty));
    document.querySelector('#demo-empty').classList.toggle('is-selected',state.empty);
    stage.querySelector('.page-return').disabled=state.closed;
    if(state.closed) {stage.querySelector('.folder-front').inert=true;stage.querySelector('.folder-tabs').inert=true;stage.querySelector('[data-action="open"]').focus();}
    fit();
  }
  function fit(){const viewport=document.querySelector('#viewport');const scale=Math.min(viewport.clientWidth/1672,viewport.clientHeight/941);stage.style.transform=`scale(${scale})`;stage.dataset.scale=String(scale);}
  stage.addEventListener('click',event=>{
    const section=event.target.closest('[data-section]');
    if(section){state.section=section.dataset.section;state.selected=state.section==='lore'?'all':null;state.query='';state.completed=false;render();return;}
    const filter=event.target.closest('[data-filter]');
    if(filter){state.completed=filter.dataset.filter==='completed';state.selected=null;render();return;}
    const entry=event.target.closest('[data-entry]');
    if(entry){state.selected=entry.dataset.entry;render();return;}
    const action=event.target.closest('[data-action]')?.dataset.action;
    if(action==='all'){state.selected='all';render();}
    if(action==='track'){const e=selected(entries());if(e){state.tracked=state.tracked===e.id?null:e.id;render();}}
    if(action==='close'){state.closed=true;render();}
    if(action==='open'){state.closed=false;render();}
  });
  stage.addEventListener('input',event=>{
    if(event.target.id!=='journal-search')return;
    const caret=event.target.selectionStart;state.query=event.target.value;render();
    const input=document.querySelector('#journal-search');input.focus();try{input.setSelectionRange(caret,caret);}catch{}
  });
  stage.addEventListener('keydown',event=>{
    if(event.key==='Escape'){event.preventDefault();state.closed=!state.closed;render();return;}
    if(event.target.matches('[role="tab"]')&&['ArrowLeft','ArrowRight','Home','End'].includes(event.key)){
      event.preventDefault();const keys=['lore','tasks','records'];let index=keys.indexOf(state.section);
      index=event.key==='Home'?0:event.key==='End'?2:(index+(event.key==='ArrowRight'?1:2))%3;
      state.section=keys[index];state.selected=state.section==='lore'?'all':null;state.query='';state.completed=false;render();document.querySelector('#tab-'+state.section).focus();
    }
  });
  document.querySelector('#demo-empty').addEventListener('click',()=>{state.empty=!state.empty;render();});
  document.querySelector('#demo-long').addEventListener('click',()=>{state.section='lore';state.selected='all';state.query='';state.empty=false;state.closed=false;render();});
  document.querySelector('#demo-reset').addEventListener('click',()=>{Object.assign(state,{section:'lore',selected:'all',completed:false,query:'',tracked:null,empty:false,closed:false});render();});
  window.addEventListener('resize',fit);window.addEventListener('keydown',event=>{if(event.key==='Escape'&&!event.defaultPrevented&&!stage.contains(event.target)){event.preventDefault();state.closed=!state.closed;render();}});
  window.JournalPreview={getState:()=>structuredClone(state),getEntries:()=>structuredClone(entries()),render,reset:()=>document.querySelector('#demo-reset').click()};
  render();
})();
