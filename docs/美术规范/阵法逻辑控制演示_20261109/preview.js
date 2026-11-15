(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const NS = 'http://www.w3.org/2000/svg';
  const conditionIds = ['guard-supply','guard-eligible','guard-commit','attack-budget','shared-peak','attack-eligible','attack-commit'];
  const presetData = {
    ready: {shield:true, note:'先点一次轻击：已有盾就不重复补防，只检查是否值得转攻。'},
    guard: {shield:false, note:'先点轻击或重击：无盾立即补防；只有真实补盾成功，才会继续本次响应。'},
    shortage: {shield:false, note:'先点受袭，再点“新的供能机会”：先记下需求，恢复后只补防，不补旧攻击。'},
    barrage: {shield:true, note:'连点至N后换窗：下一窗首击仍保守；一个完整非密集窗结束后，再有新受袭才恢复转攻。'}
  };
  const entryNames = {new_hit:'本次受袭',retry:'新机会重试',shield_gain:'只撤销需求',window:'窗口更新',control:'启用控制',state:'外部状态'};
  let preset='ready', sequence=0, state;

  function svgEl(parent,tag,attrs={},text) {
    const node=document.createElementNS(NS,tag);
    for(const [key,value] of Object.entries(attrs)) node.setAttribute(key,String(value));
    if(text!==undefined) node.textContent=text;
    parent.appendChild(node); return node;
  }
  function makeContext(entry,title,rootId) {
    const prefixes={new_hit:'H',retry:'O',shield_gain:'S',window:'W',control:'M',state:'V'};
    return {id:rootId||`${prefixes[entry]}${String(++sequence).padStart(2,'0')}`,entry,title,
      nodes:new Set(),edges:new Set(),blocked:new Set(),success:new Set(),steps:[],payments:[],
      guardAttempts:0,attackAttempts:0,guardSuccess:false,attackSuccess:false,
      guardNote:'未请求',attackNote:'未请求',snapshot:null,ack:null,summary:'',duplicate:false,
      sharedPeakAllowsAttack:$('shared-peak').checked};
  }
  function finish(ctx) {
    state.last=ctx;
    const snapshot=ctx.snapshot;
    const sample=snapshot?`观频${snapshot.count}／${snapshot.threshold}，${pressureText(snapshot)}。` : '';
    state.history.unshift({id:ctx.id,entry:ctx.entry,title:ctx.title,text:sample+ctx.summary,payment:ctx.payments.join('＋')||'无'});
    state.history=state.history.slice(0,8);
    render();
  }
  function failGuard(ctx,reason) {
    ctx.blocked.add('guard'); ctx.guardNote='失败，未扣费';ctx.attackNote='先补防，未转攻';
    ctx.summary=`${reason}；没有成功回执，P保留。此次不攻击。`;
    ctx.steps.push('护持失败不扣C护，未发成功回执');
  }
  function clearFromShieldGain(ctx,own) {
    // This observation is a separate clear-only message. It never changes the response entry.
    state.pending=false;
    ctx.nodes.add('gain');ctx.nodes.add('clearonly');ctx.edges.add('gain-clear');ctx.success.add('clearonly');
    ctx.steps.push(own?'本次实际增盾观测：只清P，不再观频，不进入攻击':'外界实际增盾：只清P');
  }
  function tryGuard(ctx) {
    if(ctx.guardAttempts) return;
    ctx.guardAttempts=1;ctx.nodes.add('guard');ctx.edges.add('state-guard');
    if(!$('guard-supply').checked){failGuard(ctx,'护持缺能');return;}
    if(!$('guard-eligible').checked){failGuard(ctx,'护持原资格或峰值未通过');return;}
    if(!$('guard-commit').checked){failGuard(ctx,'护持事务未实际提交成功');return;}
    // The fixture commits a positive shield result and its symbolic charge together.
    ctx.guardSuccess=true;ctx.guardNote='实际成功';ctx.payments.push('C护');state.paidGuard++;
    state.hasShield=true;$('after-hit-shield').value='yes';ctx.success.add('guard');
    clearFromShieldGain(ctx,true);
    ctx.ack=Object.freeze({rootId:ctx.id,entry:ctx.entry,requestId:`${ctx.id}:guard:1`,executorId:'demo_guard',pressureSnapshot:ctx.snapshot,result:Object.freeze({shield_delta_positive:true,paid_symbol:'C护'})});
    ctx.nodes.add('ack');ctx.success.add('ack');ctx.edges.add('guard-ack');ctx.edges.add('ack-satisfied');
    ctx.steps.push('成功ACK保留原始事件、响应入口和pressure_snapshot');
    defenseSatisfied(ctx);
  }
  function stopAttack(ctx,reason,note) {
    ctx.blocked.add('gate');ctx.attackNote=note||'本次停止';ctx.summary=reason;
  }
  function defenseSatisfied(ctx) {
    state.pending=false;ctx.nodes.add('satisfied');ctx.success.add('satisfied');
    ctx.nodes.add('gate');ctx.edges.add('satisfied-gate');
    if(ctx.entry!=='new_hit') {
      stopAttack(ctx,'补防已满足；这次入口是恢复机会，没有新的受袭，不补打一记旧攻击。','恢复入口，不补攻');return;
    }
    const snapshot=ctx.ack?ctx.ack.pressureSnapshot:ctx.snapshot;
    const judgement=snapshot?judgePressure(snapshot):null;
    if(!judgement||judgement.level==='unknown') {
      const cause=snapshot?.observed_prefix_complete===false?'本窗前缀有观测缺口':'上一窗报告不完整或缺失';
      stopAttack(ctx,`${cause}，尚不能确认低压；补防照常，只保守停止可选攻击。`,'观测可信度不足');return;
    }
    if(judgement.level==='high') {
      const cause=snapshot.heavy?'本次达到重击门槛':judgement.dense?'本窗受袭已达密集阈值':'上一实际结束窗口已密集';
      stopAttack(ctx,`${cause}：防护已经满足，保留资源，停止本次可选攻击。`,'高压，保留防护');return;
    }
    if(!$('attack-budget').checked) {
      stopAttack(ctx,'支付攻击后无法留下下一次C护；只保留当前防护，不发出攻击请求。','保底预算不足');return;
    }
    if(!ctx.sharedPeakAllowsAttack) {
      stopAttack(ctx,'本原始事件的共享峰值不够继续攻击；护持回执没有重开峰值额度。','共享峰值不足');return;
    }
    tryAttack(ctx);
  }
  function tryAttack(ctx) {
    if(ctx.attackAttempts) return;
    ctx.attackAttempts=1;ctx.nodes.add('attack');ctx.edges.add('gate-attack');
    if(!$('attack-eligible').checked) {
      ctx.blocked.add('attack');ctx.attackNote='原资格未通过';ctx.summary='攻击目标、原费用、冷却或限次未通过；不扣C攻，也不保存待补攻击。';return;
    }
    // Final execution rechecks the reserve and the original-event peak budget atomically.
    if(!$('attack-budget').checked||!ctx.sharedPeakAllowsAttack||!$('attack-commit').checked) {
      ctx.blocked.add('attack');ctx.attackNote='未提交，未扣费';ctx.summary='攻击最终事务未通过；撤销预留，不扣C攻，不补发。';return;
    }
    ctx.attackSuccess=true;ctx.attackNote='实际提交成功';ctx.payments.push('C攻');state.paidAttack++;
    ctx.success.add('gate');ctx.success.add('attack');
    ctx.summary=ctx.guardSuccess?'护持实际成功后，低压、保底预算与同源峰值均通过；完成本次可选攻击。':'已有盾，不重复补防；低压、保底预算与同源峰值均通过，完成本次可选攻击。';
  }
  function processNeed(ctx) {
    ctx.nodes.add('memory');
    if(!state.pending) {ctx.summary='P为0，没有尚未完成的补防需求；此次机会直接结束。';ctx.attackNote='无新受袭，不攻击';return;}
    ctx.nodes.add('state');ctx.edges.add('memory-state');
    if(state.hasShield) {
      ctx.edges.add('state-satisfied');ctx.steps.push('当前已有盾，动作支路继续；另行清P');defenseSatisfied(ctx);
    } else tryGuard(ctx);
  }
  function pressureText(snapshot) {
    const judgement=judgePressure(snapshot);
    if(judgement.level==='unknown')return'未知（观测有缺口）';
    if(snapshot.heavy)return'本次重击';if(judgement.dense)return'本窗密集';if(judgement.previousDense)return'上一窗密集';return'低压 · 当前可判断';
  }
  function judgePressure(snapshot) {
    const dense=snapshot.count>=snapshot.threshold,previous=snapshot.previous_report;
    const previousDense=previous?.kind==='closed'&&previous.count>=snapshot.threshold;
    const previousKnownLow=previous?.kind==='opening_none'||(previous?.kind==='closed'&&previous.closed_window_complete&&previous.count<snapshot.threshold);
    const knownHigh=snapshot.heavy||dense||previousDense;
    return {dense,previousDense,level:knownHigh?'high':snapshot.observed_prefix_complete&&previousKnownLow?'low':'unknown'};
  }
  function makePressureSnapshot(heavy) {
    // Observe and forward facts only. Defense is processed before attack routing reads them.
    return Object.freeze({windowId:state.windowId,count:state.count,threshold:state.threshold,heavy,
      observed_prefix_complete:state.observedPrefixComplete,previous_report:state.previousReport});
  }
  function receiveHit(event) {
    const ctx=makeContext('new_hit',event.heavy?'重击':'轻击',event.id);ctx.nodes.add('hit');
    if(state.seenHits.has(event.id)) {
      ctx.duplicate=true;ctx.blocked.add('hit');ctx.summary='同一个原始动作的重复通知：不再观频、记需、扣费或输出。';finish(ctx);return;
    }
    // The external damage result is real even while the array is disabled.
    // Deduplicate before committing it so an old notification cannot roll world state back.
    state.seenHits.add(event.id);state.hasShield=event.afterShield;
    if(!state.enabled){ctx.blocked.add('hit');ctx.summary='本次真实伤害已改变外部护盾；阵盘停用，未观测或建立需求，也不追补。';finish(ctx);return;}
    state.count++;
    ctx.nodes.add('observe');ctx.edges.add('hit-observe');
    ctx.snapshot=makePressureSnapshot(event.heavy);ctx.steps.push('只在new_hit采样一次；本次pressure_snapshot固定');
    state.lastObservedHit={...event};
    state.pending=true;ctx.nodes.add('memory');ctx.edges.add('observe-memory');
    ctx.steps.push('先置P=1；高压与低压都立即走补防');
    processNeed(ctx);finish(ctx);
  }
  function sendHit(heavy) {
    const id=`H${String(++sequence).padStart(2,'0')}`;
    receiveHit({id,heavy,afterShield:$('after-hit-shield').value==='yes'});
  }
  function supplyOpportunity() {
    // A new fixture supply result, not an arithmetic refill or a replay of an old hit.
    $('guard-supply').checked=true;
    const ctx=makeContext('retry','新的供能机会');ctx.nodes.add('retry');ctx.edges.add('retry-memory');
    if(!state.enabled){ctx.blocked.add('retry');ctx.summary='供能条件已变为通过，但阵盘停用，不响应或自动重启。';finish(ctx);return;}
    processNeed(ctx);finish(ctx);
  }
  function externalShield() {
    const ctx=makeContext('shield_gain','外界实际补盾');state.hasShield=true;$('after-hit-shield').value='yes';
    if(!state.enabled){ctx.summary='外界已实际补盾；阵盘停用期间不接收观测，P维持取消状态。';finish(ctx);return;}
    clearFromShieldGain(ctx,false);ctx.summary='外界已实际补入护盾，旧补防需求立即结束；没有护持请求，也没有攻击。';finish(ctx);
  }
  function nextWindow() {
    const ctx=makeContext('window','下个共同回合');
    state.previousReport=Object.freeze({kind:'closed',windowId:state.windowId,count:state.count,closed_window_complete:state.observedPrefixComplete});
    state.windowId++;state.count=0;state.observedPrefixComplete=state.enabled;
    const report=state.previousReport;
    ctx.summary=`上一实际结束窗记录${report.count}次（${report.closed_window_complete?'完整':'部分观测'}），新窗从0次开始。只替换观测报告，P、费用与执行资格不变；不补防、攻击或回能。`;finish(ctx);
  }
  function toggleEnabled() {
    const wasEnabled=state.enabled;const ctx=makeContext('control',wasEnabled?'主动停用':'重新启用');
    state.enabled=!wasEnabled;state.observedPrefixComplete=false;
    if(wasEnabled){state.pending=false;ctx.summary='主动停用，取消未完成补防P；本窗已见次数与已付费用保持。';}
    else ctx.summary='已重启，不补旧事件也不重试；本窗部分观测，未达N前不开放低频可选攻击。';
    finish(ctx);
  }
  function clearExternalShield() {
    const ctx=makeContext('state','情境设为无盾');state.hasShield=false;$('after-hit-shield').value='no';
    ctx.summary='当前外部护盾状态改为无盾；没有新受袭输入，不建立新需求，也不复活已清除的P。';finish(ctx);
  }
  function reset(nextPreset=preset) {
    preset=nextPreset;sequence=0;
    state={enabled:true,hasShield:presetData[preset].shield,pending:false,
      windowId:1,count:0,threshold:Number($('threshold').value),observedPrefixComplete:true,
      previousReport:Object.freeze({kind:'opening_none',count:null,closed_window_complete:null}),
      paidGuard:0,paidAttack:0,seenHits:new Set(),lastObservedHit:null,history:[],last:null};
    for(const id of conditionIds)$(id).checked=true;
    if(preset==='shortage'){$('guard-supply').checked=false;$('attack-budget').checked=false;}
    $('after-hit-shield').value=state.hasShield?'yes':'no';
    document.querySelectorAll('[data-preset]').forEach(button=>{const active=button.dataset.preset===preset;button.classList.toggle('selected',active);button.setAttribute('aria-pressed',String(active));});
    $('preset-note').textContent=presetData[preset].note;render();
  }

  const nodes=[
    {id:'hit',x:24,y:24,w:220,h:72,title:'本次真实受袭',detail:['new_hit · 原始完整动作']},
    {id:'retry',x:284,y:24,w:220,h:72,title:'新的可用机会',detail:['retry · 只查询补防需求']},
    {id:'gain',x:544,y:24,w:232,h:72,title:'护盾实际增加',detail:['shield_gain · 只清需']},
    {id:'observe',x:24,y:132,w:220,h:72,title:'观频记录',detail:()=>[`本窗${state.last?.snapshot?.count??state.count}次；再全量转发`]},
    {id:'memory',x:284,y:132,w:220,h:72,title:'需求记忆 P',detail:['新受袭置1；机会查询']},
    {id:'clearonly',x:544,y:132,w:232,h:72,title:'清P；本支路结束',detail:['没有攻击出口']},
    {id:'state',x:284,y:248,w:220,h:72,title:'现在有实际护盾？',detail:['读取当前真实状态']},
    {id:'guard',x:544,y:248,w:232,h:72,title:'尝试护持',detail:()=>[state.last?.guardSuccess?'正数护盾已提交 · C护':state.last?.guardAttempts?'失败不扣费，不发回执':'供能、峰值与原资格']},
    {id:'ack',x:544,y:364,w:232,h:72,title:'实际成功回执',detail:['原ID／入口／采样不变']},
    {id:'satisfied',x:284,y:472,w:220,h:72,title:'防护已满足',detail:['动作合流；另行清P']},
    {id:'gate',x:284,y:590,w:220,h:102,title:'可选攻击条件',detail:['本次受袭＋当前守备判断','保底预算＋峰值＋资格']},
    {id:'attack',x:544,y:590,w:232,h:102,title:'尝试攻击',detail:()=>[state.last?.attackSuccess?'实际提交 · 支付C攻':'只扣C攻；留足C护','不保存待反击']}
  ];
  const edges=[
    ['hit-observe','M134 96V132'],['observe-memory','M244 168H284'],['retry-memory','M394 96V132'],
    ['gain-clear','M660 96V132'],['memory-state','M394 204V248'],['state-guard','M504 284H544'],
    ['guard-ack','M660 320V364'],['state-satisfied','M394 320V472'],
    ['ack-satisfied','M544 400H410Q394 400 394 416V472'],['satisfied-gate','M394 544V590'],['gate-attack','M504 641H544']
  ];
  function renderFlow() {
    const svg=$('flow-diagram'),ctx=state.last;svg.replaceChildren();
    const defs=svgEl(svg,'defs');
    for(const [id,color]of[['arrow-idle','#8d8270'],['arrow-active','#b95f4b']]){const marker=svgEl(defs,'marker',{id,viewBox:'0 0 8 8',refX:7,refY:4,markerWidth:5,markerHeight:5,orient:'auto'});svgEl(marker,'path',{d:'M0 0L8 4L0 8Z',fill:color});}
    for(const[id,d]of edges){const active=ctx?.edges.has(id);svgEl(svg,'path',{d,class:`flow-wire${active?' active':''}`,'marker-end':`url(#${active?'arrow-active':'arrow-idle'})`});}
    svgEl(svg,'text',{x:516,y:268,class:'flow-label'},'无');svgEl(svg,'text',{x:365,y:356,class:'flow-label'},'有');
    svgEl(svg,'text',{x:666,y:346,class:'flow-label'},'成功才继续');
    for(const node of nodes){let cls='flow-node';if(ctx?.nodes.has(node.id))cls+=' active';if(ctx?.success.has(node.id))cls+=' success';if(ctx?.blocked.has(node.id))cls+=' blocked';const group=svgEl(svg,'g',{class:cls,'data-node':node.id});svgEl(group,'rect',{x:node.x,y:node.y,width:node.w,height:node.h,rx:5,class:'node-body'});svgEl(group,'rect',{x:node.x+4,y:node.y+4,width:node.w-8,height:node.h-8,rx:3,class:'node-inset'});svgEl(group,'text',{x:node.x+node.w/2,y:node.y+29,'text-anchor':'middle',class:'node-title'},node.title);const lines=typeof node.detail==='function'?node.detail():node.detail;lines.forEach((line,i)=>svgEl(group,'text',{x:node.x+node.w/2,y:node.y+53+i*22,'text-anchor':'middle',class:'node-detail'},line));}
    svgEl(svg,'text',{x:30,y:278,class:'flow-label'},'不论强弱与频次');svgEl(svg,'text',{x:30,y:301,class:'flow-label'},'都先即时处理补防');
    svgEl(svg,'text',{x:30,y:490,class:'flow-label'},'只有攻击分支');svgEl(svg,'text',{x:30,y:513,class:'flow-label'},'才读取压力采样');
  }
  function renderHistory() {
    const list=$('event-history');list.replaceChildren();
    if(!state.history.length){const li=document.createElement('li');li.className='empty';li.textContent='尚无输入。';list.appendChild(li);return;}
    for(const event of state.history){const li=document.createElement('li');for(const[cls,text]of[['history-identity',`${event.id} · ${event.title}`],['history-entry',`${event.entry}／${entryNames[event.entry]}`],['history-summary',event.text]]){const span=document.createElement('span');span.className=cls;span.textContent=text;if(cls==='history-summary'){span.appendChild(document.createTextNode(' 本次支付：'));const fee=document.createElement('span');fee.className='payment-token';fee.textContent=`${event.payment}。`;span.appendChild(fee);}li.appendChild(span);}list.appendChild(li);}
  }
  function render() {
    const ctx=state.last;
    $('window-name').textContent=`共同回合${state.windowId} · ${state.observedPrefixComplete?'截至当前观测完整':'本窗前缀有缺口'}`;
    $('enabled-state').textContent=state.enabled?'阵盘启用中':'阵盘已停用';$('enabled-state').classList.toggle('off',!state.enabled);
    $('toggle-enabled').textContent=state.enabled?'主动停用':'重新启用';
    $('state-shield').textContent=state.hasShield?'有盾':'无盾';
    $('state-pending').textContent=state.pending?'1 · 仍待补防':'0 · 无待补防';$('state-pending').classList.toggle('needed',state.pending);
    $('state-count').textContent=`${state.count} 次／阈${state.threshold}`;
    const currentSnapshot=ctx?.snapshot&&!ctx.duplicate?ctx.snapshot:makePressureSnapshot(false);
    const pressure=pressureText(currentSnapshot),level=judgePressure(currentSnapshot).level;
    $('state-pressure').textContent=pressure;$('state-pressure').classList.toggle('high',level==='high');$('state-pressure').classList.toggle('unknown',level==='unknown');
    const previous=state.previousReport;
    $('state-previous').textContent=previous?.kind==='opening_none'?'开场 · 无前窗':previous?.kind==='closed'?`第${previous.windowId}窗 · ${previous.count}次`:'报告缺失';
    $('previous-trust').textContent=previous?.kind==='opening_none'?'仅真正开场视为中性':previous?.kind==='closed'?`${previous.closed_window_complete?'完整观测':'部分观测'} · ${previous.count>=state.threshold?'已见密集':'未达密集阈'}`:'不能据此确认低压';
    $('last-source').textContent=ctx?`${ctx.id} · ${ctx.entry}`:'尚无输入';
    $('last-summary').textContent=ctx?.summary||'先点击轻击或重击。';$('last-summary').className='result-summary'+(ctx?.guardAttempts&&!ctx.guardSuccess?' guard-failed':ctx?.attackSuccess?' succeeded':ctx?.blocked.has('gate')?' attack-stopped':'');
    $('last-guard').textContent=ctx?.guardNote||'未请求';$('last-attack').textContent=ctx?.attackNote||'未请求';$('last-payment').textContent=ctx?.payments.join('＋')||'无';
    $('commit-total').textContent=`演示累计提交：护持${state.paidGuard}次，攻击${state.paidAttack}次。停用／重启不清此记录。`;
    $('snapshot-note').textContent=ctx?.snapshot?`采样固定：窗口${ctx.snapshot.windowId}，本次后${ctx.snapshot.count}次，${pressureText(ctx.snapshot)}。${ctx.ack?'ACK继承原受袭采样，增盾结果不改其强弱。':''}`:ctx?.entry==='retry'?'retry保留自身入口，不借用先前受袭的身份或压力采样。':'';
    const dataset=$('state-ribbon').dataset;Object.assign(dataset,{count:String(state.count),pending:String(state.pending),shield:String(state.hasShield),window:String(state.windowId),observedPrefixComplete:String(state.observedPrefixComplete),previousKind:previous?.kind||'missing',previousCount:String(previous?.count??'missing'),previousComplete:String(previous?.closed_window_complete??'missing'),guardPaid:String(state.paidGuard),attackPaid:String(state.paidAttack),guardAttempts:String(ctx?.guardAttempts||0),attackAttempts:String(ctx?.attackAttempts||0),entry:ctx?.entry||'none',pressure:level});
    renderFlow();renderHistory();
  }
  $('light-hit').addEventListener('click',()=>sendHit(false));$('heavy-hit').addEventListener('click',()=>sendHit(true));
  $('supply-opportunity').addEventListener('click',supplyOpportunity);$('external-shield').addEventListener('click',externalShield);
  $('next-window').addEventListener('click',nextWindow);$('toggle-enabled').addEventListener('click',toggleEnabled);
  $('clear-external-shield').addEventListener('click',clearExternalShield);
  $('duplicate-hit').addEventListener('click',()=>{if(state.lastObservedHit)receiveHit({...state.lastObservedHit});});
  $('reset-demo').addEventListener('click',()=>reset());$('threshold').addEventListener('change',()=>reset());
  $('refresh-fixture').addEventListener('click',()=>{for(const id of conditionIds)$(id).checked=true;});
  document.querySelectorAll('[data-preset]').forEach(button=>button.addEventListener('click',()=>reset(button.dataset.preset)));
  reset();
})();
