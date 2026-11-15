const fs=require('fs'),path=require('path'),{spawn}=require('child_process'),{once}=require('events');
const ROOT=__dirname;
(async()=>{
 let html=fs.readFileSync(path.join(ROOT,'index.html'),'utf8');
 html=html.replace('<script src="assets.js"></script>','<script>'+fs.readFileSync(path.join(ROOT,'assets.js'),'utf8')+'</script>');
 html=html.replace('<script src="battle.js"></script>','<script>'+fs.readFileSync(path.join(ROOT,'battle.js'),'utf8')+'</script>');
 fs.writeFileSync(path.join(ROOT,'battle_preview.html'),html);
 if(/<script[^>]+src=/.test(html))throw Error('Standalone preview still has external scripts');
 const ffmpeg=path.resolve(ROOT,'../../tmp/wolf_media_tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe');
 async function run(args,logName){const p=spawn(ffmpeg,args,{windowsHide:true,stdio:['ignore','ignore','pipe']});let log='';p.stderr.on('data',d=>log+=d);const [code]=await once(p,'close');fs.writeFileSync(path.join(ROOT,'review',logName),log);if(code!==0)throw Error(log);}
 await run(['-y','-hide_banner','-loglevel','warning','-i',path.join(ROOT,'review/battle_preview.mp4'),'-filter_complex','[0:v]split[x][y];[x]trim=start=3.2:end=5.5,setpts=PTS-STARTPTS[x1];[y]trim=start=9.2:end=11.6,setpts=PTS-STARTPTS[y1];[x1][y1]concat=n=2:v=1:a=0,fps=12,scale=720:-1:flags=lanczos,split[a][b];[a]palettegen=stats_mode=diff:max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle','-loop','0',path.join(ROOT,'review/battle_highlight.gif')],'gif_encode.log');
 await run(['-hide_banner','-v','warning','-i',path.join(ROOT,'review/battle_preview.mp4'),'-f','null','-'],'video_decode.log');
 console.log(JSON.stringify({standalone_html:fs.statSync(path.join(ROOT,'battle_preview.html')).size,video:fs.statSync(path.join(ROOT,'review/battle_preview.mp4')).size,gif:fs.statSync(path.join(ROOT,'review/battle_highlight.gif')).size}));
})();
