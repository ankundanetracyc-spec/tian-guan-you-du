async(page)=>{
 await page.waitForFunction(()=>window.reviewState&&reviewState().loaded===0);
 const checks=[];const state=await page.evaluate(()=>reviewState());
 checks.push({name:'portable_counts_and_images',pass:state.nodes===123&&state.built===53&&state.assets===9&&state.loaded===0,detail:state});
 await page.getByRole('button',{name:'玉虚',exact:true}).click();
 checks.push({name:'portable_actual_scale',pass:await page.evaluate(()=>reviewState().scale===1&&reviewState().selected==='bc_place_001'),detail:await page.evaluate(()=>reviewState())});
 await page.getByRole('button',{name:'素材与大小',exact:true}).click();const cards=await page.locator('#cards .card').count();
 checks.push({name:'portable_assets_page',pass:cards===9&&await page.locator('#catalog').isVisible(),detail:{cards}});
 const resources=await page.evaluate(()=>{const prefix=new URL('.',location.href).href;return {prefix,resources:[...document.images].map(i=>i.src).concat([...document.scripts].filter(s=>s.src).map(s=>s.src)),outside:[...document.images].map(i=>i.src).concat([...document.scripts].filter(s=>s.src).map(s=>s.src)).filter(u=>!u.startsWith(prefix)&&!u.startsWith('data:'))};});
 checks.push({name:'portable_no_external_asset_dependencies',pass:resources.outside.length===0,detail:resources});
 await page.evaluate(c=>window.__portable=c,checks);return checks;
}
