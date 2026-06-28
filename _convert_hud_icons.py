#!/usr/bin/env python3
"""
Convert HUD Title TextMeshProUGUI components to UnityEngine.UI.Image components
with the appropriate resource sprite icons.

Approach: Find each Title GameObject by name, then find its TMP_Text MonoBehaviour
via the m_GameObject reference, then replace it with an Image MonoBehaviour.
"""
import re
from pathlib import Path

SCENE = Path(r"C:\Users\ADMIN\Desktop\AGU301_G2\Assets\Scenes\SampleScene.unity")

# (Title name, sprite_guid, sprite_fileid)
TITLES = [
    ("WoodTitle",         "8fb03c1bcdf9a4a44938fd51db1d3e45", "-2067898546876541913"),  # Wood
    ("StoneTitle",        "958c1849b0e3c2c42a7a2ea85d18fa54", "-2383031484441755576"),  # Stone
    ("IronTitle",         "1075e0c847a3f4c4ba021012fc6e75d0", "21300000"),             # Iron
    ("CopperTitle",       "82ce56146912ca743a51ad603ac3dd30", "21300000"),             # Copper
    ("BlueGemTitle",      "a99e5f70e6b2f8048ab7fdbebf6b699b", "21300000"),             # BlueGem
    ("PurpleGemTitle",    "e1419ec5a39a6ee48887e9d7e3c63f49", "21300000"),             # PurpleGem
    ("CoinTitle",         "d0e58bc06bf07d047b59446530740c04", "21300000"),             # Coin (use token sprite)
    ("TokenTitle",        "d0e58bc06bf07d047b59446530740c04", "21300000"),             # Token
    ("MiningSkillTitle",  "435a6138bbd19c244b9169e6837be6e3", "21300000"),             # MiningSkill (Icon1)
    ("ForgaingSkillTitle","800689b8fe29d634e8f318e2622aa390", "21300000"),             # ForgingSkill (Icon7)
]


def make_image_block(file_id: str, game_object_id: str, sprite_guid: str, sprite_fileid: str) -> str:
    return f"""--- !u!114 &{file_id}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {game_object_id}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: fe87c0e1cc204ed48ad3b37840f39efc, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: UnityEngine.UI::UnityEngine.UI.Image
  m_Material: {{fileID: 0}}
  m_Color: {{r: 1, g: 1, b: 1, a: 1}}
  m_RaycastTarget: 0
  m_RaycastPadding: {{x: 0, y: 0, z: 0, w: 0}}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: {{fileID: {sprite_fileid}, guid: {sprite_guid}, type: 3}}
  m_Type: 0
  m_PreserveAspect: 1
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: 1"""


def find_gameobject_id(lines: list, name: str) -> str:
    """Find the GameObject fileID for a given GameObject name."""
    pattern = re.compile(rf"--- !u!1 &(\d+)\nGameObject:.*?m_Name: {re.escape(name)}\n", re.DOTALL)
    for i, line in enumerate(lines):
        if line.startswith("--- !u!1 &"):
            # Look ahead to see if m_Name matches
            block_start = i
            for j in range(i, min(i + 30, len(lines))):
                if lines[j].startswith("--- !u!"):
                    if j != i:
                        break
                if re.match(rf"\s+m_Name: {re.escape(name)}\s*$", lines[j]):
                    m = re.match(r"--- !u!1 &(\d+)", lines[i])
                    if m:
                        return m.group(1)
    raise SystemExit(f"Could not find GameObject for {name}")


def find_recttransform_id_for_gameobject(lines: list, game_object_id: str) -> str:
    """Find the RectTransform fileID whose m_GameObject matches the given GameObject ID."""
    for i, line in enumerate(lines):
        if line.startswith("--- !u!224 &"):
            m = re.match(r"--- !u!224 &(\d+)", line)
            if not m:
                continue
            # Look ahead for m_GameObject
            for j in range(i + 1, min(i + 15, len(lines))):
                if lines[j].startswith("--- !u!"):
                    break
                mo = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\s*\}", lines[j])
                if mo and mo.group(1) == game_object_id:
                    return m.group(1)
    raise SystemExit(f"Could not find RectTransform for gameObject {game_object_id}")


