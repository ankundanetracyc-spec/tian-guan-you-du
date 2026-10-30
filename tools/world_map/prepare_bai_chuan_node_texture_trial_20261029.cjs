const fs=require('node:fs'),sharp=require('sharp');
const dir='docs/美术规范/百川独立节点贴图样板_20261029',source='docs/美术规范/百川节点小景试稿_20261029/百川_三类节点局部效果_v1.png';
async function main(){
 for(const d of [dir,dir+'/素材',dir+'/参考裁切',dir+'/提示词'])fs.mkdirSync(d,{recursive:true});
 const crops={sect:[210,115,480,225],city:[480,520,490,185],town:[1015,655,385,150]};
 for(const [id,[left,top,width,height]] of Object.entries(crops))await sharp(source).extract({left,top,width,height}).png().toFile(dir+'/参考裁切/'+id+'.png');
 console.log('Prepared three original-node references.');
}
main().catch(e=>{console.error(e);process.exitCode=1});
