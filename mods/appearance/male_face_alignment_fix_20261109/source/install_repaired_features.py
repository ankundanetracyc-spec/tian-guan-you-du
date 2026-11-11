#!/usr/bin/env python3
"""Preflight13 approved existing PNG repairs; only --apply changes the active pack.

The live semantic/visual gate is run again on every invocation. A cached report
never authorizes a write. The frozen67 PNGs+2 configs are the before-state;
after-state permits only the13 explicit PNG replacements and one manifest key.
Already-installed invocations verify current candidates/approvals/preservation
and leave active files untouched. No import, Godot process, code or user save is
changed. QA/install reports are the only writes made without --apply.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
PACK = Path(__file__).resolve().parents[1]
ACTIVE = ROOT / "mods/appearance/ink_three_variants_20261004"
INSTALLATION = PACK / "installation"
BASELINE = PACK / "source/baseline.json"
GATE = ROOT / "tools/validate_portrait_feature_alignment.py"
FRAME = ROOT / "docs/美术规范/男性五官统一定位与验收_20261109/feature_frame.json"
SEMANTICS = PACK / "qa/final_semantic_measurements.json"
VISUAL = PACK / "qa/final_visual_review.json"
META = "male_face_alignment_fix_20261109"
CONFIG = "appearance_parts.json"
MANIFEST = "portrait_manifest.json"
REPAIRS = (
    "male/eyes/01.png", "male/eyes/04.png", "male/eyes/05.png",
    "male/eyebrows/01.png", "male/eyebrows/04.png", "male/eyebrows/05.png",
    "male/nose/01.png", "male/mouth/01.png",
    "male/face_accessory/02.png", "male/face_accessory/03.png",
    "male/face_accessory/04.png", "male/face_accessory/05.png",
    "male/face_accessory/06.png",
)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def file_hash(path):
    return digest(path.read_bytes())


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2)+"\n").encode("utf-8")


def read_json(path):
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    require(isinstance(value, dict), "Expected JSON object: "+str(path))
    return value


def atomic_write(path, data, expected_sha=None):
    """Write a same-directory temp, then compare and atomically replace.

    Active targets always supply the expected current SHA. This is a filesystem
    precondition check immediately before replace, not a cross-program lock.
    Rollback uses the same guard and never knowingly overwrites another writer.
    """
    descriptor, temporary = tempfile.mkstemp(dir=path.parent, prefix=path.name+".", suffix=".tmp")
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(data)
            handle.flush()
            os.fsync(handle.fileno())
        if expected_sha is not None:
            require(path.is_file() and file_hash(path)==expected_sha, "Concurrent file change; refusing replace: "+str(path))
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def report_file(name, value):
    INSTALLATION.mkdir(parents=True, exist_ok=True)
    atomic_write(INSTALLATION/name, encoded(value))


class Installation:
    def __init__(self):
        self.baseline = read_json(BASELINE)
        self.baseline_sha = file_hash(BASELINE)
        self.old = {}
        self.candidates = {}
        self.candidate_sha = {}
        self.proof_stamps = {}
        self.written = []
        self.manifest_after = None
        self.gate_report = None
        self.state = "unchecked"
        self.records = []

    def frozen_sources(self):
        prefix = ACTIVE.relative_to(ROOT).as_posix()+"/"
        for record in self.baseline["files"]:
            name = record["file"]
            require(isinstance(name,str) and name.startswith(prefix), "Baseline outside explicit active pack")
            relative = name[len(prefix):]
            require(relative not in self.old and ".." not in Path(relative).parts, "Invalid/duplicate baseline path")
            backup = (PACK/record["backup"]).resolve()
            require(backup.is_relative_to((PACK/"source/frozen").resolve()), "Backup must stay under source/frozen")
            data = backup.read_bytes()
            require(digest(data)==record["sha256"], "Frozen backup changed: "+relative)
            self.old[relative] = {"bytes":data, "sha256":record["sha256"], "backup":record["backup"]}
        pngs = {name for name in self.old if name.endswith(".png")}
        require(len(self.old)==69 and len(pngs)==67 and {CONFIG,MANIFEST}.issubset(self.old), "Baseline must be exactly67 PNGs+2 configs")
        require(set(REPAIRS).issubset(pngs) and len(REPAIRS)==13, "Explicit repair inventory mismatch")
        require(len(pngs-set(REPAIRS))==54, "Exactly54 original PNGs must be protected")
        for name in REPAIRS:
            path = PACK/name
            data = path.read_bytes()
            with Image.open(path) as image:
                require(image.mode=="RGBA" and image.size==(1024,1536), "RGBA shared canvas required: "+name)
                require(image.getchannel("A").getbbox() is not None, "Empty replacement feature: "+name)
            self.candidates[name] = data
            self.candidate_sha[name] = digest(data)
            self.records.append({"target":name,"source":(PACK/name).relative_to(ROOT).as_posix(),
                                 "before_sha256":self.old[name]["sha256"],"sha256":digest(data),
                                 "same_size":[1024,1536],"runtime_offset":[0,0]})

    def observed(self):
        return {name:file_hash(ACTIVE/name) for name in self.old}

    def expected(self, installed=False, partial=False):
        result = {name:row["sha256"] for name,row in self.old.items()}
        if installed:
            result.update(self.candidate_sha)
            result[MANIFEST] = file_hash(ACTIVE/MANIFEST)
        elif partial:
            for name in self.written:
                result[name] = digest(self.manifest_after) if name==MANIFEST else self.candidate_sha[name]
        return result

    def physical_inventory(self):
        return {(p.relative_to(ACTIVE)).as_posix() for gender in ("male","female") for p in (ACTIVE/gender).rglob("*.png")}

    def guard(self, expected):
        require(file_hash(BASELINE)==self.baseline_sha, "Baseline definition changed during installation")
        require(self.physical_inventory()=={name for name in self.old if name.endswith(".png")}, "Active PNG inventory changed; expected the original67 files")
        observed = self.observed()
        changed = [name for name in expected if observed[name]!=expected[name]]
        require(not changed, "Concurrent or unexpected active changes: "+", ".join(changed))
        return observed

    def inspect_state(self):
        manifest = read_json(ACTIVE/MANIFEST)
        frozen = json.loads(self.old[MANIFEST]["bytes"].decode("utf-8-sig"))
        require(META not in frozen, "Frozen manifest unexpectedly already includes this repair")
        if META not in manifest:
            self.state = "not_installed"
            self.guard(self.expected())
        else:
            self.state = "already_installed_verified"
            before_keys = copy.deepcopy(manifest)
            metadata = before_keys.pop(META)
            require(before_keys==frozen, "Existing installation changed another manifest key")
            declared = {x["target"]:x["sha256"] for x in metadata.get("records",[])}
            require(declared==self.candidate_sha, "Existing repair metadata differs from current13 candidates")
            self.guard(self.expected(installed=True))
        config = read_json(ACTIVE/CONFIG)
        require(sum(int(x["variant_count"]) for x in config["components"])==60, "Active60 choice count changed")
        require(file_hash(ACTIVE/CONFIG)==self.old[CONFIG]["sha256"], "appearance_parts.json bytes changed")

    def current_proofs(self):
        paths = [GATE, FRAME, SEMANTICS, VISUAL, BASELINE]+[PACK/name for name in REPAIRS]
        return {str(path):file_hash(path) for path in paths}

    def proof_guard(self):
        require(self.current_proofs()==self.proof_stamps, "Candidate, gate, canonical frame, measurements or visual review changed during preflight/apply")

    def run_live_gate(self):
        self.proof_stamps = self.current_proofs()
        output = PACK/"qa/alignment_gate_install_preflight.json"
        command = [sys.executable,str(GATE),"--pack-root",str(ACTIVE),"--candidate-root",str(PACK),
                   "--frame",str(FRAME),"--measurements",str(SEMANTICS),"--review",str(VISUAL),"--report",str(output)]
        process = subprocess.run(command,cwd=ROOT,capture_output=True,text=True,encoding="utf-8")
        self.proof_guard()
        require(process.returncode==0, "Current live alignment gate rejected/incomplete (exit%d). See qa/alignment_gate_install_preflight.json. %s" % (process.returncode,process.stdout[:1500]))
        self.gate_report = read_json(output)
        require(self.gate_report.get("schema")=="portrait-feature-alignment-gate/v1" and self.gate_report.get("pass") is True, "Live gate did not produce a passing current report")
        gate_hashes = {x["file"]:x["sha256"] for x in self.gate_report.get("records",[])}
        require(gate_hashes==self.candidate_sha, "Live gate result must cover exactly the current13 explicit repair PNGs")
        require(self.gate_report.get("frame_sha256")==file_hash(FRAME), "Live gate canonical-frame SHA changed")
        self.gate_sha = file_hash(output)
        self.inspect_state()

    def preservation(self, status, after=None):
        observed = self.observed()
        retained = [name for name in self.old if name.endswith(".png") and name not in REPAIRS]
        manifest = read_json(ACTIVE/MANIFEST)
        manifest.pop(META,None)
        frozen_manifest = json.loads(self.old[MANIFEST]["bytes"].decode("utf-8-sig"))
        records = [{"file":name,"expected_sha256":self.old[name]["sha256"],"actual_sha256":observed[name],"unchanged":observed[name]==self.old[name]["sha256"]} for name in retained]
        result = {"schema":"male-face-preservation/v1","status":status,"retained_png_count":54,"retained_pngs":records,
                  "all54_unchanged":all(x["unchanged"] for x in records),"active_png_count":len(self.physical_inventory()),
                  "appearance_parts_byte_identical":observed[CONFIG]==self.old[CONFIG]["sha256"],
                  "all_other_manifest_keys_identical":manifest==frozen_manifest,"active_choice_count":sum(int(x["variant_count"]) for x in read_json(ACTIVE/CONFIG)["components"])}
        if after:
            result["all13_installed_candidate_hashes_match"] = all(observed[name]==self.candidate_sha[name] for name in REPAIRS)
        result["pass"] = result["all54_unchanged"] and result["active_png_count"]==67 and result["active_choice_count"]==60 and result["appearance_parts_byte_identical"] and result["all_other_manifest_keys_identical"] and (not after or result["all13_installed_candidate_hashes_match"])
        return result

    def metadata(self):
        return {"source":PACK.relative_to(ROOT).as_posix(),"replacement_only":True,"replaced_pngs":13,"retained_pngs":54,
                "axis_x":513,"fixed_head_sha256":self.gate_report["fixed_face_sha256"],"records":self.records,
                "appearance_parts_byte_identical":True,"runtime_offsets_added":False,
                "live_gate_report":"qa/alignment_gate_install_preflight.json","live_gate_sha256":self.gate_sha,
                "canonical_frame_sha256":self.gate_report["frame_sha256"],"semantic_measurements_sha256":file_hash(SEMANTICS),
                "independent_visual_review_sha256":file_hash(VISUAL),"runtime_status":"pending_verification"}

    def report(self, status):
        return {"schema":"male-face-alignment-install/v1","status":status,"installed":self.state=="already_installed_verified",
                "time_utc":datetime.now(timezone.utc).isoformat(),"explicit_replacement_count":13,"retained_png_count":54,
                "active_png_count":67,"active_choice_count":60,"baseline_sha256":self.baseline_sha,
                "appearance_parts_sha256":self.old[CONFIG]["sha256"],"records":self.records,
                "gate_pass":bool(self.gate_report and self.gate_report.get("pass")),
                "gate_report":"qa/alignment_gate_install_preflight.json","active_files_written":list(self.written)}

    def rollback(self):
        results = []
        for name in (*REPAIRS,MANIFEST):
            row = {"file":name}
            try:
                if name not in self.written:
                    row["status"] = "not_written_by_this_invocation"
                else:
                    path = ACTIVE/name
                    current = file_hash(path)
                    owned = digest(self.manifest_after) if name==MANIFEST else self.candidate_sha[name]
                    if current==self.old[name]["sha256"]:
                        row["status"] = "already_original"
                    elif current!=owned:
                        row["status"] = "concurrent_change_preserved_requires_attention"
                    else:
                        atomic_write(path,self.old[name]["bytes"],expected_sha=owned)
                        require(file_hash(path)==self.old[name]["sha256"], "Restored byte hash mismatch")
                        row["status"] = "restored_original"
            except Exception as error:
                row.update(status="restore_failed_requires_attention",error=str(error))
            results.append(row)
        return results

    def apply(self):
        # Both the full baseline and all current approvals are checked again
        # immediately before the first active mutation and before each replace.
        self.proof_guard()
        self.guard(self.expected())
        frozen_manifest = json.loads(self.old[MANIFEST]["bytes"].decode("utf-8-sig"))
        manifest = copy.deepcopy(frozen_manifest)
        manifest[META] = self.metadata()
        compare = copy.deepcopy(manifest)
        compare.pop(META)
        require(compare==frozen_manifest, "Only one new manifest key is allowed")
        self.manifest_after = encoded(manifest)
        try:
            for name in (*REPAIRS,MANIFEST):
                self.proof_guard()
                self.guard(self.expected(partial=True))
                data = self.manifest_after if name==MANIFEST else self.candidates[name]
                atomic_write(ACTIVE/name,data,expected_sha=self.old[name]["sha256"])
                self.written.append(name)
            self.proof_guard()
            self.guard(self.expected(partial=True))
            preservation = self.preservation("installed_pending_runtime_verification",after=True)
            require(preservation["pass"], "Post-install preservation failed")
            report_file("preservation_check.json",preservation)
            report = self.report("installed_pending_runtime_verification")
            report.update(installed=True,after_manifest_sha256=digest(self.manifest_after))
            report_file("install_report.json",report)
            return report
        except Exception as error:
            recovery = self.rollback()
            attention = [x for x in recovery if "attention" in x["status"]]
            report = self.report("rollback_requires_attention" if attention else "rolled_back_after_failed_install")
            report.update(installed=False,error=str(error),rollback=recovery)
            try:
                report_file("install_report.json",report)
                report_file("preservation_check.json",self.preservation(report["status"]))
            except Exception as report_error:
                print("Could not write all failure reports: "+str(report_error),file=sys.stderr)
            raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply",action="store_true",help="After successful current preflight, replace only13 explicit PNGs and append manifest metadata")
    args = parser.parse_args()
    INSTALLATION.mkdir(parents=True,exist_ok=True)
    lock = INSTALLATION/"repair_install.lock"
    descriptor = None
    try:
        # Protects two invocations of this installer. Other writers are detected
        # by all69 hash guards; their content is never intentionally rolled back.
        descriptor = os.open(lock,os.O_CREAT|os.O_EXCL|os.O_WRONLY)
        os.write(descriptor,str(os.getpid()).encode("ascii"))
        installation = Installation()
        installation.frozen_sources()
        installation.inspect_state()
        installation.run_live_gate()
        preservation = installation.preservation("preflight_verified",after=installation.state=="already_installed_verified")
        require(preservation["pass"],"Preflight preservation check failed")
        preflight = installation.report("already_installed_verified" if installation.state=="already_installed_verified" else "preflight_passed")
        report_file("preflight.json",preflight)
        report_file("preservation_check.json",preservation)
        if installation.state=="already_installed_verified":
            # Verify-only idempotency: no active image or manifest writes.
            preflight["installed"] = True
            report_file("install_report.json",preflight)
            print(json.dumps({"status":"already_installed_verified","gate_pass":True,"active_files_written":0}))
        elif args.apply:
            report = installation.apply()
            print(json.dumps({"status":report["status"],"installed_pngs":13,"retained_pngs":54,"active_pngs":67,"choices":60}))
        else:
            print(json.dumps({"status":"preflight_passed","gate_pass":True,"apply_requested":False,"active_files_written":0}))
        return 0
    except Exception as error:
        failure = {"schema":"male-face-alignment-install/v1","status":"preflight_rejected","installed":False,"apply_requested":args.apply,"error":str(error)}
        try:
            report_file("preflight.json",failure)
        except Exception as report_error:
            print("Could not write preflight failure report: "+str(report_error),file=sys.stderr)
        print(str(error),file=sys.stderr)
        return 1
    finally:
        if descriptor is not None:
            os.close(descriptor)
            lock.unlink(missing_ok=True)


if __name__=="__main__":
    sys.exit(main())
