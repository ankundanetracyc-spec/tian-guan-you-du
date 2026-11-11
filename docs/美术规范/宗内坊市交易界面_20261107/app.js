/* Isolated UI sample; no project data, backend, storage or real transactions. */
const iconRoot = '../../../textures/items/inventory_density_20261029/';
const sampleGoods = [
  {id:'mist',name:'雾铃花',category:'灵植',icon:'mat_mist_bell_flower.png',stock:3,price:2,seller:'同门甲',desc:'生于湿润地带的灵植。'},
  {id:'moss',name:'苦苔根',category:'灵植',icon:'bitter_moss_root_2x4.png',stock:2,price:2,seller:'同门乙',desc:'可用作材料的山野植物。'},
  {id:'bone',name:'兽骨',category:'兽材',icon:'mat_beast_bone.png',stock:2,price:3,seller:'同门丙',desc:'妖兽留下的骨材。'},
  {id:'copper',name:'铜髓矿',category:'矿材',icon:'mat_tong_sui_ore.png',stock:1,price:4,seller:'同门丁',desc:'可用于制作的矿材。'},
];
const sampleInventory = [
  {id:'mist',name:'雾铃花',category:'灵植',icon:'mat_mist_bell_flower.png',count:4},
  {id:'moss',name:'苦苔根',category:'灵植',icon:'bitter_moss_root_2x4.png',count:3},
  {id:'bone',name:'兽骨',category:'兽材',icon:'mat_beast_bone.png',count:2},
];
const categories=['全部','灵植','兽材','矿材'];
let tab='market', filter='全部', selected=sampleGoods[0].id, selectedInventory='mist';
let balance=18, quantity=1, rented=false, pending=0, listings=[], modalAction=null;
const $=id=>document.getElementById(id);
const icon=name=>iconRoot+name;
const message=value=>{$('message').textContent=value};
function fit(){document.documentElement.style.setProperty('--scale',Math.min(innerWidth/1664,innerHeight/936));}
addEventListener('resize',fit);fit();
function modal(title,body,action,confirmText='确认'){
  $('modal-title').textContent=title;$('modal-body').innerHTML=body;$('modal-confirm').textContent=confirmText;
  modalAction=action;$('modal').hidden=false;$('modal-confirm').focus();
}
function closeModal(){$('modal').hidden=true;modalAction=null;}
$('modal-cancel').onclick=closeModal;$('modal-x').onclick=closeModal;
$('modal-confirm').onclick=()=>{const action=modalAction;closeModal();if(action)action();};
$('modal').onclick=e=>{if(e.target===$('modal'))closeModal()};
document.addEventListener('keydown',e=>{if(e.key==='Escape'){if(!$('modal').hidden)closeModal();else if(tab==='stall')switchTab('market');}});
function switchTab(next){tab=next;document.querySelectorAll('.tab').forEach(b=>b.classList.toggle('active',b.dataset.tab===tab));$('market-view').hidden=tab!=='market';$('stall-view').hidden=tab!=='stall';message(tab==='market'?'选择一件物品查看详情':'选择行囊物品设置售价');render();}
document.querySelectorAll('.tab').forEach(b=>b.onclick=()=>switchTab(b.dataset.tab));
$('back').onclick=()=>{if(tab==='stall')switchTab('market');else message('样板已在坊市入口；返回和光峰将在接入游戏后实现。');};
$('search').oninput=()=>renderMarket();
function render(){ $('balance').textContent=balance;renderMarket();renderStall(); }
function renderMarket(){
  $('categories').innerHTML=categories.map(cat=>`<button class="category ${cat===filter?'active':''}" data-category="${cat}"><span>${cat}</span><small>${cat==='全部'?'ALL':String(sampleGoods.filter(g=>g.category===cat&&g.stock>0).length).padStart(2,'0')}</small></button>`).join('');
  $('categories').querySelectorAll('button').forEach(b=>b.onclick=()=>{filter=b.dataset.category;renderMarket()});
  const query=$('search').value.trim();const list=sampleGoods.filter(g=>g.stock>0&&(filter==='全部'||g.category===filter)&&g.name.includes(query));
  if(!list.some(g=>g.id===selected)){selected=list[0]?.id??null;quantity=1;}
  $('result-count').textContent=`在售物品 · ${list.length} 种`;
  $('market-list').innerHTML=list.length?list.map(g=>`<button class="item ${g.id===selected?'selected':''}" data-id="${g.id}"><span class="item-visual"><img src="${icon(g.icon)}" alt=""></span><span class="item-copy"><h3>${g.name}</h3><p>${g.seller} · ${g.category}</p><p>可购 ${g.stock} 件</p></span><span class="item-price">${g.price} <small>灵石</small></span></button>`).join(''):'<div class="empty">暂无符合条件的物品</div>';
  $('market-list').querySelectorAll('.item').forEach(b=>b.onclick=()=>{selected=b.dataset.id;quantity=1;renderMarket()});
  const g=list.find(x=>x.id===selected);
  if(!g||g.stock<=0){$('market-detail').innerHTML='<div class="my-empty">选择在售物品<br>查看详情</div>';return;}
  quantity=Math.max(1,Math.min(quantity,g.stock));
  $('market-detail').innerHTML=`<div class="detail-title"><div><span class="eyebrow">摊货详情 · ${g.category}</span><h2>${g.name}</h2></div><span class="badge">${g.category}</span></div><div class="hero-item"><img src="${icon(g.icon)}" alt="${g.name}"></div><div class="facts"><div><span>摊主</span><strong>${g.seller}</strong></div><div><span>所余数量</span><strong>${g.stock} 件</strong></div><div><span>单价</span><strong>${g.price} 灵石</strong></div><div><span>${g.desc}</span></div></div><div class="quantity"><span>购买数量</span><div class="stepper"><button id="minus" aria-label="减少购买数量">−</button><output>${quantity}</output><button id="plus" aria-label="增加购买数量">+</button></div></div><div class="total"><span>共需</span><strong>${g.price*quantity} 灵石</strong></div><button id="buy" class="primary wide" ${balance<g.price*quantity?'disabled':''}>${balance<g.price*quantity?'灵石不足':'购买'}</button>`;
  $('minus').onclick=()=>{quantity=Math.max(1,quantity-1);renderMarket()};$('plus').onclick=()=>{quantity=Math.min(g.stock,quantity+1);renderMarket()};
  $('buy').onclick=()=>{const total=g.price*quantity;modal('确认购买',`<p>${g.name}　× ${quantity}<br>摊主：${g.seller}<br>共需：<strong>${total} 灵石</strong></p><p class="warn">样板操作，仅改变本页演示数据。</p>`,()=>{if(g.stock<quantity||balance<total)return;g.stock-=quantity;balance-=total;let own=sampleInventory.find(x=>x.id===g.id);if(own)own.count+=quantity;else sampleInventory.push({id:g.id,name:g.name,category:g.category,icon:g.icon,count:quantity});message(`已购买 ${g.name} × ${quantity}（样例）`);quantity=1;render();},'确认购买')};
}
function renderStall(){
  $('stall-status').textContent=rented?'已租用':'尚未租用';$('stall-status').parentElement.classList.toggle('active',rented);$('term').textContent=rented?'一游戏月（样例）':'一游戏月';$('rent').textContent=rented?'已租用':'租用摊位';$('rent').disabled=rented;
  $('inventory-list').innerHTML=sampleInventory.filter(x=>x.count>0).map(x=>`<button class="inventory-row ${x.id===selectedInventory?'selected':''}" data-id="${x.id}"><img src="${icon(x.icon)}" alt=""><span>${x.name}</span><small>${x.category}</small><span class="count">${x.count} 件</span></button>`).join('')||'<div class="empty">行囊中暂无可上架物品</div>';
  $('inventory-list').querySelectorAll('.inventory-row').forEach(b=>b.onclick=()=>{selectedInventory=b.dataset.id;renderStall()});
  const item=sampleInventory.find(x=>x.id===selectedInventory&&x.count>0);
  $('listing-editor').innerHTML=item?`<div class="editor-title">上架 · ${item.name}</div><div class="form-row"><label>上架数量<input id="sell-qty" type="number" min="1" max="${item.count}" value="1"></label><label>单价 / 灵石<input id="sell-price" type="number" min="1" max="999" value="2"></label></div><div class="editor-action"><span>${rented?'可上架 '+item.count+' 件':'请先租用摊位'}</span><button id="list-item" class="primary" ${rented?'':'disabled'}>上架</button></div>`:'<div class="editor-title">选择一件行囊物品</div>';
  if(item&&rented)$('list-item').onclick=()=>{const n=Number($('sell-qty').value),price=Number($('sell-price').value);if(!Number.isInteger(n)||n<1||n>item.count||!Number.isInteger(price)||price<1||price>999){message('请填写有效数量与售价');return;}modal('确认上架',`<p>${item.name}　× ${n}<br>单价：<strong>${price} 灵石</strong><br>上架后物品由摊位保管。</p><p class="warn">样板操作，不移动实际物品。</p>`,()=>{item.count-=n;listings.push({id:item.id,name:item.name,icon:item.icon,count:n,price});message(`${item.name} 已上架（样例）`);render();},'确认上架')};
  $('listing-count').textContent=listings.reduce((sum,x)=>sum+x.count,0)+' 件';
  $('my-listings').innerHTML=listings.length?listings.map((x,i)=>`<div class="listing-row"><img src="${icon(x.icon)}" alt=""><div class="listing-copy"><h3>${x.name}</h3><p>余 ${x.count} 件 · ${x.price} 灵石 / 件</p></div><button data-remove="${i}">撤下</button></div>`).join(''):'<div class="my-empty">摊上还没有物品<br>从行囊选择一件上架</div>';
  $('my-listings').querySelectorAll('[data-remove]').forEach(b=>b.onclick=()=>{const i=Number(b.dataset.remove),x=listings[i];modal('撤下物品',`<p>将 ${x.name} × ${x.count} 收回行囊？</p><p class="warn">样板不模拟正式行囊空间。</p>`,()=>{let own=sampleInventory.find(g=>g.id===x.id);if(own)own.count+=x.count;listings.splice(i,1);message(`${x.name} 已撤下（样例）`);render();},'确认撤下')});
  $('pending').textContent=pending;$('claim').disabled=pending<=0;
}
$('rent').onclick=()=>modal('租用摊位','<p>租金：<strong>1 灵石 / 游戏月</strong><br>到期停止售卖，不自动续租。<br>剩余物品与所得可取回。</p><p class="warn">点击仅演示界面，不支付实际灵石。</p>',()=>{if(balance<1){message('灵石不足，不能租用摊位');return;}balance--;rented=true;message('已租用摊位（样例）');render();},'确认租用');
$('simulate').onclick=()=>{if(!listings.length){message('先上架一件物品，再模拟成交');return;}const x=listings[0];x.count--;pending+=x.price;if(x.count===0)listings.shift();message(`样例同门购得 ${x.name} × 1`);render();};
$('claim').onclick=()=>modal('领取所得',`<p>待领取：<strong>${pending} 灵石</strong></p><p class="warn">样板不模拟正式行囊空间。</p>`,()=>{balance+=pending;pending=0;message('已领取摊位所得（样例）');render();},'确认领取');
render();
