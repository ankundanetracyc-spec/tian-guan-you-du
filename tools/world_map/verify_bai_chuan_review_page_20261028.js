async (page) => {
  const ids = await page.locator('#jump option').evaluateAll(es => es.map(e => e.value));
  let visited = 0;
  for (const id of ids) {
    await page.locator('#jump').selectOption(id);
    if (!(await page.locator('#site').textContent())) throw new Error('Empty location');
    visited++;
  }
  const image = await page.locator('.canvas img').evaluate(e => ({
    loaded: e.complete,
    width: e.naturalWidth,
    renderedWidth: e.getBoundingClientRect().width
  }));
  if (visited !== 123 || !image.loaded || image.width !== 6144 || image.renderedWidth !== 6144)
    throw new Error('Native review dimensions or coverage failed');
  await page.locator('#jump').selectOption('bc_place_001');
  await page.locator('#clear').click();
  return { visited, image, fixedScale: true };
}
