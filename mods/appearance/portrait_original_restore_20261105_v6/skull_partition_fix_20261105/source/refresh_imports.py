from pathlib import Path
import json,shutil,re,hashlib,sys
R=Path(__file__).resolve().parent.parent;P=R.parent.parents[2];S=R/'installation/import_project'
report=json.loads((R/'installation/install_report.json').read_text(encoding='utf8'))
changed=[r for r in report['records'] if r['sha256']!=r['before_sha256']]
if '--prepare' in sys.argv:
 S.mkdir(exist_ok=True)
 (S/'project.godot').write_text('config_version=5\n[application]\nconfig/name="Portrait hair import"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n',encoding='utf8')
 for r in changed:
  rel=Path(r['resource_path'][6:]);dst=S/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(P/rel,dst);shutil.copy2(str(P/rel)+'.import',str(dst)+'.import')
 print('Prepared six isolated imports with identical resource paths and settings')
elif '--apply' in sys.argv:
 records=[]
 for r in changed:
  rel=Path(r['resource_path'][6:]);source=Path(str(P/rel)+'.import').read_text(encoding='utf8');temp=Path(str(S/rel)+'.import').read_text(encoding='utf8')
  dest=re.search(r'^path="res://([^"]+)"',source,re.M).group(1);other=re.search(r'^path="res://([^"]+)"',temp,re.M).group(1);assert dest==other
  for suffix in ['.ctex','.md5']:
   path=Path(dest).with_suffix(suffix);assert (S/path).exists();shutil.copy2(S/path,P/path)
  records.append({'resource':r['resource_path'],'cache':dest,'source_md5':hashlib.md5((P/rel).read_bytes()).hexdigest()})
 (S/'.gdignore').write_text('',encoding='utf8')
 (R/'installation/import_refresh.json').write_text(json.dumps(records,indent=2),encoding='utf8')
 print('Applied six resource-specific caches')
else:raise SystemExit('Specify --prepare or --apply')
