import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load('docs/plans/战斗与构筑/历史表格/锻躯气血体系卡牌规划.xlsx'));
const table = await workbook.inspect({ kind: 'table', range: '卡牌总表!A12:J53', include: 'values', tableMaxRows: 42, tableMaxCols: 10 });
const errors = await workbook.inspect({ kind: 'match', searchTerm: '#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!', options: { useRegex: true, maxResults: 100 }, summary: 'formula errors' });
console.log(table.ndjson);
console.log(errors.ndjson);
