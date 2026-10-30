async (page) => {
 const checks=[];const check=(name,ok,detail)=>{if(!ok)throw new Error(name+': '+JSON.stringify(detail));checks.push({name,detail})};
 const nav=page.locator('#categories');const metrics=()=>nav.evaluate(n=>({left:n.scrollLeft,width:n.clientWidth,total:n.scrollWidth,names:[...n.children].map(x=>x.textContent)}));
 const first=await metrics();check('all_original_ten_categories',JSON.stringify(first.names)===JSON.stringify(['全部','装备','材料','秘籍','阵盘','器具','符箓','丹药','灵膳','器物']),first);
 check('categories_overflow_in_narrow_list',first.total>first.width,{width:first.width,total:first.total});
 await page.getByRole('button',{name:'下一组分类',exact:true}).click();await page.waitForTimeout(400);check('right_arrow_scrolls',(await metrics()).left>0,await metrics());
 await page.getByRole('button',{name:'器物',exact:true}).click();check('last_category_can_be_selected',await page.locator('.index-row').count()===1,{category:await page.evaluate(()=>window.inventoryPrototype.getSnapshot().category)});
 await page.getByRole('button',{name:'灵膳',exact:true}).click();check('empty_category_keeps_actual_inventory',await page.locator('.index-row').count()===0&&await page.locator('.bag-item').count()===19,{});
 await page.evaluate(()=>window.inventoryPrototype.reset());await page.waitForTimeout(50);
 const rect=await nav.boundingBox();await page.mouse.move(rect.x+rect.width-18,rect.y+rect.height/2);await page.mouse.down();await page.mouse.move(rect.x+18,rect.y+rect.height/2,{steps:12});await page.mouse.up();await page.waitForTimeout(100);
 check('drag_scrolls_without_selecting_tab',(await metrics()).left>60&&(await page.evaluate(()=>window.inventoryPrototype.getSnapshot().category))==='全部',{scroll:(await metrics()).left});
 await page.evaluate(()=>window.inventoryPrototype.reset());await page.waitForTimeout(80);await page.mouse.move(rect.x+rect.width/2,rect.y+rect.height/2);await page.mouse.wheel(0,210);await page.waitForTimeout(200);
 check('wheel_scrolls_categories',(await metrics()).left>60,await metrics());
 await page.getByRole('button',{name:'上一组分类',exact:true}).click();await page.waitForTimeout(400);check('left_arrow_returns',Math.round((await metrics()).left)===0,await metrics());
 await page.getByRole('button',{name:'下一组分类',exact:true}).click();await page.waitForTimeout(400);await page.screenshot({path:'output/playwright/inventory-index-categories-scrolled.png'});
 await page.evaluate(()=>window.inventoryPrototype.reset());
 return {checks:checks.length,results:checks};
}
