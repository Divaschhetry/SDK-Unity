#!/usr/bin/env python3
"""Validate Unity build-scene paths and GUIDs against tracked assets."""

from __future__ import annotations

import re
import sys
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[2]
SETTINGS_FILE = ROOT / "ProjectSettings" / "EditorBuildSettings.asset"
GUID_PATTERN = re.compile(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$")


def error(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)


def read_scene_entries() -> list[dict[str, str | int]]:
    if not SETTINGS_FILE.is_file():
        raise ValueError(f"Missing {SETTINGS_FILE.relative_to(ROOT)}")

    lines = SETTINGS_FILE.read_text(encoding="utf-8").splitlines()
    scene_header = next(
        (index for index, line in enumerate(lines) if line.strip() == "m_Scenes:"),
        None,
    )
    if scene_header is None:
        raise ValueError("EditorBuildSettings.asset has no m_Scenes section")

    entries: list[dict[str, str | int]] = []
    current: dict[str, str | int] | None = None

    def finish_entry() -> None:
        nonlocal current
        if current is None:
            return
        missing = {"enabled", "path", "guid"} - current.keys()
        if missing:
            missing_names = ", ".join(sorted(missing))
            raise ValueError(
                f"Scene entry at line {current['line']} is missing: {missing_names}"
            )
        entries.append(current)
        current = None

    for line_number, line in enumerate(
        lines[scene_header + 1 :], start=scene_header + 2
    ):
        if not line.strip():
            continue

        # A key at the m_Scenes indentation ends the scene list.
        if (
            line.startswith("  ")
            and not line.startswith("    ")
            and not line.startswith("  - ")
        ):
            break

        if line.startswith("  - "):
            finish_entry()
            match = re.fullmatch(r"  - enabled:\s*([01])\s*", line)
            if match is None:
                raise ValueError(
                    f"Invalid scene entry at line {line_number}: expected enabled: 0 or 1"
                )
            current = {"enabled": match.group(1), "line": line_number}
            continue

        if current is None or not line.startswith("    "):
            raise ValueError(f"Unexpected content in m_Scenes at line {line_number}")

        key, separator, value = line.strip().partition(":")
        if not separator:
            raise ValueError(f"Invalid scene field at line {line_number}")
        if key in {"path", "guid"}:
            if key in current:
                raise ValueError(f"Duplicate {key} in scene entry at line {line_number}")
            current[key] = value.strip()

    finish_entry()
    if not entries:
        raise ValueError("EditorBuildSettings.asset contains no build scenes")
    return entries


def validate() -> int:
    entries = read_scene_entries()
    seen_paths: set[str] = set()
    seen_guids: set[str] = set()
    enabled_count = 0

    for entry in entries:
        line_number = int(entry["line"])
        scene_path_text = str(entry["path"])
        expected_guid = str(entry["guid"]).lower()

        if scene_path_text in seen_paths:
            raise ValueError(f"Duplicate build scene path: {scene_path_text}")
        if expected_guid in seen_guids:
            raise ValueError(f"Duplicate build scene GUID: {expected_guid}")
        seen_paths.add(scene_path_text)
        seen_guids.add(expected_guid)

        scene_path = PurePosixPath(scene_path_text)
        if scene_path.is_absolute() or ".." in scene_path.parts:
            raise ValueError(
                f"Scene entry at line {line_number} must use a repository-relative path"
            )
        if scene_path.suffix != ".unity":
            raise ValueError(
                f"Scene entry at line {line_number} is not a .unity file: {scene_path_text}"
            )

        scene_file = ROOT.joinpath(*scene_path.parts)
        resolved_scene = scene_file.resolve()
        try:
            resolved_scene.relative_to(ROOT.resolve())
        except ValueError as exc:
            raise ValueError(
                f"Scene entry at line {line_number} resolves outside the repository"
            ) from exc
        if not scene_file.is_file():
            raise ValueError(f"Build scene does not exist: {scene_path_text}")

        meta_file = Path(f"{scene_file}.meta")
        if not meta_file.is_file():
            raise ValueError(
                f"Build scene is missing its Unity metadata: {meta_file.relative_to(ROOT)}"
            )
        meta_text = meta_file.read_text(encoding="utf-8")
        matches = GUID_PATTERN.findall(meta_text)
        if len(matches) != 1:
            raise ValueError(f"Expected one GUID in {meta_file.relative_to(ROOT)}")
        actual_guid = matches[0].lower()
        if actual_guid != expected_guid:
            raise ValueError(
                f"GUID mismatch for {scene_path_text}: settings has {expected_guid}, "
                f"metadata has {actual_guid}"
            )

        enabled = str(entry["enabled"]) == "1"
        enabled_count += int(enabled)
        state = "enabled" if enabled else "disabled"
        print(f"OK: {state} scene {scene_path_text} ({expected_guid})")

    print(f"Validated {len(entries)} build scene(s); {enabled_count} enabled.")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(validate())
    except (OSError, ValueError) as exc:
        error(str(exc))
        sys.exit(1)