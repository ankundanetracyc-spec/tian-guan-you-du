(() => {
'use strict';
const W=1600,H=900,DURATION=22.5,canvas=document.querySelector('#battle'),ctx=canvas.getContext('2d',{alpha:false});
const el=id=>document.getElementById(id),clamp=(v,a=0,b=1)=>Math.min(b,Math.max(a,v)),mix=(a,b,p)=>a+(b-a)*p;
const smooth=p=>{p=clamp(p);return p*p*(3-2*p)},out=p=>1-Math.pow(1-clamp(p),3),TAU=Math.PI*2;
const images={},meta=window.BATTLE_ASSETS.meta;
let t=0,playing=true,speed=1,fx=true,focus=true,sound=false,ready=false,prev=0,raf=0,audio=null;
const keys=[
 [0,460,1140,'idle','idle','候势','松风入袖，静候交锋','none'],
 [1.8,460,1140,'move','move','接敌','双方进入交锋位置','none'],
 [2.8,620,1000,'idle','idle','候势','一招未发，气机先定','none'],
 [3.35,620,1000,'move','guard','应敌','近身出掌','A'],
 [4.10,768,1000,'strike','guard','应敌','近身出掌','A'],
 [4.27,768,1000,'strike','guard','架招','来势相接 · 化解','B'],
 [4.66,768,1000,'move','guard','回势','收掌，卸去余劲','A'],
 [5.35,700,1000,'idle','idle','候势','静待后招','none'],
 [6.1,700,1000,'guard','move','应敌','对手进身出掌','B'],
 [6.87,700,912,'guard','strike','应敌','对手进身出掌','B'],
 [7.05,700,912,'guard','strike','架招','来势相接 · 化解','A'],
 [7.45,700,912,'guard','move','回势','收势回身','B'],
 [8.2,700,1010,'idle','idle','候势','凝神蓄势','none'],
 [9.12,700,1010,'guard','idle','痛击','沉肩蓄力','A'],
 [9.65,700,1010,'move','idle','痛击','踏步进身','A'],
 [10.18,810,1010,'strike','idle','痛击','掌劲将发','A'],
 [10.35,810,1010,'strike','hurt','痛击','重掌命中','A'],
 [10.55,810,1010,'strike','hurt','痛击','余劲推退','A'],
 [11.2,820,1120,'idle','hurt','余势','墨散，衣袂回落','none'],
 [12.2,820,1120,'idle','idle','候势','重新稳住身形','none'],
 [13.0,820,1120,'guard','move','应敌','对手再度切入','B'],
 [13.8,820,1032,'guard','strike','应敌','近身出掌','B'],
 [13.97,820,1032,'guard','strike','架招','横臂化解','A'],
 [14.38,820,1032,'guard','move','回势','袖口余墨消散','B'],
 [15.25,800,1080,'idle','idle','候势','最后一式','none'],
 [16.0,800,1080,'move','idle','痛击','进身追掌','A'],
 [16.76,880,1080,'strike','idle','痛击','劲透掌心','A'],
 [16.93,880,1080,'strike','hurt','痛击','重掌命中','A'],
 [17.14,880,1080,'strike','hurt','痛击','余劲推退','A'],
 [17.85,890,1180,'idle','hurt','收势','尘落，松声又起','none'],
 [18.6,890,1180,'idle','idle','收势','一轮演武结束','none'],
 [20.0,890,1180,'move','move','归位','再次演武','none'],
 [21.6,460,1140,'idle','idle','候势','松风入袖，静候交锋','none'],
 [22.5,460,1140,'idle','idle','候势','松风入袖，静候交锋','none']
];
const contacts=[{at:4.27,dir:1,type:'guard',x:910},{at:7.05,dir:-1,type:'guard',x:783},{at:10.35,dir:1,type:'hit',x:936},{at:13.97,dir:-1,type:'guard',x:903},{at:16.93,dir:1,type:'heavy',x:1007}];
function stateAt(time){
 let index=keys.length-2;for(let i=0;i<keys.length-1;i++)if(time<keys[i+1][0]){index=i;break;}
 const k=keys[index],n=keys[index+1],u=clamp((time-k[0])/(n[0]-k[0])),p=smooth(u);
 const moving=k[3]==='move'||k[4]==='move';
 return {index,xA:mix(k[1],n[1],p),xB:mix(k[2],n[2],p),poseA:k[3],poseB:k[4],action:k[5],line:k[6],actor:k[7],u,moving};
}
function text(str,x,y,size,color='#efe7d4',align='left',font='"Microsoft YaHei"'){
 ctx.fillStyle=color;ctx.textAlign=align;ctx.font=`${size}px ${font}`;ctx.fillText(str,x,y);
}
function line(x1,y1,x2,y2,color,width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();ctx.moveTo(x1,y1);ctx.lineTo(x2,y2);ctx.stroke();}
function roundRect(x,y,w,h,r,fill,stroke){ctx.beginPath();ctx.roundRect(x,y,w,h,r);if(fill){ctx.fillStyle=fill;ctx.fill()}if(stroke){ctx.strokeStyle=stroke;ctx.lineWidth=1;ctx.stroke()}}
function sprite(name,x,y,w,h,alpha=1,rotation=0,flip=1){const im=images[name];if(!im)return;ctx.save();ctx.globalAlpha=alpha;ctx.translate(x,y);ctx.rotate(rotation);ctx.scale(flip,1);ctx.drawImage(im,-w/2,-h/2,w,h);ctx.restore();}
function actor(who,x,y,pose,dir,time){
 const g=meta.actors[who][pose],im=images[`${who}_${pose}`],scale=.535;
 let bob=0,tilt=0;
 if(pose==='idle')bob=-Math.sin(time*2.1+(who==='inner'?0:1.3))*1.15;
 if(pose==='move'){bob=-Math.abs(Math.sin(time*21))*3.6;tilt=.025*dir;}
 if(pose==='hurt')tilt=-.035*dir;
 ctx.save();ctx.fillStyle='rgba(17,26,22,.18)';ctx.beginPath();ctx.ellipse(x,y+2,51,11,0,0,TAU);ctx.fill();ctx.restore();
 ctx.save();ctx.translate(x,y+bob);ctx.rotate(tilt);ctx.scale(dir*scale,scale);ctx.imageSmoothingQuality='high';ctx.drawImage(im,-g.foot[0],-g.foot[1]);ctx.restore();
}
function trail(who,x,y,dir,pose,time,intensity=1){
 if(!fx||pose!=='move')return;
 sprite('fx_wake',x-dir*70,y-14,280,100,.34*intensity,0,dir);
 for(let j=2;j>0;j--){const g=meta.actors[who][pose];ctx.save();ctx.globalAlpha=.055*(3-j);ctx.translate(x-dir*24*j,y);ctx.scale(dir*.535,.535);ctx.drawImage(images[`${who}_${pose}`],-g.foot[0],-g.foot[1]);ctx.restore();}
}
function effects(time,behind=false){
 if(!fx)return;
 for(const c of contacts){
  const dt=time-c.at;
  if(dt<-.20||dt>1.05)continue;
  const strong=c.type!=='guard',phase=clamp((dt+.20)/.34),decay=clamp(1-Math.max(0,dt-.14)/.65);
  if(behind){
   if(dt<.5){const grow=out(phase),sz=(strong?325:255)*(.5+.5*grow);sprite('fx_sweep',c.x-c.dir*52,518,sz,sz,decay*.76,(-.4+.52*grow)*c.dir,c.dir)}
   continue;
  }
  if(dt>=0){
   if(c.type==='guard'){
    const k=out(clamp(dt/.13)),a=clamp(1-Math.max(0,dt-.22)/.36);
    sprite('fx_deflect',c.x+c.dir*19,545,180+28*k,190+18*k,a*.94,0,-c.dir);
   }
   const age=clamp(dt/.75),burst=(strong?245:155)*(.65+.65*out(age));
   sprite('fx_impact',c.x,519,burst,burst,(1-age)*(strong?.97:.79),c.dir*.18,c.dir);
   if(strong){
    sprite('fx_wake',c.x+c.dir*(72+75*out(age)),694,345,105,(1-age)*.76,0,c.dir);
   }
   for(let j=0;j<14;j++){
    const ang=(j*2.399+1.1),dist=15+age*(48+(j%4)*27),xx=c.x+Math.cos(ang)*dist,yy=517+Math.sin(ang)*dist*.6+age*age*45;
    ctx.globalAlpha=(1-age)*.75;ctx.fillStyle=j%4===0?'#d0ba80':j%3===0?'#f2e9ce':'#24342e';ctx.beginPath();ctx.ellipse(xx,yy,2+j%3,1.2+(j%2),ang,0,TAU);ctx.fill();ctx.globalAlpha=1;
   }
   if(dt<.65){const a=clamp(1-dt/.65);ctx.save();ctx.globalAlpha=a;text(c.type==='guard'?'化解':'命中',c.x,424-out(age)*21,26,c.type==='guard'?'#d7e4db':'#f0dfb3','center','KaiTi');ctx.restore();}
  }
 }
}
function render(time){
 time=clamp(time,0,DURATION);const s=stateAt(time);
 let z=1,cx=800,cy=560,dark=0,shakeX=0,shakeY=0;
 if(focus)for(const c of contacts){
  const dt=time-c.at;
  if(dt>-.55&&dt<1.05){
   const env=dt<0?smooth((dt+.55)/.55):1-smooth(Math.max(0,dt-.28)/.77),strong=c.type!=='guard';
   z=1+env*(strong?.24:.16);cx=mix(800,c.x,env*.65);cy=545;dark=env*.39;
   if(dt>=0&&dt<.23){const amp=(strong?5:2.8)*(1-dt/.23);shakeX=Math.sin(dt*135)*amp;shakeY=Math.sin(dt*107)*amp*.45;}
  }
 }
 ctx.clearRect(0,0,W,H);ctx.save();ctx.translate(W/2+shakeX,H*.61+shakeY);ctx.scale(z,z);ctx.translate(-cx,-cy);
 ctx.drawImage(images.background,0,0,W,H);
 const haze=ctx.createLinearGradient(0,190,0,650);haze.addColorStop(0,'rgba(246,243,229,.025)');haze.addColorStop(.7,'rgba(231,237,223,.05)');haze.addColorStop(1,'rgba(17,32,25,0)');ctx.fillStyle=haze;ctx.fillRect(0,130,W,600);
 // The atmosphere is independent of combat outcomes and remains low contrast.
 for(let i=0;i<16;i++){const x=(i*127+time*(4+i%3))%1740-60,y=280+(i*53)%380+Math.sin(time*.7+i)*8;ctx.globalAlpha=.08+(i%3)*.025;ctx.fillStyle='#dcd7bd';ctx.beginPath();ctx.ellipse(x,y,2.2,1.1,.4,0,TAU);ctx.fill();}ctx.globalAlpha=1;
 trail('inner',s.xA,700,1,s.poseA,time);trail('outer',s.xB,700,-1,s.poseB,time);
 effects(time,true);
 actor('inner',s.xA,700,s.poseA,1,time);actor('outer',s.xB,700,s.poseB,-1,time);
 effects(time,false);
 if(dark>0){
  const g=ctx.createRadialGradient((s.xA+s.xB)/2,535,150,(s.xA+s.xB)/2,535,660);g.addColorStop(0,'rgba(15,26,23,0)');g.addColorStop(.4,`rgba(15,26,23,${dark*.25})`);g.addColorStop(1,`rgba(11,21,20,${dark})`);ctx.fillStyle=g;ctx.fillRect(-500,-400,2600,1800);
 }
 for(const [x,label,color] of [[s.xA,'玉虚 · 内门','#ecdfc1'],[s.xB,'玉虚 · 外门','#d4e0dc']]){
  roundRect(x-62,718,124,30,3,'rgba(22,35,29,.78)','#d5c59e33');text(label,x,739,16,color,'center','KaiTi');
 }
 ctx.restore();
 const top=ctx.createLinearGradient(0,0,0,160);top.addColorStop(0,'rgba(17,28,24,.82)');top.addColorStop(1,'rgba(17,28,24,0)');ctx.fillStyle=top;ctx.fillRect(0,0,W,160);
 text('松风试演',50,67,40,'#f0e7d1','left','KaiTi');line(51,83,213,83,'#cab17b',2);text('玉虚演武  /  一对一',52,110,14,'#d1d9c9');
 roundRect(1320,31,229,48,3,'rgba(29,43,35,.82)','#b5bd9c66');text('水墨 · 动作与特效预览',1434,61,16,'#ddd9c7','center');
 const bottom=ctx.createLinearGradient(0,756,0,820);bottom.addColorStop(0,'rgba(18,30,25,0)');bottom.addColorStop(1,'rgba(18,30,25,.97)');ctx.fillStyle=bottom;ctx.fillRect(0,756,W,144);
 line(48,806,1552,806,'#a5b09955');
 text(s.actor==='A'?'玉虚内门':s.actor==='B'?'玉虚外门':'松风演武',54,839,14,'#a8b7a3');text(s.action,54,880,31,'#eadfc3','left','KaiTi');text(s.line,162,878,16,'#bbc4b2');
 const menu=[['应敌','近身出掌'],['架招','化解来袭'],['痛击','重掌退势']];
 menu.forEach(([name,desc],i)=>{const x=768+i*218,active=s.action===name;roundRect(x,821,200,66,4,active?'#bdab7b':'#2b3a31',active?'#e1c78f':'#8a9d7960');text(name,x+18,849,23,active?'#25352c':'#e3dcc5','left','KaiTi');text(desc,x+18,873,12,active?'#42523e':'#9eaf98');});
 ctx.fillStyle='#bca16d';ctx.fillRect(0,897,W*(time/DURATION),3);
 if(time<1.4){ctx.save();ctx.globalAlpha=Math.sin(clamp(time/1.4)*Math.PI)*.85;text('起  势',800,345,46,'#344b3d','center','KaiTi');ctx.restore();}
 if(time>18.6&&time<20){ctx.save();ctx.globalAlpha=Math.sin((time-18.6)/1.4*Math.PI)*.9;text('收  势',800,329,38,'#344b3d','center','KaiTi');ctx.restore();}
 el('scrub').value=time;el('time').textContent=`${time.toFixed(1)} / ${DURATION} s`;
 return {time,...s,zoom:z,effects:fx,focus};
}
function soundImpact(c){
 if(!sound)return;
 audio??=new (window.AudioContext||window.webkitAudioContext)();
 const now=audio.currentTime,n=Math.floor(audio.sampleRate*.16),buf=audio.createBuffer(1,n,audio.sampleRate),arr=buf.getChannelData(0);
 for(let i=0;i<n;i++)arr[i]=(Math.random()*2-1)*Math.pow(1-i/n,3);
 const source=audio.createBufferSource(),filter=audio.createBiquadFilter(),gain=audio.createGain();source.buffer=buf;filter.type='lowpass';filter.frequency.value=c.type==='guard'?1900:800;gain.gain.value=.14;source.connect(filter).connect(gain).connect(audio.destination);source.start(now);
 const osc=audio.createOscillator(),g=audio.createGain();osc.type='sine';osc.frequency.setValueAtTime(c.type==='guard'?660:115,now);osc.frequency.exponentialRampToValueAtTime(c.type==='guard'?220:45,now+.16);g.gain.setValueAtTime(.09,now);g.gain.exponentialRampToValueAtTime(.001,now+.18);osc.connect(g).connect(audio.destination);osc.start(now);osc.stop(now+.2);
}
function frame(now){
 const dt=Math.min(.05,(now-prev)/1000||0);prev=now;
 if(ready&&playing){let nt=t+dt*speed;if(nt>DURATION)nt=0;for(const c of contacts)if(c.at>t&&c.at<=nt)soundImpact(c);t=nt;}
 if(ready)render(t);raf=requestAnimationFrame(frame);
}
function play(value){playing=value;el('play').textContent=playing?'暂停':'继续';}
el('replay').onclick=()=>{t=0;play(true)};el('play').onclick=()=>play(!playing);
el('speed').onchange=e=>{speed=Number(e.target.value)};
el('fx').onclick=()=>{fx=!fx;el('fx').setAttribute('aria-pressed',fx);el('fx').textContent=`特效：${fx?'开':'关'}`;render(t)};
el('focus').onclick=()=>{focus=!focus;el('focus').setAttribute('aria-pressed',focus);el('focus').textContent=`聚焦：${focus?'开':'关'}`;render(t)};
el('sound').onclick=()=>{sound=!sound;el('sound').setAttribute('aria-pressed',sound);el('sound').textContent=`音效：${sound?'开':'关'}`;if(sound){audio??=new (window.AudioContext||window.webkitAudioContext)();audio.resume()}};
el('scrub').oninput=e=>{t=Number(e.target.value);play(false);render(t)};
window.addEventListener('keydown',e=>{if(['INPUT','SELECT'].includes(document.activeElement.tagName))return;if(e.code==='Space'){e.preventDefault();play(!playing)}if(e.code==='ArrowRight'||e.code==='ArrowLeft'){e.preventDefault();t=clamp(t+(e.code==='ArrowRight'?.2:-.2),0,DURATION);play(false);render(t)}});
window.preview={get ready(){return ready},get duration(){return DURATION},seek(value){t=clamp(value,0,DURATION);play(false);return render(t)},state(){return {playing,speed,fx,focus,sound,...stateAt(t),time:t}},renderAt:render,contacts,play};
Promise.all(Object.entries(window.BATTLE_ASSETS.images).map(([name,url])=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>{images[name]=im;resolve()};im.onerror=()=>reject(new Error(`图片载入失败：${name}`));im.src=url}))).then(()=>{ready=true;render(0);raf=requestAnimationFrame(frame)}).catch(e=>{el('error').style.display='block';el('error').textContent=e.message;console.error(e)});
})();
