"""Build an offline review artifact. No inventory, contribution or catalog rules are changed."""
from pathlib import Path
from io import BytesIO
import re,json,base64
from fontTools import subset
ROOT=Path(__file__).resolve().parents[1]
DOC=ROOT/'docs/美术规范/交易与善功堂文字目录_20261030'
pricing=json.loads((ROOT/'docs/plans/物品与装备/善功堂临时贡献价_20261030.json').read_text(encoding='utf-8'))
rows=[]
for path in sorted((ROOT/'resources/entities').rglob('*.tres')):
    text=path.read_text(encoding='utf-8-sig')
    def string(key):
        m=re.search(r'^'+key+r' = &?"([^"]*)"',text,re.M)
        return m.group(1) if m else ''
    icon=string('icon_path')
    if '/inventory_density_20261029/' not in icon:continue
    name=string('entity_name');identity=string('definition_id');cat=string('item_category')
    # Do not bring paused 静心/通脉 or unconfirmed breakthrough products back via the review catalog.
    if cat=='丹药' and not name.startswith('回春丹'):continue
    if not name or not identity or any(x['id']==identity for x in rows):continue
    def number(key):
        m=re.search(r'^'+key+r' = (\d+)',text,re.M)
        return int(m.group(1)) if m else 1
    if 'equipment/' in str(path).replace('\\','/') and '剑' in name:cat='器物'
    price=pricing['items'].get(identity,{'contribution':pricing['category_defaults'].get(cat,10),'source':'临时拟价'})
    rows.append(dict(id=identity,name=name,category=cat,icon='../../../'+icon.removeprefix('res://'),w=number('inventory_width'),h=number('inventory_height'),contribution=price['contribution'],priceSource=price['source']))
(DOC/'目录数据.js').write_text('const PREVIEW_DATA='+json.dumps(rows,ensure_ascii=False)+';\n',encoding='utf-8')
html=(DOC/'目录样板.html').read_text(encoding='utf-8')
css=(DOC/'目录样板.css').read_text(encoding='utf-8')
html=html.replace('<link rel="stylesheet" href="目录样板.css">','<style>'+css+'</style>')
html=html.replace('<link rel="stylesheet" href="善功堂原框架.css">','<style>'+(DOC/'善功堂原框架.css').read_text(encoding='utf-8')+'</style>')
for file in ['目录数据.js','目录样板.js']:
    source=(DOC/file).read_text(encoding='utf-8')
    if file=='目录样板.js':source=source.replace('img.src=item.icon','img.src=PREVIEW_ICONS[item.icon]||item.icon')
    html=html.replace(f'<script src="{file}"></script>','<script>'+source+'</script>')
def url(path,mime):return 'data:'+mime+';base64,'+base64.b64encode(path.read_bytes()).decode()
icons={r['icon']:url((DOC/r['icon']).resolve(),'image/png') for r in rows}
html=html.replace('<script>const PREVIEW_DATA','<script>const INLINE_PREVIEW_ICONS='+json.dumps(icons)+';</script><script>const PREVIEW_DATA')
for file in ['textures/ui/inventory_20261012/approved_shell_v1.png','textures/ui/inventory_index_20261029/index_panel_paper.png','textures/ui/shangong_columns_20261030/column_mountain_paper_v1.png']:
    html=html.replace('../../../'+file,url(ROOT/file,'image/png'))
html=html.replace('../交易列表与物品占格悬停_20261029/善功堂_分类分列文字目录_v2.png',url(ROOT/'docs/美术规范/交易列表与物品占格悬停_20261029/善功堂_分类分列文字目录_v2.png','image/png'))
options=subset.Options();font=subset.load_font(str(ROOT/'resources/font/LXGWZhenKaiSlabGB-Regular.ttf'),options);sub=subset.Subsetter(options=options);sub.populate(text=html);sub.subset(font);font.flavor='woff2';buf=BytesIO();font.save(buf)
html=html.replace('../../../resources/font/LXGWZhenKaiSlabGB-Regular.ttf','data:font/woff2;base64,'+base64.b64encode(buf.getvalue()).decode())
(DOC/'交易与善功堂_独立预览.html').write_text(html,encoding='utf-8')
out=ROOT/'output/playwright';out.mkdir(parents=True,exist_ok=True)
(out/'load_trade_shop_preview.js').write_text('async page=>{await page.goto("about:blank");await page.setContent('+json.dumps(html,ensure_ascii=True)+');await page.evaluate(()=>document.fonts.ready.then(()=>true));return {title:await page.title(),columns:await page.locator(".shop-column").count()}}',encoding='utf-8')
print(f'Built offline review: {len(rows)} candidate item definitions; nine category columns; no game mutation.')
