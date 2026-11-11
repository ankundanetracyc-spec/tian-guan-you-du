(() => {
  'use strict';
  const NS='http://www.w3.org/2000/svg', signal='#bb6652', ink='#51463d';
  const guideToggle=document.getElementById('show-guides');
  const portData={
    counter:{W:{mouth:[.0519,.5096],core:[.0716,.5096],width:6.49,seal:1.1,outside:6},N:{mouth:[.7370,.1871],core:[.7370,.2266],width:6.35,seal:1.1,outside:5.7}},
    split:{W:{mouth:[.11563,.57257],core:[.16507,.57257],width:5.51,seal:2.6},N:{mouth:[.50598,.16587],core:[.50598,.2177],width:5.21,seal:2.6},E:{mouth:[.88517,.57217],core:[.83931,.57217],width:5.61,seal:2.6}},
    merge:{W:{mouth:[.10128,.4996],core:[.17982,.4996],width:3.98,seal:6.6},S:{mouth:[.48764,.87321],core:[.48764,.80343],width:3.78,seal:5.6},E:{mouth:[.89952,.4996],core:[.83094,.4996],width:3.98,seal:5.9}},
    sensor:{E:{mouth:[.8533,.5056],core:[.7974,.5056],width:4.3,seal:3.05}},
    source:{E:{mouth:[.9679,.4966],core:[.9543,.4966],width:5.3,network:'energy'}},
    ward:{N:{mouth:[.5006,.0880],core:[.5006,.1088],width:7.5},S:{mouth:[.5006,.9115],core:[.5006,.8825],width:9.5,network:'energy'}}
  };
  const counter={name:'counter',origin:[128,64],size:[256,128],turn:0,offset:[3.33,-1.23]}, split={name:'split',origin:[64,256],size:[128,128],turn:0,offset:[0,-9.3]}, merge={name:'merge',origin:[448,256],size:[128,128],turn:90,offset:[1.6,0]};
  let count=0, releases=0, didOutput=false;
  function el(parent,tag,attrs={},text){const n=document.createElementNS(NS,tag);for(const[k,v]of Object.entries(attrs))n.setAttribute(k,String(v));if(text!==undefined)n.textContent=text;parent.appendChild(n);return n;}
  function lane(parent,attrs={}){const g=el(parent,'g',attrs);return{edge:el(g,'g'),fill:el(g,'g'),brush:el(g,'g')};}
  function wire(target,d,width=target.width||6.5){el(target.edge,'path',{d,fill:'none',stroke:ink,'stroke-width':width+3,'stroke-linecap':'round','stroke-linejoin':'round'});el(target.fill,'path',{d,fill:'none',stroke:target.color||signal,'stroke-width':width,'stroke-linecap':'round','stroke-linejoin':'round'});el(target.brush,'path',{d,fill:'none',stroke:target.light||'#e0a18b','stroke-width':.8,'stroke-opacity':.32,'stroke-dasharray':'9 16 2 13'});}
  function globalPoint(node,uv,art=true){let[x,y]=[uv[0]*node.size[0]+(art?node.offset[0]:0),uv[1]*node.size[1]+(art?node.offset[1]:0)];if(node.turn===90)[x,y]=[node.size[1]-y,x];if(node.turn===180)[x,y]=[node.size[0]-x,node.size[1]-y];if(node.turn===270)[x,y]=[y,node.size[0]-x];return[node.origin[0]+x,node.origin[1]+y];}
  function logical(node,side){const uv={W:[0,.5],N:[node.name==='counter'?.75:.5,0],E:[1,.5],S:[.5,1]}[side];return globalPoint(node,uv,false);}
  function taper(target,a,b,wa,wb){const dx=b[0]-a[0],dy=b[1]-a[1],len=Math.hypot(dx,dy);if(!len)return;const nx=-dy/len,ny=dx/len;const points=[[a[0]+nx*wa/2,a[1]+ny*wa/2],[b[0]+nx*wb/2,b[1]+ny*wb/2],[b[0]-nx*wb/2,b[1]-ny*wb/2],[a[0]-nx*wa/2,a[1]-ny*wa/2]].map(p=>p.join(',')).join(' ');el(target.edge,'polygon',{points,fill:ink,stroke:ink,'stroke-width':3,'stroke-linejoin':'round'});el(target.fill,'polygon',{points,fill:target.color||signal});}
  function socket(target,node,side){const port=portData[node.name][side],a=logical(node,side),b=globalPoint(node,port.mouth),c=globalPoint(node,port.core),main=target.width||6.5,w=Math.min(main,port.width);taper(target,a,b,main,w);taper(target,b,c,w,w);}
  function part(parent,node){const basic=['sensor','source','ward','eye'].includes(node.name),name=node.name+(node.size[0]===256?'_2x1':node.size[1]===256?'_1x2':'_1x1'),href=(basic?'../铜纹阵盘基础元件_20261108/':'')+name+'.png';let tx=node.origin[0],ty=node.origin[1];if(node.turn===90)tx+=node.size[1];if(node.turn===180){tx+=node.size[0];ty+=node.size[1];}if(node.turn===270)ty+=node.size[0];el(parent,'image',{href,x:node.offset[0],y:node.offset[1],width:node.size[0],height:node.size[1],transform:`translate(${tx} ${ty}) rotate(${node.turn})`,preserveAspectRatio:'xMidYMid meet'});}
  function bridges(parent,node,sides,color=signal){for(const side of sides){const p=portData[node.name][side];if(!p.seal)continue;const outside=p.outside||.35,v={W:[-1,0],N:[0,-1],E:[1,0],S:[0,1]}[side],a=globalPoint(node,[p.mouth[0]+v[0]*outside/node.size[0],p.mouth[1]+v[1]*outside/node.size[1]]),b=globalPoint(node,[p.mouth[0]-v[0]*p.seal/node.size[0],p.mouth[1]-v[1]*p.seal/node.size[1]]);el(parent,'path',{d:`M${a.join(' ')}L${b.join(' ')}`,fill:'none',stroke:color,'stroke-width':p.width-.8,'stroke-linecap':'butt'});}}
  function guides(parent,node,sides){const group=el(parent,'g',{'data-guides':'true',visibility:guideToggle.checked?'visible':'hidden'}),rotated=node.turn%180!==0;el(group,'rect',{x:node.origin[0],y:node.origin[1],width:node.size[rotated?1:0],height:node.size[rotated?0:1],class:'guide-line'});if(node.size[0]===256){const a=globalPoint(node,[.5,0],false),b=globalPoint(node,[.5,1],false);el(group,'path',{d:`M${a.join(' ')}L${b.join(' ')}`,class:'guide-line'});}for(const side of sides){const port=portData[node.name][side],p=globalPoint(node,port.mouth),d=`M${p[0]} ${p[1]-4}L${p[0]+4} ${p[1]}L${p[0]} ${p[1]+4}L${p[0]-4} ${p[1]}Z`;for(const cls of ['port-halo','port-mark'])port.network==='energy'?el(group,'circle',{cx:p[0],cy:p[1],r:4.5,class:cls}):el(group,'path',{d,class:cls});}}
  function arrow(parent,x,y,direction='E'){const d=direction==='E'?`M${x-6} ${y-6}L${x+2} ${y}L${x-6} ${y+6}`:`M${x-6} ${y-6}L${x} ${y+2}L${x+6} ${y-6}`;el(parent,'path',{d,fill:'none',stroke:signal,'stroke-width':2.2,'stroke-linecap':'round','stroke-linejoin':'round'});}
  function renderCounter(){const svg=document.getElementById('counter-diagram');svg.replaceChildren();const input=lane(svg);wire(input,'M12 128H128');socket(input,counter,'W');const output=lane(svg,{class:'wire-output'});wire(output,'M320 64V40Q320 24 336 24H500');socket(output,counter,'N');part(svg,counter);bridges(svg,counter,['W']);bridges(el(svg,'g',{class:'wire-output'}),counter,['N']);guides(svg,counter,['W','N']);el(svg,'text',{x:18,y:109},'输入');el(svg,'text',{x:390,y:54,class:'output-label'},'控制信号');arrow(svg,96,128);arrow(svg,472,24);svg.classList.toggle('is-output',didOutput);}
  function renderBranches(){const svg=document.getElementById('branch-diagram');svg.replaceChildren();const net=lane(svg);wire(net,'M6 320H64');socket(net,split,'W');wire(net,'M192 320H448');socket(net,split,'E');socket(net,merge,'S');wire(net,'M128 256V80Q128 64 144 64H496Q512 64 512 80V256');socket(net,split,'N');socket(net,merge,'W');wire(net,'M512 384V440');socket(net,merge,'E');part(svg,split);part(svg,merge);bridges(svg,split,['W','N','E']);bridges(svg,merge,['W','S','E']);guides(svg,split,['W','N','E']);guides(svg,merge,['W','S','E']);el(svg,'text',{x:128,y:420,'text-anchor':'middle'},'分脉');el(svg,'text',{x:544,y:420},'合脉');el(svg,'text',{x:320,y:43,'text-anchor':'middle'},'同一事件 · 绕行一路');el(svg,'text',{x:320,y:302,'text-anchor':'middle'},'同一事件 · 直行一路');arrow(svg,300,64);arrow(svg,320,320);arrow(svg,512,428,'S');}
  function renderFourGrid(){
    const svg=document.getElementById('four-grid-diagram');svg.replaceChildren();
    el(svg,'rect',{x:0,y:0,width:512,height:512,fill:'#b4a796'});
    const grid=el(svg,'g',{'data-guides':'true',visibility:guideToggle.checked?'visible':'hidden'});
    for(let i=0;i<=4;i++)el(grid,'path',{d:`M${i*128} 0V512M0 ${i*128}H512`,class:'guide-line'});
    const nodes={
      eye:{name:'eye',origin:[128,128],size:[128,128],turn:0,offset:[0,0]},
      counter:{...counter,origin:[384,0],turn:270},
      sensor:{name:'sensor',origin:[384,256],size:[128,128],turn:270,offset:[0,-.7168]},
      ward:{name:'ward',origin:[0,128],size:[128,256],turn:0,offset:[-.0768,0]},
      source:{name:'source',origin:[256,384],size:[256,128],turn:180,offset:[0,.4352]}
    };
    const control=lane(svg);wire(control,'M384 64H80Q64 64 64 80V128');
    socket(control,nodes.counter,'N');socket(control,nodes.counter,'W');socket(control,nodes.sensor,'E');socket(control,nodes.ward,'N');
    const power=lane(svg);Object.assign(power,{width:10.5,color:'#ceb787',light:'#efe0b5'});wire(power,'M256 448H80Q64 448 64 432V384');socket(power,nodes.source,'E');socket(power,nodes.ward,'S');
    for(const node of Object.values(nodes))part(svg,node);
    bridges(svg,nodes.counter,['W','N']);bridges(svg,nodes.sensor,['E']);
    guides(svg,nodes.eye,[]);guides(svg,nodes.counter,['W','N']);guides(svg,nodes.sensor,['E']);guides(svg,nodes.ward,['N','S']);guides(svg,nodes.source,['E']);
  }
  function updateCounter(){document.getElementById('counter-value').textContent=`已数${count}／3次`;const status=document.getElementById('counter-status');status.classList.toggle('did-output',didOutput);status.textContent=didOutput?'数满3次，发出一次控制信号；计数归零。':count?`已记下第${count}次，继续等待。`:'送入信号，看看候枢如何计次。';document.getElementById('counter-diagram').dataset.count=String(count);document.getElementById('counter-diagram').dataset.releases=String(releases);renderCounter();}
  document.getElementById('send-signal').addEventListener('click',()=>{count++;didOutput=count===3;if(didOutput){count=0;releases++;}updateCounter();});
  document.getElementById('reset-counter').addEventListener('click',()=>{count=0;releases=0;didOutput=false;updateCounter();});
  guideToggle.addEventListener('change',()=>{renderCounter();renderBranches();renderFourGrid();});
  updateCounter();renderBranches();renderFourGrid();
})();
