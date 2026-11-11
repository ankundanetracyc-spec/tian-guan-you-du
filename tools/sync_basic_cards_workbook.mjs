import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';

const path = 'docs/plans/战斗与构筑/历史表格/锻躯气血体系卡牌规划.xlsx';
const outputPath = 'outputs/锻躯气血体系卡牌规划_同步版.xlsx';
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path));
const jiazhao = '敌方攻击自身时使用；获得化解。化解层数 = 3 + floor(体魄/4) + floor(敏捷/5)。化解仅抵消本次攻击的物理伤害：每层抵消1点；该次攻击结算后全部消散。';
const tongji = '对指定1名敌方造成物理伤害。伤害 = round(11 + 体魄×0.45 + 敏捷×0.30)。';
for (const sheetName of ['卡牌总表', '基础牌库升级', '凡俗通用牌库']) {
  const sheet = workbook.worksheets.getItem(sheetName);
  const range = sheet.getUsedRange();
  const rows = range.values;
  for (const row of rows) {
    for (let i = 0; i < row.length; i += 1) {
      if (typeof row[i] === 'string') row[i] = row[i].replaceAll('灵巧', '敏捷');
    }
    if (row[0] === '架招') {
      const effectIndex = sheetName === '凡俗通用牌库' ? 4 : 4;
      row[effectIndex] = jiazhao;
    }
    if (row[0] === '痛击') {
      const effectIndex = sheetName === '凡俗通用牌库' ? 4 : 4;
      row[effectIndex] = tongji;
    }
  }
  range.values = rows;
}
const result = await workbook.inspect({ kind: 'table', range: '卡牌总表!A1:J12', include: 'values', tableMaxRows: 12, tableMaxCols: 10 });
console.log(result.ndjson);
const preview = await workbook.render({ sheetName: '卡牌总表', range: 'A1:J12', scale: 1.2, format: 'png' });
await fs.writeFile('tools/cards_workbook_synced.png', new Uint8Array(await preview.arrayBuffer()));
await fs.mkdir('outputs', { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
