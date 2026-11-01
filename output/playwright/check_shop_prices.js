async page=>{
 await page.setViewportSize({width:1920,height:1080});await page.locator('#shop-tab').click();
 const result=await page.evaluate(()=>({count:PREVIEW_DATA.length,allNumeric:PREVIEW_DATA.every(i=>Number.isInteger(i.contribution)&&i.contribution>=0),pills:PREVIEW_DATA.filter(i=>i.category==='丹药').map(i=>[i.name,i.contribution]),sourcePrices:{iron:PREVIEW_DATA.find(i=>i.id==='mat_xuantie').contribution,manual:PREVIEW_DATA.find(i=>i.id==='manual_huangting_yangxing').contribution,sword:PREVIEW_DATA.find(i=>i.id==='sect_sword_suxing').contribution},visibleCells:[...document.querySelectorAll('.shop-column .row')].every(row=>/^\d+$/.test(row.lastElementChild.textContent))}));
 if(!result.allNumeric||!result.visibleCells||result.sourcePrices.iron!==15||result.sourcePrices.manual!==15||result.sourcePrices.sword!==0)throw Error(JSON.stringify(result));
 await page.locator('[data-category="丹药"] .row').filter({hasText:'良效'}).click();if(!(await page.locator('#shop-summary').textContent()).includes('所需贡献 8'))throw Error('pill footer price mismatch');
 await page.locator('[data-category="秘籍"] .row').filter({hasText:'玄枢含章经'}).click();if(!(await page.locator('#shop-summary').textContent()).includes('所需贡献 15'))throw Error('manual footer price mismatch');
 await page.mouse.move(100,100);await page.screenshot({path:'output/playwright/shop-priced.png'});return {...result,footerPriceBinding:true};
}
