"""Set human-readable names without changing existing saved variant indexes."""
from pathlib import Path
import json

PACK=Path(__file__).resolve().parents[1]/'mods/appearance/ink_three_variants_20261004'
NAMES={
 'male':{
  'hair_front':['墨云中分','疏额双垂','清风垂鬓','清雅分流'],
  'hair_back':['墨发长披','高束散尾','低束长披','清雅半束','高束流风'],
  'brow':['墨锋平眉','疏柳斜眉','折锋细眉','凝岳剑眉','磐石浓眉','凌锋扬眉'],
  'eye':['清朗凤目','温润长目','凝岳沉目','磐石朗目','凌锋锐目'],
  'nose':['修直鼻','温钝鼻','挺秀鼻'], 'mouth':['薄唇淡赭','柔唇暖桃','平唇浅棕'],
  'face':['清俊长脸','温润卵面','削颏秀面'],
  'clothes':['碧岚云叶袍','玄青云竹袍','绛霞流云袍'],
  'beard':['无须','短八字须','八字颏须'],
 },
 'female':{
  'hair_front':['流云中分','柔丝垂鬓','疏影双垂','清雅分流'],
  'hair_back':['墨发长披','云髻散尾','柔波长披','清雅半束','高束流风'],
  'brow':['清秀平眉','柔柳细眉','微扬秀眉','温和远山眉'],
  'eye':['清润杏目','柔和圆目','含锋凤目','温婉杏目'],
  'nose':['清秀鼻','圆润鼻','小巧鼻','温润鼻'],
  'mouth':['桃色薄唇','朱粉柔唇','淡玫平唇','暖桃微唇'],
  'face':['清秀鹅蛋面','温婉圆颏面','秀长尖颏面','柔和卵面'],
  'clothes':['青瓷云梅袍','月白竹影袍','藕荷花云袍'],
  'makeup':['素颜','淡花叶颊妆','浅玫眼妆'],
 }
}

def main():
 p=PACK/'appearance_parts.json';config=json.loads(p.read_text(encoding='utf-8'))
 for c in config['components']:
  c['variant_names']=NAMES[c['gender']][c['part']]
  c['variant_count']=len(c['variant_names'])
  if c['gender']=='male' and c['part']=='eye':c['variant_indices']=[1,2,4,5,6]
 config['rig_id']='ink_three_variants_20261004_active70'
 p.write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 manifest=json.loads((PACK/'portrait_manifest.json').read_text(encoding='utf-8'))
 manifest['active_catalog']='appearance_parts.json'
 manifest['active_variant_count']=sum(c['variant_count'] for c in config['components'])
 manifest['masculine_revision']='source/masculine_revision_20261005/registration.json'
 manifest['visual_approved_by_user']=False
 (PACK/'portrait_manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
 print('Named',manifest['active_variant_count'],'active parts; rejected files remain excluded.')

if __name__=='__main__':main()
