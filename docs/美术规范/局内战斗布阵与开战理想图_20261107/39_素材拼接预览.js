/* Material-only composition preview. It neither loads a save nor runs combat. */
(() => {
  'use strict';
  const manifest = window.BATTLE_UI_SKIN_MANIFEST;
  const assetRoot = '../../../textures/ui/combat_board_20261107_v1/';
  const canvas = document.getElementById('materialCanvas');
  const ctx = canvas.getContext('2d', { willReadFrequently: true });
  const images = {};
  const extra = {};
  const drawnArt = [];
  let buttonHover = '';
  const labels = {
    cell_face: '单格底面', outer_edge: '外边条', outer_corner: '外转角', inner_corner: '内转角',
    hand_connector: '手持连接件', hand_socket: '独立手持槽', junction_pin: '交点铆钉', focus_outline: '选中细框',
    primary_normal: '主按钮', primary_hover: '主按钮悬停', secondary_normal: '次按钮', secondary_hover: '次按钮悬停',
    source_panel: '来源底板', tooltip_panel: '悬停底板', resource_frame: '资源底板',
    stamina_glyph: '体力图符', mana_glyph: '法力图符', spirit_glyph: '灵识图符'
  };
  const fixtures = [
    { name: '九格拼接', origin: [170, 170], cells: [[0,0],[1,0],[2,0],[0,1],[1,1],[2,1],[0,2],[1,2],[2,2]] },
    { name: 'L形拼接', origin: [730, 170], cells: [[0,0],[1,0],[0,1],[1,1],[0,2],[1,2],[2,2]] },
    { name: '台阶拼接', origin: [170, 700], cells: [[0,0],[1,0],[0,1],[1,1],[2,1],[1,2],[2,2],[3,2],[2,3]] },
    { name: '凹口拼接', origin: [730, 700], cells: [[0,0],[2,0],[0,1],[1,1],[2,1],[0,2],[1,2],[2,2]] }
  ];
  const cellSize = 86;
  const key = (x,y) => `${x},${y}`;
  const load = path => new Promise((resolve,reject) => { const im = new Image(); im.onload = () => resolve(im); im.onerror = () => reject(new Error(`素材未加载：${path}`)); im.src = path; });

  function drawPart(name, x, y, w, h, target = ctx) {
    const part = manifest.parts[name], r = part.rect;
    target.drawImage(images[part.image], r[0],r[1],r[2],r[3], x,y,w,h);
  }
  function fitPart(name,x,y,w,h,target=ctx) {
    const r=manifest.parts[name].rect,s=Math.min(w/r[2],h/r[3]);
    drawPart(name,x+(w-r[2]*s)/2,y+(h-r[3]*s)/2,r[2]*s,r[3]*s,target);
  }
  function nineSlice(name, x, y, w, h, target = ctx) {
    const part = manifest.parts[name], r = part.rect;
    const s = part.source_insets, d = part.display_insets;
    if (!s) { drawPart(name,x,y,w,h,target); return; }
    const dx = [x,x+d[0],x+w-d[2],x+w], dy = [y,y+d[1],y+h-d[3],y+h];
    const sx = [r[0],r[0]+s[0],r[0]+r[2]-s[2],r[0]+r[2]], sy = [r[1],r[1]+s[1],r[1]+r[3]-s[3],r[1]+r[3]];
    for (let row=0;row<3;row++) for (let col=0;col<3;col++) {
      target.drawImage(images[part.image],sx[col],sy[row],sx[col+1]-sx[col],sy[row+1]-sy[row],dx[col],dy[row],dx[col+1]-dx[col],dy[row+1]-dy[row]);
    }
  }
  function text(value,x,y,size=23,color='#e8e2d3',align='left') {
    ctx.save();ctx.font=`${size}px "臻楷", "KaiTi", serif`;ctx.fillStyle=color;ctx.textAlign=align;ctx.fillText(value,x,y);ctx.restore();
  }
  function edge(x,y,length,vertical=false,alpha=1,thickness=3.4) {
    ctx.save();ctx.globalAlpha=alpha;ctx.translate(x,y);if(vertical)ctx.rotate(Math.PI/2);drawPart('outer_edge',0,-thickness/2,length,thickness);ctx.restore();
  }
  function corner(name,x,y,turn) {
    const anchor=manifest.parts[name].anchor_fraction;
    ctx.save();ctx.translate(x,y);ctx.rotate(turn*Math.PI/2);drawPart(name,-18*anchor[0],-18*anchor[1],18,18);ctx.restore();
  }
  function drawBoard(cells,ox,oy,size,groups=[]) {
    const present=new Set(cells.map(c=>key(...c))), has=(x,y)=>present.has(key(x,y));
    const owners=new Map();groups.forEach((group,id)=>group.forEach(c=>owners.set(key(...c),id)));
    const same=(x,y,xx,yy)=>owners.has(key(x,y))&&owners.get(key(x,y))===owners.get(key(xx,yy));
    // Pass 1: only real cells receive paper. Missing coordinates stay transparent.
    for(const [x,y] of cells)drawPart('cell_face',ox+x*size,oy+y*size,size,size);
    // Pass 2: external edges and faint shared seams. No rectangular backing exists.
    for(const [x,y] of cells) {
      const px=ox+x*size,py=oy+y*size;
      if(!has(x,y-1))edge(px,py,size);
      if(!has(x-1,y))edge(px,py,size,true);
      if(!has(x+1,y))edge(px+size,py,size,true);
      else if(!same(x,y,x+1,y))edge(px+size,py,size,true,.38,1.25);
      if(!has(x,y+1))edge(px,py+size,size);
      else if(!same(x,y,x,y+1))edge(px,py+size,size,false,.38,1.25);
    }
    const vertices=new Set();for(const [x,y] of cells)for(const [dx,dy]of[[0,0],[1,0],[0,1],[1,1]])vertices.add(key(x+dx,y+dy));
    for(const vertex of vertices) {
      const [x,y]=vertex.split(',').map(Number),q=[has(x,y),has(x-1,y),has(x-1,y-1),has(x,y-1)];
      const count=q.filter(Boolean).length,px=ox+x*size,py=oy+y*size;
      if(count===1)corner('outer_corner',px,py,q.indexOf(true));
      else if(count===3)corner('inner_corner',px,py,[1,2,3,0][q.indexOf(false)]);
      else if(count===4) {ctx.save();ctx.globalAlpha=.48;drawPart('junction_pin',px-2.2,py-2.2,4.4,4.4);ctx.restore();}
    }
  }
  function fitImage(im,x,y,w,h,padding=8,target=ctx) {
    const scale=Math.min((w-padding*2)/im.width,(h-padding*2)/im.height),dw=im.width*scale,dh=im.height*scale;
    target.drawImage(im,x+(w-dw)/2,y+(h-dh)/2,dw,dh);
    return {x:x+(w-dw)/2,y:y+(h-dh)/2,width:dw,height:dh};
  }
  function hand(ox,oy,size) {
    drawPart('hand_connector',ox-14,oy+size*.38,28,size*.24);
    drawPart('hand_socket',ox-size-8,oy,size,size);
    if(document.getElementById('artToggle').checked)fitImage(extra.sword,ox-size-8,oy,size,size,8);
  }
  function fixture(f) {
    const [ox,oy]=f.origin;
    text(f.name,ox-12,oy-40,30);
    drawBoard(f.cells,ox,oy,cellSize,[[[1,1],[1,2]]]);hand(ox,oy,cellSize);
    if(document.getElementById('artToggle').checked) {
      const underlay=ctx.getImageData(ox+cellSize,oy+cellSize,cellSize,cellSize*2);
      const palm=fitImage(extra.palm,ox,oy,cellSize,cellSize,8);
      const fist=fitImage(extra.fist,ox+cellSize,oy+cellSize,cellSize,cellSize*2,8);
      drawnArt.push({fixture:f.name,palm,fist,underlay});
    }
  }
  function render() {
    ctx.clearRect(0,0,canvas.width,canvas.height);drawnArt.length=0;
    for(const f of fixtures)fixture(f);
    text('来源底板与格形示意',1155,43,24);
    nineSlice('source_panel',1150,64,500,500);
    text('招式　化法　丹药　符箓　法宝',1180,110,23,'#352e26');
    ctx.strokeStyle='#7b7262';ctx.lineWidth=1;ctx.strokeRect(1182,128,436,39);text('搜索',1200,154,20,'#6c6558');
    drawBoard([[0,0]],1190,190,64);drawBoard([[0,0],[0,1]],1190,288,64,[[[0,0],[0,1]]]);
    if(document.getElementById('artToggle').checked) {
      fitImage(extra.palm,1190,190,64,64,4);fitImage(extra.fist,1190,288,64,128,4);
    }
    text('应敌',1370,231,27,'#2e2922');text('痛击',1370,360,27,'#2e2922');
    nineSlice('secondary_normal',1280,480,240,47);text('方案',1400,512,24,'#30291f','center');
    text('只读悬停底板',1155,622,24);nineSlice('tooltip_panel',1150,642,500,185);
    text('按钮常态／移入查看悬停',1155,875,24);
    nineSlice(buttonHover==='primary'?'primary_hover':'primary_normal',1150,898,240,50);
    nineSlice(buttonHover==='secondary'?'secondary_hover':'secondary_normal',1410,898,240,50);
    text('开始战斗',1270,933,23,'#eee6d5','center');text('方案',1530,933,23,'#3c3026','center');
    nineSlice('resource_frame',1150,985,500,133);
    for(const [i,part,label]of[[0,'stamina_glyph','体力'],[1,'mana_glyph','法力'],[2,'spirit_glyph','灵识占用']]) {
      fitPart(part,1185,1002+i*31,23,25);text(label,1222,1024+i*31,22,'#312c25');text('— / —',1550,1024+i*31,22,'#312c25','right');
    }
  }
  function inspect() {
    const results=[];
    for(const f of fixtures) {
      const set=new Set(f.cells.map(c=>key(...c))),[ox,oy]=f.origin;
      let painted=0,clear=0;const errors=[];
      const width=Math.max(...f.cells.map(c=>c[0]))+1,height=Math.max(...f.cells.map(c=>c[1]))+1;
      for(let y=0;y<height;y++)for(let x=0;x<width;x++) {
        const alpha=ctx.getImageData(Math.round(ox+(x+.5)*cellSize),Math.round(oy+(y+.5)*cellSize),1,1).data[3];
        if(set.has(key(x,y))) {painted++;if(alpha<220)errors.push(`未绘制有效格 ${x},${y}`);}
        else {clear++;if(alpha!==0)errors.push(`缺口被填充 ${x},${y}`);}
      }
      results.push({pattern:f.name,validCells:painted,missingCells:clear,errors});
    }
    const layerChecks=[];
    if(document.getElementById('artToggle').checked) {
      const only=document.createElement('canvas');only.width=canvas.width;only.height=canvas.height;
      const o=only.getContext('2d',{willReadFrequently:true});
      for(const f of fixtures) {
        const [ox,oy]=f.origin;o.clearRect(0,0,only.width,only.height);
        fitImage(extra.fist,ox+cellSize,oy+cellSize,cellSize,cellSize*2,8,o);
        const yy=oy+cellSize*2;
        let tested=0,bad=0;
        for(let xx=ox+cellSize+9;xx<ox+cellSize*2-9;xx++) {
          const icon=o.getImageData(xx,yy,1,1).data;if(icon[3]<16)continue;
          const actual=ctx.getImageData(xx,yy,1,1).data;
          const background=drawnArt.find(a=>a.fixture===f.name).underlay;
          const at=(cellSize*cellSize+xx-(ox+cellSize))*4,fa=icon[3]/255,ba=background.data[at+3]/255,out=fa+ba*(1-fa);
          const expected=[0,1,2].map(c=>(icon[c]*fa+background.data[at+c]*ba*(1-fa))/out);
          tested++;if(Math.max(...expected.map((v,c)=>Math.abs(v-actual[c])))>5)bad++;
        }
        layerChecks.push({pattern:f.name,paintedIconPixelsAtGridSeam:tested,overpaintedPixels:bad});
      }
    }
    return {patterns:results,iconLayerChecks:layerChecks,artCopiesPerBoard:drawnArt.map(a=>({pattern:a.fixture,palm:1,fist:1})),assetParts:Object.keys(manifest.parts).length,pngCount:Object.keys(manifest.images).length,buttonHover,overflow:document.documentElement.scrollWidth>innerWidth};
  }
  function gallery() {
    const host=document.getElementById('partGallery');
    for(const name of manifest.part_order) {
      const card=document.createElement('div');card.className='part-card';
      const thumb=document.createElement('canvas');thumb.width=240;thumb.height=140;
      const t=thumb.getContext('2d'),r=manifest.parts[name].rect,scale=Math.min(216/r[2],116/r[3]);
      drawPart(name,(240-r[2]*scale)/2,(140-r[3]*scale)/2,r[2]*scale,r[3]*scale,t);
      const label=document.createElement('span');label.textContent=labels[name]||name;
      card.append(thumb,label);host.appendChild(card);
    }
  }
  document.getElementById('artToggle').addEventListener('change',render);
  document.getElementById('backgroundToggle').addEventListener('click',event=>{
    const stage=document.getElementById('stage'),light=stage.dataset.light!=='true';stage.dataset.light=String(light);event.currentTarget.textContent=light?'切换深底':'切换浅底';
  });
  canvas.addEventListener('pointermove',event=>{
    const r=canvas.getBoundingClientRect(),x=(event.clientX-r.left)*canvas.width/r.width,y=(event.clientY-r.top)*canvas.height/r.height;
    const hit=y>=898&&y<=948?(x>=1150&&x<=1390?'primary':x>=1410&&x<=1650?'secondary':''):'';
    if(hit!==buttonHover){buttonHover=hit;render();}
  });
  canvas.addEventListener('pointerleave',()=>{buttonHover='';render();});
  Promise.all([
    ...Object.keys(manifest.images).map(async name=>{images[name]=await load(assetRoot+name);}),
    load('../九格招式盘与战斗界面_20261106/招式样张_应敌_1x1.png').then(im=>{extra.palm=im;}),
    load('../九格招式盘与战斗界面_20261106/招式样张_痛击_1x2.png').then(im=>{extra.fist=im;}),
    load('../../../textures/items/inventory_density_20261029/slender_sword_2x6.png').then(im=>{extra.sword=im;}),
    document.fonts.ready
  ]).then(()=>{
    render();gallery();window.materialPreview={inspect,render,manifest};
    document.getElementById('status').textContent='5张透明源图、19个可单独引用的区域。示意图标用于检查图层，未接入战斗与真实资源。';
  }).catch(error=>{document.getElementById('status').textContent=error.message;throw error;});
})();
