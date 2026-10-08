import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputPath = process.argv[2] ?? "resources/cultivation/锻躯百炼配置.xlsx";
await fs.mkdir(new URL("../../resources/cultivation/", import.meta.url), { recursive: true });

const workbook = Workbook.create();
const guide = workbook.worksheets.add("填写说明");
const attributes = workbook.worksheets.add("属性字典");
const mechanisms = workbook.worksheets.add("机制字典");
const qiBloodRules = workbook.worksheets.add("气血规则");

guide.showGridLines = false;
guide.getRange("A1:H1").merge();
guide.getRange("A1").values = [["锻躯百炼配置表"]];
guide.getRange("A3:H10").values = [
  ["填写规则", "", "", "", "", "", "", ""],
  ["1", "每行表示“达到该炼度后”获得的属性；炼度 0 是初始状态，不写获得属性。", "", "", "", "", "", ""],
  ["2", "“升至下一炼需进度”填写绝对进度值，不是百分比；最高炼度必须留空。", "", "", "", "", "", ""],
  ["3", "战斗奖励使用中文名：如“物理防御:1;最大生命:3”。百分比战斗奖励用小数：如“闪避:0.05”（即 +5%）。", "", "", "", "", "", ""],
  ["4", "战斗奖励参照“属性字典”；机制效果参照“机制字典”。填写时只需使用中文名。", "", "", "", "", "", ""],
  ["5", "八个部位各有独立工作表，炼度必须从 0 连续填写，不能跳级；进度可因炼度、部位而不同。", "", "", "", "", "", ""],
  ["6", "气血与血炉的公共数值统一填写在“气血规则”，不写在部位奖励里。血炉由有效受击积攒；每满 2 点提供 1% 物理伤害加成。", "", "", "", "", "", ""],
  ["提示", "当前字典只列已有战斗字段；后续新增战斗字段后，再补入字典并接入程序。", "", "", "", "", "", ""],
];
guide.getRange("A1:H1").format = { fill: "#7C2D12", font: { bold: true, color: "#FFFFFF", size: 16 }, horizontalAlignment: "center", verticalAlignment: "center" };
guide.getRange("A3:H3").format = { fill: "#FDE68A", font: { bold: true } };
guide.getRange("A4:A10").format = { font: { bold: true, color: "#7C2D12" } };
guide.getRange("A3:H10").format.wrapText = true;
guide.getRange("A:A").format.columnWidth = 12;
guide.getRange("B:B").format.columnWidth = 95;
guide.getRange("A1:H1").format.rowHeight = 28;
guide.getRange("A3:H10").format.rowHeight = 30;

