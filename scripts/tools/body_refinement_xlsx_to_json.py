"""锻躯炼度 Excel 配置 → JSON。

用法：
    python scripts/tools/body_refinement_xlsx_to_json.py
    python scripts/tools/body_refinement_xlsx_to_json.py 输入.xlsx 输出.json

Excel 的“锻躯炼度”表中，每一行描述“达到该炼度时获得什么”，
以及“从该炼度升至下一炼所需的进度”。进度是绝对数值，不是百分比。
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import Any

from openpyxl import load_workbook


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_INPUT = ROOT / "resources" / "cultivation" / "锻躯百炼配置.xlsx"
DEFAULT_OUTPUT = ROOT / "resources" / "cultivation" / "body_refinement.json"
PART_SHEET_NAMES = ("皮", "肉", "筋", "骨", "髓", "血", "脏腑", "心")
QI_BLOOD_RULES_SHEET = "气血规则"
MECHANISM_DICTIONARY_SHEET = "机制字典"
REQUIRED_COLUMNS = (
    "部位ID",
    "部位名称",
    "炼度",
    "升至下一炼需进度",
    "战斗奖励",
    "百分比战斗奖励",
    "机制效果",
    "节点效果说明",
)
ID_PATTERN = re.compile(r"^[a-z][a-z0-9_]*$")


def clean(value: Any) -> str:
    return "" if value is None else str(value).strip()


def number(value: Any, field: str, row_number: int) -> int | float:
    if isinstance(value, bool):
        raise ValueError(f"第 {row_number} 行“{field}”不能为布尔值")
    if isinstance(value, (int, float)):
        return value
    text = clean(value)
    try:
        parsed = float(text)
    except ValueError as exc:
        raise ValueError(f"第 {row_number} 行“{field}”必须是数值：{text!r}") from exc
    return int(parsed) if parsed.is_integer() else parsed


def read_attribute_dictionary(workbook: Any) -> dict[str, str]:
    if "属性字典" not in workbook.sheetnames:
        raise ValueError("未找到工作表“属性字典”")
    sheet = workbook["属性字典"]
    headers = [clean(cell.value) for cell in sheet[1]]
    if "显示名称" not in headers or "程序ID" not in headers:
        raise ValueError("“属性字典”缺少列：显示名称、程序ID")
    name_index = headers.index("显示名称")
    id_index = headers.index("程序ID")
    mapping: dict[str, str] = {}
    for row_number, cells in enumerate(sheet.iter_rows(min_row=2, values_only=True), start=2):
        display_name = clean(cells[name_index] if name_index < len(cells) else None)
        internal_id = clean(cells[id_index] if id_index < len(cells) else None)
        if not display_name and not internal_id:
            continue
        if not display_name or not ID_PATTERN.fullmatch(internal_id):
            raise ValueError(f"“属性字典”第 {row_number} 行显示名称或程序ID无效")
        mapping[display_name] = internal_id
    return mapping


def read_qi_blood_rules(workbook: Any) -> dict[str, int | float]:
    if QI_BLOOD_RULES_SHEET not in workbook.sheetnames:
        raise ValueError(f"未找到工作表“{QI_BLOOD_RULES_SHEET}”")
    sheet = workbook[QI_BLOOD_RULES_SHEET]
    headers = [clean(cell.value) for cell in sheet[1]]
    if "规则ID" not in headers or "当前值" not in headers:
        raise ValueError(f"“{QI_BLOOD_RULES_SHEET}”缺少列：规则ID、当前值")
    id_index = headers.index("规则ID")
    value_index = headers.index("当前值")
    rules: dict[str, int | float] = {}
    for row_number, cells in enumerate(sheet.iter_rows(min_row=2, values_only=True), start=2):
        rule_id = clean(cells[id_index] if id_index < len(cells) else None)
        raw_value = cells[value_index] if value_index < len(cells) else None
        if not rule_id and raw_value is None:
            continue
        if not ID_PATTERN.fullmatch(rule_id):
            raise ValueError(f"“{QI_BLOOD_RULES_SHEET}”第 {row_number} 行规则ID无效：{rule_id!r}")
        if rule_id in rules:
            raise ValueError(f"“{QI_BLOOD_RULES_SHEET}”第 {row_number} 行规则ID重复：{rule_id}")
        rules[rule_id] = number(raw_value, "当前值", row_number)
    return rules


def read_mechanism_dictionary(workbook: Any) -> dict[str, str]:
    if MECHANISM_DICTIONARY_SHEET not in workbook.sheetnames:
        raise ValueError(f"未找到工作表“{MECHANISM_DICTIONARY_SHEET}”")
    sheet = workbook[MECHANISM_DICTIONARY_SHEET]
    headers = [clean(cell.value) for cell in sheet[1]]
    if "显示名称" not in headers or "程序ID" not in headers:
        raise ValueError(f"“{MECHANISM_DICTIONARY_SHEET}”缺少列：显示名称、程序ID")
    name_index = headers.index("显示名称")
    id_index = headers.index("程序ID")
    mapping: dict[str, str] = {}
    for row_number, cells in enumerate(sheet.iter_rows(min_row=2, values_only=True), start=2):
        display_name = clean(cells[name_index] if name_index < len(cells) else None)
        internal_id = clean(cells[id_index] if id_index < len(cells) else None)
        if not display_name and not internal_id:
            continue
        if not display_name or not ID_PATTERN.fullmatch(internal_id):
            raise ValueError(f"“{MECHANISM_DICTIONARY_SHEET}”第 {row_number} 行显示名称或程序ID无效")
        mapping[display_name] = internal_id
    return mapping


def parse_modifiers(value: Any, field: str, row_number: int, dictionary: dict[str, str]) -> dict[str, int | float]:
    """解析 key:value;key:value 格式，百分比使用小数，例如 melee_damage:0.05。"""
    text = clean(value)
    if not text:
        return {}

    modifiers: dict[str, int | float] = {}
    for item in text.replace("；", ";").split(";"):
        item = item.strip()
        if not item:
            continue
        if ":" not in item:
            raise ValueError(
                f"第 {row_number} 行“{field}”格式错误：{item!r}；应为 key:value;key:value"
            )
        key, raw_value = (part.strip() for part in item.split(":", 1))
        internal_id = dictionary.get(key, key)
        if not ID_PATTERN.fullmatch(internal_id):
            raise ValueError(
                f"第 {row_number} 行“{field}”属性 ID 无效：{key!r}；请使用小写英文、数字和下划线"
            )
        if internal_id in modifiers:
            raise ValueError(f"第 {row_number} 行“{field}”重复奖励：{key}")
        modifiers[internal_id] = number(raw_value, field, row_number)
    return modifiers


def read_rows(input_path: Path) -> tuple[list[dict[str, Any]], dict[str, int | float]]:
    workbook = load_workbook(input_path, data_only=True)
    dictionary = read_attribute_dictionary(workbook)
    qi_blood_rules = read_qi_blood_rules(workbook)
    mechanism_dictionary = read_mechanism_dictionary(workbook)
    rows: list[dict[str, Any]] = []
    missing_sheets = [name for name in PART_SHEET_NAMES if name not in workbook.sheetnames]
    if missing_sheets:
        raise ValueError(f"缺少部位工作表：{', '.join(missing_sheets)}")

    for sheet_name in PART_SHEET_NAMES:
        sheet = workbook[sheet_name]
        headers = [clean(cell.value) for cell in sheet[1]]
        missing = [column for column in REQUIRED_COLUMNS if column not in headers]
        if missing:
            raise ValueError(f"“{sheet_name}”缺少列：{', '.join(missing)}")
        indexes = {header: headers.index(header) for header in REQUIRED_COLUMNS}
        for row_number, cells in enumerate(sheet.iter_rows(min_row=2, values_only=True), start=2):
            values = {column: cells[indexes[column]] if indexes[column] < len(cells) else None for column in REQUIRED_COLUMNS}
            if not any(clean(value) for value in values.values()):
                continue
            if clean(values["部位ID"]).startswith("#"):
                continue

            part_id = clean(values["部位ID"])
            part_name = clean(values["部位名称"])
            if not ID_PATTERN.fullmatch(part_id):
                raise ValueError(f"“{sheet_name}”第 {row_number} 行部位ID无效：{part_id!r}")
            if not part_name:
                raise ValueError(f"“{sheet_name}”第 {row_number} 行部位名称不能为空")

            level = number(values["炼度"], "炼度", row_number)
            if not isinstance(level, int) or level < 0:
                raise ValueError(f"“{sheet_name}”第 {row_number} 行炼度必须为不小于 0 的整数")

            progress_raw = clean(values["升至下一炼需进度"])
            progress_to_next = None if not progress_raw else number(progress_raw, "升至下一炼需进度", row_number)
            if progress_to_next is not None and progress_to_next <= 0:
                raise ValueError(f"“{sheet_name}”第 {row_number} 行升至下一炼需进度必须大于 0；最高炼度请留空")

            mechanism_name = clean(values["机制效果"])
            mechanism_id = mechanism_dictionary.get(mechanism_name, mechanism_name)
            if mechanism_name and not ID_PATTERN.fullmatch(mechanism_id):
                raise ValueError(f"“{sheet_name}”第 {row_number} 行机制效果未在“{MECHANISM_DICTIONARY_SHEET}”中登记：{mechanism_name!r}")

            rows.append(
                {
                    "part_id": part_id,
                    "part_name": part_name,
                    "level": level,
                    "progress_to_next": progress_to_next,
                    "fixed_attributes": parse_modifiers(values["战斗奖励"], "战斗奖励", row_number, dictionary),
                    "percent_attributes": parse_modifiers(values["百分比战斗奖励"], "百分比战斗奖励", row_number, dictionary),
                    "milestone_effect_id": mechanism_id,
                    "milestone_effect_text": clean(values["节点效果说明"]),
                    "source_row": row_number,
                }
            )
    if not rows:
        raise ValueError("部位工作表中没有可导入的数据行")
    return rows, qi_blood_rules


def build_config(rows: list[dict[str, Any]], qi_blood_rules: dict[str, int | float]) -> dict[str, Any]:
    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    names: dict[str, str] = {}
    for row in rows:
        part_id = row["part_id"]
        if part_id in names and names[part_id] != row["part_name"]:
            raise ValueError(f"部位 {part_id} 使用了不一致的名称：{names[part_id]} / {row['part_name']}")
        names[part_id] = row["part_name"]
        grouped[part_id].append(row)

    parts: dict[str, Any] = {}
    for part_id, levels in grouped.items():
        levels.sort(key=lambda item: item["level"])
        actual_levels = [item["level"] for item in levels]
        expected_levels = list(range(actual_levels[-1] + 1))
        if actual_levels != expected_levels:
            raise ValueError(
                f"部位 {part_id} 的炼度必须从 0 连续填写至 {actual_levels[-1]}；当前为 {actual_levels}"
            )
        if levels[-1]["progress_to_next"] is not None:
            raise ValueError(f"部位 {part_id} 的最高炼度 {levels[-1]['level']} 必须将“升至下一炼需进度”留空")

        exported_levels = []
        for item in levels:
            exported_levels.append(
                {
                    "level": item["level"],
                    "progress_to_next": item["progress_to_next"],
                    "fixed_attributes": item["fixed_attributes"],
                    "percent_attributes": item["percent_attributes"],
                    "milestone_effect_id": item["milestone_effect_id"],
                    "milestone_effect_text": item["milestone_effect_text"],
                }
            )
        parts[part_id] = {"name": names[part_id], "levels": exported_levels}

    return {"schema_version": 1, "parts": parts, "qi_blood_rules": qi_blood_rules}


def main() -> int:
    parser = argparse.ArgumentParser(description="将锻躯炼度 Excel 配置转换为 JSON")
    parser.add_argument("input", nargs="?", type=Path, default=DEFAULT_INPUT, help="Excel 输入文件")
    parser.add_argument("output", nargs="?", type=Path, default=DEFAULT_OUTPUT, help="JSON 输出文件")
    args = parser.parse_args()

    try:
        rows, qi_blood_rules = read_rows(args.input)
        config = build_config(rows, qi_blood_rules)
    except (FileNotFoundError, ValueError) as exc:
        print(f"[失败] {exc}", file=sys.stderr)
        return 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(config, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"[完成] 导出 {len(config['parts'])} 个部位至 {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
