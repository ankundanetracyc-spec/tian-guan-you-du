async page => {
const checks=[],captures=[],errors=[],failedResources=[];
const root='E:/stacklands/docs/美术规范/符箓三工艺界面样稿_20261108/';
const check=(name,pass,detail=null)=>{checks.push({name,pass:!!pass,detail});if(!pass)throw new Error(name+' '+JSON.stringify(detail));};
page.on('pageerror',e=>errors.push(e.message));
page.on('response',r=>{if(r.status()>=400)failedResources.push({url:r.url(),status:r.status()});});
const settle=async()=>{await page.evaluate(()=>document.fonts.ready);await page.waitForTimeout(35);};
const read=()=>page.evaluate(()=>TALISMAN_PREVIEW.getState());
const analysis=()=>page.evaluate(()=>TALISMAN_PREVIEW.analysis());
const craft=async mode=>{await page.getByRole('tab',{name:{paper:'制符纸',ink:'研墨',draw:'画符'}[mode],exact:true}).click();await settle();};
const action=async a=>{await page.locator('[data-action='+a+']').first().click();await settle();};
const review=async phase=>{await page.locator('[data-review='+phase+']').click();await settle();};
const close=async()=>{await page.getByRole('button',{name:'关闭',exact:true}).click();await settle();};
const reset=async mode=>{await page.evaluate(()=>TALISMAN_PREVIEW.reset());await craft(mode);};
const shot=async name=>{await page.screenshot({path:root+name});captures.push(name);};
const rawId=name=>page.evaluate(n=>TalismanCatalog.materials.find(m=>m.name===n).id,name);
const chooseRaw=async name=>{const id=await rawId(name);await page.locator('#material-search').fill(name);await page.locator('[data-pick-material='+id+']').click();await settle();};
await page.reload();await page.waitForLoadState('networkidle');await settle();
const beforeInventory=await page.evaluate(()=>TALISMAN_PREVIEW.getInventorySnapshot());
const beforeStorage=await page.evaluate(()=>({local:JSON.stringify({...localStorage}),session:JSON.stringify({...sessionStorage})}));
try{
for(const [width,height] of [[1920,1080],[1280,720]]){
 await page.setViewportSize({width,height});
 for(const mode of ['paper','ink','draw']){
  await reset(mode);
  const geometry=await page.evaluate(()=>{
   const sels=['.game-header',document.body.dataset.craft==='draw'?'.draw-left':'.materials-panel','.control-panel','.notes-panel','.control-panel>.primary'];
   const escaped=sels.filter(s=>{const r=document.querySelector(s).getBoundingClientRect();return r.x<-.6||r.y<-.6||r.right>innerWidth+.6||r.bottom>innerHeight+.6;});
   const header=document.querySelector('.game-header').getBoundingClientRect(),back=document.querySelector('.back').getBoundingClientRect();
   const panel=document.querySelector('.control-panel'),scrollWidth=panel.scrollWidth-panel.clientWidth;
   const left=document.querySelector('.materials-panel,.draw-left'),list=document.querySelector('.selected-materials');
   return {escaped,rightOverflow:scrollWidth,leftOverflow:left.scrollWidth-left.clientWidth,mainFont:getComputedStyle(document.querySelector('.technique-title,.writing-choice strong')).fontSize,backRight:Math.abs(header.right-back.right),materials:list?list.querySelectorAll('.material-card').length:null};
  });
  check(width+' '+mode+' 主要区域完整可见',geometry.escaped.length===0,geometry);
  check(width+' '+mode+' 两侧正文不横溢',geometry.rightOverflow<3&&geometry.leftOverflow<3,geometry);
  check(width+' '+mode+' 返回位于页眉右端',geometry.backRight<40,geometry);
  check(width+' '+mode+' 开始按钮可操作',await page.locator('[data-action=start]').isEnabled());
  check(width+' '+mode+' 主区未出现内部公式',!(await page.locator('#interface').innerText()).match(/×|\\sum|最高功用|来源预算|材性签名|成功率\s*[0-9]/));
  check(width+' '+mode+' 中央无题句流程条且分隔为直线',await page.locator('.scene-heading,.scene-caption,.draw-flow,.rule-divider svg').count()===0&&await page.locator('.rule-divider').evaluateAll(nodes=>nodes.length>0&&nodes.every(n=>parseFloat(getComputedStyle(n).borderTopWidth)>0)));
  if(mode!=='draw'){
   check(width+' '+mode+' 准备页没有成品属性占位',await page.locator('.field-list,.field-section').count()===0);
   const tech=await page.locator('.technique-choice').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect();return {top:r.top,bottom:r.bottom,pressed:n.getAttribute('aria-pressed')};}));
   check(width+' '+mode+' 三项技艺竖排且单选',tech.length===3&&tech.filter(x=>x.pressed==='true').length===1&&tech.every((x,i)=>i===0||x.top>=tech[i-1].bottom),tech);
   const beforeMethod=(await read())[mode],nextMethod=mode==='paper'?'nurture':'clear';
   await page.locator('[data-method='+nextMethod+']').click();
   const afterMethod=(await read())[mode];
   check(width+' '+mode+' 技艺可切换且保留用料',afterMethod.method===nextMethod&&JSON.stringify(afterMethod.materials)===JSON.stringify(beforeMethod.materials));
   await page.locator('[data-method='+beforeMethod.method+']').click();
  }else{
   check(width+' 画符读取纸墨四字段',(await page.locator('.mini-fields').allTextContents()).join('').includes('渗墨性')&&(await page.locator('.mini-fields').allTextContents()).join('').includes('流动性'));
   check(width+' 默认只选一种主写法',await page.locator('.writing-choice.is-selected').count()===1);
  }
  await shot({paper:'制符纸',ink:'研墨',draw:'画符'}[mode]+'_'+width+'.png');
 }
 await reset('paper');await review('result');
 check(width+' 纸鉴看展示真实字段结构',await page.locator('.result-data .field-list>div').count()===4);
 check(width+' 鉴看外层明确为样例',(await page.locator('#review-note').innerText()).includes('手填鉴看样例'));
 await shot('符纸鉴看_'+width+'.png');await review('prepare');
 await craft('ink');await review('result');
 check(width+' 墨鉴看展示真实字段结构',await page.locator('.result-data .field-list>div').count()===4);
 await shot('符墨鉴看_'+width+'.png');await review('prepare');
 await craft('draw');await review('decision');
 check(width+' 应变只展示当前选择',await page.locator('.decision-options button').count()===3);
 check(width+' 有镇墨时才能镇纹',await page.locator('[data-response=匀息镇纹]').isEnabled());
 const skipDecision=await page.evaluate(()=>TALISMAN_PREVIEW.skip());
 check(width+' 跳过不代选应变',skipDecision===false&&(await read()).phase==='decision');
 await shot('画符应变_'+width+'.png');
 await page.locator('[data-response=舍势收符]').click();await action('skip');
 check(width+' 舍势并非保底成符',(await page.locator('.result-data .field-list dd').allTextContents()).every(x=>x==='—'));
 await review('prepare');await reset('draw');await action('advanced');
 check(width+' 进阶配置只按需展开',await page.locator('#modal-title').innerText()==='进阶配置');
 check(width+' 不提供自动跳过未决选择开关',await page.locator('[data-setting=ask]').count()===0);
 await shot('画符进阶_'+width+'.png');await close();
}
await page.setViewportSize({width:1280,height:720});await reset('paper');
await action('raw-picker');
check('制纸选材自动匹配23候选',await page.locator('#picker-grid .picker-item').count()===23);
await shot('选材_1280.png');await chooseRaw('兽皮');await page.getByRole('button',{name:'完成选材',exact:true}).click();
check('材料可加入本批且不重复',(await read()).paper.materials.length===4);
const silkId=await rawId('山蚕长丝');const dose=page.locator('input[data-dose-id='+silkId+']');
await dose.fill('100');await dose.press('Tab');await settle();
check('投入份数不超过演示库存',(await read()).paper.materials.find(x=>x.id===silkId).qty===12);
await page.locator('input[data-dose-id='+silkId+']').fill('0');await page.locator('input[data-dose-id='+silkId+']').press('Tab');await settle();
check('投入份数保持正整数',(await read()).paper.materials.find(x=>x.id===silkId).qty===1);
await page.locator('[data-detail='+silkId+']').click();
check('材料资料保留真实单项品级',(await page.locator('.modal-body').innerText()).includes('藏·九品'));await page.keyboard.press('Escape');
check('Esc只收起资料',!(await read()).modal&&(await read()).phase==='prepare');
await reset('ink');await action('raw-picker');
check('研墨自动匹配32候选',await page.locator('#picker-grid .picker-item').count()===32);
await page.locator('#show-ineligible').check();await page.locator('#material-search').fill('山蚕长丝');
check('纤维不能进入研墨',await page.locator('[data-pick-material='+silkId+']').isDisabled());
await page.locator('#show-ineligible').uncheck();await page.locator('#material-search').fill('不存在的材料');
check('筛选空状态明确',await page.locator('.picker-empty').count()===1);
await page.locator('#material-search').fill('雾心菌核');const nucleusId=await rawId('雾心菌核');await page.locator('[data-pick-material='+nucleusId+']').click();await page.getByRole('button',{name:'完成选材',exact:true}).click();
await page.locator('[data-retain=固]').click();await page.locator('[data-retain=化]').click();await page.locator('[data-retain=镇]').click();
check('保留功用最多四项',(await read()).ink.retained.length===4&&!((await read()).ink.retained.includes('镇')));
await reset('ink');await action('plans');await page.locator('[data-plan=default-1]').click();
check('纯液体可以开始研墨',(await read()).ink.materials.length===1&&(await analysis()).isValid&&await page.locator('[data-action=start]').isEnabled());
await page.locator('[data-action=start]').click();check('开始后角色自动完成',(await read()).phase==='process');await action('skip');
check('跳过演出进入鉴看',(await read()).phase==='result');await action('finish');
await reset('draw');await page.locator('[data-picker=inks]').click();await page.locator('[data-pick-inventory=ink-c]').click();
check('兼用墨不是三个必填槽',(await read()).draw.inkIds.length===2);await shot('选择纸墨_1280.png');await page.getByRole('button',{name:'完成选择',exact:true}).click();
const leftFit=await page.evaluate(()=>{const p=document.querySelector('.draw-left'),last=p.querySelector('.brush-choice');return {panel:p.getBoundingClientRect().bottom,footer:last.getBoundingClientRect().bottom};});
check('多墨时左栏仍能完整操作',leftFit.footer<=leftFit.panel+.6,leftFit);
await page.locator('[data-picker=recipes]').click();await page.locator('[data-pick-inventory=recipe-flame]').click();
check('缺破来源时阻止画对应符谱',await page.locator('[data-action=start]').isDisabled()&&(await analysis()).blockers.some(x=>x.includes('破')));
await reset('draw');await action('writings');await page.locator('[data-modal-writing=penetrate]').click();
check('透纸行墨不能凭写法补来源',await page.locator('[data-action=start]').isDisabled()&&(await analysis()).blockers.some(x=>x.includes('通、行')));
await reset('paper');await action('plans');await page.locator('#plan-name').fill('我的凝纸配伍');await action('save-plan');
check('记方只保存在样稿会话',(await read()).plans.length===1);await close();await action('clear');
check('空投料不能开始',await page.locator('[data-action=start]').isDisabled());await action('plans');
const savedId=(await read()).plans[0].id;await page.locator('[data-plan='+savedId+']').click();
check('配伍册可载入材料和工法',(await read()).paper.materials.length===3&&(await analysis()).isValid);
await page.setViewportSize({width:1920,height:1080});await reset('draw');await review('process');await shot('自动画符_1920.png');await action('skip');
await review('prepare');await action('back');check('返回收起页面且不丢用料',(await read()).closed);await action('resume');check('可回到符工继续查看',!(await read()).closed);
await page.emulateMedia({reducedMotion:'reduce'});const motion=await page.evaluate(()=>getComputedStyle(document.querySelector('.room-background')).animationName);check('减少动态效果可正常使用',motion==='none');
await page.emulateMedia({reducedMotion:'no-preference'});await reset('paper');
const afterInventory=await page.evaluate(()=>TALISMAN_PREVIEW.getInventorySnapshot());
check('全程不扣演示库存、不生成正式物品',beforeInventory===afterInventory);
const afterStorage=await page.evaluate(()=>({local:JSON.stringify({...localStorage}),session:JSON.stringify({...sessionStorage})}));
check('全程不写浏览器存储',JSON.stringify(beforeStorage)===JSON.stringify(afterStorage));
check('浏览器无脚本错误',errors.length===0,errors);check('素材资源均成功加载',failedResources.length===0,failedResources);
const report={date:'2026-11-09',status:'passed',scope:'隔离三页界面与选择操作；不验证生产公式或人物存档',count:checks.length,checks,captures,errors,failedResources};
await page.evaluate(r=>{window.TALISMAN_QA_REPORT=r;},report);
return {status:report.status,count:report.count,captures:report.captures,errors,failedResources};
}catch(error){return {status:'failed',message:error.message,count:checks.length,lastChecks:checks.slice(-3),captures,errors,failedResources};}
}