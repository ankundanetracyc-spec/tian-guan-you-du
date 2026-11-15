(() => {
  'use strict';

  // Read-only projection of AlchemyFurnaceView.FrontSurface / SpriteRect.
  const scriptUrl = document.currentScript?.src || document.baseURI;
  const width = 608, height = 869, sourceWidth = 1024, sourceHeight = 1536;
  const pixelRatio = 2, footBaseline = 1475 / 1536;
  const sourcePaths = Object.freeze({
    rear: 'res://textures/ui/alchemy_room_20261017/furnace_rear_half_soft_v13.png',
    front: 'res://textures/ui/alchemy_room_20261017/furnace_front_half_soft_v13.png'
  });
  const sourceUrls = Object.freeze(Object.fromEntries(Object.entries(sourcePaths).map(([layer, path]) =>
    [layer, new URL('../../../' + path.slice('res://'.length), scriptUrl).href])));
  const sourceY = Object.freeze([576, 590, 610, 630, 650, 680, 700, 750, 800, 850, 900, 950, 1000, 1050, 1100, 1150, 1190, 1205, 1210, 1235, 1300, 1400, 1446]);
  const sourceLeft = Object.freeze([198, 183, 180, 172, 160, 132, 111, 64, 32, 14, 6, 10, 23, 48, 88, 152, 244, 317, 419, 427, 448, 417, 419]);
  const targetY = Object.freeze([550, 576, 604, 630, 652, 680, 700, 750, 800, 850, 900, 950, 1000, 1050, 1100, 1161, 1210, 1229, 1235, 1260, 1326, 1428, 1475]);
  const targetLeft = Object.freeze([175, 173, 170, 155, 132, 139, 112, 63, 32, 15, 7, 9, 20, 40, 71, 140, 244, 317, 401, 427, 448, 417, 419]);

  const scale = Math.min(width / sourceWidth, height / (sourceHeight * footBaseline));
  const spriteRect = Object.freeze({
    x: (width - sourceWidth * scale) / 2,
    y: height - sourceHeight * scale * footBaseline,
    width: sourceWidth * scale,
    height: sourceHeight * scale,
    scale
  });
  const vertex = (row, col) => Object.freeze({
    source: Object.freeze([col === 0 ? sourceLeft[row] + 2 : col === 1 ? 512 : 1024 - sourceLeft[row] - 2, sourceY[row]]),
    target: Object.freeze([col === 0 ? targetLeft[row] + 1 : col === 1 ? 512 : 1024 - targetLeft[row] - 1, targetY[row]])
  });
  const triangles = [];
  for (let row = 0; row < sourceY.length - 1; row++) {
    for (let col = 0; col < 2; col++) {
      triangles.push(Object.freeze([vertex(row, col), vertex(row + 1, col), vertex(row, col + 1)]));
      triangles.push(Object.freeze([vertex(row, col + 1), vertex(row + 1, col), vertex(row + 1, col + 1)]));
    }
  }
  Object.freeze(triangles);

  function loadImage(layer) {
    return new Promise((resolve, reject) => {
      const image = new Image();
      image.decoding = 'async';
      image.onload = () => {
        if (image.naturalWidth !== sourceWidth || image.naturalHeight !== sourceHeight) {
          reject(new Error(`${layer}: expected ${sourceWidth}×${sourceHeight}, received ${image.naturalWidth}×${image.naturalHeight}`));
        } else resolve(image);
      };
      image.onerror = () => reject(new Error(`${layer}: failed to load ${sourceUrls[layer]}`));
      image.src = sourceUrls[layer];
    });
  }

  function drawTriangle(context, image, vertices) {
    const [[x0, y0], [x1, y1], [x2, y2]] = vertices.map(vertex => vertex.source);
    const [[u0, v0], [u1, v1], [u2, v2]] = vertices.map(vertex => vertex.target);
    const determinant = x0 * (y1 - y2) + x1 * (y2 - y0) + x2 * (y0 - y1);
    const coefficients = (q0, q1, q2) => [
      (q0 * (y1 - y2) + q1 * (y2 - y0) + q2 * (y0 - y1)) / determinant,
      (q0 * (x2 - x1) + q1 * (x0 - x2) + q2 * (x1 - x0)) / determinant,
      (q0 * (x1 * y2 - x2 * y1) + q1 * (x2 * y0 - x0 * y2) + q2 * (x0 * y1 - x1 * y0)) / determinant
    ];
    const [a, c, e] = coefficients(u0, u1, u2);
    const [b, d, f] = coefficients(v0, v1, v2);
    context.save();
    context.beginPath();
    context.moveTo(u0, v0); context.lineTo(u1, v1); context.lineTo(u2, v2);
    context.closePath(); context.clip();
    context.transform(a, b, c, d, e, f);
    context.drawImage(image, 0, 0);
    context.restore();
  }

  const metadata = (ok, errors = []) => Object.freeze({
    ok, status: ok ? 'ready' : 'error', sourcePaths, sourceUrls,
    designSize: Object.freeze([width, height]),
    sourceSize: Object.freeze([sourceWidth, sourceHeight]),
    backingSize: Object.freeze([width * pixelRatio, height * pixelRatio]),
    pixelRatio, footBaseline, spriteRect,
    mouth: Object.freeze([spriteRect.x + spriteRect.width * .5, spriteRect.y + spriteRect.height * .68]),
    mesh: Object.freeze({ sourceY, sourceLeft, targetY, targetLeft, triangles, triangleCount: triangles.length }),
    layerOrder: Object.freeze(['rear', 'front']),
    animation: false,
    errors: Object.freeze(errors)
  });

  async function mount(canvas) {
    if (!canvas || canvas.tagName !== 'CANVAS' || typeof canvas.getContext !== 'function')
      return metadata(false, ['AlchemyFurnace.mount requires a canvas element.']);
    canvas.width = width * pixelRatio; canvas.height = height * pixelRatio;
    canvas.style.width = `${width}px`; canvas.style.height = `${height}px`;
    const context = canvas.getContext('2d', { alpha: true });
    if (!context) return metadata(false, ['Canvas2D is unavailable.']);
    context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
    context.clearRect(0, 0, width, height);
    context.imageSmoothingEnabled = true; context.imageSmoothingQuality = 'high';
    const loaded = await Promise.allSettled([loadImage('rear'), loadImage('front')]);
    const errors = loaded.flatMap(result => result.status === 'rejected' ? [String(result.reason?.message || result.reason)] : []);
    if (errors.length) return metadata(false, errors);
    try {
      const [rear, front] = loaded.map(result => result.value);
      context.drawImage(rear, spriteRect.x, spriteRect.y, spriteRect.width, spriteRect.height);
      context.save();
      context.translate(spriteRect.x, spriteRect.y); context.scale(spriteRect.scale, spriteRect.scale);
      for (const triangle of triangles) drawTriangle(context, front, triangle);
      context.restore();
      return metadata(true);
    } catch (error) {
      return metadata(false, [String(error?.message || error)]);
    }
  }

  window.AlchemyFurnace = Object.freeze({ mount });
})();
