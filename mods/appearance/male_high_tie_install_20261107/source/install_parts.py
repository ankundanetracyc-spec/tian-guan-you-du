"""Append reviewed high-tie male layers; default is a read-only preflight.

selection.json: {"schema":"male-style-expansion/v1", "approved_portrait":"res://...",
 "additions":[{"part":"eyes", "source":"res://...", "name":"...",
 "index":5, "style_id":4}], "deduplication":[]}. index/style_id are optional.
Existing styles0/3 and PNG01/04 remain; only retired IDs1/2 resolve to style0.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / "mods/appearance/ink_three_variants_20261004"
INSTALLATION = PACK / "installation"
CONFIG = ACTIVE / "appearance_parts.json"
MANIFEST = ACTIVE / "portrait_manifest.json"
REPORT = INSTALLATION / "install_report.json"
APPROVED = PACK / "source/approved_male.png"
APPROVED_SHA = "fe7b9280cbb19019e76ffd6d451679e5e3ff3dc2b685b9020471b96451312f18"
META_KEY = "male_high_tie_expansion_20261107"
SLOTS = ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
CATALOG = ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup"]
ALLOWED = {"hair_front": "hair_front", "hair_back": "hair_back", "eyebrows": "brow", "eyes": "eye", "nose": "nose", "mouth": "mouth"}


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def encoded(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def resource(path: Path) -> str:
    return "res://" + path.resolve().relative_to(ROOT).as_posix()


def resolve(value: str) -> Path:
    return (ROOT / value[6:]).resolve() if value.startswith("res://") else (ROOT / value).resolve()


def png_info(data: bytes, label: str) -> tuple[dict, bytes]:
    with Image.open(io.BytesIO(data)) as image:
        image.verify()
    with Image.open(io.BytesIO(data)) as image:
        if image.mode != "RGBA" or image.size != (1024, 1536):
            raise ValueError("Expected full-canvas 1024x1536 RGBA: " + label)
        alpha = image.getchannel("A")
        if alpha.getextrema()[0] != 0 or alpha.getbbox() is None:
            raise ValueError("Expected transparent, nonempty reviewed layer: " + label)
        return {"size": list(image.size), "alpha_bbox": list(alpha.getbbox())}, alpha.tobytes()


def inventory() -> dict[str, str]:
    return {resource(path): digest(path.read_bytes()) for gender in ("male", "female")
            for path in sorted((ACTIVE / gender).rglob("*.png"))}


def verify_inventory(expected: dict[str, str]) -> None:
    if inventory() != expected:
        raise RuntimeError("Active PNG inventory/hashes changed; no overwrite is permitted")


def slot_counts(config: dict) -> dict:
    result = {}
    for gender in ("male", "female"):
        counts = []
        for i, _slot in enumerate(SLOTS):
            part = "beard" if gender == "male" and i == 8 else CATALOG[i]
            found = [entry for entry in config["components"] if entry["gender"] == gender and entry["part"] == part]
            if len(found) != 1:
                raise ValueError("Expected one component: " + gender + "/" + part)
            counts.append(int(found[0]["variant_count"]))
        result[gender] = counts
    return result


def save_atomic(path: Path, data: bytes, expected: bytes) -> str:
    """Verified replacement, with Windows reader-compatible write and rollback."""
    if path.read_bytes() != expected:
        raise RuntimeError("Concurrent change detected: " + str(path))
    descriptor, temporary_name = tempfile.mkstemp(prefix=path.name + ".male_high_tie.", suffix=".tmp", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as output:
            if output.write(data) != len(data):
                raise OSError("Incomplete staged write")
            output.flush()
            os.fsync(output.fileno())
        if path.read_bytes() != expected:
            raise RuntimeError("Concurrent change detected before replacement: " + str(path))
        try:
            temporary.replace(path)
            if path.read_bytes() != data:
                raise OSError("Atomic replacement readback mismatch: " + str(path))
            return "atomic_replace"
        except PermissionError:
            with path.open("r+b") as output:
                if output.read() != expected:
                    raise RuntimeError("Concurrent change detected before Windows fallback: " + str(path))
                try:
                    output.seek(0)
                    if output.write(data) != len(data):
                        raise OSError("Incomplete in-place write")
                    output.truncate(len(data))
                    output.flush()
                    os.fsync(output.fileno())
                    output.seek(0)
                    if output.read() != data:
                        raise OSError("In-place write readback mismatch")
                except Exception as write_error:
                    try:
                        output.seek(0)
                        if output.write(expected) != len(expected):
                            raise OSError("Incomplete in-place rollback")
                        output.truncate(len(expected))
                        output.flush()
                        os.fsync(output.fileno())
                        output.seek(0)
                        if output.read() != expected:
                            raise OSError("In-place rollback readback mismatch")
                    except Exception as rollback_error:
                        raise RuntimeError(f"Write/rollback both failed: {path}; write={write_error}; rollback={rollback_error}") from write_error
                    raise
            return "verified_in_place"
    finally:
        if temporary.exists():
            try:
                temporary.unlink()
            except PermissionError:
                pass


def plan(selection_path: Path) -> dict:
    selection_bytes = selection_path.read_bytes()
    selection = json.loads(selection_bytes.decode("utf-8-sig"))
    if selection.get("schema") != "male-style-expansion/v1":
        raise ValueError("Unsupported/missing selection schema")
    approved = resolve(selection.get("approved_portrait", resource(APPROVED)))
    if not approved.is_relative_to(ROOT) or approved.is_relative_to(ACTIVE):
        raise ValueError("Approved source must be a staged project resource outside the active package")
    approved_bytes = approved.read_bytes()
    if digest(approved_bytes) != APPROVED_SHA:
        raise ValueError("Approved portrait is not the user-approved high-tie source")
    chosen = selection.get("additions", [])
    if not chosen or len(chosen) > 6 or len({item.get("part") for item in chosen}) != len(chosen):
        raise ValueError("Require one to six distinct reviewed male additions")
    config_before, manifest_before = CONFIG.read_bytes(), MANIFEST.read_bytes()
    config = json.loads(config_before.decode("utf-8-sig"))
    updated = copy.deepcopy(config)
    baseline = inventory()
    records, new_bytes, new_aliases = [], {}, {}
    for item in chosen:
        part = item["part"]
        if part not in ALLOWED:
            raise ValueError("Only reviewed male front/back hair and four facial slots may be appended")
        name = str(item.get("name", "")).strip()
        if not name:
            raise ValueError("Every addition needs a display name")
        entries = [entry for entry in updated["components"] if entry["gender"] == "male" and entry["part"] == ALLOWED[part]]
        if len(entries) != 1:
            raise ValueError("Expected one male component for " + part)
        entry = entries[0]
        count = int(entry["variant_count"])
        start = int(entry.get("index_start", config.get("index_start", 0)))
        indices = list(entry.get("variant_indices", range(start, start + count)))
        names = list(entry.get("variant_names", []))
        styles = list(entry.get("variant_style_ids", range(count)))
        if any(len(values) != count for values in (indices, names, styles)) or len(set(styles)) != count:
            raise ValueError("Existing variant arrays disagree for " + part)
        if 0 not in styles or any(retired in styles for retired in (1, 2)):
            raise ValueError("Expected retained style0 and retired styles1/2 for " + part)
        aliases = dict(entry.get("variant_aliases", {}))
        for retired in (1, 2):
            if aliases.get(str(retired)) != 0:
                raise ValueError("Required old numeric alias is missing/different: " + part)
        added_aliases = {}
        for retired in (1, 2):
            alias = f"{entry['id']}_{retired:02d}"
            if alias in aliases and aliases[alias] != 0:
                raise ValueError("Old full component ID has incompatible alias: " + alias)
            if alias not in aliases:
                added_aliases[alias] = 0
            aliases[alias] = 0
        # Style3/_03 is a live loose-style choice, never a retired alias.
        for existing_style in styles:
            full_id = f"{entry['id']}_{int(existing_style):02d}"
            if full_id in aliases and int(aliases[full_id]) != int(existing_style):
                raise ValueError("An active full component ID has an incompatible alias: " + full_id)
        used_styles = {int(value) for value in styles}
        for alias in aliases:
            if str(alias).lstrip("-").isdigit():
                used_styles.add(int(alias))
            prefix = str(entry["id"]) + "_"
            if str(alias).startswith(prefix) and str(alias)[len(prefix):].isdigit():
                used_styles.add(int(str(alias)[len(prefix):]))
        style = int(item.get("style_id", max(used_styles | {3}) + 1))
        if style != item.get("style_id", style) or style < 4 or style in used_styles:
            raise ValueError("New saved style must avoid all existing/retired numeric and full IDs: " + part)
        physical = [int(path.stem) for path in (ACTIVE / "male" / part).glob("*.png") if path.stem.isdigit()]
        index = int(item.get("index", max(physical + indices + [4]) + 1))
        if index != item.get("index", index) or index < 5 or index in indices:
            raise ValueError("New physical index must be unused and at least05: " + part)
        padding = int(entry.get("index_padding", config.get("index_padding", 0)))
        relative = entry["file"].replace("{index}", str(index).zfill(padding))
        target, source = (ACTIVE / relative).resolve(), resolve(item["source"])
        if not target.is_relative_to(ACTIVE / "male" / part) or target.exists():
            raise ValueError("Refusing to overwrite/escape existing PNG: " + relative)
        if not source.is_relative_to(ROOT) or source.is_relative_to(ACTIVE):
            raise ValueError("Source must be a staged project resource outside active package")
        after = source.read_bytes()
        info, _alpha = png_info(after, part)
        fingerprint = digest(after)
        if any(digest(path.read_bytes()) == fingerprint for path in (ACTIVE / "male" / part).glob("*.png")):
            raise ValueError("Byte-identical existing option must be deduplicated: " + part)
        entry["variant_count"] = count + 1
        entry["variant_indices"], entry["variant_names"], entry["variant_style_ids"] = indices + [index], names + [name], styles + [style]
        entry["variant_aliases"] = aliases
        new_aliases[entry["id"]] = added_aliases
        records.append({"target": relative, "part": part, "catalog_part": ALLOWED[part], "name": name,
                        "component_id": entry["id"], "full_component_id": f"{entry['id']}_{style:02d}",
                        "index": index, "style_id": style, "ui_index": count, "new_aliases": added_aliases,
                        "resource_path": resource(target), "source_path": resource(source),
                        "candidate_path": resource(source), "sha256": fingerprint, **info})
        new_bytes[relative] = after
    snapshot = {"schema": "male-style-expansion-snapshot/v1", "selection_sha256": digest(selection_bytes),
                "approved_portrait": resource(approved), "approved_portrait_sha256": APPROVED_SHA,
                "sources": {record["source_path"]: record["sha256"] for record in records},
                "config_sha256": digest(config_before), "manifest_before_sha256": digest(manifest_before),
                "baseline_sha256": baseline}
    return {"selection_bytes": selection_bytes, "config_before": config_before,
            "manifest_before": manifest_before, "config": config, "records": records,
            "new_bytes": new_bytes, "baseline": baseline, "updated": updated,
            "new_aliases": new_aliases, "approved": approved, "snapshot": snapshot,
            "before_slot_counts": slot_counts(config), "slot_counts": slot_counts(updated),
            "deduplication": selection.get("deduplication", [])}


def backup_members(state: dict) -> dict[str, bytes]:
    members = {CONFIG.relative_to(ROOT).as_posix(): state["config_before"],
               MANIFEST.relative_to(ROOT).as_posix(): state["manifest_before"],
               "_installation/selection.json": state["selection_bytes"],
               "_installation/batch_snapshot.json": encoded(state["snapshot"])}
    for file, expected in state["baseline"].items():
        path = resolve(file)
        data = path.read_bytes()
        if digest(data) != expected:
            raise RuntimeError("PNG changed while preparing backup: " + file)
        members[path.relative_to(ROOT).as_posix()] = data
    return members


def verify_retry_backup(state: dict, members: dict[str, bytes]) -> None:
    expected_files = {"appearance_parts.before.json": state["config_before"],
                      "portrait_manifest.before.json": state["manifest_before"],
                      "selection.json": state["selection_bytes"],
                      "baseline_sha256.json": encoded(state["baseline"]),
                      "batch_snapshot.json": encoded(state["snapshot"])}
    for name, expected in expected_files.items():
        path = INSTALLATION / name
        if not path.is_file() or path.read_bytes() != expected:
            raise ValueError("Existing installation batch differs or is incomplete: " + name + "; archive the old batch before staging changed provenance")
    verify_inventory(state["baseline"])
    if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
        raise RuntimeError("Retry requires both exact original configuration files")
    with zipfile.ZipFile(INSTALLATION / "before_install.zip") as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or set(names) != set(members) or archive.testzip() is not None:
            raise ValueError("Existing backup ZIP inventory/integrity differs")
        for name, expected in members.items():
            if archive.read(name) != expected:
                raise ValueError("Existing backup ZIP bytes differ: " + name)


def prepare_backup(state: dict) -> None:
    members = backup_members(state)
    if INSTALLATION.exists():
        verify_retry_backup(state, members)
        return
    INSTALLATION.mkdir()
    with zipfile.ZipFile(INSTALLATION / "before_install.zip", "x", zipfile.ZIP_DEFLATED) as archive:
        for name, data in members.items():
            archive.writestr(name, data)
    for name, data in {"appearance_parts.before.json": state["config_before"],
                       "portrait_manifest.before.json": state["manifest_before"],
                       "selection.json": state["selection_bytes"],
                       "baseline_sha256.json": encoded(state["baseline"]),
                       "batch_snapshot.json": encoded(state["snapshot"])}.items():
        with (INSTALLATION / name).open("xb") as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
    verify_retry_backup(state, members)


def verify_completed(selection_path: Path) -> dict:
    report = read_json(REPORT)
    if report.get("schema") != "male-style-expansion/v1":
        raise ValueError("Unexpected completed expansion report")
    if (INSTALLATION / "selection.json").read_bytes() != selection_path.read_bytes():
        raise ValueError("Completed batch has different selection provenance")
    snapshot = read_json(INSTALLATION / "batch_snapshot.json")
    if snapshot["selection_sha256"] != digest(selection_path.read_bytes()):
        raise ValueError("Selection differs from completed frozen batch")
    if digest(resolve(snapshot["approved_portrait"]).read_bytes()) != APPROVED_SHA or snapshot["approved_portrait_sha256"] != APPROVED_SHA:
        raise ValueError("Approved original changed after installation")
    if digest(CONFIG.read_bytes()) != report["after_config_sha256"] or read_json(CONFIG) != report["installed_config"]:
        raise ValueError("Active appearance configuration changed after installation")
    if digest(MANIFEST.read_bytes()) != report["after_manifest_sha256"]:
        raise ValueError("Active manifest changed after installation")
    expected = dict(report["baseline_sha256"])
    for record in report["records"]:
        for key in ("resource_path", "source_path"):
            if digest(resolve(record[key]).read_bytes()) != record["sha256"]:
                raise ValueError("Installed/staged reviewed layer differs: " + record[key])
        if snapshot["sources"].get(record["source_path"]) != record["sha256"]:
            raise ValueError("Completed source provenance differs from frozen batch")
        expected[record["resource_path"]] = record["sha256"]
    verify_inventory(expected)
    return report


def install(selection_path: Path, state: dict) -> dict:
    # Exclusive lock covers backup, revalidation, replacement and report creation.
    lock = PACK / ".male_high_tie_install.lock"
    with lock.open("x", encoding="utf-8") as output:
        output.write(str(os.getpid()))
    try:
        if REPORT.exists():
            return verify_completed(selection_path)
        prepare_backup(state)
        verify_inventory(state["baseline"])
        if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
            raise RuntimeError("Configuration changed after preflight")
        if selection_path.read_bytes() != state["selection_bytes"] or digest(state["approved"].read_bytes()) != APPROVED_SHA:
            raise RuntimeError("Selection/approved source changed after preflight")
        for record in state["records"]:
            if resolve(record["source_path"]).read_bytes() != state["new_bytes"][record["target"]]:
                raise RuntimeError("Reviewed source changed after preflight: " + record["source_path"])
        manifest = json.loads(state["manifest_before"].decode("utf-8-sig"))
        if META_KEY in manifest:
            raise ValueError("Manifest already has a different high-tie expansion batch")
        meta = {"approved_portrait": resource(state["approved"]), "approved_portrait_sha256": APPROVED_SHA,
                "records": state["records"], "append_only": True, "new_aliases": state["new_aliases"],
                "selection": resource(selection_path), "install_report": resource(REPORT),
                "pixel_exact_extraction": False, "art_review": "pending_user_review"}
        manifest[META_KEY] = meta
        manifest.setdefault("variants_per_slot", {}).update(state["slot_counts"])
        active_count = sum(int(entry["variant_count"]) for entry in state["updated"]["components"])
        manifest["active_variant_count"] = active_count
        manifest["total_pngs"] = len(state["baseline"]) + len(state["records"])
        config_after = encoded(state["updated"])
        manifest_after = encoded(manifest)
        writes, created = {}, []
        report_created = False
        try:
            for record in state["records"]:
                relative = record["target"]
                target = ACTIVE / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open("xb") as output:
                    created.append(target)
                    if output.write(state["new_bytes"][relative]) != len(state["new_bytes"][relative]):
                        raise OSError("Incomplete new PNG write: " + relative)
                    output.flush()
                    os.fsync(output.fileno())
                if digest(target.read_bytes()) != record["sha256"]:
                    raise OSError("New PNG readback mismatch: " + relative)
            writes["appearance_parts.json"] = save_atomic(CONFIG, config_after, state["config_before"])
            writes["portrait_manifest.json"] = save_atomic(MANIFEST, manifest_after, state["manifest_before"])
            expected = dict(state["baseline"])
            expected.update({record["resource_path"]: record["sha256"] for record in state["records"]})
            verify_inventory(expected)
            if CONFIG.read_bytes() != config_after:
                raise RuntimeError("Appearance configuration changed concurrently")
            if MANIFEST.read_bytes() != manifest_after:
                raise RuntimeError("Manifest changed after installation")
            report = {"schema": "male-style-expansion/v1", "installed": True, "append_only": True,
                      "approved_portrait": resource(state["approved"]), "approved_portrait_sha256": APPROVED_SHA,
                      "records": state["records"], "slot_counts": state["slot_counts"],
                      "before_slot_counts": state["before_slot_counts"], "new_aliases": state["new_aliases"],
                      "baseline_components": state["config"]["components"],
                      "baseline_config": state["config"], "installed_config": state["updated"],
                      "baseline_sha256": state["baseline"], "preserved_sha256": state["baseline"],
                      "before_config_sha256": digest(state["config_before"]),
                      "after_config_sha256": digest(config_after),
                      "manifest_before_sha256": digest(state["manifest_before"]),
                      "after_manifest_sha256": digest(manifest_after),
                      "head_and_clothes_unchanged": True, "png_count": len(expected), "active_variant_count": active_count,
                      "female_pngs_preserved": sum("/female/" in file for file in state["baseline"]),
                      "deduplication": state["deduplication"],
                      "backup": resource(INSTALLATION / "before_install.zip"),
                      "selection_sha256": digest(state["selection_bytes"]), "write_methods": writes,
                      "pixel_exact_extraction": False, "art_review": "pending_user_review"}
            with REPORT.open("x", encoding="utf-8") as output:
                report_created = True
                json.dump(report, output, ensure_ascii=False, indent=2)
                output.write("\n")
                output.flush()
                os.fsync(output.fileno())
            return report
        except Exception as install_error:
            rollback_errors = []
            restore = [(CONFIG, state["config_before"], config_after), (MANIFEST, state["manifest_before"], manifest_after)]
            for path, before, after in reversed(restore):
                try:
                    current = path.read_bytes()
                    if current == before:
                        continue
                    if current != after:
                        raise RuntimeError("Concurrent/partial bytes preserved; cannot overwrite " + str(path))
                    save_atomic(path, before, after)
                except Exception as error:
                    rollback_errors.append(str(error))
            for target in reversed(created):
                try:
                    if not target.exists():
                        continue
                    relative = target.relative_to(ACTIVE).as_posix()
                    if target.read_bytes() != state["new_bytes"][relative]:
                        raise RuntimeError("Concurrent/partial new PNG preserved; cannot remove " + str(target))
                    target.unlink()
                except Exception as error:
                    rollback_errors.append(str(error))
            try:
                verify_inventory(state["baseline"])
                if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
                    raise RuntimeError("Configuration baseline differs after rollback")
            except Exception as error:
                rollback_errors.append(str(error))
            if report_created and REPORT.exists():
                # A failed report write is ours only if its bytes are a prefix of the intended report.
                intended = encoded(report) if "report" in locals() else b""
                if intended.startswith(REPORT.read_bytes()):
                    REPORT.unlink()
            if rollback_errors:
                raise RuntimeError(f"Installation failed: {install_error}; rollback issues: {rollback_errors}") from install_error
            raise RuntimeError(f"Installation failed and fully rolled back: {install_error}") from install_error
    finally:
        lock.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selection", type=Path, default=PACK / "source/selection.json")
    parser.add_argument("--install", action="store_true", help="Append reviewed male layers after preflight")
    args = parser.parse_args()
    selection_path = args.selection.resolve()
    if REPORT.exists():
        report = verify_completed(selection_path)
        print(json.dumps({"status": "already_installed_verified", "report": resource(REPORT), "additions": len(report["records"])}, ensure_ascii=False))
        return 0
    state = plan(selection_path)
    if not args.install:
        if INSTALLATION.exists():
            verify_retry_backup(state, backup_members(state))
        print(json.dumps({"status": "preflight_passed_read_only", "root": str(ROOT),
                          "additions": state["records"], "slot_counts": state["slot_counts"],
                          "baseline_pngs": len(state["baseline"]), "new_aliases": state["new_aliases"],
                          "head_and_clothes_unchanged": True}, ensure_ascii=False, indent=2))
        return 0
    report = install(selection_path, state)
    print(json.dumps({"status": "installed", "report": resource(REPORT), "additions": len(report["records"])}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"MALE_HIGH_TIE_INSTALL_ERROR: {error}", file=sys.stderr)
        sys.exit(1)
