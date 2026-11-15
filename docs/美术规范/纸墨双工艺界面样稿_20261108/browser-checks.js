async (page) => {
  const checks=[],errors=[],badResponses=[],captures=[];
  const check=(name,pass,detail=null)=>{checks.push({name,pass:!!pass,detail});if(!pass)throw new Error(name+' '+JSON.stringify(detail));};
  const root='E:/stacklands/docs/美术规范/纸墨双工艺界面样稿_20261108/';
  const storageKey='paper-ink-ui-preview-20261108-v1';
  page.on('pageerror',e=>errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)badResponses.push({url:r.url(),status:r.status()});});
  const settle=async()=>{await page.evaluate(()=>document.fonts.ready);await page.waitForTimeout(70);};
  const inspect=()=>page.evaluate(()=>PAPER_INK_PREVIEW.getState());
  const reset=async()=>{await page.evaluate(()=>PAPER_INK_PREVIEW.reset());await settle();};
  const action=async name=>{await page.locator('[data-action="'+name+'"]').first().click();await settle();};
  const craft=async name=>{await page.locator('button[data-craft="'+name+'"]').click();await settle();};
  const materialId=name=>page.evaluate(n=>PAPER_INK_CATALOG.materials.find(m=>m.name===n).id,name);
  const shot=async name=>{await page.screenshot({path:root+name});captures.push(name);};
  const addToPicker=async name=>{await page.locator('#material-search').fill(name);await page.locator('#picker-grid [data-pick]').first().click();await settle();};
  const chooseSpec=async id=>{await page.locator('.target-select').click();await page.locator('[data-spec="'+id+'"]').click();await action('use-spec');};
  await page.reload();await page.waitForLoadState('networkidle');await settle();
  const originalStorage=await page.evaluate(key=>localStorage.getItem(key),storageKey);
  try {
    for(const [width,height] of [[1920,1080],[1280,720]]) {
      await page.setViewportSize({width,height});await reset();
      for(const mode of ['paper','ink']) {
        await craft(mode);await settle();
        const geometry=await page.evaluate(()=>{
          const selectors=['.game-header','.materials','.work-title','.batch','.target','.spirit-section','#start-button'];
          const escaped=selectors.filter(s=>{const r=document.querySelector(s).getBoundingClientRect();return r.left<-.6||r.top<-.6||r.right>innerWidth+.6||r.bottom>innerHeight+.6;});
          const batch=document.querySelector('.batch').getBoundingClientRect(),relation=document.querySelector('#relation-strip').getBoundingClientRect();
          const target=document.querySelector('.target').getBoundingClientRect(),start=document.querySelector('#start-button').getBoundingClientRect();
          const list=document.querySelector('#material-list');
          const back=document.querySelector('.back'),b=back.getBoundingClientRect(),header=document.querySelector('.game-header').getBoundingClientRect();
          return {escaped,fields:document.querySelectorAll('#field-list>div').length,loaded:[...document.images].filter(i=>i.offsetParent!==null).every(i=>i.complete&&i.naturalWidth>0),relationFits:relation.bottom<=batch.bottom-5,startFits:start.bottom<=target.bottom-5,cardsFit:list.scrollHeight<=list.clientHeight+2,returnFits:Math.abs(b.right-header.right)<1&&Math.abs(b.top-header.top)<1&&back.getAttribute('aria-label')==='返回'&&!!back.querySelector('.return-triangle')};
        });
        check(width+'/'+mode+': panels stay in viewport',!geometry.escaped.length,geometry);
        check(width+'/'+mode+': five visible fields and images loaded',geometry.fields===5&&geometry.loaded,geometry);
        check(width+'/'+mode+': summary and start fit',geometry.relationFits&&geometry.startFits,geometry);
        check(width+'/'+mode+': four initial material cards fit',geometry.cardsFit,geometry);
        check(width+'/'+mode+': return sits at upper right with separate triangle',geometry.returnFits,geometry);
        await shot((width===1920?(mode==='paper'?'01_制符纸_1920.png':'02_研墨_1920.png'):(mode==='paper'?'06_制符纸_1280.png':'07_研墨_1280.png')));
        if(width===1920){
          await action('spirit');
          check(mode+': spiritual field explains soft adaptation without realm gate',(await page.locator('.field-explanation').innerText()).includes('不按境界或纸墨品阶直接禁用')&&await page.locator('.dialog h2').innerText()===(mode==='paper'?'承灵品阶':'灵蕴品阶'));
          await shot(mode==='paper'?'11_承灵品阶说明.png':'12_灵蕴品阶说明.png');await action('close');
        }
      }
    }
    await page.setViewportSize({width:1920,height:1080});await reset();
    const catalog=await page.evaluate(()=>PAPER_INK_PREVIEW.getCatalogSummary());
    check('paper scope contains 23 of 50 source materials',catalog.total===50&&catalog.eligible===23,catalog);
    await action('materials');
    check('picker opens with 23 eligible materials',await page.locator('#picker-grid [data-pick]').count()===23);
    await page.locator('#show-all').check();
    check('show all reveals 27 disabled materials',await page.locator('#picker-grid [data-pick]:disabled').count()===27);
    await page.locator('#texture-filter').selectOption('致密');
    check('dense material is visible but cannot enter paper',await page.locator('#picker-grid [data-pick]').count()===22&&await page.locator('#picker-grid [data-pick]:enabled').count()===0);
    await action('reset-filters');
    await page.locator('#element-filter').selectOption('水');
    check('element filter matches actual data',await page.locator('#picker-grid [data-pick]').count()===5);
    await action('reset-filters');await page.locator('[data-filter-utility="藏"]').click();
    const storedFilter=await page.locator('#picker-grid [data-pick]').count();
    check('utility filter narrows selectable materials',storedFilter>0&&storedFilter<23,storedFilter);
    await action('reset-filters');await addToPicker('山蚕长丝');await addToPicker('山蚕长丝');
    const silkId=await materialId('山蚕长丝');
    check('repeated selection merges one material entry',(await inspect()).drafts.paper.selection[silkId]===4);
    await action('reset-filters');await page.locator('#texture-filter').selectOption('纤维');await page.locator('#element-filter').selectOption('木');await shot('03_材料筛选.png');await action('close');
    const dose=page.locator('[data-quantity="'+silkId+'"]');
    await dose.fill('12');await dose.press('Tab');
    check('upper stock boundary is accepted',await page.locator('[data-quantity="'+silkId+'"]').inputValue()==='12');
    for(const bad of ['0','13','1.5']) {
      await page.locator('[data-quantity="'+silkId+'"]').fill(bad);await page.locator('[data-quantity="'+silkId+'"]').press('Tab');
      check('invalid quantity '+bad+' restores previous value',await page.locator('[data-quantity="'+silkId+'"]').inputValue()==='12');
    }
    await page.locator('[data-dose="'+silkId+'"][data-delta="-1"]').click();
    check('minus respects current integer quantity',(await inspect()).drafts.paper.selection[silkId]===11);
    await chooseSpec('paper-fine');
    check('paper spec changes fields and compatible method',await page.locator('#target-name').innerText()==='细纹符纸'&&await page.locator('#method-name').innerText()==='凝纹成纸');
    await page.locator('#method-name').click();
    check('incompatible methods are visibly disabled',await page.locator('[data-method]:disabled').count()===2);await action('close');
    await page.locator('[data-field="2"]').click();
    check('paper field explains concrete meaning',(await page.locator('.field-explanation').innerText()).includes('细小笔画能否分清'));
    await shot('04_纸字段说明.png');await action('close');
    await chooseSpec('paper-seal');await action('clear');await action('materials');await addToPicker('青岚藤心');await addToPicker('月露菌汁');await action('close');
    check('paper retained utility checks real sources',await page.locator('#start-button').isDisabled()&&(await page.locator('#requirements').innerText()).includes('缺少藏来源'));
    await action('clear');check('empty input gives missing paper base and liquid',await page.locator('#start-button').isDisabled()&&(await page.locator('#requirements').innerText()).includes('缺少纤维纸基'));
    await reset();await action('plans');await page.locator('#plan-name').fill('纸墨选材复核');await action('save-plan');await action('close');
    const saved=JSON.stringify((await inspect()).drafts.paper);await action('clear');await action('plans');
    await page.locator('.plan-row').filter({hasText:'纸墨选材复核'}).getByRole('button',{name:'载入',exact:true}).click();
    check('saved plan restores spec method and input quantities',JSON.stringify((await inspect()).drafts.paper)===saved);
    await craft('ink');check('ink scope includes all 50 current eligible materials',(await page.evaluate(()=>PAPER_INK_PREVIEW.getCatalogSummary())).eligible===50);
    check('ink fields are distinct and retained sources shown separately',await page.locator('#field-list').innerText().then(t=>t.includes('浓度')&&!t.includes('承墨量'))&&await page.locator('#retained-list .utility-chip').count()===2);
    await chooseSpec('ink-fine');check('ink target changes retained source labels',(await page.locator('#retained-list').innerText()).includes('藏')&&!(await page.locator('#retained-list').innerText()).includes('引'));
    await page.locator('#method-name').click();await page.locator('[data-method="ink-fine"]').click();
    check('ink learned-method layout can select precision method',await page.locator('#method-name').innerText()==='澄灵成墨');
    await shot('05_研墨规格与留存.png');
    await action('clear');await action('materials');await addToPicker('灵泉露');await addToPicker('凝露松脂');await action('close');
    check('liquid and gum alone cannot substitute for ink matter',await page.locator('#start-button').isDisabled()&&(await page.locator('#requirements').innerText()).includes('缺少可研取墨料'));
    async function pair(order){await action('clear');await action('materials');for(const name of order)await addToPicker(name);await action('close');return (await page.locator('#relation-strip').innerText()).trim();}
    const forward=await pair(['灵石','灵泉露']),backward=await pair(['灵泉露','灵石']);
    check('contact phase is independent of addition order',forward===backward,{forward,backward});
    await reset();const paperBefore=JSON.stringify((await inspect()).drafts.paper);
    await page.locator('#start-button').click();check('paper begins automatic scene and locks craft switch',await page.locator('body').getAttribute('data-phase')==='process'&&await page.locator('button[data-craft="ink"]').isDisabled());
    await shot('08_制纸自动过程.png');await action('skip');
    check('skip opens result illustration with unknown quantity',await page.locator('body').getAttribute('data-phase')==='result'&&await page.locator('#result-quantity').innerText()==='张数　—');
    check('five result fields remain the target direction',await page.locator('#result-fields>div').count()===5);
    await action('finish');check('illustration leaves material draft unchanged',JSON.stringify((await inspect()).drafts.paper)===paperBefore);
    await craft('ink');const inkBefore=JSON.stringify((await inspect()).drafts.ink);await page.locator('#start-button').click();
    check('ink uses three automatic symbolic stages',await page.locator('#process-steps li').count()===3&&(await page.locator('#process-steps').innerText()).includes('融灵')&&!(await page.locator('#process-steps').innerText()).includes('研碎'));
    await shot('09_研墨自动过程.png');await page.waitForFunction(()=>document.body.dataset.phase==='result',{},{timeout:10000});
    check('ink completes automatically without timed player input',await page.locator('#result-quantity').innerText()==='份数　—');
    await shot('10_符墨鉴看示意.png');await action('finish');
    check('ink illustration also leaves stock and draft unchanged',JSON.stringify((await inspect()).drafts.ink)===inkBefore&&await page.evaluate(()=>PAPER_INK_CATALOG.materials.every(m=>m.stock===12)));
    await page.emulateMedia({reducedMotion:'reduce'});await page.locator('#start-button').click();await action('skip');await action('finish');
    check('reduced-motion mode retains complete interaction',await page.locator('body').getAttribute('data-phase')==='prepare');await page.emulateMedia({reducedMotion:'no-preference'});
    check('no browser runtime errors',errors.length===0,errors);check('no failed asset requests',badResponses.length===0,badResponses);
    await reset();
    return {passed:checks.length,failed:0,checks,errors,badResponses,captures};
  } finally {
    await page.evaluate(({key,value})=>{if(value===null)localStorage.removeItem(key);else localStorage.setItem(key,value);},{key:storageKey,value:originalStorage});
  }
}
