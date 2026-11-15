// 隔离设计夹具：不读取或写入人物、正式存档、浏览器持久化存储。
(() => {
  const rules = window.marketOwnerSpaceRules;
  const catalog = new Map(source.stalls.flatMap(stall => stall.rows).map(row => [row.key, row]));
  const DAY = 24, YEAR = 360 * DAY, CELL = rules.cellDesignUnits * rules.previewCanvasWidth / rules.referenceDesignWidth;
  const sourceNames = { body: '随身行囊', 'ring-1': '储物戒指①' };
  function item(key, id, sourceId, extra = {}) {
    const value = { ...structuredClone(catalog.get(key)), id, sourceId, rotated: false, ...extra };
    delete value.quantity;
    return value;
  }
  const owner = {
    hasLease: true, now: 10 * DAY, leaseEnd: rules.leaseGameDays * DAY,
    reclaimEnd: rules.leaseGameDays * DAY + rules.reclaimGameYears * YEAR,
    wallet: 18, pending: 6, claimed: 0,
    inventory: [
      item('mist', 'held_mist_body_1', 'body'), item('mist', 'held_mist_body_2', 'body'), item('mist', 'held_mist_ring_1', 'ring-1'),
      item('healing', 'held_healing_body_1', 'body'), item('healing', 'held_healing_ring_1', 'ring-1'),
      item('copper', 'held_copper_body_1', 'body'), item('copper', 'held_copper_ring_1', 'ring-1'),
      item('bitter', 'held_bitter_ring_1', 'ring-1'), item('scale', 'held_scale_ring_1', 'ring-1'), item('bone', 'held_bone_body_1', 'body')
    ],
    listings: [item('mist', 'stall_mist_1', 'body', { x: 0, y: 0 }), item('mist', 'stall_mist_2', 'ring-1', { x: 2, y: 0 }),
      item('healing', 'stall_healing_1', 'body', { x: 4, y: 0 }), item('copper', 'stall_copper_1', 'ring-1', { x: 5, y: 1 })],
    receipts: [{ name: catalog.get('healing').name, quantity: 1, price: 6 }],
    forfeited: [], selection: { kind: 'inventory', id: 'held_mist_body_1' }, price: 2, rotated: false
  };
  const scenario = new URLSearchParams(location.search).get('state');
  if (scenario === 'before-expiry') owner.now = owner.leaseEnd - 1;
  if (scenario === 'expired') owner.now = owner.leaseEnd;
  if (scenario === 'near-deadline') owner.now = owner.reclaimEnd - 1;
  if (scenario === 'forfeited') owner.now = owner.reclaimEnd;
  if (scenario === 'empty') { owner.hasLease = false; owner.listings = []; owner.pending = 0; owner.receipts = []; }
  if (owner.hasLease && owner.now >= owner.leaseEnd && owner.now < owner.reclaimEnd) owner.selection = { kind: 'listing', id: owner.listings[0].id };
  let view = 'browse', scope = 'all', ownDrag = null, suppressOwnClick = 0;
  let message = !owner.hasLease ? '先租用摊位，再从左侧移入物品。' : owner.now >= owner.leaseEnd ? '租期已满，请将摊货拖回左侧收回。' : '';
  const queries = { browse: '', mine: '' }, oldBrowse = $('browse').onclick, oldSearch = $('search').oninput;
  const grid = $('ownerGrid');
  stage.style.setProperty('--cell', `${CELL}px`);
  let listingDraft = null;
  const current = () => (owner.selection?.kind === 'listing' ? owner.listings : owner.inventory).find(row => row.id === owner.selection?.id);
  const status = () => !owner.hasLease ? 'unleased' : owner.now >= owner.reclaimEnd ? 'forfeited' : owner.now >= owner.leaseEnd ? 'expired' : 'active';
  const active = () => status() === 'active';
  const occupied = () => owner.listings.reduce((sum, row) => sum + row.width * row.height, 0);
  const validPrice = value => Number.isSafeInteger(value) && value > 0;
  const shape = (row, rotated = row.rotated) => ({ w: rotated ? row.height : row.width, h: rotated ? row.width : row.height });
  function calendar(hours) {
    const days = Math.floor(hours / DAY);
    return `${Math.floor(days / 360) + 1}年${Math.floor(days % 360 / 30) + 1}月${days % 30 + 1}日${hours % DAY}时`;
  }
  function duration(hours) {
    const years = Math.floor(hours / YEAR), days = Math.floor(hours % YEAR / DAY), rest = hours % DAY;
    return [years && `${years} 年`, days && `${days} 日`, rest && `${rest} 时`].filter(Boolean).join(' ') || '0 时';
  }
  function fits(row, x, y, rotated, exclude = null, positions = owner.listings) {
    const { w, h } = shape(row, rotated);
    return Number.isInteger(x) && Number.isInteger(y) && x >= 0 && y >= 0 && x + w <= rules.columns && y + h <= rules.rows &&
      positions.every(other => {
        if (other.id === exclude) return true;
        const size = shape(other);
        return x + w <= other.x || x >= other.x + size.w || y + h <= other.y || y >= other.y + size.h;
      });
  }
  function firstFit(row, rotated, positions = owner.listings) {
    for (let y = 0; y < rules.rows; y++) for (let x = 0; x < rules.columns; x++) if (fits(row, x, y, rotated, null, positions)) return { x, y };
    return null;
  }
  function reconcile() {
    if (owner.hasLease && owner.now >= owner.reclaimEnd && owner.listings.length) {
      owner.forfeited.push(...owner.listings.map(row => ({ ...row, disposition: '归宗门' })));
      owner.listings = [];
      message = '取回期限已过，未收回物品已归宗门。';
    }
    if (!current()) {
      const row = owner.inventory[0];
      owner.selection = row ? { kind: 'inventory', id: row.id } : null;
      if (row) { owner.price = row.price; owner.rotated = false; }
    }
  }
  function notifyOwner(text, warning = false) {
    message = text; $('ownerMessage').classList.toggle('warning', warning); renderOwner();
  }
  function select(kind, id, keep = false) {
    const same = owner.selection?.kind === kind && owner.selection.id === id;
    owner.selection = { kind, id };
    const row = current();
    if (row && !(keep && same)) { owner.price = row.price; owner.rotated = row.rotated; }
    renderOwner();
  }
  function availableRows() {
    const term = $('search').value.trim();
    return owner.inventory.filter(row => (scope === 'all' || (scope === 'body' ? row.sourceId === 'body' : row.sourceId !== 'body')) && (!term || row.name.includes(term)));
  }
  function renderInventory() {
    const groups = new Map();
    for (const row of availableRows()) {
      // 夹具中的正式定义已区分品质／药效；仅同一资源路径合并，不按名称或图标合并。
      if (!groups.has(row.path)) groups.set(row.path, []);
      groups.get(row.path).push(row);
    }
    $('ownerInventory').replaceChildren();
    for (const members of groups.values()) {
      const chosen = owner.selection?.kind === 'inventory' && members.some(row => row.id === owner.selection.id);
      const row = members.find(member => member.id === owner.selection?.id) || members[0];
      const button = document.createElement('button');
      button.className = 'item-row' + (chosen ? ' selected' : ''); button.draggable = false; button.dataset.path = row.path;
      const name = document.createElement('span'); name.className = 'name'; name.textContent = row.name;
      const count = document.createElement('span'); count.className = 'count'; count.textContent = members.length;
      button.append(name, count); button.setAttribute('aria-label', `行囊 ${row.name} ${members.length}件`);
      button.onclick = () => select('inventory', row.id, true);
      button.onpointerdown = event => beginDrag(event, 'inventory', row.id);
      button.onmouseenter = event => { if (!ownDrag?.active) showHover({ ...row, price: owner.selection?.id === row.id ? owner.price : row.price }, event); };
      button.onmousemove = event => { if (!ownDrag?.active) showHover(row, event); };
      button.onmouseleave = hideHover; button.onblur = hideHover;
      $('ownerInventory').append(button);
    }
    if (!groups.size) { const empty = document.createElement('div'); empty.className = 'empty'; empty.textContent = '暂无符合条件的物品。'; $('ownerInventory').append(empty); }
    $('scopeAll').classList.toggle('active', scope === 'all'); $('scopeBody').classList.toggle('active', scope === 'body'); $('scopeRing').classList.toggle('active', scope === 'ring');
  }
  function sizeImage(image, row, rotated) {
    image.src = imagePath(row); image.style.width = `${row.width * CELL}px`; image.style.height = `${row.height * CELL}px`;
    image.style.transform = `translate(-50%,-50%) rotate(${rotated ? 90 : 0}deg)`;
  }
  function renderGrid() {
    for (const element of grid.querySelectorAll('.grid-item')) element.remove();
    const term = $('search').value.trim();
    for (const row of owner.listings) {
      const button = document.createElement('button'), size = shape(row);
      button.className = 'grid-item'; button.dataset.id = row.id; button.draggable = false;
      button.style.cssText = `left:${row.x * CELL}px;top:${row.y * CELL}px;width:${size.w * CELL}px;height:${size.h * CELL}px`;
      button.classList.toggle('selected', owner.selection?.kind === 'listing' && owner.selection.id === row.id);
      button.classList.toggle('dim', !!term && !row.name.includes(term));
      button.setAttribute('aria-label', `摊位 ${row.name} 单价${row.price}灵石 第${row.x + 1}列第${row.y + 1}行 占${size.w}乘${size.h}格`);
      const image = document.createElement('img'); image.alt = ''; sizeImage(image, row, row.rotated);
      const tag = document.createElement('span'); tag.className = 'grid-price'; tag.textContent = `${row.price}灵`;
      button.append(image, tag);
      button.onclick = event => { event.stopPropagation(); select('listing', row.id, true); };
      button.onpointerdown = event => beginDrag(event, 'listing', row.id);
      button.ondblclick = () => openPriceDialog(row.id);
      button.onmouseenter = event => { if (!ownDrag?.active) showHover(row, event); }; button.onmousemove = event => { if (!ownDrag?.active) showHover(row, event); };
      button.onmouseleave = hideHover; button.onblur = hideHover;
      grid.insertBefore(button, $('ownerPlacement'));
    }
    $('ownerGridEmpty').hidden = owner.listings.length > 0;
    $('ownerGridEmpty').replaceChildren();
    if (!owner.listings.length) {
      const label = document.createElement('div'); label.textContent = status() === 'forfeited' ? '逾期物品已归宗门' : '摊位空置';
      const note = document.createElement('small'); note.textContent = active() ? '从左侧拖入物品' : `${rules.columns} × ${rules.rows} 格`;
      $('ownerGridEmpty').append(label, note);
    }
  }
  function renderOwner() {
    reconcile(); $('wallet').textContent = owner.wallet; $('ownerUsed').textContent = occupied();
    const state = status();
    $('ownerLeaseState').textContent = { active: '租用中', expired: '到期待领', forfeited: '逾期已处理', unleased: '未租用' }[state];
    $('ownerLeaseState').className = 'space-badge' + (state === 'expired' ? ' expired' : state === 'forfeited' ? ' forfeited' : '');
    $('ownerTimeCaption').textContent = state === 'active' ? '租期剩余' : state === 'expired' ? '取回期限剩余' : state === 'forfeited' ? '取回期限已过' : '每游戏月摊租';
    $('ownerTimeLeft').textContent = state === 'active' ? duration(owner.leaseEnd - owner.now) : state === 'expired' ? duration(owner.reclaimEnd - owner.now) : state === 'forfeited' ? '物品归宗门' : '1 灵石';
    $('ownerExpiry').textContent = state === 'active' ? `到期 · ${calendar(owner.leaseEnd)}` : state === 'expired' || state === 'forfeited' ? `取回截止 · ${calendar(owner.reclaimEnd)}` : '租期三十日 · 含坊市代管';
    $('ownerReclaimNote').textContent = state === 'active' ? '到期停止售卖，五年内收回物品。\n逾期未取回物品归宗门。' : state === 'expired' ? '已停止售卖，请在期限内收回。\n逾期未取回物品归宗门。' : state === 'forfeited' ? '未取回物品不再返还。\n售得灵石仍可领取。' : '到期不自动续租。\n租满后五年内收回剩余物品。';
    $('ownerReclaimNote').classList.toggle('warning', state === 'expired' || state === 'forfeited');
    $('ownerLease').textContent = state === 'unleased' || state === 'forfeited' ? '租用摊位 · 1 灵石' : '查看租约';
    $('ownerLease').disabled = (state === 'unleased' || state === 'forfeited') && owner.wallet < rules.rentSpiritStones;
    $('ownerPending').textContent = owner.pending; $('ownerClaim').disabled = owner.pending <= 0;
    $('ownerIncomeNote').textContent = owner.claimed ? `已领取 ${owner.claimed} 灵石。` : '租期结束或物品逾期，所得仍可领取。';
    $('ownerMessage').textContent = message;
    renderInventory(); renderGrid();
    if (view === 'mine') $('bottomNote').textContent = '坊市代管 · 灵石交易 · 到期后五个游戏年内收回物品';
  }
  function setListingBackgroundLocked(locked) {
    for (const element of [document.querySelector('.toolbar'), $('ownerPane')]) element.inert = locked;
  }
  function closeListingDialog() {
    listingDraft = null; $('listingShade').hidden = true; setListingBackgroundLocked(false);
  }
  function planBatch(draft, count) {
    if (!Number.isSafeInteger(count) || count < 1 || count > draft.ids.length) return null;
    const plan = [], positions = [...owner.listings];
    for (const [index,id] of draft.ids.slice(0,count).entries()) {
      const row = owner.inventory.find(item => item.id === id); if (!row) return null;
      const place = index === 0 ? {x:draft.x,y:draft.y} : firstFit(row,draft.rotated,positions);
      if (!place || !fits(row,place.x,place.y,draft.rotated,null,positions)) return null;
      const placed = {...row,...place,rotated:draft.rotated}; plan.push(placed); positions.push(placed);
    }
    return plan;
  }
  function refreshListingDialog() {
    if (!listingDraft) return;
    const draft=listingDraft, count=Number($('listingQuantity').value), price=Number($('listingPrice').value);
    const row=draft.mode==='price' ? owner.listings.find(item=>item.id===draft.id) : owner.inventory.find(item=>item.id===draft.ids[0]);
    const valid=!!row && active() && validPrice(price) && Number.isSafeInteger(count) && count>0 && Number.isSafeInteger(price*count) && (draft.mode==='price' ? count===1 : !!planBatch(draft,count));
    $('listingConfirm').disabled=!valid;
    $('listingLess').disabled=draft.mode==='price'||count<=1; $('listingMore').disabled=draft.mode==='price'||count>=draft.ids.length;
    $('listingFit').classList.toggle('invalid',!valid);
    $('listingFit').textContent=valid ? (draft.mode==='price' ? '只修改这件尚未售出物品的单价。' : `共占 ${row.width*row.height*count} 格，每件独立摆放。\n首件留在落点，其余放入空位。`) : '数量、单价或摊位空间不足，请调整后确认。';
    const counts=new Map();
    const chosen=draft.mode==='price' ? [row] : draft.ids.slice(0,Number.isSafeInteger(count)&&count>0 ? Math.min(count,draft.ids.length) : 0).map(id=>owner.inventory.find(item=>item.id===id));
    for (const item of chosen.filter(Boolean)) counts.set(item.sourceId,(counts.get(item.sourceId)||0)+1);
    $('listingSource').textContent=draft.mode==='price' ? '已售部分不受此次调价影响。' : '来源 · '+[...counts].map(([id,n])=>`${sourceNames[id]} ${n}件`).join('、');
  }
  function showListingDialog(draft,row) {
    listingDraft=draft; hideHover();
    $('listingTitle').textContent=draft.mode==='price' ? '修改售价' : '上架物品'; $('listingName').textContent=row.name;
    $('listingIcon').src=imagePath(row); $('listingQuantity').value=1; $('listingQuantity').max=draft.ids.length;
    $('listingQuantity').disabled=draft.mode==='price'; $('listingPrice').value=row.price;
    $('listingConfirm').textContent=draft.mode==='price' ? '确认改价' : '确认上架';
    $('listingShade').hidden=false; setListingBackgroundLocked(true); refreshListingDialog();
    (draft.mode==='price' ? $('listingPrice') : $('listingQuantity')).focus();
  }
  function requestPlace(id,x,y,rotated) {
    const row=owner.inventory.find(item=>item.id===id); if (!row||!active()) return;
    if (!fits(row,x,y,rotated)) { notifyOwner('此处放不下，物品仍留在原空间。',true); return; }
    const members=[row,...availableRows().filter(item=>item.path===row.path&&item.id!==id)];
    showListingDialog({mode:'create',ids:members.map(item=>item.id),x,y,rotated},row);
  }
  function openPriceDialog(id) {
    const row=owner.listings.find(item=>item.id===id); if (!row||!active()) return;
    showListingDialog({mode:'price',id,ids:[id]},row);
  }
  function confirmListing() {
    if (!listingDraft || $('listingConfirm').disabled || !active()) return;
    const draft=listingDraft, count=Number($('listingQuantity').value), price=Number($('listingPrice').value);
    if (!validPrice(price)||!Number.isSafeInteger(price*count)) return;
    if (draft.mode==='price') {
      const row=owner.listings.find(item=>item.id===draft.id); if (!row||count!==1) return;
      row.price=price; closeListingDialog(); notifyOwner('已修改这件摊货的售价。');
    } else {
      const plan=planBatch(draft,count); if (!plan) { refreshListingDialog(); return; }
      const ids=new Set(plan.map(item=>item.id));
      owner.inventory=owner.inventory.filter(item=>!ids.has(item.id)); owner.listings.push(...plan.map(item=>({...item,price})));
      owner.selection={kind:'listing',id:plan[0].id}; owner.price=price; owner.rotated=draft.rotated;
      closeListingDialog(); notifyOwner(`已上架 ${plan[0].name} ×${count}，单价 ${price} 灵石。`);
    }
  }
  function withdrawListing(id) {
    reconcile(); const row=owner.listings.find(item=>item.id===id); if (!row) return;
    const held={...row}; delete held.x; delete held.y;
    owner.listings=owner.listings.filter(item=>item.id!==id); owner.inventory.push(held);
    owner.selection={kind:'inventory',id}; owner.price=held.price; owner.rotated=false; scope='all';
    notifyOwner(`已收回 ${held.name} 至${sourceNames[held.sourceId]}。`);
  }
  function moveListing(id, x, y, rotated) {
    const row = owner.listings.find(item => item.id === id);
    if (!row || status() === 'forfeited' || !fits(row, x, y, rotated, id)) { notifyOwner('此处放不下，已恢复原位。', true); return; }
    Object.assign(row, { x, y, rotated }); owner.rotated = rotated;
    notifyOwner('已调整摊位内摆放。');
  }
  function rotateSelection() {
    const row = current(); if (!row) return;
    if (owner.selection.kind === 'listing') moveListing(row.id, row.x, row.y, !row.rotated);
    else if (active()) { owner.rotated = !owner.rotated; renderOwner(); }
  }
  function point(event) {
    const rect = stage.getBoundingClientRect(); return { x: (event.clientX - rect.left) / scale, y: (event.clientY - rect.top) / scale };
  }
  function gridPoint() {
    const area = grid.getBoundingClientRect(), rect = stage.getBoundingClientRect(); return { x: (area.left - rect.left) / scale, y: (area.top - rect.top) / scale };
  }
  function beginDrag(event, kind, id) {
    if (event.button !== 0 || !event.isPrimary || (kind === 'inventory' && !active())) return;
    const row = (kind === 'listing' ? owner.listings : owner.inventory).find(item => item.id === id); if (!row) return;
    const p = point(event), g = gridPoint();
    ownDrag = { kind, id, row, pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, active: false,
      rotated: owner.selection?.id === id ? owner.rotated : row.rotated,
      offsetX: kind === 'listing' ? p.x - g.x - row.x * CELL : CELL / 2,
      offsetY: kind === 'listing' ? p.y - g.y - row.y * CELL : CELL / 2, last: event };
  }
  function drawDrag(event) {
    if (!ownDrag?.active) return;
    ownDrag.last = event; const p = point(event), g = gridPoint(), size = shape(ownDrag.row, ownDrag.rotated);
    const x = Math.round((p.x - g.x - ownDrag.offsetX) / CELL), y = Math.round((p.y - g.y - ownDrag.offsetY) / CELL);
    const inside = p.x >= g.x && p.y >= g.y && p.x < g.x + rules.columns * CELL && p.y < g.y + rules.rows * CELL;
    const left=document.querySelector('.inventory-sheet').getBoundingClientRect();
    const returning=ownDrag.kind==='listing' && event.clientX>=left.left && event.clientX<left.right && event.clientY>=left.top && event.clientY<left.bottom;
    document.querySelector('.inventory-sheet').classList.toggle('drop-target',returning);
    const valid = returning || inside && fits(ownDrag.row, x, y, ownDrag.rotated, ownDrag.kind === 'listing' ? ownDrag.id : null);
    ownDrag.target = { x, y, valid, kind:returning?'inventory':'grid' };
    const ghost = $('ownerDragPiece');
    ghost.style.cssText = `display:block;left:${p.x - ownDrag.offsetX}px;top:${p.y - ownDrag.offsetY}px;width:${size.w * CELL}px;height:${size.h * CELL}px`;
    sizeImage(ghost.querySelector('img'), ownDrag.row, ownDrag.rotated);
    const preview = $('ownerPlacement'); preview.classList.toggle('invalid', !valid);
    preview.style.cssText = `display:${inside&&!returning ? 'block' : 'none'};left:${x * CELL}px;top:${y * CELL}px;width:${size.w * CELL}px;height:${size.h * CELL}px`;
  }
  function cancelOwnDrag() {
    const previous = ownDrag;
    if (previous?.active) suppressOwnClick = performance.now() + 220;
    ownDrag = null;
    if (previous && stage.hasPointerCapture(previous.pointerId)) stage.releasePointerCapture(previous.pointerId);
    $('ownerDragPiece').style.display = $('ownerPlacement').style.display = 'none';
    stage.style.userSelect = ''; document.querySelector('.inventory-sheet').classList.remove('drop-target');
    for (const item of grid.querySelectorAll('.moving')) item.classList.remove('moving');
    return previous;
  }
  addEventListener('pointermove', event => {
    if (!ownDrag || ownDrag.pointerId !== event.pointerId) return;
    if (!ownDrag.active && Math.hypot(event.clientX - ownDrag.startX, event.clientY - ownDrag.startY) > 4) {
      ownDrag.active = true; select(ownDrag.kind, ownDrag.id, true); hideHover(); stage.style.userSelect = 'none'; stage.setPointerCapture(event.pointerId);
      grid.querySelector(`[data-id="${ownDrag.id}"]`)?.classList.add('moving');
    }
    if (ownDrag.active) { event.preventDefault(); drawDrag(event); }
  }, { passive: false });
  addEventListener('pointerup', event => {
    if (!ownDrag || ownDrag.pointerId !== event.pointerId) return;
    const drag = cancelOwnDrag(); if (!drag?.active) return;
    if (!drag.target?.valid) { notifyOwner('落点无效，物品保持原状。', true); return; }
    if (drag.target.kind==='inventory') { withdrawListing(drag.id); return; }
    if (drag.kind === 'inventory') requestPlace(drag.id, drag.target.x, drag.target.y, drag.rotated, owner.price);
    else moveListing(drag.id, drag.target.x, drag.target.y, drag.rotated);
  });
  addEventListener('pointercancel', cancelOwnDrag); addEventListener('blur', cancelOwnDrag); addEventListener('resize', cancelOwnDrag);
  addEventListener('keydown', event => {
    if (view !== 'mine' || event.target instanceof HTMLInputElement) return;
    if (event.key === 'Escape') { cancelOwnDrag(); return; }
    if (event.key.toLowerCase() !== 'r' || event.repeat || $('shade').style.display === 'flex') return;
    event.preventDefault();
    if (ownDrag?.active) {
      const oldX = ownDrag.offsetX, oldY = ownDrag.offsetY, oldSize = shape(ownDrag.row, ownDrag.rotated);
      ownDrag.rotated = !ownDrag.rotated; ownDrag.offsetX = oldSize.h * CELL - oldY; ownDrag.offsetY = oldX;
      drawDrag(ownDrag.last);
    } else rotateSelection();
  });
  function showView(next) {
    queries[view] = $('search').value; view = next; cancelDrag(); cancelOwnDrag(); hideHover(); closeModal(); closeListingDialog();
    $('search').value = queries[view]; $('search').placeholder = view === 'mine' ? '查找行囊或摊货' : '查找摊货';
    $('viewport').hidden = $('browseSelection').hidden = view === 'mine'; $('ownerPane').hidden = view !== 'mine'; $('dragHint').hidden = view === 'mine';
    stage.classList.toggle('owner-space-mode', view === 'mine'); document.querySelector('.title').textContent = view === 'mine' ? '我的摊位' : '宗内坊市';
    $('mine').classList.toggle('active', view === 'mine'); $('browse').classList.toggle('active', view === 'browse');
    if (view === 'mine') renderOwner(); else { render(); renderSelection(); $('bottomNote').textContent = '成员寄存货物，由坊市照看 · 以灵石交易'; }
  }
  $('mine').onclick = () => showView('mine');
  $('browse').onclick = () => { const wasBrowse = view === 'browse'; showView('browse'); if (wasBrowse) oldBrowse(); };
  $('search').oninput = () => { queries[view] = $('search').value; if (view === 'mine') renderOwner(); else oldSearch(); };
  for (const [id, next] of [['scopeAll', 'all'], ['scopeBody', 'body'], ['scopeRing', 'ring']]) $(id).onclick = () => {
    scope = next; const rows = availableRows();
    if (owner.selection?.kind === 'inventory' && !rows.some(row => row.id === owner.selection.id) && rows.length) {
      owner.selection.id = rows[0].id; owner.price = rows[0].price; owner.rotated = false;
    }
    renderOwner();
  };
  $('listingQuantity').oninput=refreshListingDialog; $('listingPrice').oninput=refreshListingDialog;
  $('listingLess').onclick=()=>{ $('listingQuantity').value=Math.max(1,Number($('listingQuantity').value)-1); refreshListingDialog(); };
  $('listingMore').onclick=()=>{ if(listingDraft) $('listingQuantity').value=Math.min(listingDraft.ids.length,Number($('listingQuantity').value)+1); refreshListingDialog(); };
  $('listingCancel').onclick=closeListingDialog; $('listingConfirm').onclick=confirmListing;
  document.addEventListener('keydown',event=>{
    if (!listingDraft) return;
    if(event.key==='Escape') { event.preventDefault(); event.stopImmediatePropagation(); closeListingDialog(); }
    else if(event.key==='Enter') { event.preventDefault(); event.stopImmediatePropagation(); confirmListing(); }
    else if(event.key==='Tab') {
      const controls=[...$('listingShade').querySelectorAll('input,button')].filter(control=>!control.disabled);
      const current=controls.indexOf(document.activeElement), next=(current+(event.shiftKey?-1:1)+controls.length)%controls.length;
      event.preventDefault(); controls[next].focus();
    }
  },true);
  $('ownerClaim').onclick = () => {
    const amount = owner.pending; if (amount <= 0) return;
    modal('领取售得灵石', `领取 ${amount} 枚灵石。`, () => {
      if (owner.pending < amount) return; owner.pending -= amount; owner.wallet += amount; owner.claimed += amount;
      notifyOwner(`已领取 ${amount} 枚灵石。`);
    });
  };
  $('ownerLease').onclick = () => {
    if (status() === 'unleased' || status() === 'forfeited') {
      modal('租用摊位', `${rules.columns} × ${rules.rows} 格，共 ${rules.columns*rules.rows} 格。\n租金 ${rules.rentSpiritStones} 灵石，含坊市代管；租期三十日。\n租满后五年内收回剩余物品，逾期归宗门。`, () => {
        if (!['unleased', 'forfeited'].includes(status()) || owner.wallet < rules.rentSpiritStones) return;
        owner.wallet -= rules.rentSpiritStones; owner.hasLease = true; owner.leaseEnd = owner.now + rules.leaseGameDays * DAY;
        owner.reclaimEnd = owner.leaseEnd + rules.reclaimGameYears * YEAR;
        notifyOwner('已支付 1 灵石，摊位租期三十日。');
      });
    } else modal('摊位租约', `${rules.columns} × ${rules.rows} 格，共 ${rules.columns*rules.rows} 格；摊租每游戏月 1 灵石，含代管。\n租期至 ${calendar(owner.leaseEnd)}，到期不自动续租。\n剩余物品须于 ${calendar(owner.reclaimEnd)} 前取回，逾期归宗门。\n售得灵石在此领取，逾期仍保留。`);
  };
  window.ownerPreview = {
    getState: () => structuredClone({ ...owner, status: status(), usedCells: occupied(), scope, view, rules,
      dialogOpen: !!listingDraft, placementValid: owner.listings.every(row => fits(row, row.x, row.y, row.rotated, row.id)) }),
    showView
  };
  renderOwner(); showView(location.hash === '#browse' ? 'browse' : 'mine'); window.ownerPreviewReady = true;
})();
