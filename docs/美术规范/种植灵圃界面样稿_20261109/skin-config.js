"use strict";
(() => {
  const base="../角色与静室纸墨木石样稿_20261108/";
  const data=window.SKIN_DATA;
  data.file=base+data.file;
  for(const spec of Object.values(data.frames))if(spec.file?.startsWith("assets/"))spec.file=base+spec.file;
})();
