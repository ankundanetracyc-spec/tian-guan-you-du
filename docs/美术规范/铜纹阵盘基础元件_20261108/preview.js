(() => {
  'use strict';
  const NS = 'http://www.w3.org/2000/svg';
  const unit = 128;
  // UV refers to the full unchanged native PNG, including its transparent margin.
  const ports = {
    sensor: { core: [.7974, .5056], mouth: [.8533, .5056], cell: [128, 64], origin: [0, 0], size: [128, 128], slot: 4.3, kind: 'signal' },
    source: { core: [.9543, .4966], mouth: [.9679, .4966], cell: [256, 448], origin: [0, 384], size: [256, 128], slot: 5.3, kind: 'energy' },
    wardNorth: { core: [.5006, .1088], mouth: [.5006, .0880], cell: [448, 128], origin: [384, 128], size: [128, 256], slot: 7.5, kind: 'signal' },
    wardSouth: { core: [.5006, .8825], mouth: [.5006, .9115], cell: [448, 384], origin: [384, 128], size: [128, 256], slot: 9.5, kind: 'energy' }
  };
  const palette = { signal: { width: 6.5, fill: '#bb6652', light: '#e0a18b' }, energy: { width: 10.5, fill: '#ceb787', light: '#efe0b5' } };
  const svg = document.getElementById('board-demo');
  const gridToggle = document.getElementById('show-grid');
  const status = document.getElementById('scene-status');
  const radios = [...document.querySelectorAll('input[name="scene"]')];
  let scene = 'complete';

  function el(parent, tag, attrs = {}, text) {
    const n = document.createElementNS(NS, tag);
    Object.entries(attrs).forEach(([k, v]) => n.setAttribute(k, String(v)));
    if (text !== undefined) n.textContent = text;
    parent.appendChild(n); return n;
  }
  const point = (port, key) => [port.origin[0] + port[key][0] * port.size[0], port.origin[1] + port[key][1] * port.size[1]];
  function lane(parent, kind, mask) {
    const g = el(parent, 'g', mask ? { mask: `url(#${mask})` } : {});
    return { edge: el(g, 'g'), fill: el(g, 'g'), brush: el(g, 'g'), kind };
  }
  function trace(target, d, width = palette[target.kind].width) {
    const c = palette[target.kind];
    el(target.edge, 'path', { d, fill: 'none', stroke: '#51463d', 'stroke-width': width + 3, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
    el(target.fill, 'path', { d, fill: 'none', stroke: c.fill, 'stroke-width': width, 'stroke-linecap': 'round', 'stroke-linejoin': 'round' });
    el(target.brush, 'path', { d, fill: 'none', stroke: c.light, 'stroke-width': Math.max(.7, width * .12), 'stroke-opacity': .32, 'stroke-dasharray': '9 16 2 13', 'stroke-linecap': 'butt' });
  }
  function tapered(target, a, b, wa, wb) {
    const dx = b[0] - a[0], dy = b[1] - a[1], len = Math.hypot(dx, dy);
    if (!len) return;
    const nx = -dy / len, ny = dx / len;
    const pts = [[a[0] + nx * wa / 2, a[1] + ny * wa / 2], [b[0] + nx * wb / 2, b[1] + ny * wb / 2], [b[0] - nx * wb / 2, b[1] - ny * wb / 2], [a[0] - nx * wa / 2, a[1] - ny * wa / 2]].map(p => p.join(',')).join(' ');
    el(target.edge, 'polygon', { points: pts, fill: '#51463d', stroke: '#51463d', 'stroke-width': 3, 'stroke-linejoin': 'round' });
    el(target.fill, 'polygon', { points: pts, fill: palette[target.kind].fill });
  }
  function connect(target, port) {
    const mouth = point(port, 'mouth'), core = point(port, 'core');
    const width = palette[port.kind].width;
    tapered(target, port.cell, mouth, width, Math.min(width, port.slot));
    tapered(target, mouth, core, Math.min(width, port.slot), Math.min(width, port.slot));
  }
  function image(parent, name, x, y, w, h) {
    return el(parent, 'image', { href: name + '.png', x, y, width: w, height: h, preserveAspectRatio: 'xMidYMid meet', 'data-asset': name });
  }
  function marker(parent, kind, x, y, attrs = {}) {
    return kind === 'signal'
      ? el(parent, 'path', { d: `M${x} ${y - 5}L${x + 5} ${y}L${x} ${y + 5}L${x - 5} ${y}Z`, ...attrs })
      : el(parent, 'circle', { cx: x, cy: y, r: 6, ...attrs });
  }
  function sensorBridge(parent) {
    const mouth = point(ports.sensor, 'mouth');
    el(parent, 'path', { d: `M${mouth[0] - 3.05} ${mouth[1]}H${mouth[0] + .65}`, fill: 'none', stroke: palette.signal.fill, 'stroke-width': 3.8, 'stroke-linecap': 'butt', 'data-join': 'sensor-short-bridge' });
  }
  function debugLayer(parent) {
    const g = el(parent, 'g', { id: 'grid-checks', visibility: gridToggle.checked ? 'visible' : 'hidden' });
    for (let i = 0; i <= 4; i++) {
      el(g, 'path', { d: `M${i * unit} 0V512 M0 ${i * unit}H512`, class: 'grid-line' });
      if (i < 4) {
        el(g, 'text', { x: i * unit + 64, y: -9, 'text-anchor': 'middle' }, i + 1);
        el(g, 'text', { x: -13, y: i * unit + 68, 'text-anchor': 'middle' }, i + 1);
      }
    }
    return g;
  }
  function renderBoard() {
    svg.replaceChildren();
    el(svg, 'title', {}, '基础护持：四件原图与真实格位接线对照');
    const defs = el(svg, 'defs');
    for (const kind of ['signal', 'energy']) {
      const mask = el(defs, 'mask', { id: `${kind}-break`, maskUnits: 'userSpaceOnUse', x: -24, y: -24, width: 560, height: 560 });
      el(mask, 'rect', { x: -24, y: -24, width: 560, height: 560, fill: 'white' });
      if (scene === kind) el(mask, 'rect', kind === 'signal' ? { x: 298, y: 48, width: 44, height: 32, fill: 'black' } : { x: 330, y: 432, width: 44, height: 32, fill: 'black' });
    }
    el(svg, 'rect', { x: 0, y: 0, width: 512, height: 512, fill: '#b4a796' });
    debugLayer(svg);
    const signal = lane(svg, 'signal', 'signal-break');
    trace(signal, 'M128 64H432Q448 64 448 80V128');
    connect(signal, ports.sensor); connect(signal, ports.wardNorth);
    const energy = lane(svg, 'energy', 'energy-break');
    trace(energy, 'M256 448H432Q448 448 448 432V384');
    connect(energy, ports.source); connect(energy, ports.wardSouth);
    const parts = el(svg, 'g', { id: 'part-images' });
    image(parts, 'sensor_1x1', 0, 0, 128, 128);
    image(parts, 'eye_1x1', 128, 128, 128, 128);
    image(parts, 'source_2x1', 0, 384, 256, 128);
    image(parts, 'ward_1x2', 384, 128, 128, 256);
    // Only the sensor's native mouth has a ~2.5-design-pixel dark closing edge.
    // Repair that tiny opening above the unchanged PNG, without covering its copper lips.
    sensorBridge(svg);
    if (scene !== 'complete') {
      const ends = scene === 'signal' ? [[298, 64], [342, 64]] : [[330, 448], [374, 448]];
      for (const [x, y] of ends) marker(svg, scene, x, y, { fill: '#b4a796', stroke: palette[scene].fill, 'stroke-width': 1.8, 'paint-order': 'stroke', class: 'broken-end' });
    }
    const marks = el(svg, 'g', { id: 'port-checks', visibility: gridToggle.checked ? 'visible' : 'hidden' });
    const names = { sensor: '感应枢 · 信号输出 · 东口', source: '阵源 · 供能输出 · 东口', wardNorth: '护持阵器 · 信号输入 · 北口', wardSouth: '护持阵器 · 供能输入 · 南口' };
    for (const [key, port] of Object.entries(ports)) {
      const p = point(port, 'mouth'), g = el(marks, 'g');
      el(g, 'title', {}, names[key]);
      marker(g, port.kind, p[0], p[1], { class: 'port-check-halo' });
      marker(g, port.kind, p[0], p[1], { class: 'port-check' });
    }
    status.textContent = scene === 'complete' ? '信号、供能各自连通；阵眼无接线口。' : scene === 'signal' ? '信号纹已断开；供能纹保持原连接。' : '供能纹已断开；信号纹保持原连接。';
    svg.dataset.scene = scene; svg.dataset.grid = String(gridToggle.checked);
  }
  function renderDetails() {
    const straight = document.getElementById('detail-straight');
    trace(lane(straight, 'signal'), 'M28 30H272');
    trace(lane(straight, 'energy'), 'M28 76H272');
    const corner = document.getElementById('detail-corner');
    trace(lane(corner, 'signal'), 'M22 25H105Q121 25 121 41V83');
    trace(lane(corner, 'energy'), 'M182 83V41Q182 25 198 25H279');
    const tee = document.getElementById('detail-tee'), power = lane(tee, 'energy');
    // One common underlay then a common fill keeps the T junction free of black seams.
    trace(power, 'M34 70H266M150 70V20');
    for (const [key, port] of Object.entries(ports)) {
      const socket = document.getElementById(`socket-${key}`), net = lane(socket, port.kind);
      const [x, y] = port.cell;
      trace(net, key === 'sensor' || key === 'source' ? `M${x} ${y}H${x + 160}` : key === 'wardNorth' ? `M${x} ${y - 30}V${y}` : `M${x} ${y}V${y + 30}`);
      connect(net, port);
      const asset = key === 'sensor' ? 'sensor_1x1' : key === 'source' ? 'source_2x1' : 'ward_1x2';
      image(socket, asset, ...port.origin, ...port.size);
      if (key === 'sensor') sensorBridge(socket);
    }
  }
  gridToggle.addEventListener('change', renderBoard);
  radios.forEach(r => r.addEventListener('change', () => { if (r.checked) { scene = r.value; renderBoard(); } }));
  renderBoard(); renderDetails();
})();
