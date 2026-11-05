const fs=require('fs'),path=require('path'),sharp=require('sharp'),{fit,render}=require('../register_master.cjs');
const base=path.resolve(__dirname,'../..');
const specs=[
 {name:'eyes01',part:'eyes/01',src:[[483,317],[1656,317],[780,403]],dst:[[467,377],[568,377],[491.5,385]]},
 {name:'eyes02',part:'eyes/02',src:[[453,332],[1620,328],[758,424]],dst:[[464,379],[566,379],[491,387]]},
 {name:'nose',part:'nose/01',src:[[451,1012],[566,1012],[510,963]],dst:[[504,451.8],[526,451.8],[515,442.5]]},
 {name:'mouth',part:'mouth/01',src:[[350,307],[1790,307],[1080,326]],dst:[[480,482],[550,482],[515.2,483.5]]},
 {name:'face',part:'face_shape/01',src:[[278,718],[741,718],[511,944]],dst:[[405,438],[627,438],[516,546]],note:'Ear targets are hidden design supports centered on reference chin, not claimed visible reference landmarks.'}
];
(async()=>{let records=[];for(const s of specs){const t=fit(s.src,s.dst);if(Math.max(...t.residuals)>2)throw Error('Control residual '+s.name);const input=s.name==='nose'?'nose_soft.png':s.name+(s.name==='face'?'_raw.png':'_clean.png');await render(path.join(__dirname,input),path.join(base,'male',s.part+'.png'),t);records.push({...s,input,...t});}fs.writeFileSync(path.join(__dirname,'registration.json'),JSON.stringify(records,null,2));console.log(records.map(s=>({name:s.name,error:Math.max(...s.residuals)})));})();
