"""Verify the nine manually placed Windgrove FBX roots without editing the scene.

Run from repository root:
    python Tools/WindgroveShoal/verify_scene.py
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
scene = (root / "Assets/Scenes/PrototypeWorld.unity").read_text(encoding="utf-8")
guid = "1fbed736ce4cf7971772fd376187c561"
docs = re.split(r"(?=^--- !u!)", scene, flags=re.MULTILINE)

turbines = []
for block in docs:
    if not block.startswith("--- !u!1001 "):
        continue
    if f"m_SourcePrefab: {{fileID: 100100000, guid: {guid}, type: 3}}" not in block:
        continue
    match = re.search(r"propertyPath: m_Name\s+value: (WindTurbine(?: \(\d+\))?)\s", block)
    if not match:
        continue
    position = {}
    scale = {}
    for axis in "xyz":
        value = re.search(r"propertyPath: m_LocalPosition\." + axis + r"\s+value: ([-\d.eE]+)", block)
        if value:
            position[axis] = float(value.group(1))
        value = re.search(r"propertyPath: m_LocalScale\." + axis + r"\s+value: ([-\d.eE]+)", block)
        if value:
            scale[axis] = float(value.group(1))
    assert len(position) == 3 and len(scale) == 3, match.group(1)
    assert all(value > 0 for value in scale.values()), match.group(1)
    turbines.append((match.group(1), position, scale))

assert len(turbines) == 9, f"Expected nine authored turbines; found {len(turbines)}"
northmost = max(turbines, key=lambda t: t[1]["z"])
zone = (root / "Assets/_Game/Scripts/Islands/WindgroveShoalZone.cs").read_text()
assert "Radius = 150f;" in zone
assert "water.BaseWaterLevel + 3f" in zone
assert "northern.position.z + 9f" in zone
assert 'Id = "windgrove-shoal"' in zone
assert (root / "Assets/_Game/Scripts/Islands/WindgroveShoalDiscovery.cs").exists()

print(
    "PASS: nine authored turbine prefabs, correct 150m zone/discovery, "
    f"northmost {northmost[0]} at {northmost[1]}, "
    "teleport at sea +3m. Scene was not changed."
)
