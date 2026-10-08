"""Copy only the six current hair imports from the isolated Godot import."""
from pathlib import Path
import re,hashlib,shutil,sys
ROOT=Path(__file__).resolve().parents[1]
TEMP=ROOT/'tmp/ink_soft_import_project_20261005'
sex='female' if '--female' in sys.argv else 'male'
targets=[(sex,slot,index) for slot in ['hair_front','hair_back'] for index in [6,7,8]]
if '--refinement' in sys.argv:
 targets=[('male','hair_front',8)]+[('female','hair_front',i) for i in [6,7,8]]
if '--straight' in sys.argv:
 targets=[('male','hair_front',8)]+[('female','hair_front',i) for i in [6,7,8]]+[('male','hair_back',8),('female','hair_back',8)]
if '--sideburn' in sys.argv:
 targets=[('male','hair_front',8),('female','hair_front',8),('male','hair_back',8)]
for target_sex,slot,index in targets:
  png=Path('mods/appearance/ink_three_variants_20261004')/target_sex/slot/f'{index:02}.png'
  meta=png.with_suffix('.png.import')
  content=(TEMP/meta).read_text(encoding='utf-8')
  cache=Path(re.search(r'path="res://([^"]+\.ctex)"',content).group(1))
  assert cache.parts[:2]==('.godot','imported')
  md5=cache.with_suffix('.md5')
  assert hashlib.md5((ROOT/png).read_bytes()).hexdigest() in (TEMP/md5).read_text()
  for file in [meta,cache,md5]:
   (ROOT/file).parent.mkdir(parents=True,exist_ok=True);shutil.copy2(TEMP/file,ROOT/file)
  print(slot,index,'verified import copied')
