async (page) => {
 const out={browser:'Microsoft Edge',viewport:{width:1920,height:1080},checks:[]};
 const add=(name,pass,detail)=>out.checks.push({name,pass,detail});
 await page.waitForFunction(()=>window.reviewState&&window.reviewState().loaded===0);
 let state=await page.evaluate(()=>window.reviewState());add('image_loading',state.loaded===0,state);
 const data=await page.evaluate(()=>({nodes:MAP_REVIEW.nodes.length,built:MAP_REVIEW.nodes.filter(n=>n.asset).length,assets:MAP_REVIEW.assets.length,siteButtons:document.querySelectorAll('.site').length,images:document.images.length,assetGroups:MAP_REVIEW.nodes.filter(n=>n.asset).reduce((a,n)=>{const k=n.type==='sect'?(n.level===1?'main':'local'):n.type;(a[k]??=[]).push(n.asset);return a;},{})}));
 add('counts',data.nodes===123&&data.built===53&&data.assets===9&&data.siteButtons===123,data);
 const groups=data.assetGroups;add('template_reuse',new Set(groups.main).size===3&&Object.entries(groups).filter(([k])=>k!=='main').every(([,v])=>new Set(v).size===1),Object.fromEntries(Object.entries(groups).map(([k,v])=>[k,[...new Set(v)]])));
 for (const [short,name] of [['玉虚','玉虚仙宗'],['玄都','玄都天府'],['无相','无相圣宗']]){
  await page.getByRole('button',{name:short,exact:true}).click();state=await page.evaluate(()=>({state:reviewState(),info:document.querySelector('#info').textContent}));add('main_focus_'+short,state.state.scale===1&&state.info.includes(name),state);
 }
 await page.getByRole('button',{name:'全图',exact:true}).click();
 const search=page.getByRole('searchbox',{name:'查找地点'});await search.fill('玉虚');
 let names=await page.locator('#list .entry').allTextContents();add('search',names.length===1&&names[0].includes('玉虚仙宗'),names);await search.fill('');
 const category=page.locator('#category');
 for(const [cat,count] of [['main',3],['sect',13],['city',16],['town',14],['market',5],['village',3],['dock',2],['wild',70],['all',123]]){
  await category.selectOption(cat);let countActual=await page.locator('#list .entry').count();add('category_'+cat,countActual===count,{expected:count,actual:countActual});
 }
 await page.getByRole('checkbox',{name:'名称',exact:true}).uncheck();add('hide_names',await page.locator('#world').evaluate(el=>el.classList.contains('nonames')),null);await page.getByRole('checkbox',{name:'名称',exact:true}).check();
 await page.getByRole('checkbox',{name:'节点',exact:true}).uncheck();add('hide_nodes',await page.locator('#world').evaluate(el=>el.classList.contains('nonodes')),null);await page.getByRole('checkbox',{name:'节点',exact:true}).check();
 let wildVisible=await page.locator('.site.wild:visible').count();await page.getByRole('checkbox',{name:'自然地点',exact:true}).check();let visibleAfter=await page.locator('.site.wild:visible').count();add('natural_toggle',wildVisible===0&&visibleAfter===70,{before:wildVisible,after:visibleAfter});await page.getByRole('checkbox',{name:'自然地点',exact:true}).uncheck();
 await page.getByRole('checkbox',{name:'九宫格',exact:true}).check();add('grid_toggle',await page.locator('#grid').isVisible(),null);await page.getByRole('checkbox',{name:'九宫格',exact:true}).uncheck();
 await page.getByRole('button',{name:'全图',exact:true}).click();const before=await page.locator('#world').evaluate(e=>e.style.transform);
 await page.mouse.move(1000,600);await page.mouse.down();await page.mouse.move(1120,660,{steps:12});await page.mouse.up();
 const after=await page.locator('#world').evaluate(e=>e.style.transform);add('drag_pan',before!==after,{before,after});
 const beforeWheel=await page.evaluate(()=>reviewState().scale);await page.mouse.wheel(0,700);await page.waitForTimeout(150);const afterWheel=await page.evaluate(()=>reviewState().scale);add('wheel_zoom_locked',beforeWheel===afterWheel,{before:beforeWheel,after:afterWheel});
 await page.getByRole('button',{name:'实际尺寸',exact:true}).click();add('actual_scale',await page.evaluate(()=>reviewState().scale===1),null);
 await page.getByRole('button',{name:'素材与大小',exact:true}).click();let cards=await page.locator('#cards .card').count();add('assets_page',await page.locator('#catalog').isVisible()&&cards===9,{cards});
 const rendered=await page.locator('#cards .sample img').evaluateAll(xs=>xs.map(i=>({width:i.width,height:i.height,naturalWidth:i.naturalWidth,naturalHeight:i.naturalHeight,complete:i.complete})));
 add('asset_actual_sizes',rendered.length===9&&rendered.every(i=>i.complete&&i.width===i.naturalWidth&&i.height===i.naturalHeight),rendered);
 await page.getByRole('button',{name:'查看地图',exact:true}).click();await page.getByRole('button',{name:'玉虚',exact:true}).click();
 const site=page.getByRole('button',{name:'玉虚仙宗',exact:true});await site.hover();let filter=await site.locator('img').evaluate(e=>getComputedStyle(e).filter);await site.click();add('site_hover_click',filter!=='none'&&(await page.evaluate(()=>reviewSelected))==='bc_place_001',{filter,selected:await page.evaluate(()=>reviewSelected)});
 await page.getByRole('button',{name:'全图',exact:true}).click();
 out.failed=out.checks.filter(x=>!x.pass);out.total=out.checks.length;out.finalState=await page.evaluate(()=>reviewState());await page.evaluate(v=>window.__audit=v,out);return out;
}
