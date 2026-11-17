// Standard PDF highlight annotations created by FDP.
const marker = 'FDP.Highlight.v1';
const key = (lib, value) => lib.PDFName.of(value);
const str = value => value?.decodeText?.() || '';
function annotations(lib, doc, pageIndex) {
  const list = doc.getPage(pageIndex).node.Annots(); if (!list) return [];
  const found = [];
  for (let i = 0; i < list.size(); i++) {
    const dict = doc.context.lookup(list.get(i));
    if (!(dict instanceof lib.PDFDict) || str(dict.get(key(lib,'FDPKind'))) !== marker) continue;
    const quad = dict.lookup(key(lib,'QuadPoints'))?.asArray().map(x => x.asNumber());
    if (quad?.length) found.push({id:str(dict.get(key(lib,'NM'))), index:i, quad});
  }
  return found;
}
export async function listHighlights(lib, bytes, pageIndex) {
  const doc = await lib.PDFDocument.load(bytes); return annotations(lib,doc,pageIndex).map(({id,quad})=>({id,quad}));
}
export async function changeHighlight({lib,bytes,pageIndex,id,rects,color=[1,.86,.15],remove=false}) {
  const doc = await lib.PDFDocument.load(bytes); if (doc.getForm().getFields().length) throw new Error('暂不支持修改包含表单的文档');
  const page = doc.getPage(pageIndex), existing = annotations(lib,doc,pageIndex).find(x=>x.id===id);
  if (remove) { if (!existing) throw new Error('找不到此高亮'); page.node.Annots().remove(existing.index); }
  else {
    if (typeof id !== 'string' || !/^[A-Za-z0-9_-]{1,120}$/.test(id) || !Array.isArray(rects) || !rects.length || rects.length > 1000 || rects.some(r=>!Array.isArray(r) || r.length!==4 || r.some(n=>!Number.isFinite(n)) || r[0]>=r[2] || r[1]>=r[3])) throw new Error('高亮区域无效');
    if (!Array.isArray(color) || color.length!==3 || color.some(c=>!Number.isFinite(c)||c<0||c>1)) throw new Error('高亮颜色无效');
    const x0=Math.min(...rects.map(r=>r[0])),y0=Math.min(...rects.map(r=>r[1])),x1=Math.max(...rects.map(r=>r[2])),y1=Math.max(...rects.map(r=>r[3]));
    const quad=rects.flatMap(([l,b,r,t])=>[l,t,r,t,l,b,r,b]);
    const paint=rects.map(([l,b,r,t])=>`${l-x0} ${b-y0} ${r-l} ${t-b} re f`).join('\n');
    const stream=doc.context.flateStream(`q\n/GS0 gs\n${color.join(' ')} rg\n${paint}\nQ`,{Type:'XObject',Subtype:'Form',BBox:[0,0,x1-x0,y1-y0],Resources:{ExtGState:{GS0:{Type:'ExtGState',ca:0.32,CA:0.32,BM:'Multiply'}}}});
    const ref=doc.context.register(doc.context.obj({Type:'Annot',Subtype:'Highlight',Rect:[x0,y0,x1,y1],QuadPoints:quad,C:color,CA:0.32,F:4,NM:lib.PDFString.of(id),FDPKind:lib.PDFString.of(marker),AP:{N:doc.context.register(stream)}}));
    page.node.addAnnot(ref);
  }
  return new Uint8Array(await doc.save());
}
