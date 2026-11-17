// FDP FreeText annotations. Callers supply the font and fontkit.
const MARKER = 'FDP.Text.v1';
const name = (lib, key) => lib.PDFName.of(key);
const readText = value => typeof value?.decodeText === 'function' ? value.decodeText() : '';

function findAnnotations(lib, document, pageIndex) {
  const page = document.getPage(pageIndex);
  const entries = page.node.Annots();
  if (!entries) return [];
  const result = [];
  for (let index = 0; index < entries.size(); index++) {
    const reference = entries.get(index);
    const dict = document.context.lookup(reference);
    if (!(dict instanceof lib.PDFDict) || readText(dict.get(name(lib, 'FDPKind'))) !== MARKER) continue;
    const rect = dict.lookup(name(lib, 'Rect'));
    const values = rect?.asArray().map(value => value.asNumber());
    if (!values || values.length !== 4) continue;
    const size = dict.lookup(name(lib, 'FDPSize'));
    const flags = dict.lookup(name(lib, 'FDPStyle'))?.asNumber?.() || 0;
    const storedColor = readText(dict.get(name(lib, 'FDPColor')));
    result.push({ index, id: readText(dict.get(name(lib, 'NM'))), text: readText(dict.get(name(lib, 'Contents'))), rect: values, fontSize: size?.asNumber?.() || 16, color: /^#[0-9a-f]{6}$/i.test(storedColor) ? storedColor : '#000000', bold: !!(flags & 1), italic: !!(flags & 2), underline: !!(flags & 4) });
  }
  return result;
}

export async function listTextAnnotations(lib, bytes, pageIndex) {
  const document = await lib.PDFDocument.load(bytes);
  return findAnnotations(lib, document, pageIndex).map(({ id, text, rect, fontSize, color, bold, italic, underline }) => ({ id, text, rect, fontSize, color, bold, italic, underline }));
}

export async function changeTextAnnotation({ lib, fontkit, fontBytes, bytes, pageIndex, id, text, rect, fontSize = 16, color = '#000000', bold = false, italic = false, underline = false, remove = false }) {
  const document = await lib.PDFDocument.load(bytes);
  if (document.getForm().getFields().length) throw new Error('暂不支持修改包含表单的文档');
  const page = document.getPage(pageIndex);
  if (page.getRotation().angle % 360 !== 0) throw new Error('旋转页面上的文字框尚未支持');
  const existing = findAnnotations(lib, document, pageIndex).find(item => item.id === id);
  if (remove && !existing) throw new Error('找不到此文字框');
  if (!remove) {
    if (typeof id !== 'string' || !/^[A-Za-z0-9_-]{1,120}$/.test(id)) throw new Error('文字框标识无效');
    if (typeof text !== 'string' || !text.trim() || /[\r\n\t]/.test(text) || text.length > 300) throw new Error('请输入不超过 300 个字符的单行文字');
    if (!Array.isArray(rect) || rect.length !== 4 || rect.some(value => !Number.isFinite(value)) || rect[2] <= rect[0] || rect[3] <= rect[1]) throw new Error('文字框位置无效');
    if (!Number.isFinite(fontSize) || fontSize < 6 || fontSize > 72) throw new Error('字号应在 6 到 72 之间');
    if (typeof color !== 'string' || !/^#[0-9a-f]{6}$/i.test(color)) throw new Error('文字颜色无效');
    const font = fontkit.create(fontBytes);
    for (const char of text) if (!font.hasGlyphForCodePoint(char.codePointAt(0))) throw new Error(`字体不支持字符：${char}`);
    document.registerFontkit(fontkit);
    const embedded = await document.embedFont(fontBytes, { subset: true });
    const width = rect[2] - rect[0], height = rect[3] - rect[1];
    const textWidth = embedded.widthOfTextAtSize(text, fontSize);
    if (textWidth + 6 + (italic ? fontSize * .24 : 0) > width || fontSize * 1.25 > height) throw new Error('文字超出文字框，请缩短文字或调整大小');
    const context = document.context;
    const baseline = Math.max(0, (height - fontSize) / 2);
    const rgb = [1, 3, 5].map(i => (parseInt(color.slice(i, i + 2), 16) / 255).toFixed(4)).join(' ');
    const line = underline ? `\n${Math.max(.5, fontSize / 18).toFixed(2)} w\n3 ${Math.max(1, baseline - fontSize * .12).toFixed(2)} m\n${(3 + textWidth).toFixed(2)} ${Math.max(1, baseline - fontSize * .12).toFixed(2)} l\nS` : '';
    const stream = context.flateStream(`q\n${rgb} rg\n${rgb} RG\n${bold ? `${(fontSize * .025).toFixed(2)} w` : ''}\nBT\n/F0 ${fontSize} Tf\n${bold ? '2' : '0'} Tr\n1 0 ${italic ? '.2' : '0'} 1 3 ${baseline} Tm\n${embedded.encodeText(text)} Tj\nET${line}\nQ`, {
      Type: 'XObject', Subtype: 'Form', BBox: [0, 0, width, height], Resources: { Font: { F0: embedded.ref } }
    });
    const appearance = context.register(stream);
    const annotation = context.register(context.obj({
      Type: 'Annot', Subtype: 'FreeText', Rect: rect,
      Contents: lib.PDFHexString.fromText(text), NM: lib.PDFString.of(id), FDPKind: lib.PDFString.of(MARKER), FDPSize: fontSize,
      FDPColor: lib.PDFString.of(color), FDPStyle: (bold ? 1 : 0) | (italic ? 2 : 0) | (underline ? 4 : 0),
      DA: lib.PDFString.of(`/Helv ${fontSize} Tf ${rgb} rg`), F: 4, Border: [0, 0, 0], AP: { N: appearance }
    }));
    if (existing) page.node.Annots().remove(existing.index);
    page.node.addAnnot(annotation);
  } else page.node.Annots().remove(existing.index);
  return new Uint8Array(await document.save());
}