const headers = ["部位ID", "部位名称", "炼度", "升至下一炼需进度", "战斗奖励", "百分比战斗奖励", "机制效果", "机制效果说明", "备注"];
const parts = [
  ["skin", "皮"], ["muscle", "肉"], ["sinew", "筋"], ["bone", "骨"],
  ["marrow", "髓"], ["blood", "血"], ["viscera", "脏腑"], ["heart", "心"],
];
const sampleByPart = {
  skin: { fixed: "物理防御:1", five: "皮炼五重", fiveText: "示例：外护强化（请按设计改写）", ten: "皮炼十重", tenText: "示例：皮膜初成（请按设计改写）" },
  muscle: { fixed: "最大生命:3", five: "肉炼五重", fiveText: "示例：发力强化（请按设计改写）", ten: "肉炼十重", tenText: "示例：肌肉初成（请按设计改写）" },
  sinew: { fixed: "闪避:1", five: "筋炼五重", fiveText: "示例：筋力强化（请按设计改写）", ten: "筋炼十重", tenText: "示例：筋络初成（请按设计改写）" },
  bone: { fixed: "暴抗:1", five: "骨炼五重", fiveText: "示例：骨架强化（请按设计改写）", ten: "骨炼十重", tenText: "示例：骨架初成（请按设计改写）" },
  marrow: { fixed: "最大生命:2", five: "髓炼五重", fiveText: "示例：生机强化（请按设计改写）", ten: "髓炼十重", tenText: "示例：髓海初成（请按设计改写）" },
  blood: { fixed: null, five: "血炼五重", fiveText: "示例：气血机制由“气血规则”统一配置（请按设计改写）", ten: "血炼十重", tenText: "示例：血气初成（请按设计改写）" },
  viscera: { fixed: "最大生命:2", five: "脏腑炼五重", fiveText: "示例：内壮强化（请按设计改写）", ten: "脏腑炼十重", tenText: "示例：脏腑初成（请按设计改写）" },
  heart: { fixed: "最大精力:2", five: "心炼五重", fiveText: "示例：循环强化（请按设计改写）", ten: "心炼十重", tenText: "示例：心血初成（请按设计改写）" },
};
const progressToNext = [20, 24, 28, 32, 40, 48, 58, 70, 84, 100, null];
function populatePartSheet(partId, partName) {
  const data = workbook.worksheets.add(partName);
  const rows = [];
  for (let level = 0; level <= 10; level += 1) {
    const sample = sampleByPart[partId];
    rows.push([
      partId,
      partName,
      level,
      progressToNext[level],
      level === 0 ? null : sample.fixed,
      null,
      level === 5 ? sample.five : level === 10 ? sample.ten : null,
      level === 5 ? sample.fiveText : level === 10 ? sample.tenText : null,
      "示例数据：可直接覆盖",
    ]);
  }
  data.getRange("A1:I1").values = [headers];
  data.getRange(`A2:I${rows.length + 1}`).values = rows;
  data.getRange("A1:I1").format = { fill: "#7C2D12", font: { bold: true, color: "#FFFFFF" }, horizontalAlignment: "center", verticalAlignment: "center", wrapText: true };
  data.getRange(`A2:I${rows.length + 1}`).format = { verticalAlignment: "center" };
  data.getRange(`D2:I${rows.length + 1}`).format.fill = "#FEF3C7";
  data.getRange(`A1:I${rows.length + 1}`).format.borders = { preset: "all", style: "thin", color: "#E7D3B0" };
  data.getRange(`C2:C${rows.length + 1}`).format.numberFormat = "0";
  data.getRange(`D2:D${rows.length + 1}`).format.numberFormat = "0.##";
  data.getRange(`A2:B${rows.length + 1}`).format.fill = "#FFF7ED";
  data.getRange("A:A").format.columnWidth = 16;
  data.getRange("B:B").format.columnWidth = 12;
  data.getRange("C:C").format.columnWidth = 10;
  data.getRange("D:D").format.columnWidth = 22;
  data.getRange("E:F").format.columnWidth = 32;
  data.getRange("G:G").format.columnWidth = 24;
  data.getRange("H:H").format.columnWidth = 42;
  data.getRange("I:I").format.columnWidth = 24;
  data.getRange("A1:I1").format.rowHeight = 34;
  data.freezePanes.freezeRows(1);
  data.freezePanes.freezeColumns(2);
  data.dataValidations.add({ range: `C2:C${rows.length + 1}`, rule: { type: "whole", operator: "between", formula1: 0, formula2: 100 } });
}

for (const [partId, partName] of parts) {
  populatePartSheet(partId, partName);
}

attributes.getRange("A1:D1").values = [["显示名称", "程序ID", "填写栏", "用途说明"]];
attributes.getRange("A2:D10").values = [
  ["最大生命", "max_health", "战斗奖励", "战斗中的生命上限"],
  ["最大精力", "max_stamina", "战斗奖励", "战斗中的精力上限"],
  ["物理防御", "physical_defense", "战斗奖励／百分比战斗奖励", "物理伤害结算时使用"],
  ["精准", "precision", "战斗奖励／百分比战斗奖励", "命中判定时使用"],
  ["闪避", "dodge", "战斗奖励／百分比战斗奖励", "闪避判定时使用"],
  ["暴击", "critical", "战斗奖励／百分比战斗奖励", "暴击判定时使用"],
  ["暴抗", "crit_resist", "战斗奖励／百分比战斗奖励", "抵抗暴击判定时使用"],
  ["暴击伤害", "crit_damage_mult", "战斗奖励／百分比战斗奖励", "暴击伤害倍率"],
  ["行动速度", "initiative_speed", "战斗奖励／百分比战斗奖励", "战斗行动顺序使用"],
];
attributes.getRange("A1:C1").format = { fill: "#7C2D12", font: { bold: true, color: "#FFFFFF" } };
attributes.getRange("A1:D1").format = { fill: "#7C2D12", font: { bold: true, color: "#FFFFFF" } };
attributes.getRange("A1:D10").format.borders = { preset: "all", style: "thin", color: "#E7D3B0" };
attributes.getRange("A:A").format.columnWidth = 32;
attributes.getRange("B:B").format.columnWidth = 26;
attributes.getRange("C:C").format.columnWidth = 20;
attributes.getRange("D:D").format.columnWidth = 58;
attributes.freezePanes.freezeRows(1);

