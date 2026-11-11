// 隔离设计夹具：不读取或写入人物、正式存档、浏览器持久化存储。
(() => {
  const rules = window.marketOwnerSpaceRules;
  const catalog = new Map(source.stalls.flatMap(stall => stall.rows).map(row => [row.key, row]));
  const DAY = 24, YEAR = 360 * DAY, CELL = rules.cellDesignUnits;
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
  let message = !owner.hasLease ? '先租用摊位，再从左侧移入物品。' : owner.now >= owner.leaseEnd ? '租期已满，请选中摊货收回。' : '从左侧选货，定价后拖入摊位。';
  const queries = { browse: '', mine: '' }, oldBrowse = $('browse').onclick, oldSearch = $('search').oninput;
  const grid = $('ownerGrid');
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
  function fits(row, x, y, rotated, exclude = null) {
    const { w, h } = shape(row, rotated);
    return Number.isInteger(x) && Number.isInteger(y) && x >= 0 && y >= 0 && x + w <= rules.columns && y + h <= rules.rows &&
      owner.listings.every(other => {
        if (other.id === exclude) return true;
        const size = shape(other);
        return x + w <= other.x || x >= other.x + size.w || y + h <= other.y || y >= other.y + size.h;
      });
  }
  function firstFit(row, rotated) {
    for (let y = 0; y < rules.rows; y++) for (let x = 0; x < rules.columns; x++) if (fits(row, x, y, rotated)) return { x, y };
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
      button.onmouseenter = event => { if (!ownDrag?.active) showHover(row, event); }; button.onmousemove = event => { if (!ownDrag?.active) showHover(row, event); };
      button.onmouseleave = hideHover; button.onblur = hideHover;
      grid.insertBefore(button, $('ownerPlacement'));
    }
    $('ownerGridEmpty').hidden = owner.listings.length > 0;
    $('ownerGridEmpty').replaceChildren();
    if (!owner.listings.length) {
      const label = document.createElement('div'); label.textContent = status() === 'forfeited' ? '逾期物品已归宗门' : '摊位空置';
      const note = document.createElement('small'); note.textContent = active() ? '从左侧选择物品上架' : '10 × 6 格';
      $('ownerGridEmpty').append(label, note);
    }
  }
  function renderEditor(sync = true) {
    const row = current(), listing = owner.selection?.kind === 'listing';
    $('ownerWithdraw').hidden = !listing || !row; $('ownerPrimary').textContent = listing ? '保存单价' : '上架一件';
    $('ownerPrimary').classList.toggle('with-withdraw', false);
    $('ownerRotate').disabled = !row || (!active() && !listing);
    $('ownerPrice').disabled = !row || !active();
    $('ownerPrimary').disabled = !row || !active() || !validPrice(owner.price);
    $('ownerPlannedCells').hidden = listing;
    if (row) {
      $('ownerChosenIcon').hidden = false; $('ownerChosenIcon').src = imagePath(row);
      $('ownerChosen').replaceChildren(document.createTextNode(`${listing ? '摊上' : '行囊'} · ${row.name}`));
      const detail = document.createElement('small'), size = shape(row, listing ? row.rotated : owner.rotated);
      detail.textContent = `${listing ? '取回至' : '来源'} · ${sourceNames[row.sourceId]} · 占 ${size.w} × ${size.h} 格`;
      $('ownerChosen').append(detail);
      $('ownerPlannedCells').textContent = `每次 1 件 · ${row.width * row.height} 格`;
      if (sync) $('ownerPrice').value = Number.isFinite(owner.price) ? owner.price : '';
    } else {
      $('ownerChosenIcon').hidden = true; $('ownerChosen').textContent = '选择行囊物品或摊上货物';
      $('ownerPlannedCells').textContent = ''; if (sync) $('ownerPrice').value = '';
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
    const receipt = owner.receipts.at(-1);
    $('ownerReceipt').textContent = receipt ? `最近售出：${receipt.name} ×${receipt.quantity}，所得 ${receipt.price * receipt.quantity} 灵石。` : '暂无成交。';
    $('ownerMessage').textContent = message;
    renderInventory(); renderGrid(); renderEditor();
    if (view === 'mine') $('bottomNote').textContent = '坊市代管 · 灵石交易 · 到期后五个游戏年内收回物品';
  }
  function requestPlace(id, x, y, rotated, price) {
    const row = owner.inventory.find(item => item.id === id);
    if (!row || !active() || !validPrice(price)) return;
    if (!fits(row, x, y, rotated)) { notifyOwner('此处放不下，物品仍留在原空间。', true); return; }
    const size = shape(row, rotated);
    modal('确认上架', `${row.name} ×1 · 单价 ${price} 灵石\n占 ${size.w} × ${size.h} 格，从${sourceNames[row.sourceId]}移入摊位。`, () => {
      const held = owner.inventory.find(item => item.id === id);
      if (!held || !active() || !validPrice(price) || !fits(held, x, y, rotated)) return;
      owner.inventory = owner.inventory.filter(item => item.id !== id);
      owner.listings.push({ ...held, x, y, rotated, price });
      owner.selection = { kind: 'listing', id }; owner.price = price; owner.rotated = rotated;
      notifyOwner(`已从${sourceNames[held.sourceId]}上架 ${held.name}，单价 ${price} 灵石。`);
    });
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
    else if (active()) { owner.rotated = !owner.rotated; renderEditor(); }
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
    const valid = inside && fits(ownDrag.row, x, y, ownDrag.rotated, ownDrag.kind === 'listing' ? ownDrag.id : null);
    ownDrag.target = { x, y, valid };
    const ghost = $('ownerDragPiece');
    ghost.style.cssText = `display:block;left:${p.x - ownDrag.offsetX}px;top:${p.y - ownDrag.offsetY}px;width:${size.w * CELL}px;height:${size.h * CELL}px`;
    sizeImage(ghost.querySelector('img'), ownDrag.row, ownDrag.rotated);
    const preview = $('ownerPlacement'); preview.classList.toggle('invalid', !valid);
    preview.style.cssText = `display:${inside ? 'block' : 'none'};left:${x * CELL}px;top:${y * CELL}px;width:${size.w * CELL}px;height:${size.h * CELL}px`;
  }
  function cancelOwnDrag() {
    const previous = ownDrag;
    if (previous?.active) suppressOwnClick = performance.now() + 220;
    ownDrag = null;
    if (previous && stage.hasPointerCapture(previous.pointerId)) stage.releasePointerCapture(previous.pointerId);
    $('ownerDragPiece').style.display = $('ownerPlacement').style.display = 'none';
    stage.style.userSelect = '';
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
  grid.onclick = event => {
    if (performance.now() < suppressOwnClick || owner.selection?.kind !== 'inventory' || !active() || event.target.closest('.grid-item')) return;
    const row = current(); if (!row) return; const p = point(event), g = gridPoint();
    requestPlace(row.id, Math.floor((p.x - g.x) / CELL), Math.floor((p.y - g.y) / CELL), owner.rotated, owner.price);
  };
  function showView(next) {
    queries[view] = $('search').value; view = next; cancelDrag(); cancelOwnDrag(); hideHover(); closeModal();
    $('search').value = queries[view]; $('search').placeholder = view === 'mine' ? '查找行囊或摊货' : '查找摊货';
    $('viewport').hidden = $('browseSelection').hidden = view === 'mine'; $('ownerPane').hidden = $('ownerSelection').hidden = view !== 'mine'; $('dragHint').hidden = view === 'mine';
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
  $('ownerPrice').oninput = () => { owner.price = Number($('ownerPrice').value); renderEditor(false); };
  $('ownerRotate').onclick = rotateSelection;
  $('ownerPrimary').onclick = () => {
    const row = current(); if (!row || !active() || !validPrice(owner.price)) return;
    if (owner.selection.kind === 'inventory') {
      const place = firstFit(row, owner.rotated);
      if (!place) notifyOwner('没有能放下此物的空位，请先整理摊位。', true);
      else requestPlace(row.id, place.x, place.y, owner.rotated, owner.price);
    } else {
      const id = row.id, price = owner.price;
      modal('修改单价', `${row.name} ×1\n单价改为 ${price} 灵石，只影响尚未成交的这件物品。`, () => {
        const listing = owner.listings.find(item => item.id === id); if (!listing || !active() || !validPrice(price)) return;
        listing.price = price; notifyOwner('已修改这件摊货的售价。');
      });
    }
  };
  $('ownerWithdraw').onclick = () => {
    const row = current(); if (!row || owner.selection.kind !== 'listing') return;
    const id = row.id;
    modal('取回物品', `取回 ${row.name} ×1 至${sourceNames[row.sourceId]}。`, () => {
      reconcile(); const listing = owner.listings.find(item => item.id === id); if (!listing) return;
      const held = { ...listing }; delete held.x; delete held.y;
      owner.listings = owner.listings.filter(item => item.id !== id); owner.inventory.push(held);
      owner.selection = { kind: 'inventory', id }; owner.price = held.price; owner.rotated = false;
      notifyOwner(`已收回至${sourceNames[held.sourceId]}，原占格已腾空。`);
    });
  };
  $('ownerClaim').onclick = () => {
    const amount = owner.pending; if (amount <= 0) return;
    modal('领取售得灵石', `领取 ${amount} 枚灵石。`, () => {
      if (owner.pending < amount) return; owner.pending -= amount; owner.wallet += amount; owner.claimed += amount;
      notifyOwner(`已领取 ${amount} 枚灵石。`);
    });
  };
  $('ownerLease').onclick = () => {
    if (status() === 'unleased' || status() === 'forfeited') {
      modal('租用摊位', `10 × 6 格，共 60 格。\n租金 ${rules.rentSpiritStones} 灵石，含坊市代管；租期三十日。\n租满后五年内收回剩余物品，逾期归宗门。`, () => {
        if (!['unleased', 'forfeited'].includes(status()) || owner.wallet < rules.rentSpiritStones) return;
        owner.wallet -= rules.rentSpiritStones; owner.hasLease = true; owner.leaseEnd = owner.now + rules.leaseGameDays * DAY;
        owner.reclaimEnd = owner.leaseEnd + rules.reclaimGameYears * YEAR;
        notifyOwner('已支付 1 灵石，摊位租期三十日。');
      });
    } else modal('摊位租约', `10 × 6 格，共 60 格；摊租每游戏月 1 灵石，含代管。\n租期至 ${calendar(owner.leaseEnd)}，到期不自动续租。\n剩余物品须于 ${calendar(owner.reclaimEnd)} 前取回，逾期归宗门。\n售得灵石在此领取，逾期仍保留。`);
  };
  window.ownerPreview = {
    getState: () => structuredClone({ ...owner, status: status(), usedCells: occupied(), scope, view, rules,
      placementValid: owner.listings.every(row => fits(row, row.x, row.y, row.rotated, row.id)) }),
    showView
  };
  renderOwner(); showView(location.hash === '#browse' ? 'browse' : 'mine'); window.ownerPreviewReady = true;
})();
