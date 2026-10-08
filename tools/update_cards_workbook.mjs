import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';

const filePath = 'docs/锻躯气血体系卡牌规划.xlsx';
const implemented = new Set([
  '黄庭养形', '返照', '抱一', '周行', '归藏',
  '赤阳参同', '伏火', '偃月', '朱明',
  '阴符应物', '承机', '挟势', '合劲',
  '乘物游骸', '蜕形', '托身', '解形', '冯虚',
  '补虚行血', '还阳', '灵枢经筋', '去故', '内景损益', '淬厉'
]);

const input = await FileBlob.load(filePath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem('卡牌总表');
const used = sheet.getUsedRange();
const rows = used.values;
for (let rowIndex = 1; rowIndex < rows.length; rowIndex += 1) {
  const name = String(rows[rowIndex][0] ?? '');
  const source = String(rows[rowIndex][1] ?? '');
  if (!source.startsWith('《')) continue;
  rows[rowIndex][7] = implemented.has(name) ? '已实装' : '策划待实装';
}
used.values = rows;

const header = sheet.getRange('A1:J1');
header.format = {
  fill: '#3D2818',
  font: { name: 'Aptos', bold: true, color: '#FFFFFF' },
  horizontalAlignment: 'center',
  verticalAlignment: 'center',
  wrapText: true,
};
sheet.getRange(`H2:H${rows.length}`).format.horizontalAlignment = 'center';
sheet.getRange(`A2:J${rows.length}`).format.verticalAlignment = 'top';
sheet.getRange(`E2:E${rows.length}`).format.wrapText = true;
sheet.getRange(`A2:J${rows.length}`).format.rowHeight = 48;

const check = await workbook.inspect({ kind: 'table', range: '卡牌总表!A1:J53', include: 'values', tableMaxRows: 53, tableMaxCols: 10 });
console.log(check.ndjson);
const preview = await workbook.render({ sheetName: '卡牌总表', range: 'A1:J18', scale: 1.2, format: 'png' });
await fs.writeFile('tools/cards_workbook_after.png', new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(filePath);
