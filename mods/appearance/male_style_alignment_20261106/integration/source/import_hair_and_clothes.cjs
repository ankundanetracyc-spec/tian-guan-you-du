// Technical import only: preserve native generated Alpha and original canvas;
// retain approved portrait RGB in visible material cores, fill hidden parts from image_gen.
const fs = require('fs'), path = require('path'), sharp = require('sharp'), crypto = require('crypto');
const R = path.resolve(__dirname, '..'), W = 1024, H = 1536;
const sha = f => crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
async function read(f) {
  const {data,info} = await sharp(f).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(info.width !== W || info.height !== H) throw Error('Full canvas required: '+f);
  return data;
}
function alphaHash(d) { const a=Buffer.alloc(W*H); for(let p=0;p<W*H;p++)a[p]=d[p*4+3]; return crypto.createHash('sha256').update(a).digest('hex'); }
function inSourceNeck(x,y) {
  if(y<545 || y>684) return false;
  const t=(y-545)/139;
  return x>433+(532-433)*t-2 && x<599+(532-599)*t+2;
}
(async()=>{
  const masterPath=path.join(R,'source/approved_male.png'), master=await read(masterPath), records=[];
  if(sha(masterPath)!=='d97b79e3372df42db24b4dd5c3ec42f376d652616d9fa25d78a393b004da81a8') throw Error('Wrong approved portrait');
  for(const part of ['hair_front','hair_back','clothing','clothing_back']) {
    const input=path.join(R,'raw',part+'.png'), native=await read(input), out=Buffer.from(native);
    let retained=0;
    for(let p=0;p<W*H;p++) {
      const k=p*4, x=p%W, y=Math.floor(p/W);
      if(native[k+3]<240) continue;
      const r=master[k],g=master[k+1],b=master[k+2], lum=.2126*r+.7152*g+.0722*b;
      const chroma=Math.max(r,g,b)-Math.min(r,g,b);
      let keep=false;
      if(part==='hair_front') keep=lum<165 && chroma<29;
      if(part==='hair_back') keep=(y>=90 && y<175 && lum<165 && chroma<65);
      // The front garment has reconstructed fabric where the source hair crossed it.
      // Keep its single continuous native painting; a color classifier cannot
      // safely distinguish source hair shadows from embroidery at those crossings.
      // The posterior band is largely hidden in the approved whole image;
      // its generated native silk is retained, never copy neck pixels here.
      if(keep) { out[k]=r;out[k+1]=g;out[k+2]=b;retained++; }
    }
    const output=path.join(R,'male',part,'01.png'); fs.mkdirSync(path.dirname(output),{recursive:true});
    await sharp(out,{raw:{width:W,height:H,channels:4}}).png().toFile(output);
    const check=await read(output);
    if(alphaHash(native)!==alphaHash(check)) throw Error('Generated alpha changed: '+part);
    records.push({part,input:path.relative(R,input),output:path.relative(R,output),source_sha256:sha(input),output_sha256:sha(output),native_alpha_unchanged:true,native_rgb_unchanged:part.startsWith('clothing'),canvas:[W,H],transform:'identity; no translation, scale, local warps or silhouette trimming',approved_core_rgb_pixels:retained,hidden_regions:'native image_gen painting',native_alpha_sha256:alphaHash(native)});
  }
  fs.writeFileSync(path.join(R,'source/hair_clothes_import.json'),JSON.stringify({approved_sha256:sha(masterPath),method:'Native image_gen transparent components with approved-source visible material RGB guard; full canvas and generated Alpha preserved exactly',registration_claim:'Same native frame; anatomical/garment alignment and visual fidelity require actual composite review',records},null,2)+'\n');
  console.log(JSON.stringify(records.map(x=>({part:x.part,approved_core_rgb_pixels:x.approved_core_rgb_pixels,alpha_unchanged:x.native_alpha_unchanged}))));
})().catch(e=>{console.error(e);process.exitCode=1;});
