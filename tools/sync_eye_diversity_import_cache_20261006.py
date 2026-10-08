"""Copy only the three verified Godot texture imports from isolated import."""
from pathlib import Path
import re,hashlib,shutil
ROOT=Path(__file__).resolve().parents[1]
TEMP=ROOT/'tmp/ink_soft_import_project_20261005'
FOLDER=Path('mods/appearance/ink_three_variants_20261004/male/eyes')
for index in [4,8,9]:
    meta=FOLDER/f'{index:02}.png.import'
    text=(TEMP/meta).read_text(encoding='utf-8')
    cache=Path(re.search(r'path="res://([^"]+\.ctex)"',text).group(1))
    assert cache.parts[:2]==('.godot','imported')
    md5=cache.with_suffix('.md5')
    digest=hashlib.md5((ROOT/FOLDER/f'{index:02}.png').read_bytes()).hexdigest()
    assert digest in (TEMP/md5).read_text()
    for file in [meta,cache,md5]:shutil.copy2(TEMP/file,ROOT/file)
    print(index,'import cache verified and copied')
