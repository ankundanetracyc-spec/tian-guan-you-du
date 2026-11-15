"""Append reviewed female facial PNGs without replacing existing art or saved styles.

Default is a read-only preflight. Pass --install after the selected PNGs are final.
selection.json schema: {"schema": "female-feature-expansion/v1", "additions":
[{"part": "eyebrows", "name": "...", "source": "res://...", "index": 4,
  "style_id": 3}], "deduplication": [...]}. index/style_id are optional.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / "mods/appearance/ink_three_variants_20261004"
INSTALLATION = PACK / "installation"
CONFIG = ACTIVE / "appearance_parts.json"
MANIFEST = ACTIVE / "portrait_manifest.json"
REPORT = INSTALLATION / "install_report.json"
CATALOG_PARTS = {"eyebrows": "brow", "eyes": "eye", "nose": "nose", "mouth": "mouth"}
SLOTS = ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def encoded_json(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resource(path: Path) -> str:
    try:
        return "res://" + path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.resolve().as_posix()


def resolve_source(value: str) -> Path:
    if value.startswith("res://"):
        return (ROOT / value[6:]).resolve()
    path = Path(value)
    return path.resolve() if path.is_absolute() else (ROOT / path).resolve()


def component(config: dict, gender: str, slot: str) -> dict:
    candidates = [entry for entry in config["components"] if entry["gender"] == gender and f"/{slot}/" in entry["file"]]
    if len(candidates) != 1:
        raise ValueError(f"Expected exactly one {gender}/{slot} component")
    return candidates[0]


def reserved_styles(entry: dict) -> set[int]:
    used = set(entry.get("variant_style_ids", range(entry["variant_count"])))
    for alias in entry.get("variant_aliases", {}):
        if str(alias).lstrip("-").isdigit():
            used.add(int(alias))
    return used


def check_png(path: Path) -> dict:
    with Image.open(path) as encoded:
        encoded.verify()
    with Image.open(path) as image:
        if image.size != (1024, 1536) or image.mode != "RGBA":
            raise ValueError(f"Expected full-canvas 1024x1536 RGBA: {path}")
        alpha = image.getchannel("A")
        if alpha.getextrema()[0] != 0 or alpha.getbbox() is None:
            raise ValueError(f"Expected genuinely transparent nonempty feature: {path}")
        result = {"size": list(image.size), "mode": image.mode, "alpha_bbox": list(alpha.getbbox())}
    return result


def old_png_hashes() -> dict[str, str]:
    return {resource(path): sha(path) for gender in ("male", "female") for path in sorted((ACTIVE / gender).rglob("*.png"))}


def verify_baseline(baseline: dict[str, str]) -> None:
    for path, expected in baseline.items():
        if sha(resolve_source(path)) != expected:
            raise ValueError("Existing PNG changed: " + path)


def save_atomic(path: Path, data: bytes, expected: bytes) -> str:
    """Compare before writing; tolerate Windows readers that deny file replacement.

    The fallback verifies the bytes again through the same r+b handle before
    touching them. A failed in-place write restores the verified previous bytes
    through that handle and fsyncs them before propagating the error.
    """
    if path.read_bytes() != expected:
        raise RuntimeError(f"Concurrent change detected; refusing to overwrite {path}")
    temporary = path.with_name(path.name + ".female_expansion.tmp")
    try:
        with temporary.open("wb") as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
        try:
            temporary.replace(path)
            return "atomic_replace"
        except PermissionError:
            # A Godot/Windows reader may allow writes but deny Delete/Replace.
            # Opening r+b never truncates the existing target before validation.
            with path.open("r+b") as output:
                if output.read() != expected:
                    raise RuntimeError(f"Concurrent change detected before in-place fallback: {path}")
                try:
                    output.seek(0)
                    if output.write(data) != len(data):
                        raise OSError("Incomplete in-place configuration write")
                    output.truncate(len(data))
                    output.flush()
                    os.fsync(output.fileno())
                    output.seek(0)
                    if output.read() != data:
                        raise OSError("In-place configuration readback differs from intended bytes")
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
                            raise OSError("In-place rollback readback differs from original bytes")
                    except Exception as rollback_error:
                        raise RuntimeError(
                            f"Configuration write and rollback both failed for {path}; "
                            f"write={write_error}; rollback={rollback_error}"
                        ) from write_error
                    raise
            return "verified_in_place"
    finally:
        # Remove only this operation's staging bytes, never a changed temp file.
        if temporary.exists() and temporary.read_bytes() == data:
            try:
                temporary.unlink()
            except PermissionError:
                # Harmless staging bytes can remain if an indexer opened them.
                pass


def verify_retry_backup(
    backup: Path, selection_path: Path, config_before: bytes,
    manifest_before: bytes, baseline: dict[str, str], records: list[dict],
) -> None:
    """Permit retry only when the prior attempt is demonstrably fully rolled back."""
    required = [INSTALLATION / name for name in (
        "appearance_parts.before.json", "portrait_manifest.before.json",
        "selection.json", "baseline_sha256.json",
    )]
    if not backup.is_file() or any(not path.is_file() for path in required):
        raise ValueError("Existing backup is incomplete; refusing to reuse interrupted installation state")
    saved_config, saved_manifest, saved_selection, saved_baseline_file = required
    if saved_config.read_bytes() != config_before or CONFIG.read_bytes() != config_before:
        raise ValueError("Existing backup config does not exactly match the rolled-back active config")
    if saved_manifest.read_bytes() != manifest_before or MANIFEST.read_bytes() != manifest_before:
        raise ValueError("Existing backup manifest does not exactly match the rolled-back active manifest")
    if saved_selection.read_bytes() != selection_path.read_bytes():
        raise ValueError("Existing backup selection differs from the requested retry selection")
    saved_baseline = read_json(saved_baseline_file)
    if saved_baseline != baseline:
        raise ValueError("Existing backup PNG inventory/hashes differ from the current rolled-back baseline")
    verify_baseline(saved_baseline)
    for record in records:
        if resolve_source(record["resource_path"]).exists():
            raise ValueError("New feature still exists; retry requires a complete rollback")
    expected_members = {
        CONFIG.relative_to(ROOT).as_posix(): config_before,
        MANIFEST.relative_to(ROOT).as_posix(): manifest_before,
    }
    png_members = {
        resolve_source(file).relative_to(ROOT).as_posix(): expected
        for file, expected in saved_baseline.items()
    }
    with zipfile.ZipFile(backup, "r") as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or set(names) != set(expected_members) | set(png_members):
            raise ValueError("Existing ZIP member inventory differs from the exact baseline snapshot")
        if archive.testzip() is not None:
            raise ValueError("Existing backup ZIP failed its integrity check")
        for name, expected in expected_members.items():
            if archive.read(name) != expected:
                raise ValueError("Existing ZIP configuration bytes differ: " + name)
        for name, expected in png_members.items():
            if hashlib.sha256(archive.read(name)).hexdigest() != expected:
                raise ValueError("Existing ZIP PNG differs from the verified baseline: " + name)


def plan(selection: dict, config: dict) -> tuple[dict, list[dict]]:
    if selection.get("schema") != "female-feature-expansion/v1":
        raise ValueError("Unsupported or missing selection schema")
    additions = selection.get("additions", [])
    if not additions:
        raise ValueError("Selection must contain at least one reviewed addition")
    updated = copy.deepcopy(config)
    records = []
    claimed_targets: set[Path] = set()
    for chosen in additions:
        part = chosen["part"]
        if part not in CATALOG_PARTS:
            raise ValueError("Only female eyebrows/eyes/nose/mouth may be appended")
        name = str(chosen["name"]).strip()
        if not name:
            raise ValueError("Every added feature needs a display name")
        source = resolve_source(chosen["source"])
        png_info = check_png(source)
        entry = component(updated, "female", part)
        count = int(entry["variant_count"])
        start = int(entry.get("index_start", config.get("index_start", 0)))
        indices = list(entry.get("variant_indices", range(start, start + count)))
        names = list(entry.get("variant_names", []))
        styles = list(entry.get("variant_style_ids", range(count)))
        if any(len(values) != count for values in (indices, names, styles)):
            raise ValueError("Existing component arrays disagree with variant_count: " + part)
        physical = [int(path.stem) for path in (ACTIVE / "female" / part).glob("*.png") if path.stem.isdigit()]
        physical += [record["index"] for record in records if record["part"] == part]
        index = int(chosen.get("index", max(physical + indices, default=0) + 1))
        if index <= 0 or index in indices:
            raise ValueError("New physical index must not replace an existing file: " + part)
        used_styles = reserved_styles(entry)
        style = int(chosen.get("style_id", max(used_styles, default=-1) + 1))
        if style < 0 or style in used_styles:
            raise ValueError(f"Saved style ID {style} conflicts with active/retired IDs for {part}")
        padding = int(entry.get("index_padding", config.get("index_padding", 0)))
        file_index = str(index).zfill(padding)
        target = (ACTIVE / entry["file"].replace("{index}", file_index)).resolve()
        if not target.is_relative_to(ACTIVE) or target.exists() or target in claimed_targets:
            raise ValueError("Refusing to overwrite or escape the active package: " + str(target))
        claimed_targets.add(target)
        # Duplicate byte-identical art is never a valid addition, even if renamed.
        fingerprint = sha(source)
        if any(sha(path) == fingerprint for path in (ACTIVE / "female" / part).glob("*.png")):
            raise ValueError("Selected feature is byte-identical to an existing option: " + str(source))
        if any(record["part"] == part and record["sha256"] == fingerprint for record in records):
            raise ValueError("Selection contains duplicate feature files: " + str(source))
        entry["variant_count"] = count + 1
        entry["variant_indices"] = indices + [index]
        entry["variant_names"] = names + [name]
        entry["variant_style_ids"] = styles + [style]
        records.append({"part": part, "catalog_part": CATALOG_PARTS[part], "name": name,
                        "index": index, "style_id": style, "ui_index": count,
                        "source_path": resource(source), "candidate_path": resource(source),
                        "resource_path": resource(target), "sha256": fingerprint,
                        "png": png_info, "selection": chosen})
    return updated, records


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("selection", type=Path)
    parser.add_argument("--install", action="store_true", help="Append files and update config after successful preflight")
    args = parser.parse_args()
    selection_path = args.selection.resolve()
    selection = read_json(selection_path)
    if REPORT.exists():
        previous = read_json(REPORT)
        if previous.get("selection_sha256") != sha(selection_path):
            raise ValueError("A different selection was already installed; preserve the existing installation report")
        verify_baseline(previous["baseline_sha256"])
        for record in previous["records"]:
            if sha(resolve_source(record["resource_path"])) != record["sha256"]:
                raise ValueError("Previously installed feature changed: " + record["resource_path"])
        if read_json(CONFIG) != previous["installed_config"]:
            raise ValueError("Configuration changed after installation; inspect before rerunning")
        print(json.dumps({"already_installed": True, "report": resource(REPORT)}, ensure_ascii=False))
        return 0
    config_before = CONFIG.read_bytes()
    manifest_before = MANIFEST.read_bytes()
    config = json.loads(config_before.decode("utf-8-sig"))
    manifest = json.loads(manifest_before.decode("utf-8-sig"))
    updated, records = plan(selection, config)
    baseline = old_png_hashes()
    backup = INSTALLATION / "before_install.zip"
    backup_reused = backup.exists()
    if backup_reused:
        verify_retry_backup(backup, selection_path, config_before, manifest_before, baseline, records)
    slot_counts = {gender: [component(updated, gender, slot)["variant_count"] for slot in SLOTS] for gender in ("male", "female")}
    before_slot_counts = {gender: [component(config, gender, slot)["variant_count"] for slot in SLOTS] for gender in ("male", "female")}
    counts = {part: component(updated, "female", part)["variant_count"] for part in CATALOG_PARTS}
    summary = {"install_requested": args.install, "records": records, "counts": counts,
               "slot_counts": slot_counts, "baseline_png_count": len(baseline),
               "reuse_verified_rollback_backup": backup_reused}
    if not args.install:
        print(json.dumps(summary, ensure_ascii=False, indent=2))
        return 0
    INSTALLATION.mkdir(parents=True, exist_ok=True)
    verify_baseline(baseline)
    if CONFIG.read_bytes() != config_before or MANIFEST.read_bytes() != manifest_before:
        raise RuntimeError("Configuration changed during preflight; retry from the new state")
    if not backup_reused:
        with zipfile.ZipFile(backup, "x", compression=zipfile.ZIP_DEFLATED) as archive:
            for path in [CONFIG, MANIFEST] + [resolve_source(file) for file in baseline]:
                archive.write(path, path.relative_to(ROOT).as_posix())
        (INSTALLATION / "appearance_parts.before.json").write_bytes(config_before)
        (INSTALLATION / "portrait_manifest.before.json").write_bytes(manifest_before)
        shutil.copyfile(selection_path, INSTALLATION / "selection.json")
        (INSTALLATION / "baseline_sha256.json").write_bytes(encoded_json(baseline))
    total_variants = sum(int(entry["variant_count"]) for entry in updated["components"])
    manifest.setdefault("variants_per_slot", {}).update(slot_counts)
    manifest["active_variant_count"] = total_variants
    manifest["total_pngs"] = len(baseline) + len(records)
    manifest["female_feature_expansion_20261106"] = {
        "source": resource(PACK), "counts": counts, "records": records,
        "selection": resource(selection_path), "append_only": True,
        "preserved_existing_pngs": len(baseline), "independent_slots": True}
    config_after = encoded_json(updated)
    manifest_after = encoded_json(manifest)
    created: list[Path] = []
    try:
        for record in records:
            target = resolve_source(record["resource_path"])
            target.parent.mkdir(parents=True, exist_ok=True)
            # Exclusive creation prevents a race from replacing an existing PNG.
            with target.open("xb") as output:
                output.write(resolve_source(record["source_path"]).read_bytes())
            created.append(target)
            if sha(target) != record["sha256"]:
                raise RuntimeError("Installed bytes differ from source: " + str(target))
        config_write_mode = save_atomic(CONFIG, config_after, config_before)
        manifest_write_mode = save_atomic(MANIFEST, manifest_after, manifest_before)
        verify_baseline(baseline)
        report = {"installed": True, "schema": "female-feature-expansion/v1", "append_only": True,
                  "selection_path": resource(selection_path), "selection_sha256": sha(selection_path),
                  "records": records, "counts": counts, "slot_counts": slot_counts,
                  "before_slot_counts": before_slot_counts, "active_variant_count": total_variants,
                  "baseline_sha256": baseline, "baseline_components": config["components"],
                  "baseline_config": config, "installed_config": updated,
                  "before_config_sha256": hashlib.sha256(config_before).hexdigest(),
                  "after_config_sha256": sha(CONFIG), "after_manifest_sha256": sha(MANIFEST),
                  "backup": resource(backup), "backup_reused_after_verified_rollback": backup_reused,
                  "config_write_mode": config_write_mode, "manifest_write_mode": manifest_write_mode,
                  "deduplication": selection.get("deduplication", [])}
        REPORT.write_bytes(encoded_json(report))
    except Exception as install_error:
        # Restore only our own just-written versions; never overwrite another worker's edits.
        rollback_errors = []
        for path, previous_bytes, installed_bytes in (
            (CONFIG, config_before, config_after),
            (MANIFEST, manifest_before, manifest_after),
        ):
            try:
                current_bytes = path.read_bytes()
                if current_bytes == installed_bytes:
                    save_atomic(path, previous_bytes, installed_bytes)
                elif current_bytes != previous_bytes:
                    rollback_errors.append(f"Unexpected concurrent/partial bytes; not overwritten: {path}")
            except Exception as error:
                rollback_errors.append(f"Configuration rollback failed for {path}: {error}")
        for target in created:
            try:
                if target.is_relative_to(ACTIVE) and target.exists():
                    expected = next(record["sha256"] for record in records
                                    if resolve_source(record["resource_path"]) == target)
                    if sha(target) == expected:
                        target.unlink()
                    else:
                        rollback_errors.append(f"New PNG changed concurrently; not removed: {target}")
            except Exception as error:
                rollback_errors.append(f"New PNG rollback failed for {target}: {error}")
        if rollback_errors:
            raise RuntimeError("Installation failed and rollback needs inspection: "
                               + "; ".join(rollback_errors)) from install_error
        raise
    print(json.dumps({"installed": len(records), "counts": counts, "slot_counts": slot_counts,
                      "unchanged_existing_pngs": len(baseline), "report": resource(REPORT)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
