"""Refresh and validate the repository's design-document navigation (stdlib only)."""
from __future__ import annotations

import argparse
import base64
import collections
import hashlib
import html
import json
import posixpath
import re
import zipfile
import zlib
import xml.etree.ElementTree as ET
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / 'docs'
CATALOG = DOCS / '文档索引.json'
ARCHIVE = DOCS / '归档/目录整理_20261109'
REFERENCE_STATUS = DOCS / '引用附件状态.json'
REFERENCE_REPORT = DOCS / '引用与附件核对.md'
BEGIN, END = '<!-- catalog:start -->', '<!-- catalog:end -->'
CATEGORIES = ['总纲与流程', '战斗与构筑', '人物与修炼', '世界与探索', '剧情与宗门', '物品与装备', '百工技艺', '界面与美术']
LINK = re.compile(r'!?\[[^\]\n]*\]\(<?([^\s)>]+)>?(?:\s+"[^"]*")?\)')
KINDS = {'.md': '正文', '.txt': '文字资料', '.drawio': '图稿', '.xlsx': '数值表', '.docx': '介绍文档', '.json': '数据附件', '.html': '交互原型'}


def document_paths():
    """Include design sources, dictionaries and evidence; exclude logs/prompts/media caches."""
    for p in sorted(DOCS.rglob('*')):
        if not p.is_file() or p.name.startswith('.') or p.suffix not in KINDS:
            continue
        rel = p.relative_to(DOCS)
        if rel.parts[0] in {'归档', '废案', '.claude'}:
            continue
        if p.suffix == '.json' and rel.parts[0] not in {'plans', 'prototypes'}:
            continue
        if p.suffix == '.txt' and rel.parts[0] == '美术规范':
            continue
        yield p


def title(p):
    if p.suffix == '.md':
        for line in p.read_text('utf-8-sig').splitlines():
            if line.startswith('# '):
                return line[2:].strip()
    return p.stem


def aliases():
    if not (ARCHIVE / '路径迁移.json').exists():
        return {}
    return {m['new']: m['old'] for m in json.loads((ARCHIVE / '路径迁移.json').read_text('utf-8'))['moves']}


def records():
    old_paths = aliases()
    result = []
    for p in document_paths():
        path = p.relative_to(ROOT).as_posix()
        parts = p.relative_to(DOCS).parts
        group = '/'.join(parts[1:3]) if parts[0] == 'plans' and len(parts) > 3 and parts[1] == '百工技艺' else (parts[1] if parts[0] == 'plans' and len(parts) > 2 else parts[0])
        result.append({'path': path, 'title': title(p), 'category': group, 'kind': KINDS[p.suffix], 'old_path': old_paths.get(path, '')})
    return result


def table_block(readme, items):
    rows = ['| 文档 | 形式 |', '| --- | --- |']
    for rec in items:
        p = ROOT / rec['path']
        label = p.name if p.suffix != '.md' else rec['title']
        relative = posixpath.relpath(rec['path'], readme.parent.relative_to(ROOT).as_posix())
        rows.append(f"| [{label.replace('|', '／')}]({relative}) | {rec['kind']} |")
    return '\n'.join(rows)


