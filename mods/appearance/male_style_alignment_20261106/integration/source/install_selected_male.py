"""Install the nine reviewed male layers; default is a read-only preflight.

selection.json: {"schema":"male-style-replacement/v1",
 "approved_portrait":"res://.../integration/source/approved_male.png",
 "head_alpha_policy":"preserve_existing", "replacements":[
 {"target":"male/eyes/01.png", "source":"res://.../integration/male/eyes/01.png"}, ...]}
All nine whitelisted paths are required. No choices, saved IDs or aliases change.
"""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[5]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / "mods/appearance/ink_three_variants_20261004"
INSTALLATION = PACK / "installation"
CONFIG = ACTIVE / "appearance_parts.json"
MANIFEST = ACTIVE / "portrait_manifest.json"
REPORT = INSTALLATION / "install_report.json"
APPROVED = PACK / "source/approved_male.png"
APPROVED_SHA = "d97b79e3372df42db24b4dd5c3ec42f376d652616d9fa25d78a393b004da81a8"
META_KEY = "male_style_alignment_20261106"
SLOTS = ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
CATALOG = ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup"]
TARGETS = {f"male/{part}/01.png" for part in SLOTS[:8]} | {"male/clothing_back/01.png"}


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
    descriptor, temporary_name = tempfile.mkstemp(prefix=path.name + ".male_alignment.", suffix=".tmp", dir=path.parent)
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
    if selection.get("schema") != "male-style-replacement/v1":
        raise ValueError("Unsupported/missing selection schema")
    if selection.get("head_alpha_policy") != "preserve_existing":
        raise ValueError("The existing seven-collar head/neck Alpha must be preserved")
    if resolve(selection.get("approved_portrait", "")) != APPROVED.resolve():
        raise ValueError("approved_portrait must identify integration/source/approved_male.png")
    approved_bytes = APPROVED.read_bytes()
    if digest(approved_bytes) != APPROVED_SHA:
        raise ValueError("Approved portrait is not the user-approved original-color 01")
    chosen = selection.get("replacements", [])
    targets = [item.get("target") for item in chosen]
    if len(targets) != 9 or set(targets) != TARGETS:
        raise ValueError("Selection must contain exactly the nine approved target paths")
    config_before, manifest_before = CONFIG.read_bytes(), MANIFEST.read_bytes()
    config = json.loads(config_before.decode("utf-8-sig"))
    baseline = inventory()
    records, old_bytes, new_bytes = [], {}, {}
    for item in chosen:
        relative = item["target"]
        target, source = ACTIVE / relative, resolve(item["source"])
        if source != (PACK / relative).resolve() or source.is_relative_to(ACTIVE):
            raise ValueError("Every source must be staged at integration/" + relative)
        before, after = target.read_bytes(), source.read_bytes()
        info, alpha = png_info(after, relative)
        if relative == "male/face_shape/01.png":
            _old_info, old_alpha = png_info(before, "active head")
            if alpha != old_alpha:
                raise ValueError("New head Alpha differs from the existing seven-collar head/neck mask")
        part = relative.split("/")[1]
        records.append({"target": relative, "part": part, "name": item.get("name", part),
                        "resource_path": resource(target), "source_path": resource(source),
                        "candidate_path": resource(source), "sha256": digest(after),
                        "before_sha256": digest(before), **info})
        old_bytes[relative], new_bytes[relative] = before, after
    target_resources = {record["resource_path"] for record in records}
    preserved = {key: value for key, value in baseline.items() if key not in target_resources}
    snapshot = {"schema": "male-style-install-snapshot/v1", "selection_sha256": digest(selection_bytes),
                "approved_portrait": resource(APPROVED), "approved_portrait_sha256": APPROVED_SHA,
                "sources": {record["source_path"]: record["sha256"] for record in records},
                "config_sha256": digest(config_before), "manifest_before_sha256": digest(manifest_before),
                "baseline_sha256": baseline}
    return {"selection_bytes": selection_bytes, "config_before": config_before,
            "manifest_before": manifest_before, "config": config, "records": records,
            "old_bytes": old_bytes, "new_bytes": new_bytes, "baseline": baseline,
            "preserved": preserved, "snapshot": snapshot, "slot_counts": slot_counts(config)}


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
    if len(report.get("records", [])) != 9 or {item.get("target") for item in report["records"]} != TARGETS:
        raise ValueError("Completed report does not describe the exact nine approved paths")
    if (INSTALLATION / "selection.json").read_bytes() != selection_path.read_bytes():
        raise ValueError("Completed batch has different selection provenance")
    snapshot = read_json(INSTALLATION / "batch_snapshot.json")
    if snapshot["selection_sha256"] != digest(selection_path.read_bytes()):
        raise ValueError("Selection differs from completed frozen batch")
    if digest(APPROVED.read_bytes()) != APPROVED_SHA or snapshot["approved_portrait_sha256"] != APPROVED_SHA:
        raise ValueError("Approved original changed after installation")
    if digest(CONFIG.read_bytes()) != report["config_sha256"] or CONFIG.read_bytes() != (INSTALLATION / "appearance_parts.before.json").read_bytes():
        raise ValueError("Active appearance configuration changed after installation")
    if digest(MANIFEST.read_bytes()) != report["manifest_after_sha256"]:
        raise ValueError("Active manifest changed after installation")
    expected = dict(report["preserved_sha256"])
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
    lock = PACK / ".male_alignment_install.lock"
    with lock.open("x", encoding="utf-8") as output:
        output.write(str(os.getpid()))
    try:
        if REPORT.exists():
            return verify_completed(selection_path)
        prepare_backup(state)
        verify_inventory(state["baseline"])
        if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
            raise RuntimeError("Configuration changed after preflight")
        if selection_path.read_bytes() != state["selection_bytes"] or digest(APPROVED.read_bytes()) != APPROVED_SHA:
            raise RuntimeError("Selection/approved source changed after preflight")
        for record in state["records"]:
            if resolve(record["source_path"]).read_bytes() != state["new_bytes"][record["target"]]:
                raise RuntimeError("Reviewed source changed after preflight: " + record["source_path"])
        manifest = json.loads(state["manifest_before"].decode("utf-8-sig"))
        if META_KEY in manifest:
            raise ValueError("Manifest already has a different male alignment batch")
        meta = {"approved_portrait": resource(APPROVED), "approved_portrait_sha256": APPROVED_SHA,
                "records": state["records"], "head_alpha_policy": "preserve_existing",
                "selection": resource(selection_path), "install_report": resource(REPORT),
                "pixel_exact_extraction": False, "art_review": "pending_user_review"}
        manifest[META_KEY] = meta
        manifest_after = encoded(manifest)
        writes = {}
        report_created = False
        try:
            for record in state["records"]:
                relative = record["target"]
                writes[relative] = save_atomic(ACTIVE / relative, state["new_bytes"][relative], state["old_bytes"][relative])
            writes["portrait_manifest.json"] = save_atomic(MANIFEST, manifest_after, state["manifest_before"])
            expected = dict(state["preserved"])
            expected.update({record["resource_path"]: record["sha256"] for record in state["records"]})
            verify_inventory(expected)
            if CONFIG.read_bytes() != state["config_before"]:
                raise RuntimeError("Appearance choices/aliases configuration changed concurrently")
            if MANIFEST.read_bytes() != manifest_after:
                raise RuntimeError("Manifest changed after installation")
            report = {"schema": "male-style-install-report/v1", "installed": True,
                      "approved_portrait": resource(APPROVED), "approved_portrait_sha256": APPROVED_SHA,
                      "records": state["records"], "slot_counts": state["slot_counts"],
                      "baseline_components": state["config"]["components"],
                      "baseline_sha256": state["baseline"], "preserved_sha256": state["preserved"],
                      "config_sha256": digest(state["config_before"]),
                      "manifest_before_sha256": digest(state["manifest_before"]),
                      "manifest_after_sha256": digest(manifest_after),
                      "head_alpha_unchanged": True, "png_count": len(expected),
                      "female_pngs_preserved": sum("/female/" in file for file in state["preserved"]),
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
            restore = [(ACTIVE / item["target"], state["old_bytes"][item["target"]], state["new_bytes"][item["target"]]) for item in state["records"]]
            restore.append((MANIFEST, state["manifest_before"], manifest_after))
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
    parser.add_argument("--install", action="store_true", help="Write reviewed nine layers after preflight")
    args = parser.parse_args()
    selection_path = args.selection.resolve()
    if REPORT.exists():
        report = verify_completed(selection_path)
        print(json.dumps({"status": "already_installed_verified", "report": resource(REPORT), "replacements": len(report["records"])}, ensure_ascii=False))
        return 0
    state = plan(selection_path)
    if not args.install:
        if INSTALLATION.exists():
            verify_retry_backup(state, backup_members(state))
        print(json.dumps({"status": "preflight_passed_read_only", "root": str(ROOT),
                          "replacements": state["records"], "slot_counts": state["slot_counts"],
                          "baseline_pngs": len(state["baseline"]), "preserved_pngs": len(state["preserved"]),
                          "head_alpha_unchanged": True, "configuration_unchanged": True}, ensure_ascii=False, indent=2))
        return 0
    report = install(selection_path, state)
    print(json.dumps({"status": "installed", "report": resource(REPORT), "replacements": len(report["records"])}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"MALE_STYLE_INSTALL_ERROR: {error}", file=sys.stderr)
        sys.exit(1)
