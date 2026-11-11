"""Exercise the installer only against disposable temporary fixtures."""
from __future__ import annotations

import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("male_installer_under_test", HERE / "install_selected_male.py")
INSTALLER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(INSTALLER)
REAL_ACTIVE = INSTALLER.ACTIVE
CONFIG_BYTES = (REAL_ACTIVE / "appearance_parts.json").read_bytes()
PNG_PATHS = [path.relative_to(REAL_ACTIVE) for gender in ("male", "female") for path in (REAL_ACTIVE / gender).rglob("*.png")]


def image_bytes(color: tuple[int, int, int, int], shifted: bool = False) -> bytes:
    image = Image.new("RGBA", (1024, 1536))
    ImageDraw.Draw(image).rectangle((200 + int(shifted), 200, 800, 1300), fill=color)
    output = io.BytesIO()
    image.save(output, "PNG")
    return output.getvalue()


class SafetyChecks(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="male_style_installer_fixture_")
        self.root = Path(self.temporary.name)
        self.pack = self.root / "mods/appearance/male_style_alignment_20261106/integration"
        self.active = self.root / "mods/appearance/ink_three_variants_20261004"
        self.patchers = []
        values = {"ROOT": self.root, "PACK": self.pack, "ACTIVE": self.active,
                  "INSTALLATION": self.pack / "installation", "CONFIG": self.active / "appearance_parts.json",
                  "MANIFEST": self.active / "portrait_manifest.json", "REPORT": self.pack / "installation/install_report.json",
                  "APPROVED": self.pack / "source/approved_male.png"}
        for name, value in values.items():
            patcher = mock.patch.object(INSTALLER, name, value)
            patcher.start()
            self.patchers.append(patcher)
        self.old = image_bytes((160, 120, 90, 255))
        self.new = image_bytes((170, 130, 100, 255))
        for relative in PNG_PATHS:
            target = self.active / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(self.old)
        INSTALLER.CONFIG.write_bytes(CONFIG_BYTES)
        self.manifest = {"id": "fixture", "keep_old_nested_meta": {"preserve": [1, 2, 3]}}
        INSTALLER.MANIFEST.write_bytes(INSTALLER.encoded(self.manifest))
        INSTALLER.APPROVED.parent.mkdir(parents=True, exist_ok=True)
        INSTALLER.APPROVED.write_bytes(self.old)
        approved_patch = mock.patch.object(INSTALLER, "APPROVED_SHA", INSTALLER.digest(self.old))
        approved_patch.start()
        self.patchers.append(approved_patch)
        self.selection = self.pack / "source/selection.json"
        replacements = []
        for relative in sorted(INSTALLER.TARGETS):
            source = self.pack / relative
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(self.new)
            replacements.append({"target": relative, "source": INSTALLER.resource(source)})
        self.selection.write_bytes(INSTALLER.encoded({"schema": "male-style-replacement/v1",
            "approved_portrait": INSTALLER.resource(INSTALLER.APPROVED),
            "head_alpha_policy": "preserve_existing", "replacements": replacements}))

    def tearDown(self) -> None:
        for patcher in reversed(self.patchers):
            patcher.stop()
        self.temporary.cleanup()

    def test_preflight_is_read_only_and_targets_are_exact(self) -> None:
        before = INSTALLER.inventory()
        state = INSTALLER.plan(self.selection)
        self.assertEqual(INSTALLER.inventory(), before)
        self.assertFalse(INSTALLER.INSTALLATION.exists())
        self.assertEqual(len(state["records"]), 9)
        self.assertEqual(len(state["preserved"]), len(PNG_PATHS) - 9)
        selected = INSTALLER.read_json(self.selection)
        selected["replacements"][0]["target"] = "female/eyes/01.png"
        self.selection.write_bytes(INSTALLER.encoded(selected))
        with self.assertRaisesRegex(ValueError, "exactly the nine"):
            INSTALLER.plan(self.selection)

    def test_head_alpha_change_and_wrong_original_are_rejected(self) -> None:
        (self.pack / "male/face_shape/01.png").write_bytes(image_bytes((170, 130, 100, 255), shifted=True))
        with self.assertRaisesRegex(ValueError, "Alpha differs"):
            INSTALLER.plan(self.selection)
        INSTALLER.APPROVED.write_bytes(self.new)
        with self.assertRaisesRegex(ValueError, "original-color"):
            INSTALLER.plan(self.selection)

    def test_install_preserves_configuration_and_supports_idempotence(self) -> None:
        state = INSTALLER.plan(self.selection)
        report = INSTALLER.install(self.selection, state)
        self.assertEqual(INSTALLER.CONFIG.read_bytes(), CONFIG_BYTES)
        actual_manifest = INSTALLER.read_json(INSTALLER.MANIFEST)
        actual_manifest.pop(INSTALLER.META_KEY)
        self.assertEqual(actual_manifest, self.manifest)
        self.assertEqual(report["female_pngs_preserved"], 24)
        self.assertEqual(INSTALLER.verify_completed(self.selection), report)
        self.assertFalse((self.pack / ".male_alignment_install.lock").exists())

    def test_failed_install_rolls_back_and_safe_retry_rejects_changed_source(self) -> None:
        state = INSTALLER.plan(self.selection)
        real_save = INSTALLER.save_atomic
        calls = 0

        def fail_third(path: Path, data: bytes, expected: bytes) -> str:
            nonlocal calls
            calls += 1
            if calls == 3:
                raise OSError("fixture failure")
            return real_save(path, data, expected)

        with mock.patch.object(INSTALLER, "save_atomic", side_effect=fail_third):
            with self.assertRaisesRegex(RuntimeError, "fully rolled back"):
                INSTALLER.install(self.selection, state)
        INSTALLER.verify_inventory(state["baseline"])
        INSTALLER.verify_retry_backup(state, INSTALLER.backup_members(state))
        changed = self.pack / "male/eyes/01.png"
        changed.write_bytes(image_bytes((180, 140, 110, 255)))
        changed_state = INSTALLER.plan(self.selection)
        with self.assertRaisesRegex(ValueError, "batch differs"):
            INSTALLER.verify_retry_backup(changed_state, INSTALLER.backup_members(changed_state))
        changed.write_bytes(self.new)
        report = INSTALLER.install(self.selection, INSTALLER.plan(self.selection))
        self.assertEqual(len(report["records"]), 9)

    def test_windows_fallback_and_failed_write_restore_original(self) -> None:
        target = self.root / "windows_fixture.bin"
        target.write_bytes(b"before")
        with mock.patch.object(Path, "replace", side_effect=PermissionError("fixture deny delete")):
            self.assertEqual(INSTALLER.save_atomic(target, b"after", b"before"), "verified_in_place")
        self.assertEqual(target.read_bytes(), b"after")
        target.write_bytes(b"before")
        real_fsync = INSTALLER.os.fsync
        count = 0

        def fail_second_fsync(descriptor: int) -> None:
            nonlocal count
            count += 1
            if count == 2:
                raise OSError("fixture in-place fsync failure")
            real_fsync(descriptor)

        with mock.patch.object(Path, "replace", side_effect=PermissionError("fixture deny delete")), mock.patch.object(INSTALLER.os, "fsync", side_effect=fail_second_fsync):
            with self.assertRaisesRegex(OSError, "fixture in-place"):
                INSTALLER.save_atomic(target, b"after", b"before")
        self.assertEqual(target.read_bytes(), b"before")

    def test_windows_fallback_preserves_concurrent_bytes(self) -> None:
        target = self.root / "concurrent_fixture.bin"
        target.write_bytes(b"before")

        def concurrent_replace(*_args: object) -> None:
            target.write_bytes(b"foreign")
            raise PermissionError("fixture concurrent reader")

        with mock.patch.object(Path, "replace", side_effect=concurrent_replace):
            with self.assertRaisesRegex(RuntimeError, "Concurrent change"):
                INSTALLER.save_atomic(target, b"after", b"before")
        self.assertEqual(target.read_bytes(), b"foreign")


if __name__ == "__main__":
    unittest.main(verbosity=2)
