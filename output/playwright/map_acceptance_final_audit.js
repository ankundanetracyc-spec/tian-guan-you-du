async(page)=>{
 const checks=[];
 await page.locator('#category').selectOption('city');
 await page.getByRole('button',{name:'玉虚',exact:true}).click();
 checks.push({name:'main_button_clears_incompatible_filter',pass:await page.locator('.site[data-node=bc_place_001]').isVisible(),detail:{category:await page.locator('#category').inputValue(),scale:await page.evaluate(()=>reviewState().scale)}});
 await page.locator('#category').selectOption('all');await page.getByRole('checkbox',{name:'自然地点',exact:true}).uncheck();
 const wild=await page.evaluate(()=>MAP_REVIEW.nodes.find(n=>!n.asset));
 await page.locator('#list .entry').filter({hasText:wild.name}).click();
 checks.push({name:'natural_list_auto_enables_layer',pass:(await page.getByRole('checkbox',{name:'自然地点',exact:true}).isChecked())&&(await page.locator('.site[data-node='+wild.id+']').isVisible()),detail:{id:wild.id,selected:await page.evaluate(()=>reviewSelected)}});
 await page.getByRole('checkbox',{name:'自然地点',exact:true}).uncheck();await page.getByRole('button',{name:'玉虚',exact:true}).click();await page.locator('#list').evaluate(e=>e.scrollTop=0);
 await page.evaluate(checks=>{window.__audit.checks.push(...checks);window.__audit.failed=window.__audit.checks.filter(x=>!x.pass);window.__audit.total=window.__audit.checks.length;window.__audit.finalState=reviewState();window.__audit.console_note='Final reload uses data favicon; document images all loaded.';},checks);
 return checks;
}
