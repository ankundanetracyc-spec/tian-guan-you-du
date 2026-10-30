// Pointer-driven spatial preview. Only commits the independent fixture on valid release.
let inventoryDrag=null,dragSuppressUntil=0;
function applyItemRotation(button,item){
 const img=button.querySelector('img');if(!img||!item.rotation)return;
 const d=definition(item);Object.assign(img.style,{position:'absolute',left:'50%',top:'50%',width:d.width*CELL+'px',height:d.height*CELL+'px',maxWidth:'none',maxHeight:'none',transform:`translate(-50%,-50%) rotate(${item.rotation*90}deg)`});
}
function dragScale(){return $('stage').getBoundingClientRect().width/1920}
function drawDrag(){
 const s=inventoryDrag;if(!s?.active)return;
 const cell=CELL*dragScale(),left=s.x-s.grabX*cell,top=s.y-s.grabY*cell;
 Object.assign(s.ghost.style,{left:left+'px',top:top+'px',width:s.width*cell+'px',height:s.height*cell+'px'});
 Object.assign(s.image.style,{width:definition(s.item).width*cell+'px',height:definition(s.item).height*cell+'px',transform:`translate(-50%,-50%) rotate(${s.rotation*90}deg)`});
 s.target=null;s.marker.remove();
 for(const grid of document.querySelectorAll('.region-grid')){
  const r=grid.getBoundingClientRect();if(s.x<r.left||s.x>=r.right||s.y<r.top||s.y>=r.bottom)continue;
  const regionId=grid.closest('.region').dataset.regionId,shape=REGIONS.find(x=>x.id===regionId);
  const x=Math.round((s.x-r.left)/cell-s.grabX),y=Math.round((s.y-r.top)/cell-s.grabY);
  const within=x>=0&&y>=0&&x+s.width<=shape.width&&y+s.height<=shape.height;
  const collision=items.some(i=>i.instanceId!==s.item.instanceId&&i.containerId===BAG_ID&&i.regionId===regionId&&x<i.cellX+i.width&&x+s.width>i.cellX&&y<i.cellY+i.height&&y+s.height>i.cellY);
  s.target={regionId,x,y,valid:within&&!collision};
  s.marker.className='drop-footprint '+(s.target.valid?'is-valid':'is-invalid');
  Object.assign(s.marker.style,{left:x*CELL+'px',top:y*CELL+'px',width:s.width*CELL+'px',height:s.height*CELL+'px'});grid.append(s.marker);break;
 }
 s.ghost.classList.toggle('is-invalid',!s.target?.valid);
 s.label.textContent=s.target?.valid?'松开放置':'不可放置';
}
function activateDrag(){
 const s=inventoryDrag;if(!s||s.active)return;s.active=true;hideTooltip();
 s.ghost=element('div','inventory-drag-ghost');s.image=icon(s.item,'');s.label=element('span','drag-placement-label');s.ghost.append(s.image,s.label);document.body.append(s.ghost);
 s.marker=element('div','drop-footprint');s.source.classList.add('is-drag-source');document.body.classList.add('is-dragging-item');
 s.source.setPointerCapture(s.pointerId);$('location-status').textContent='R 旋转 · Esc 取消';drawDrag();
}
function finishInventoryDrag(commit=false){
 const s=inventoryDrag;if(!s)return;inventoryDrag=null;
 if(!s.active)return;
 const accepted=commit&&s.target?.valid;
 if(accepted){Object.assign(s.item,{regionId:s.target.regionId,cellX:s.target.x,cellY:s.target.y,width:s.width,height:s.height,rotation:s.rotation});locatedId=null;selectedId=s.item.instanceId;selectedGroupKey=keyFor(s.item)}
 s.source.classList.remove('is-drag-source');s.ghost.remove();s.marker.remove();document.body.classList.remove('is-dragging-item');
 if(s.source.hasPointerCapture(s.pointerId))s.source.releasePointerCapture(s.pointerId);
 dragSuppressUntil=performance.now()+250;
 renderWorkspace();updateClasses();$('location-status').textContent=accepted?'已放置：'+definition(s.item).name:'已取消移动';
}
document.addEventListener('pointerdown',e=>{
 if(e.button!==0||inventoryDrag)return;const source=e.target.closest('.bag-item');if(!source)return;
 const item=find(source.dataset.instanceId);if(!item||item.containerId!==BAG_ID)return;
 const r=source.getBoundingClientRect(),cell=CELL*dragScale();
 inventoryDrag={item,source,pointerId:e.pointerId,startX:e.clientX,startY:e.clientY,x:e.clientX,y:e.clientY,grabX:(e.clientX-r.left)/cell,grabY:(e.clientY-r.top)/cell,width:item.width,height:item.height,rotation:item.rotation||0,active:false};
});
document.addEventListener('pointermove',e=>{
 const s=inventoryDrag;if(!s||s.pointerId!==e.pointerId)return;
 if(e.pointerType==='mouse'&&!(e.buttons&1)){finishInventoryDrag();return}
 s.x=e.clientX;s.y=e.clientY;
 if(!s.active&&Math.hypot(s.x-s.startX,s.y-s.startY)>=4)activateDrag();
 if(s.active){e.preventDefault();drawDrag()}
});
document.addEventListener('pointerup',e=>{if(inventoryDrag?.pointerId===e.pointerId){if(inventoryDrag.active){inventoryDrag.x=e.clientX;inventoryDrag.y=e.clientY;drawDrag()}finishInventoryDrag(true)}});
document.addEventListener('pointercancel',()=>finishInventoryDrag());
document.addEventListener('lostpointercapture',()=>finishInventoryDrag());
window.addEventListener('blur',()=>finishInventoryDrag());
window.addEventListener('resize',()=>finishInventoryDrag());
document.addEventListener('click',e=>{if(performance.now()<dragSuppressUntil){e.preventDefault();e.stopImmediatePropagation()}},true);
document.addEventListener('keydown',e=>{
 const s=inventoryDrag;if(!s?.active)return;
 if(e.key==='Escape'){e.preventDefault();finishInventoryDrag();return}
 if(e.code!=='KeyR'||e.repeat||e.ctrlKey||e.altKey||e.metaKey)return;
 e.preventDefault();const x=s.grabX;s.grabX=s.height-s.grabY;s.grabY=x;[s.width,s.height]=[s.height,s.width];s.rotation=(s.rotation+1)%4;drawDrag();
});
