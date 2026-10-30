async (page) => {
 const stone=page.getByRole('button',{name:'灵石，12件',exact:true});
 for(let i=0;i<4;i++)await stone.dblclick({delay:35});
 await page.screenshot({path:'output/playwright/inventory-index-compact-located.png'});
 await page.getByRole('button',{name:'胎息经，2件',exact:true}).hover();
 await page.screenshot({path:'output/playwright/inventory-index-compact-hover.png'});
 await page.getByRole('button',{name:'下一组分类',exact:true}).click();await page.waitForTimeout(400);
 await page.screenshot({path:'output/playwright/inventory-index-compact-categories.png'});
 await page.setViewportSize({width:1280,height:720});await page.screenshot({path:'output/playwright/inventory-index-compact-1280.png'});
 await page.setViewportSize({width:1920,height:1080});
 return {located:await page.evaluate(()=>window.inventoryPrototype.getSnapshot().locatedId),groups:await page.locator('.index-row').count(),panelWidth:await page.locator('.index-panel').evaluate(x=>x.getBoundingClientRect().width)};
}