def find_tmp_text_id_for_gameobject(lines: list, game_object_id: str) -> str:
    """Find the TMP_Text MonoBehaviour fileID whose m_GameObject matches the given GameObject ID."""
    for i, line in enumerate(lines):
        if line.startswith("--- !u!114 &"):
            m = re.match(r"--- !u!114 &(\d+)", line)
            if not m:
                continue
            # Look ahead for TMP_Text marker
            found_gameobject = None
            is_tmp = False
            for j in range(i + 1, min(i + 15, len(lines))):
                if lines[j].startswith("--- !u!"):
                    break
                mo = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\s*\}", lines[j])
                if mo:
                    found_gameobject = mo.group(1)
                if "TMPro.TextMeshProUGUI" in lines[j]:
                    is_tmp = True
            if is_tmp and found_gameobject == game_object_id:
                return m.group(1)
    raise SystemExit(f"Could not find TMP_Text for gameObject {game_object_id}")


def replace_block(lines: list, start_marker: str, end_pred, new_block: str) -> list:
    """Replace lines[start_idx:end_idx] with new_block. end_pred(line, idx) returns True when end reached."""
    start_idx = None
    for i, line in enumerate(lines):
        if line == start_marker:
            start_idx = i
            break
    if start_idx is None:
        raise SystemExit(f"start marker not found: {start_marker!r}")
    end_idx = None
    for j in range(start_idx + 1, len(lines)):
        if end_pred(lines[j], j):
            end_idx = j
            break
    if end_idx is None:
        raise SystemExit(f"end marker not found after {start_marker!r}")
    return lines[:start_idx] + [new_block + "\n"] + lines[end_idx:]


def is_block_start(line: str, _idx: int = 0) -> bool:
    return line.startswith("--- !u!")


def main() -> None:
    text = SCENE.read_text(encoding="utf-8")
    lines = text.splitlines(keepends=True)

    # First pass: gather (title, gameobject_id, rect_id, tmp_id)
    title_info = []
    for name, _, _ in TITLES:
        go_id = find_gameobject_id(lines, name)
        rect_id = find_recttransform_id_for_gameobject(lines, go_id)
        tmp_id = find_tmp_text_id_for_gameobject(lines, go_id)
        print(f"[INFO] {name}: gameObject={go_id} rect={rect_id} tmp={tmp_id}")
        title_info.append((name, go_id, rect_id, tmp_id))

    # Second pass: replace TMP_Text with Image
    for name, go_id, rect_id, tmp_id in title_info:
        sprite_guid = next(t[1] for t in TITLES if t[0] == name)
        sprite_fileid = next(t[2] for t in TITLES if t[0] == name)

        new_block = make_image_block(tmp_id, go_id, sprite_guid, sprite_fileid)
        start_marker = f"--- !u!114 &{tmp_id}\n"
        lines = replace_block(lines, start_marker, is_block_start, new_block)
        print(f"[OK] Replaced TMP_Text for {name} (fileID {tmp_id}) with Image using sprite {sprite_guid}")

    # Third pass: shrink RectTransform size from 180x28 to 32x32
    for name, go_id, rect_id, tmp_id in title_info:
        start_marker = f"--- !u!224 &{rect_id}\n"
        # Find the rect block
        start_idx = None
        for i, line in enumerate(lines):
            if line == start_marker:
                start_idx = i
                break
        if start_idx is None:
            print(f"[WARN] Could not find RectTransform {rect_id} for {name}")
            continue
        # Find next '--- !u!' marker
        end_idx = None
        for j in range(start_idx + 1, len(lines)):
            if lines[j].startswith("--- !u!"):
                end_idx = j
                break
        # Find m_SizeDelta and replace
        changed = False
        for j in range(start_idx, end_idx):
            if "m_SizeDelta:" in lines[j] and "{x: 180, y: 28}" in lines[j]:
                lines[j] = "  m_SizeDelta: {x: 32, y: 32}\n"
                changed = True
                break
        if changed:
            print(f"[OK] Shrunk RectTransform {rect_id} for {name} to 32x32")
        else:
            print(f"[WARN] Could not find m_SizeDelta 180x28 in RectTransform {rect_id} for {name}")

    SCENE.write_text("".join(lines), encoding="utf-8")
    print(f"\n[DONE] Wrote {SCENE}")


if __name__ == "__main__":
    main()
