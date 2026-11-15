async page => {
  await page.setViewportSize({width:1920,height:1080});
  await page.waitForFunction(() => window.previewReady);
  await page.evaluate(async () => {
    await document.fonts.ready;
    const image = new Image();
    image.src = getComputedStyle(document.querySelector('.cloth-ground')).backgroundImage.match(/url\(["']?(.*?)["']?\)/)[1];
    await image.decode();
    for (const element of document.querySelector('#stage').children)
      if (!['paper right-paper','cloth-ground'].includes(element.className) && !element.classList.contains('fold-hints')) element.style.visibility='hidden';
    await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));
  });
  await page.screenshot({path:'textures/ui/inventory_material_20261107/garment_inventory_panel_v5.png',clip:{x:458,y:124,width:1416,height:906},scale:'css'});
  return {size:[1416,906],source:'v5 native panel background, no grid, text or items'};
}