const mechanismRows = parts.flatMap(([partId, partName]) => [
  [`${partName}炼五重`, `${partId}_refined_5`, "示例节点；可改写中文名和说明"],
  [`${partName}炼十重`, `${partId}_refined_10`, "示例节点；可改写中文名和说明"],
]);
mechanisms.getRange("A1:C1").values = [["显示名称", "程序ID", "备注"]];
mechanisms.getRange(`A2:C${mechanismRows.length + 1}`).values = mechanismRows;
mechanisms.getRange("A1:C1").format = { fill: "#7C2D12", font: { bold: true, color: "#FFFFFF" } };
mechanisms.getRange(`A1:C${mechanismRows.length + 1}`).format.borders = { preset: "all", style: "thin", color: "#E7D3B0" };
mechanisms.getRange("A:A").format.columnWidth = 24;
mechanisms.getRange("B:B").format.columnWidth = 28;
mechanisms.getRange("C:C").format.columnWidth = 42;
mechanisms.freezePanes.freezeRows(1);

qiBloodRules.getRange("A1:E1").values = [["规则ID", "显示名称", "当前值", "单位", "说明"]];
qiBloodRules.getRange("A2:E6").values = [
  ["qi_blood_base_cap", "基础气血上限", 3, "层", "单场战斗中气血可达到的基础层数上限"],
  ["qi_blood_drop_heal_ratio", "气血回落回复", 0.03, "最大生命比例", "每回落 1 层气血，回复最大生命的比例"],
  ["blood_furnace_gain_per_hit", "每次受击血炉值", 1, "点／次", "每次造成有效生命伤害的受击，积攒的血炉值"],
  ["blood_furnace_physical_damage_per_two_value", "每两点血炉物理增伤", 0.01, "物理伤害比例／2点", "每满 2 点血炉值，物理伤害提高 1%；不足 2 点的余值不计入加成，血炉值不自动消耗"],
  ["blood_furnace_value_cap", "血炉基础上限", 100, "点", "默认最多积攒至 100 点；秘籍、丹药或其他效果可提高当前上限"],
];
qiBloodRules.getRange("A1:E1").format = { fill: "#7C2D12", font: { bold: true, color: "#FFFFFF" }, horizontalAlignment: "center" };
qiBloodRules.getRange("A1:E6").format.borders = { preset: "all", style: "thin", color: "#E7D3B0" };
qiBloodRules.getRange("C2:C6").format.fill = "#FEF3C7";
qiBloodRules.getRange("C2").format.numberFormat = "0";
qiBloodRules.getRange("C3").format.numberFormat = "0.0%";
qiBloodRules.getRange("C4").format.numberFormat = "0";
qiBloodRules.getRange("C5").format.numberFormat = "0.0%";
qiBloodRules.getRange("C6").format.numberFormat = "0";
qiBloodRules.getRange("A:A").format.columnWidth = 32;
qiBloodRules.getRange("B:B").format.columnWidth = 24;
qiBloodRules.getRange("C:C").format.columnWidth = 16;
qiBloodRules.getRange("D:D").format.columnWidth = 20;
qiBloodRules.getRange("E:E").format.columnWidth = 60;
qiBloodRules.freezePanes.freezeRows(1);

const file = await SpreadsheetFile.exportXlsx(workbook);
await file.save(outputPath);

const preview = await workbook.render({
  sheetName: "皮",
  range: "A1:I12",
  scale: 1.2,
  format: "png",
});
await fs.mkdir("tmp/body_refinement_excel", { recursive: true });
await fs.writeFile("tmp/body_refinement_excel/皮部位预览.png", new Uint8Array(await preview.arrayBuffer()));
