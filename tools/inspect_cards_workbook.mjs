import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';

const input = await FileBlob.load('docs/plans/战斗与构筑/历史表格/锻躯气血体系卡牌规划.xlsx');
const workbook = await SpreadsheetFile.importXlsx(input);
const result = await workbook.inspect({
  kind: 'workbook,sheet,table',
  maxChars: 16000,
  tableMaxRows: 30,
  tableMaxCols: 12,
  tableMaxCellChars: 140,
});
console.log(result.ndjson);
const sheet = workbook.worksheets.getItemAt(0);
const preview = await workbook.render({ sheetName: sheet.name, autoCrop: 'all', scale: 1, format: 'png' });
import fs from 'node:fs/promises';
await fs.writeFile('tools/cards_workbook_before.png', new Uint8Array(await preview.arrayBuffer()));
