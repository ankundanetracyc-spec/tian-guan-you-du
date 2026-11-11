(() => {
'use strict';
const base=new URL('../角色与静室纸墨木石样稿_20261108/',document.baseURI);
const catalog=structuredClone(window.SKIN_DATA);
catalog.file=new URL(catalog.file,base).href;
catalog.frameFile=new URL(catalog.frameFile,base).href;
for(const spec of Object.values(catalog.frames)) if(spec.file) spec.file=new URL(spec.file,base).href;
catalog.frames.craftPanel={...catalog.frames.panel,rendered:[12,12,12,12]};
catalog.frames.craftHeading={...catalog.frames.paper,rendered:[8,8,8,8]};
window.SKIN_DATA=catalog;
})();