def refresh():
    data = records()
    CATALOG.write_text(json.dumps({'schema': 1, 'note': '自动盘点目录；category仅为检索分类，文档是否现行须从各类README及原文确认。归档、日志、提示词、图片和导入缓存不计入策划目录。', 'documents': data}, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    readmes = [DOCS / 'plans' / c / 'README.md' for c in CATEGORIES if c != '百工技艺']
    readmes += [DOCS / 'plans/百工技艺' / c / 'README.md' for c in ['炼丹', '炼器', '制符', '阵法', '种植']]
    readmes += [DOCS / '美术规范/README.md', DOCS / '项目日志/README.md', DOCS / 'prototypes/README.md']
    for readme in readmes:
        text = readme.read_text('utf-8-sig')
        if BEGIN not in text or END not in text:
            raise ValueError(f'缺少目录标记: {readme}')
        prefix = readme.parent.relative_to(ROOT).as_posix() + '/'
        items = [r for r in data if r['path'].startswith(prefix) and r['path'] != readme.relative_to(ROOT).as_posix()]
        if readme.parent.name == '美术规范':
            blocks = []
            # Separate specifications, review records and usable previews without assuming their approval status.
            for label, suffixes in [('规范与审阅记录', {'.md', '.drawio'}), ('可操作样稿', {'.html'})]:
                subset = [r for r in items if Path(r['path']).suffix in suffixes]
                if subset:blocks.append(f'### {label}\n\n' + table_block(readme, subset))
            body = '\n\n'.join(blocks)
        else:
            body = table_block(readme, items)
        text = text[:text.index(BEGIN)] + BEGIN + '\n\n' + body + '\n\n' + text[text.index(END):]
        readme.write_text(text, 'utf-8')
    print(f'目录已刷新：{len(data)} 份正文、图稿、表格、数据与原型。')


def local_target(src, raw):
    raw = unquote(raw).replace('\\', '/')
    if raw.startswith(('#', 'http:', 'https:', 'data:', 'mailto:', 'codex:', 'skill:', 'plugin:', 'app:')):
        return None
    raw = raw.split('#', 1)[0].split('?', 1)[0]
    if raw.startswith('E:/stacklands/'):
        return raw[len('E:/stacklands/'):]
    if raw.startswith('res://'):
        return raw[6:]
    if raw.startswith('docs/'):
        return raw
    if raw.startswith('/') or re.match(r'^[A-Za-z]:', raw):
        return None
    return posixpath.normpath(posixpath.join(posixpath.dirname(src), raw))


def check():
    failures = []
    current = records()
    saved = json.loads(CATALOG.read_text('utf-8'))['documents']
    expected = {r['path']: r for r in current}
    indexed = {r['path']: r for r in saved}
    if len(saved) != len(indexed):failures.append('检索索引存在重复路径')
    for p in sorted(expected.keys() - indexed.keys()):failures.append('未收录文档: ' + p)
    for p in sorted(indexed.keys() - expected.keys()):failures.append('目录残留路径: ' + p)
    for p in expected.keys() & indexed.keys():
        if expected[p] != indexed[p]:failures.append('索引元数据过期: ' + p)
    for rec in current:
        if rec['path'].endswith('.json'):
            try:
                json.loads((ROOT / rec['path']).read_text('utf-8-sig'))
            except (ValueError, UnicodeError) as exc:
                failures.append(f"数据附件无效: {rec['path']} ({exc})")
    for rec in current:
        if not rec['path'].startswith('docs/plans/') or rec['path'].endswith('/README.md'):
            continue
        c = rec['category']
        readme = DOCS / 'plans' / c / 'README.md'
        text = readme.read_text('utf-8') if readme.exists() else ''
        target = posixpath.relpath(rec['path'], readme.parent.relative_to(ROOT).as_posix())
        if f']({target})' not in text:failures.append('分类目录漏项: ' + rec['path'])
    for p in (DOCS / 'plans').iterdir():
        if p.is_file() and p.name != 'README.md':failures.append('策划根目录出现散落文件: ' + p.name)
    try:
        status = json.loads(REFERENCE_STATUS.read_text('utf-8'))['records']
        report = REFERENCE_REPORT.read_text('utf-8-sig')
        ids = set()
        for record in status:
            target = record['original_target']
            anchor = record['id']
            expected_anchor = 'ref-' + hashlib.sha256(target.encode('utf-8')).hexdigest()[:12]
            if anchor in ids or anchor != expected_anchor:
                failures.append(f'附件状态编号重复或不符: {target}')
            ids.add(anchor)
            if f'id="{anchor}"' not in report:
                failures.append(f'附件核对清单缺少记录: {target}')
            p = ROOT / target
            if record['condition'] == 'missing':
                if p.exists():failures.append(f'已登记缺失的原路径重新出现，请核对版本并更新状态: {target}')
            elif record['condition'] == 'empty_directory':
                if not p.is_dir() or any(p.iterdir()):
                    failures.append(f'已登记的空目录状态发生变化，请核对并更新状态: {target}')
            else:
                failures.append(f'附件状态不认识: {target}')
            for ref in record['references']:
                if not (ROOT / ref['source']).is_file():
                    failures.append(f'附件记录的来源文档不存在: {ref["source"]}')
    except (OSError, ValueError, KeyError, TypeError) as exc:
        failures.append(f'无法检查附件状态记录: {exc}')
        status, report = [], ''
    missing, empty, invalid_anchors, checked = set(), set(), set(), 0
    for p in sorted(DOCS.rglob('*.md')):
        src = p.relative_to(ROOT).as_posix()
        for match in LINK.finditer(p.read_text('utf-8-sig')):
            target = local_target(src, match.group(1))
            if target:
                checked += 1
                destination = ROOT / target
                if not destination.exists():missing.add((src, target))
                elif destination.is_dir() and not any(destination.iterdir()):empty.add((src, target))
                if target == REFERENCE_REPORT.relative_to(ROOT).as_posix() and '#ref-' in match.group(1):
                    anchor = match.group(1).split('#', 1)[1]
                    if f'id="{anchor}"' not in report:invalid_anchors.add((src, anchor))
    for src, target in sorted(missing):failures.append(f'失效链接: {src} -> {target}')
    for src, target in sorted(empty):failures.append(f'链接指向空目录: {src} -> {target}')
    for src, anchor in sorted(invalid_anchors):failures.append(f'附件状态锚点不存在: {src} -> {anchor}')
    if failures:
        print('\n'.join(failures))
        print(f'未通过：{len(failures)} 项。')
        return 1
    print(f'检查通过：{len(current)} 份目录完整；{checked} 个本地链接均有效；{len(status)} 项历史原件状态已登记。')
    return 0


def searchable_text(p):
    if p.suffix in {'.docx', '.xlsx'}:
        with zipfile.ZipFile(p) as package:
            names = [n for n in package.namelist() if n == 'word/document.xml' or n == 'xl/sharedStrings.xml' or (n.startswith('xl/worksheets/') and n.endswith('.xml'))]
            return '\n'.join(' '.join(ET.fromstring(package.read(n)).itertext()) for n in names)
    if p.suffix not in {'.md', '.txt', '.drawio', '.json'}:
        return ''
    body = p.read_text('utf-8-sig', errors='replace')
    if p.suffix == '.drawio':
        root = ET.fromstring(body)
        pages = []
        for diagram in root.findall('diagram'):
            content = diagram.find('mxGraphModel')
            if content is None and diagram.text and diagram.text.strip():
                content = ET.fromstring(unquote(zlib.decompress(base64.b64decode(diagram.text.strip()), -15).decode('utf-8')))
            if content is not None:
                pages.append(' '.join(node.get('value', '') for node in content.iter()))
        body += '\n' + re.sub(r'<[^>]+>', ' ', html.unescape('\n'.join(pages)))
    return body


def find(query, evidence=False):
    hits = []
    for rec in records():
        if not evidence and rec['path'].startswith(('docs/美术规范/', 'docs/项目日志/', 'docs/prototypes/')):
            continue
        p = ROOT / rec['path']
        body = searchable_text(p)
        if query.casefold() in (rec['title'] + ' ' + rec['path'] + ' ' + rec['old_path'] + '\n' + body).casefold():
            hits.append(rec)
    for rec in hits:print(f"{rec['category']} | {rec['title']} | {rec['path']}")
    print(f'共 {len(hits)} 项；命中仅表示相关，不表示已经批准或实装。')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--refresh', action='store_true')
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--find', metavar='关键词')
    parser.add_argument('--evidence', action='store_true', help='同时搜索美术验收、原型与项目日志')
    args = parser.parse_args()
    if args.refresh:refresh()
    if args.find:find(args.find, args.evidence)
    if args.check:raise SystemExit(check())
    if not (args.refresh or args.check or args.find):parser.print_help()
