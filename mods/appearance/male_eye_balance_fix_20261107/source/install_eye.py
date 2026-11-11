"""Replace one reviewed existing male eye PNG; default is read-only preflight.

selection.json: {"schema":"male-eye-balance-fix/v1", "target":"male/eyes/XX.png",
 "source":"res://...candidate.png", "reference_screenshot":"res://..."}.
Only the confirmed male04 eye may be replaced. Configuration and manifest bytes are never written.
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

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / "mods/appearance/ink_three_variants_20261004"
INSTALLATION = PACK / "installation"
CONFIG = ACTIVE / "appearance_parts.json"
MANIFEST = ACTIVE / "portrait_manifest.json"
REPORT = INSTALLATION / "install_report.json"
BASELINE_FILE = PACK / "source/baseline_active.json"
SCREENSHOT = PACK / "reference/reported_eye_crop.png"
TARGETS = {"male/eyes/04.png"}
TARGET_BEFORE_SHA = "fd3f04b779e0849bbe9c9cf46fcfa2b5bd6b8fcb8839b2aac5c1d26819acba89"
CANDIDATE = PACK / "candidate/new_male_eyes04.png"
SLOTS = ["hair_front", "hair_back", "eyebrows", "eyes", "nose", "mouth", "face_shape", "clothing", "face_accessory"]
CATALOG = ["hair_front", "hair_back", "brow", "eye", "nose", "mouth", "face", "clothes", "makeup"]


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
    descriptor, temporary_name = tempfile.mkstemp(prefix=path.name + ".eye_balance_fix.", suffix=".tmp", dir=path.parent)
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
    if selection.get("schema") != "male-eye-balance-fix/v1":
        raise ValueError("Unsupported or missing selection schema")
    relative = selection.get("target")
    if relative not in TARGETS:
        raise ValueError("Only the confirmed male/eyes/04.png may be replaced")
    target, source = ACTIVE / relative, resolve(selection["source"])
    if not source.is_relative_to(ROOT) or source.is_relative_to(ACTIVE):
        raise ValueError("Candidate must be staged in the project outside active resources")
    if source != CANDIDATE.resolve():
        raise ValueError("Use the reviewed candidate/new_male_eyes04.png")
    screenshot = resolve(selection.get("reference_screenshot", resource(SCREENSHOT)))
    if screenshot != SCREENSHOT.resolve():
        raise ValueError("Use the frozen reference/reported_eye_crop.png")
    baseline_bytes = BASELINE_FILE.read_bytes()
    frozen = json.loads(baseline_bytes.decode("utf-8-sig"))
    baseline = {"res://" + file: value for file, value in frozen.items() if file.endswith(".png")}
    if len(frozen) != 56 or len(baseline) != 54:
        raise ValueError("Frozen baseline must contain exactly54 PNGs and both configuration files")
    expected_keys = {file[6:] for file in inventory()} | {CONFIG.relative_to(ROOT).as_posix(), MANIFEST.relative_to(ROOT).as_posix()}
    if set(frozen) != expected_keys:
        raise ValueError("Frozen baseline inventory differs from the exact current package")
    for file, expected in frozen.items():
        if digest((ROOT / file).read_bytes()) != expected:
            raise ValueError("Active resource changed since frozen issue baseline: " + file)
    config_before, manifest_before = CONFIG.read_bytes(), MANIFEST.read_bytes()
    config = json.loads(config_before.decode("utf-8-sig"))
    eye = [entry for entry in config["components"] if entry["gender"] == "male" and entry["part"] == "eye"]
    if len(eye) != 1:
        raise ValueError("Expected one male eye component")
    eye = eye[0]
    physical = int(target.stem)
    indices, styles = list(eye["variant_indices"]), list(eye["variant_style_ids"])
    if indices != [1, 4, 5] or styles != [0, 3, 4] or int(eye["variant_count"]) != 3:
        raise ValueError("Current three eye mappings must remain01/04/05 and styles0/3/4")
    ui_index = indices.index(physical)
    before, after = target.read_bytes(), source.read_bytes()
    if digest(before) != TARGET_BEFORE_SHA:
        raise ValueError("Affected eye differs from the frozen reported male04 baseline")
    info, _alpha = png_info(after, relative)
    if before == after:
        raise ValueError("Candidate is identical to the currently installed eye")
    record = {"target": relative, "gender": "male", "part": "eyes", "catalog_part": "eye", "index": physical,
              "style_id": styles[ui_index], "ui_index": ui_index, "name": eye["variant_names"][ui_index],
              "component_id": eye["id"], "full_component_id": f"{eye['id']}_{styles[ui_index]:02d}",
              "resource_path": resource(target), "source_path": resource(source), "candidate_path": resource(source),
              "before_sha256": digest(before), "sha256": digest(after), **info}
    screenshot_bytes = screenshot.read_bytes()
    snapshot = {"schema": "male-eye-balance-snapshot/v1", "selection_sha256": digest(selection_bytes),
                "source_path": resource(source), "source_sha256": digest(after),
                "reference_screenshot": resource(screenshot), "reference_screenshot_sha256": digest(screenshot_bytes),
                "frozen_baseline_sha256": digest(baseline_bytes), "baseline_sha256": baseline,
                "config_sha256": digest(config_before), "manifest_sha256": digest(manifest_before),
                "target": relative, "target_before_sha256": digest(before)}
    return {"selection_bytes": selection_bytes, "config_before": config_before, "manifest_before": manifest_before,
            "config": config, "records": [record], "baseline": baseline, "before": before, "after": after,
            "preserved": {file: value for file, value in baseline.items() if file != resource(target)},
            "screenshot_bytes": screenshot_bytes, "baseline_file_bytes": baseline_bytes,
            "snapshot": snapshot, "slot_counts": slot_counts(config)}


def backup_members(state: dict) -> dict[str, bytes]:
    members = {CONFIG.relative_to(ROOT).as_posix(): state["config_before"],
               MANIFEST.relative_to(ROOT).as_posix(): state["manifest_before"],
               "_installation/selection.json": state["selection_bytes"],
               "_installation/batch_snapshot.json": encoded(state["snapshot"]),
               "_installation/reported_eye_crop.png": state["screenshot_bytes"],
               "_installation/baseline_active.json": state["baseline_file_bytes"]}
    for file, expected in state["baseline"].items():
        path = resolve(file)
        data = path.read_bytes()
        if digest(data) != expected:
            raise RuntimeError("PNG changed while preparing backup: " + file)
        members[path.relative_to(ROOT).as_posix()] = data
    return members


def backup_sidecars(state: dict) -> dict[str, bytes]:
    return {"appearance_parts.before.json": state["config_before"],
            "portrait_manifest.before.json": state["manifest_before"],
            "selection.json": state["selection_bytes"], "baseline_sha256.json": encoded(state["baseline"]),
            "batch_snapshot.json": encoded(state["snapshot"]), "reported_eye_crop.png": state["screenshot_bytes"],
            "baseline_active.json": state["baseline_file_bytes"]}


def verify_retry_backup(state: dict, members: dict[str, bytes]) -> None:
    for name, expected in backup_sidecars(state).items():
        path = INSTALLATION / name
        if not path.is_file() or path.read_bytes() != expected:
            raise ValueError("Existing backup is incomplete/different: " + name + "; archive prior batch before changed provenance")
    verify_inventory(state["baseline"])
    if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
        raise RuntimeError("Retry requires exact frozen configuration bytes")
    with zipfile.ZipFile(INSTALLATION / "before_install.zip") as archive:
        names = archive.namelist()
        if len(names) != len(set(names)) or set(names) != set(members) or archive.testzip() is not None:
            raise ValueError("Backup ZIP integrity/inventory differs from frozen snapshot")
        for name, expected in members.items():
            if archive.read(name) != expected:
                raise ValueError("Backup ZIP bytes differ: " + name)


def prepare_backup(state: dict) -> None:
    members = backup_members(state)
    if INSTALLATION.exists():
        verify_retry_backup(state, members)
        return
    INSTALLATION.mkdir()
    with zipfile.ZipFile(INSTALLATION / "before_install.zip", "x", zipfile.ZIP_DEFLATED) as archive:
        for name, data in members.items():
            archive.writestr(name, data)
    for name, data in backup_sidecars(state).items():
        with (INSTALLATION / name).open("xb") as output:
            output.write(data)
            output.flush()
            os.fsync(output.fileno())
    verify_retry_backup(state, members)


def verify_completed(selection_path: Path) -> dict:
    report = read_json(REPORT)
    if report.get("schema") != "male-eye-balance-fix/v1" or len(report.get("records", [])) != 1:
        raise ValueError("Unexpected completed single-eye report")
    if (INSTALLATION / "selection.json").read_bytes() != selection_path.read_bytes():
        raise ValueError("Completed selection provenance changed")
    snapshot = read_json(INSTALLATION / "batch_snapshot.json")
    if snapshot["selection_sha256"] != digest(selection_path.read_bytes()):
        raise ValueError("Completed selection differs from frozen batch")
    if digest(BASELINE_FILE.read_bytes()) != snapshot["frozen_baseline_sha256"]:
        raise ValueError("Frozen issue baseline changed")
    if digest(SCREENSHOT.read_bytes()) != snapshot["reference_screenshot_sha256"]:
        raise ValueError("Frozen issue screenshot changed")
    for path, name, field in ((CONFIG, "appearance_parts.before.json", "config_sha256"), (MANIFEST, "portrait_manifest.before.json", "manifest_sha256")):
        if path.read_bytes() != (INSTALLATION / name).read_bytes() or digest(path.read_bytes()) != report[field]:
            raise ValueError("Configuration/manifest changed after eye-only correction")
    record = report["records"][0]
    if record["target"] not in TARGETS or snapshot["target"] != record["target"]:
        raise ValueError("Unexpected corrected path")
    for key in ("source_path", "resource_path"):
        if digest(resolve(record[key]).read_bytes()) != record["sha256"]:
            raise ValueError("Reviewed/installed corrected eye changed: " + record[key])
    if snapshot["source_sha256"] != record["sha256"] or snapshot["source_path"] != record["source_path"]:
        raise ValueError("Reviewed eye provenance differs from frozen batch")
    expected = dict(report["baseline_sha256"])
    expected[record["resource_path"]] = record["sha256"]
    verify_inventory(expected)
    return report


def install(selection_path: Path, state: dict) -> dict:
    lock = PACK / ".single_eye_install.lock"
    with lock.open("x", encoding="utf-8") as output:
        output.write(str(os.getpid()))
    try:
        if REPORT.exists():
            return verify_completed(selection_path)
        prepare_backup(state)
        verify_inventory(state["baseline"])
        if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
            raise RuntimeError("Frozen configuration changed after preflight")
        if selection_path.read_bytes() != state["selection_bytes"] or BASELINE_FILE.read_bytes() != state["baseline_file_bytes"] or SCREENSHOT.read_bytes() != state["screenshot_bytes"]:
            raise RuntimeError("Frozen issue provenance changed after preflight")
        record = state["records"][0]
        target, source = resolve(record["resource_path"]), resolve(record["source_path"])
        if source.read_bytes() != state["after"]:
            raise RuntimeError("Reviewed candidate changed after preflight")
        report_created = False
        try:
            method = save_atomic(target, state["after"], state["before"])
            expected = dict(state["baseline"])
            expected[record["resource_path"]] = record["sha256"]
            verify_inventory(expected)
            if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
                raise RuntimeError("Concurrent change to untouched config/manifest")
            report = {"schema": "male-eye-balance-fix/v1", "installed": True, "records": state["records"],
                      "slot_counts": state["slot_counts"], "baseline_config": state["config"],
                      "baseline_components": state["config"]["components"], "baseline_sha256": state["baseline"],
                      "preserved_sha256": state["preserved"], "png_count": len(expected),
                      "active_variant_count": sum(int(entry["variant_count"]) for entry in state["config"]["components"]),
                      "config_sha256": digest(state["config_before"]), "manifest_sha256": digest(state["manifest_before"]),
                      "reference_screenshot": resource(SCREENSHOT), "reference_screenshot_sha256": digest(state["screenshot_bytes"]),
                      "frozen_baseline": resource(BASELINE_FILE), "frozen_baseline_sha256": digest(state["baseline_file_bytes"]),
                      "selection_sha256": digest(state["selection_bytes"]), "backup": resource(INSTALLATION / "before_install.zip"),
                      "configuration_unchanged": True, "manifest_unchanged": True,
                      "write_method": method, "visual_acceptance": "pending_user_review"}
            with REPORT.open("x", encoding="utf-8") as output:
                report_created = True
                output.write(encoded(report).decode("utf-8"))
                output.flush()
                os.fsync(output.fileno())
            return report
        except Exception as error:
            rollback_errors = []
            try:
                current = target.read_bytes()
                if current == state["after"]:
                    save_atomic(target, state["before"], state["after"])
                elif current != state["before"]:
                    raise RuntimeError("Concurrent/partial target preserved; refusing overwrite")
                verify_inventory(state["baseline"])
                if CONFIG.read_bytes() != state["config_before"] or MANIFEST.read_bytes() != state["manifest_before"]:
                    raise RuntimeError("Untouched configuration baseline changed")
            except Exception as rollback_error:
                rollback_errors.append(str(rollback_error))
            if report_created and REPORT.exists() and encoded(report).startswith(REPORT.read_bytes()):
                REPORT.unlink()
            if rollback_errors:
                raise RuntimeError(f"Eye replacement failed: {error}; rollback issues: {rollback_errors}") from error
            raise RuntimeError(f"Eye replacement failed and fully rolled back: {error}") from error
    finally:
        lock.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selection", type=Path, default=PACK / "source/selection.json")
    parser.add_argument("--install", action="store_true", help="Replace only the reviewed existing eye PNG")
    args = parser.parse_args()
    selection_path = args.selection.resolve()
    if REPORT.exists():
        report = verify_completed(selection_path)
        print(json.dumps({"status": "already_installed_verified", "target": report["records"][0]["target"]}))
        return 0
    state = plan(selection_path)
    if not args.install:
        if INSTALLATION.exists():
            verify_retry_backup(state, backup_members(state))
        print(json.dumps({"status": "preflight_passed_read_only", "record": state["records"][0],
                          "baseline_pngs": len(state["baseline"]), "preserved_pngs": len(state["preserved"]),
                          "slot_counts": state["slot_counts"], "configuration_unchanged": True,
                          "manifest_unchanged": True}, ensure_ascii=False, indent=2))
        return 0
    report = install(selection_path, state)
    print(json.dumps({"status": "installed", "target": report["records"][0]["target"], "report": resource(REPORT)}))
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"MALE_EYE_BALANCE_ERROR: {error}", file=sys.stderr)
        sys.exit(1)
