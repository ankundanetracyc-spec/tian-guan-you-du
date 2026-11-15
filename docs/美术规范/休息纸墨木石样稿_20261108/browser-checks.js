async (page) => {
  const results = [], errors = [], failedResponses = [], captures = [];
  const check = (name, pass, detail = null) => { results.push({name, pass:!!pass, detail}); if (!pass) throw new Error(name + ': ' + JSON.stringify(detail)); };
  page.on('pageerror', error => errors.push(error.message));
  page.on('response', response => { if (response.status() >= 400) failedResponses.push({url:response.url(),status:response.status()}); });
  const folder = 'E:/stacklands/docs/美术规范/休息纸墨木石样稿_20261108/';
  const inspect = () => page.evaluate(() => RestReview.snapshot());
  const settle = async () => { await page.evaluate(() => document.fonts.ready); await page.waitForTimeout(90); };
  const choose = async name => { await page.locator(`.review-bar button[data-case="${name}"]`).click(); await settle(); };
  const setNumber = async (id,value) => { await page.locator('#'+id).fill(String(value)); await page.locator('#'+id).press('Tab'); await settle(); };
  const shot = async name => { await page.screenshot({path:folder+name}); captures.push(name); };
  await page.reload(); await page.waitForLoadState('networkidle'); await settle();
  for (const [width,height] of [[1920,1080],[1280,720]]) {
    await page.setViewportSize({width,height}); await settle();
    for (const name of ['day','night','date','fixed','unbound']) {
      await choose(name);
      const state = await inspect();
      check(`${width}/${name}: correct sample`,state.case===name);
      const geometry = await page.evaluate(() => {
        const visible = e => !!(e.offsetWidth||e.offsetHeight||e.getClientRects().length) && getComputedStyle(e).visibility!=='hidden';
        const controls = [...document.querySelectorAll('button,input,a')].filter(visible);
        const escaped = controls.filter(e=>{const r=e.getBoundingClientRect();return r.left<-.5||r.top<-.5||r.right>innerWidth+.5||r.bottom>innerHeight+.5}).map(e=>e.id||e.textContent.trim());
        const bare = controls.filter(e=>!e.classList.contains('step-arrow')&&!e.closest('.material-surface')).map(e=>e.id||e.textContent.trim());
        const note=document.querySelector('.note-inner');
        const box=document.querySelector('.rest-note').getBoundingClientRect();
        const text=document.querySelector('#stage').innerText;
        return {escaped,bare,innerHeight:note.clientHeight,contentHeight:note.scrollHeight,box:{x:box.x,y:box.y,w:box.width,h:box.height},text,room:document.querySelector('#room-background').getAttribute('src')};
      });
      check(`${width}/${name}: every visible control in viewport`,geometry.escaped.length===0,geometry.escaped);
      check(`${width}/${name}: material controls and triangle exception`,geometry.bare.length===0,geometry.bare);
      check(`${width}/${name}: note content fits`,geometry.contentHeight<=geometry.innerHeight+2,{height:geometry.innerHeight,content:geometry.contentHeight});
      check(`${width}/${name}: no added resources/old prologue`,!/贡献|灵石|灵感|修命|修性|主修|瓶颈|李宅|后山/.test(geometry.text));
      check(`${width}/${name}: no clock readout`,!/(?:\d{1,2}:\d{2}|\d+时(?:整|分)|子时|丑时|寅时|卯时|辰时|巳时|午时|未时|申时|酉时|戌时|亥时)/.test(geometry.text));
      check(`${width}/${name}: correct day/night room`,geometry.room.includes(name==='night'||name==='fixed'||name==='date'?'rest_night':'rest_day'));
      if(name==='unbound'){
        check(`${width}: unknown values remain dashes`,await page.locator('#current-stamina').innerText()==='— / —' && await page.locator('#wake-date').innerText()==='—');
        check(`${width}: no unbound action`,await page.locator('#start-rest').isDisabled());
      } else if(name==='fixed'){
        check(`${width}: fixed story hides free controls`,!await page.locator('#mode-tabs').isVisible()&&!await page.locator('#hours-section').isVisible()&&!await page.locator('#date-section').isVisible());
        check(`${width}: fixed existing duration`,state.preview.hours===8&&state.preview.after===48);
      } else if(name==='date'){
        check(`${width}: year rollover uses 30-day calendar`,state.date.join('/')==='2/1/1'&&state.preview.hours===24&&state.preview.date.join('/')==='2/1/1');
      }
      await shot(`休息_${{day:'白昼',night:'入夜',date:'指定日期',fixed:'剧情限定',unbound:'未绑定'}[name]}_${width}.png`);
    }
  }
  await page.setViewportSize({width:1920,height:1080}); await choose('day');
  const initial = await inspect();
  check('default duration and recovery match existing fixture',initial.hours===8&&initial.preview.recovered===8&&initial.preview.after===48);
  await page.getByRole('button',{name:'增加休息时长',exact:true}).click();
  check('bare up triangle increments duration',(await inspect()).preview.hours===9);
  await page.getByRole('button',{name:'减少休息时长',exact:true}).click();
  check('bare down triangle decrements duration',(await inspect()).preview.hours===8);
  await setNumber('hours',1); await page.getByRole('button',{name:'减少休息时长',exact:true}).click();
  check('duration minimum remains one',(await inspect()).hours===1);
  await setNumber('hours',720); await page.getByRole('button',{name:'增加休息时长',exact:true}).click();
  check('duration maximum remains 720',(await inspect()).hours===720);
  check('recovery caps at existing capacity',(await inspect()).preview.after===100&&(await inspect()).preview.recovered===60);
  await setNumber('hours',0); check('typed duration clamps to existing minimum',(await inspect()).hours===1);
  await setNumber('hours',721); check('typed duration clamps to existing maximum',(await inspect()).hours===720);
  await setNumber('hours',8.4); check('duration is integral after commit',(await inspect()).hours===8);
  await page.locator('#hours').fill('');
  check('incomplete duration cannot start',await page.locator('#start-rest').isDisabled());
  await setNumber('hours',8);
  await page.getByRole('button',{name:'指定日期',exact:true}).filter({has:page.locator('.button-content')}).click();
  check('mode changes preserve current time',(await inspect()).fixture.date.join('/')===initial.fixture.date.join('/')&&(await inspect()).fixture.hour===initial.fixture.hour);
  await setNumber('day',15); check('same-date rest disabled',await page.locator('#start-rest').isDisabled());
  check('same-date reason is visible',(await page.locator('#validation-message').innerText()).includes('晚于当前日期'));
  await page.setViewportSize({width:1280,height:720}); await settle(); await shot('休息_无效日期_1280.png');
  await setNumber('day',14); check('past date disabled',await page.locator('#start-rest').isDisabled());
  await setNumber('day',16); check('future date uses implicit same-hour alignment',(await inspect()).preview.hours===24);
  await page.getByRole('button',{name:'按时长',exact:true}).click(); check('switching back preserves duration',(await inspect()).hours===8);
  check('choosing wake date does not switch current bedroom',(await page.locator('#room-background').getAttribute('src')).includes('rest_day'));
  await choose('date'); check('next year first day displayed',await page.locator('#wake-date').innerText()==='2年1月1日');
  await setNumber('month',2); await setNumber('day',30); check('each month has 30 days',(await inspect()).preview.hours===60*24);
  await setNumber('day',31); check('date day upper bound',(await inspect()).date[2]===30);
  await setNumber('month',13); check('date month upper bound',(await inspect()).date[1]===12);
  await setNumber('year',10000); check('date year upper bound',(await inspect()).date[0]===9999);
  await choose('night'); const beforeConfirm=await inspect();
  await page.getByRole('button',{name:'开始休息',exact:true}).click();
  check('start shows arrangement preview',await page.locator('#arrangement-dialog').isVisible());
  check('prototype boundary stays in review toolbar',(await page.locator('.review-title').innerText()).includes('独立样稿，不接游戏'));
  check('viewing arrangement does not settle resources/time',JSON.stringify(await inspect())===JSON.stringify(beforeConfirm));
  await shot('休息_安排预览_1280.png');
  await page.keyboard.press('Escape'); check('Escape closes preview before rest screen',!await page.locator('#arrangement-dialog').isVisible()&&!await page.locator('#closed-view').isVisible());
  check('focus returns to primary action',await page.locator('#start-rest').evaluate(e=>e===document.activeElement));
  await page.keyboard.press('Escape'); check('Escape returns from rest',await page.locator('#closed-view').isVisible());
  await page.getByRole('button',{name:'继续查看',exact:true}).click(); check('reopen preserves configured state',!await page.locator('#closed-view').isVisible()&&JSON.stringify(await inspect())===JSON.stringify(beforeConfirm));
  await page.getByRole('button',{name:'返回',exact:true}).click(); check('return has a real preview response',await page.locator('#closed-view').isVisible());
  await page.getByRole('button',{name:'继续查看',exact:true}).click();
  await page.locator('#mode-date').focus(); await page.keyboard.press('Enter'); check('keyboard mode activation',(await inspect()).mode==='date');
  check('visible focus treatment exists',await page.locator('#mode-date').evaluate(e=>getComputedStyle(e).outlineStyle==='solid'));
  await page.emulateMedia({reducedMotion:'reduce'}); await choose('fixed');
  check('reduced-motion preserves fixed rest',(await inspect()).preview.hours===8&&await page.locator('#start-rest').isEnabled());
  await page.emulateMedia({reducedMotion:'no-preference'});
  const storage=await page.evaluate(()=>({local:localStorage.length,session:sessionStorage.length}));
  check('no browser persistence writes',storage.local===0&&storage.session===0,storage);
  check('no script errors',errors.length===0,errors);
  check('all loaded assets resolve',failedResponses.length===0,failedResponses);
  await choose('day');
  return {date:'2026-11-08',passed:results.length,failed:0,results,captures,errors,failedResponses,independentSession:'rest-review',viewports:[[1920,1080],[1280,720]],playerFilesRead:false,persistenceWritten:false,gameIntegrated:false};
}
