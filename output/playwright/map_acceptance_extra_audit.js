async (page) => {
 const extra=[];const add=(name,pass,detail)=>extra.push({name,pass,detail});
 await page.getByRole('button',{name:'素材与大小',exact:true}).click();
 await page.locator('#list .entry').first().click();
 add('asset_page_sidebar_returns_map',!(await page.locator('#catalog').isVisible())&&(await page.evaluate(()=>reviewState().scale))===1,await page.evaluate(()=>reviewState()));
 await page.getByRole('button',{name:'全图',exact:true}).click();
 await page.getByRole('checkbox',{name:'自然地点',exact:true}).check();
 const firstWild=await page.locator('.site.wild').first().getAttribute('data-node');
 await page.locator('.site.wild').first().click();
 add('natural_site_click',await page.evaluate(id=>reviewSelected===id,firstWild),{selected:await page.evaluate(()=>reviewSelected),expected:firstWild});
 await page.getByRole('checkbox',{name:'自然地点',exact:true}).uncheck();
 const expected=await page.evaluate(()=>MAP_REVIEW.nodes.map(n=>({id:n.id,name:n.name})));
 let failures=[];
 for(let i=0;i<expected.length;i++){
  await page.locator('#list .entry').nth(i).click();
  const s=await page.evaluate(()=>({selected:reviewSelected,scale:reviewState().scale,info:document.querySelector('#info').textContent}));
  if(s.selected!==expected[i].id||s.scale!==1||!s.info.includes(expected[i].name))failures.push({i,expected:expected[i],actual:s});
 }
 add('all_123_list_selection',expected.length===123&&failures.length===0,{tested:expected.length,failures});
 await page.getByRole('button',{name:'全图',exact:true}).click();
 await page.evaluate(extra=>{window.__audit.checks.push(...extra);window.__audit.failed=window.__audit.checks.filter(x=>!x.pass);window.__audit.total=window.__audit.checks.length;window.__audit.finalState=reviewState();window.__audit.console_note='Initial favicon.ico 404 only; all document images loaded.';},extra);
 return await page.evaluate(()=>window.__audit);
}
