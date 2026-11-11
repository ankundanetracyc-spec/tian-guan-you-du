// 隔离的界面演示数据；不读取或写入人物、正式存档或浏览器持久化存储。
(() => {
  const catalog = new Map(source.stalls.flatMap(stall => stall.rows).map(row => [row.key, row]));
  const sample = (key, count, extra = {}) => ({ ...structuredClone(catalog.get(key)), quantity: count, ...extra });
  const owner = {
    leased: true, wallet: 18, pending: 6, claimed: 0,
    inventory: [sample('mist', 3), sample('healing', 2), sample('copper', 1), sample('bitter', 1)],
    listings: [sample('mist', 2, { id: 'owner_mist' }), sample('healing', 1, { id: 'owner_healing' })],
    receipts: [{ id: 'sample_sale', name: catalog.get('healing').name, quantity: 1, price: 6 }],
    selection: { kind: 'inventory', identity: catalog.get('mist').path }, amount: 1, price: 2
  };
  let view = 'browse', nextId = 1, message = '上架后由坊市代管，摊租包含照看。';
  const queries = { browse: '', mine: '' };
  const oldBrowse = $('browse').onclick;
  const oldSearch = $('search').oninput;
  const occupied = () => owner.listings.reduce((sum, row) => sum + row.width * row.height * row.quantity, 0);
  const current = () => owner.selection?.kind === 'listing'
    ? owner.listings.find(row => row.id === owner.selection.identity)
    : owner.inventory.find(row => row.path === owner.selection?.identity);
  const validPrice = price => Number.isSafeInteger(price) && price > 0;
  const notifyOwner = text => { message = text; renderOwner(); };

  function selectOwner(kind, row) {
    owner.selection = { kind, identity: kind === 'listing' ? row.id : row.path };
    owner.amount = kind === 'listing' ? row.quantity : 1;
    owner.price = row.price;
    renderOwner();
  }

  function ownerRow(row, kind) {
    const button = document.createElement('button');
    button.className = 'item-row' + (kind === 'inventory' ? ' inventory-row' : '');
    button.dataset.kind = kind; button.dataset.path = row.path;
    const name = document.createElement('span'); name.className = 'name'; name.textContent = row.name;
    const count = document.createElement('span'); count.className = 'count'; count.textContent = row.quantity;
    const value = document.createElement('span'); value.className = 'price';
    value.textContent = kind === 'inventory' ? row.width * row.height : row.price;
    button.append(name, count, value);
    button.setAttribute('aria-label', `${kind === 'inventory' ? '行囊' : '摊上'} ${row.name} ${row.quantity}件`);
    const chosen = owner.selection?.kind === kind && owner.selection.identity === (kind === 'listing' ? row.id : row.path);
    button.classList.toggle('selected', chosen);
    const term = $('search').value.trim(); button.classList.toggle('dim', term.length > 0 && !row.name.includes(term));
    button.onclick = () => selectOwner(kind, row);
    button.onmouseenter = event => showHover(row, event); button.onmousemove = event => showHover(row, event);
    button.onmouseleave = hideHover; button.onblur = hideHover;
    return button;
  }

  function renderOwnerEditor(syncFields = true) {
    const row = current(), listing = owner.selection?.kind === 'listing';
    $('ownerWithdraw').hidden = !listing || !row;
    $('ownerPrimary').classList.toggle('with-withdraw', listing && !!row);
    $('ownerPrimary').textContent = listing ? '保存价格' : '上架';
    $('ownerQuantityCaption').textContent = listing ? '余量' : '数量';
    $('ownerPrice').disabled = !row;
    if (!row) {
      $('ownerChosenIcon').removeAttribute('src'); $('ownerChosenIcon').hidden = true;
      $('ownerChosen').textContent = '选择行囊货物或摊上物品';
      $('ownerQuantity').textContent = '0'; $('ownerPlannedCells').textContent = '';
      $('ownerLess').disabled = $('ownerMore').disabled = $('ownerPrimary').disabled = true;
      if (syncFields) $('ownerPrice').value = '';
      return;
    }
    $('ownerChosenIcon').hidden = false; $('ownerChosenIcon').src = imagePath(row);
    $('ownerChosen').replaceChildren();
    const label = document.createTextNode(`${listing ? '摊上' : '行囊'} · ${row.name}`);
    const detail = document.createElement('small');
    detail.textContent = listing ? `余 ${row.quantity} 件 · 调价仅作用于未售部分` : `持有 ${row.quantity} 件 · 每件占 ${row.width} × ${row.height} 格`;
    $('ownerChosen').append(label, detail);
    if (listing) owner.amount = row.quantity; else owner.amount = Math.max(1, Math.min(row.quantity, owner.amount));
    $('ownerQuantity').textContent = owner.amount;
    if (syncFields) $('ownerPrice').value = Number.isFinite(owner.price) ? owner.price : '';
    $('ownerLess').disabled = listing || owner.amount <= 1;
    $('ownerMore').disabled = listing || owner.amount >= row.quantity;
    $('ownerPrimary').disabled = !owner.leased || !validPrice(owner.price) || !Number.isSafeInteger(owner.amount * owner.price);
    $('ownerWithdraw').disabled = !listing;
    $('ownerPlannedCells').hidden = listing;
    $('ownerPlannedCells').textContent = `本次 ${row.width * row.height * owner.amount} 格`;
  }

  function renderOwner() {
    $('wallet').textContent = owner.wallet;
    $('ownerLeaseState').textContent = owner.leased ? '租用中' : '未租用';
    $('ownerLeaseState').classList.toggle('inactive', !owner.leased);
    $('ownerExpiry').innerHTML = owner.leased ? '到期<br>1年2月1日0时' : '租期<br>开摊起三十日';
    $('ownerContractNote').textContent = owner.leased ? '到期不自动续租。\n未售货物与所得持续保管。' : '租金预付，含坊市代管。\n到期不自动续租。';
    $('ownerRent').textContent = owner.leased ? '结束摆摊' : '租用摊位';
    $('ownerRent').disabled = !owner.leased && owner.wallet < 1;
    $('ownerUsed').textContent = occupied();
    $('ownerInventory').replaceChildren(...owner.inventory.filter(row => row.quantity > 0).map(row => ownerRow(row, 'inventory')));
    $('ownerListings').replaceChildren(...owner.listings.map(row => ownerRow(row, 'listing')));
    for (const [id, text] of [['ownerInventory', '行囊中暂无可上架物品。'], ['ownerListings', '摊上还没有物品。']]) {
      if (!$(id).childElementCount) { const empty = document.createElement('div'); empty.className = 'empty'; empty.textContent = text; $(id).append(empty); }
    }
    $('ownerInventoryCount').textContent = `${owner.inventory.filter(row => row.quantity > 0).length} 种可上架物品`;
    $('ownerListingCount').textContent = `${owner.listings.length} 种寄存货物`;
    $('ownerPending').textContent = owner.pending; $('ownerClaim').disabled = owner.pending <= 0;
    $('ownerClaimed').textContent = `本期已领 ${owner.claimed} 灵石`;
    $('ownerHistory').replaceChildren();
    owner.receipts.slice().reverse().forEach(receipt => {
      const item = document.createElement('div'); item.className = 'history-row';
      const amount = document.createElement('span'); amount.className = 'earned'; amount.textContent = `+${receipt.price * receipt.quantity}`;
      const name = document.createTextNode(receipt.name);
      const detail = document.createElement('small'); detail.textContent = `售出 ${receipt.quantity} 件 · 单价 ${receipt.price} 灵石`;
      item.append(amount, name, detail); $('ownerHistory').append(item);
    });
    if (view === 'mine') $('bottomNote').textContent = message;
    renderOwnerEditor();
  }

  function showView(next) {
    queries[view] = $('search').value;
    view = next; cancelDrag(); hideHover(); closeModal();
    $('search').value = queries[view];
    $('search').placeholder = view === 'mine' ? '查找行囊或摊货' : '查找摊货';
    $('viewport').hidden = $('browseSelection').hidden = view === 'mine';
    $('ownerPane').hidden = $('ownerSelection').hidden = view !== 'mine';
    $('dragHint').hidden = view === 'mine';
    $('browse').classList.toggle('active', view === 'browse'); $('mine').classList.toggle('active', view === 'mine');
    if (view === 'mine') renderOwner();
    else { render(); renderSelection(); $('bottomNote').textContent = '成员寄存货物，由坊市照看 · 以灵石交易'; }
  }

  $('mine').onclick = () => showView('mine');
  $('browse').onclick = () => { const wasBrowse = view === 'browse'; showView('browse'); if (wasBrowse) oldBrowse(); };
  $('search').oninput = () => { queries[view] = $('search').value; if (view === 'mine') renderOwner(); else oldSearch(); };
  $('ownerLess').onclick = () => { owner.amount = Math.max(1, owner.amount - 1); renderOwnerEditor(); };
  $('ownerMore').onclick = () => { const row = current(); if (row && owner.selection.kind === 'inventory') owner.amount = Math.min(row.quantity, owner.amount + 1); renderOwnerEditor(); };
  $('ownerPrice').oninput = () => { owner.price = Number($('ownerPrice').value); renderOwnerEditor(false); };

  $('ownerRent').onclick = () => {
    if (owner.leased) modal('结束摆摊', '已付租金不退。未售物品与所得继续保管，可随时取回。', () => {
      if (!owner.leased) return; owner.leased = false; notifyOwner('已结束摆摊，货物与所得仍由坊市保管。');
    });
    else modal('租用摊位', '租金：1 灵石 / 游戏月，包含坊市代管。\n开摊起三十日，到期不自动续租。', () => {
      if (owner.leased || owner.wallet < 1) return; owner.wallet -= 1; owner.leased = true; notifyOwner('已支付 1 灵石，摊位租期三十日。');
    });
  };

  $('ownerPrimary').onclick = () => {
    const row = current(); if (!row || !owner.leased || !validPrice(owner.price)) return;
    const price = owner.price, amount = owner.amount;
    if (owner.selection.kind === 'inventory') {
      const path = row.path;
      modal('确认上架', `${row.name} ×${amount}\n单价 ${price} 灵石；占用 ${row.width * row.height * amount} 格。\n确认后货物移交坊市代管。`, () => {
        const available = owner.inventory.find(item => item.path === path);
        if (!owner.leased || !available || amount < 1 || available.quantity < amount || !validPrice(price)) return;
        let listing = owner.listings.find(item => item.path === path && item.price === price);
        if (listing) listing.quantity += amount;
        else { listing = { ...structuredClone(available), id: `owner_listing_${nextId++}`, quantity: amount, price }; owner.listings.push(listing); }
        available.quantity -= amount;
        owner.selection = { kind: 'listing', identity: listing.id }; owner.price = listing.price;
        notifyOwner(`已上架 ${available.name} ×${amount}，由坊市代管。`);
      });
    } else {
      const id = row.id;
      modal('修改售价', `${row.name}\n剩余货物改为 ${price} 灵石 / 件。`, () => {
        const listing = owner.listings.find(item => item.id === id); if (!owner.leased || !listing || !validPrice(price)) return;
        const same = owner.listings.find(item => item.id !== id && item.path === listing.path && item.price === price);
        if (same) { same.quantity += listing.quantity; owner.listings = owner.listings.filter(item => item.id !== id); owner.selection.identity = same.id; }
        else listing.price = price;
        owner.price = price; notifyOwner('已更新未售货物的单价。');
      });
    }
  };

  $('ownerWithdraw').onclick = () => {
    const row = current(); if (!row || owner.selection.kind !== 'listing') return;
    const id = row.id;
    modal('撤下余货', `将 ${row.name} 的未售部分收回行囊？`, () => {
      const listing = owner.listings.find(item => item.id === id); if (!listing) return;
      let held = owner.inventory.find(item => item.path === listing.path);
      if (held) held.quantity += listing.quantity;
      else { held = { ...structuredClone(listing), quantity: listing.quantity, price: catalog.get(listing.key).price }; delete held.id; owner.inventory.push(held); }
      owner.listings = owner.listings.filter(item => item.id !== id);
      owner.selection = { kind: 'inventory', identity: held.path }; owner.amount = 1; owner.price = catalog.get(held.key).price;
      notifyOwner('未售物品已收回行囊。');
    });
  };

  $('ownerClaim').onclick = () => {
    const amount = owner.pending; if (amount <= 0) return;
    modal('领取所得', `领取待领灵石 ${amount} 枚。`, () => {
      if (owner.pending < amount) return;
      owner.pending -= amount; owner.wallet += amount; owner.claimed += amount;
      notifyOwner(`已领取 ${amount} 灵石。`);
    });
  };

  window.ownerPreview = {
    getState: () => structuredClone({ ...owner, usedCells: occupied(), view }),
    showView
  };
  renderOwner();
  if (location.hash === '#mine') showView('mine');
  window.ownerPreviewReady = true;
})();
