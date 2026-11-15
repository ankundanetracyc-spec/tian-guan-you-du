(() => {
  'use strict';
  // Read-only resource preview ported from AlchemySlotSimulation.CalculateMetrics/SpiritLoads.
  // This deliberately produces no pill result, inventory event or time advancement.
  const strength=[0,1,1.65,2.9,5.4,10.7,22.6,50.9,121,304],threshold=[0,8,32,68,122,203,325,508,783,1196];
  const costs=[0,.75,1,1.4,2,3,4.5,7,11,17];
  const functions={chen:['聚','固'],zuo:['破','化','固','镇','护','明','藏'],shi:['通','行','引','藏']};
  const yields={'纤维':.85,'致密':.75,'颗粒':.8,'黏胶':.9,'液体':.95,'气体':.9};
  const roles={jun:0,chen:1,zuo:2,shi:3};
  const active=(role,utility)=>functions[role]?.includes(utility)||false;
  function evaluate(slots,stats,materials){
    const inputs=Object.entries(slots).filter(([,value])=>value).map(([slot,value])=>({slot,role:slot.replace(/[123]/g,''),material:materials.find(m=>m.name===value.name),dose:value.dose}));
    const main=inputs.find(i=>i.role==='jun'),core=main?.material.utilities['生']?'生':'';
    const availableSpirit=Math.max(0,stats.totalSpirit-Math.max(0,stats.otherSpirit));
    const loads=inputs.map(i=>{const m=i.material;const used=Object.keys(m.utilities).filter(u=>i.role==='jun'?u===core:active(i.role,u));const base=1+.13*m.grade+.0065*m.grade*m.grade;const functionLoad=used.reduce((s,u)=>s+costs[m.utilities[u]],0);const factor=1+.4*(i.dose-1)+.04*(i.dose-1)**2;const multiplier={jun:1.4,chen:1.2,zuo:1,shi:.9}[i.role];return {...i,used,occupation:(base+functionLoad)*factor*multiplier};});
    const spirit=Math.ceil(Math.round(loads.reduce((s,l)=>s+l.occupation,0)*1e8)/1e8),count=inputs.length,total=inputs.reduce((s,i)=>s+i.dose,0);
    const hours=count?2**(Math.max(...inputs.map(i=>Math.max(1,Math.ceil(i.material.grade/3))))-1)*(1+.25*Math.max(0,count-4))*2:0;
    let energy=0;
    if(main){
      const mastery=g=>Math.min(1,stats.attainment/threshold[g]);
      const power=u=>inputs.reduce((sum,i)=>sum+(active(i.role,u)&&i.material.utilities[u]?i.dose*strength[i.material.utilities[u]]*mastery(i.material.utilities[u]):0),0);
      const portions=inputs.map(i=>({...i,yield:yields[i.material.texture]??.8,available:100*i.dose*(yields[i.material.texture]??.8),residual:100*i.dose*(1-(yields[i.material.texture]??.8))}));
      const transfer=(items,amount)=>{const sorted=[...items].sort((a,b)=>a.yield-b.yield||(a.role==='jun'?0:1)-(b.role==='jun'?0:1)||roles[a.role]-roles[b.role]||(a.material.itemPath<b.material.itemPath?-1:1));for(const p of sorted){const n=Math.min(p.residual,amount);p.residual-=n;p.available+=n;amount-=n;if(amount<=0)break;}};
      const dense=portions.filter(p=>p.material.texture==='致密'),raw=total*100,denseRaw=dense.reduce((s,p)=>s+p.available+p.residual,0);
      const broken=Math.min(dense.reduce((s,p)=>s+p.residual,0),.1*denseRaw,.05*power('破')*denseRaw);transfer(dense,broken);
      const converted=Math.min(portions.reduce((s,p)=>s+p.residual,0),Math.max(0,.2*raw-broken),.15*raw,.05*power('化')*raw);transfer(portions,converted);
      let candidate=0;
      if(core){const grade=main.material.utilities[core];const mass=portions.find(p=>p.role==='jun').available;const support=mass*strength[grade]*mastery(grade)/25;candidate=support<1?0:Math.max(1,Math.min(4,Math.floor(support*Math.min(1,.75+.05*power('聚')))));}
      const pairs=inputs.reduce((sum,i)=>sum+Object.keys(i.material.utilities).filter(u=>i.role==='jun'?(core?u===core:true):active(i.role,u)).length,0);
      energy=Math.ceil(20+(core?1.5*Math.max(0,candidate-1):0)+Math.max(0,count-1)+.5*Math.max(0,pairs-1)+.25*total);
    }
    let reason=!stats.bound?'尚未绑定人物资料。':!main?'先放入一味君药。':stats.attainment<8?'丹道造诣不足。':spirit>availableSpirit?'本炉灵识不足。':energy>stats.energy?'当前精力不足。':'';
    return {count,total,spirit,energy,hours,availableSpirit,loads,valid:!reason,reason};
  }
  window.AlchemyPreviewModel=Object.freeze({evaluate,active});
})();
