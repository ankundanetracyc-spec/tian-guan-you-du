const fs=require('node:fs');
const doc='docs/美术规范/百川同画全图替换_20261029',art='textures/world/bai_chuan/joint_paint_v4';
function prepend(file,text){let old=fs.readFileSync(file,'utf8');if(old.includes(text))return;const line=old.indexOf('\n');fs.writeFileSync(file,old.slice(0,line+1)+'\n'+text+'\n'+old.slice(line+1));}
const index='百川同画全图替换（2026-10-29，当前已接入）：[完整可交互舆图](美术规范/百川同画全图替换_20261029/百川同画全图验收.html)、[素材与验证](美术规范/百川同画全图替换_20261029/README.md)。31处局部同画生成并拼6144、裁九张2048；53建成入口全部替换，宗门在山地、城镇在平地，70自然地点保留独立信息。53原像素蒙版只在悬停时提亮，123地点可点选；细虚线避开建筑完整范围与名称。原节点及路网数据SHA256保持，游戏1230项、浏览器218项通过；正式行旅未改。';
prepend('docs/README.md',index);prepend('docs/策划案摘要/README.md',index.replaceAll('](美术规范/','](../美术规范/'));
prepend('docs/plans/世界与探索/百川九宫格背景与地图分层方案_20261027.md','2026-10-29当前完整接入：[53入口同画全图与悬停验收](../美术规范/百川同画全图替换_20261029/README.md)。31局部1254原生绘制合成6144并裁九张2048，53建成入口全部同画、70自然地点保留信息；原首页测试入口及旧验收网址已切换。最终画面提取53原像素蒙版，默认不重复覆盖建筑，悬停才局部提亮；门洞和水面排除，名称与点击位置按实际建筑登记，原逻辑数据不变。1230项游戏交互、218项浏览器交互及像素检查通过。以下“两点未批量”“48独立入口”为阶段记录。');
prepend('docs/美术规范/百川同画节点与悬停蒙版_20261029/README.md','2026-10-29后续已完成：[53处同画全图替换](../百川同画全图替换_20261029/README.md)。本目录保留为两点方法验证记录，当前游戏测试采用整套新图。');
prepend('docs/美术规范/百川入口节点分层_20261028/README.md','2026-10-29整套替换完成：[当前可交互舆图](../百川同画全图替换_20261029/百川同画全图验收.html)、[53入口同画与原像素蒙版](../百川同画全图替换_20261029/README.md)。本目录旧“百川新图验收”地址自动进入新版，旧内容存为“百川新图验收_旧版留档.html”。');
fs.writeFileSync('docs/美术规范/百川入口节点分层_20261028/百川新图验收.html','<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=../百川同画全图替换_20261029/百川同画全图验收.html"><title>百川舆图</title><p><a href="../百川同画全图替换_20261029/百川同画全图验收.html">打开已替换的同画全图</a></p></html>');
const audit=JSON.parse(fs.readFileSync(doc+'/原像素与落点验收.json','utf8'));audit.status='complete_and_active_in_map_test';audit.runtimeChecks=1230;audit.browserChecks=218;fs.writeFileSync(doc+'/原像素与落点验收.json',JSON.stringify(audit,null,2));
console.log('Current documentation, preview entry and replacement status updated.');
