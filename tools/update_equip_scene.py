#!/usr/bin/env python3
"""Replace EquipContainer in InfoPanel.tscn with new combined view."""
import os

TSN_PATH = "E:/stacklands/tscn/panel/InfoPanel.tscn"

with open(TSN_PATH, 'rb') as f:
    data = f.read()

# Find the EquipContainer block - from [node name="EquipContainer" ...]
# to the next [node at the same level (parent="ScrollBody/ContentPanel")
start_marker = b'[node name="EquipContainer" type="ScrollContainer" parent="ScrollBody/ContentPanel" unique_id=1554373122]'
idx_start = data.find(start_marker)

# Find the end: next node whose parent is "ScrollBody/ContentPanel"
# after the EquipContainer block
next_parent = b'parent="ScrollBody/ContentPanel"'
idx_end = data.find(next_parent, idx_start + 1)
# Backtrack to the [node line
idx_end = data.rfind(b'[node', 0, idx_end)

if idx_start < 0 or idx_end < 0:
    print("Could not find EquipContainer boundaries")
    exit(1)

print(f"Replacing bytes {idx_start} to {idx_end}")

new_nodes = b'''[node name="EquipContainer" type="ScrollContainer" parent="ScrollBody/ContentPanel" unique_id=1554373122]
unique_name_in_owner = true
visible = false
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
offset_left = -712.0
offset_top = -8.0
offset_right = -388.0
offset_bottom = -78.0
grow_horizontal = 2
grow_vertical = 2
horizontal_scroll_mode = 0

[node name="EquipContent" type="VBoxContainer" parent="ScrollBody/ContentPanel/EquipContainer" unique_id=340000001]
layout_mode = 2
size_flags_horizontal = 3
size_flags_vertical = 3
theme_override_constants/separation = 6

[node name="EquipTitle" type="Label" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2
theme_override_colors/font_color = Color(0.3, 0.25, 0.18, 1)
theme_override_font_sizes/font_size = 14
text = "\xe8\xa3\x85\xe5\xa4\x87"
horizontal_alignment = 1

[node name="EquipSlots" type="HBoxContainer" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2
size_flags_vertical = 3
theme_override_constants/separation = 4
alignment = 1

[node name="WeaponSlot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]
layout_mode = 2

[node name="ClothesSlot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]
layout_mode = 2

[node name="Ornament1Slot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]
layout_mode = 2

[node name="Ornament2Slot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]
layout_mode = 2

[node name="VehicleSlot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]
layout_mode = 2

[node name="EquipSep" type="HSeparator" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2

[node name="PrepTitle" type="Label" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2
theme_override_colors/font_color = Color(0.3, 0.25, 0.18, 1)
theme_override_font_sizes/font_size = 14
text = "\xe6\x88\x98\xe5\xa4\x87"
horizontal_alignment = 1

[node name="PrepGrid" type="GridContainer" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2
size_flags_vertical = 3
theme_override_constants/h_separation = 4
theme_override_constants/v_separation = 4
columns = 3
alignment = 1

[node name="PrepSlot0" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot1" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot2" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot3" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot4" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot5" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot6" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot7" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PrepSlot8" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]
layout_mode = 2

[node name="PackSep" type="HSeparator" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2

[node name="PackTitle" type="Label" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2
theme_override_colors/font_color = Color(0.3, 0.25, 0.18, 1)
theme_override_font_sizes/font_size = 14
text = "\xe8\xa1\x8c\xe8\xa2\x8b"
horizontal_alignment = 1

[node name="PackGrid" type="GridContainer" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]
layout_mode = 2
size_flags_vertical = 3
theme_override_constants/h_separation = 4
theme_override_constants/v_separation = 4
columns = 5
alignment = 1

[node name="PackSlot0" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot1" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot2" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot3" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot4" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot5" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot6" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot7" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot8" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot9" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot10" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot11" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot12" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot13" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot14" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot15" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot16" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot17" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot18" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
[node name="PackSlot19" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]
layout_mode = 2
'''

data = data[:idx_start] + new_nodes + data[idx_end:]
with open(TSN_PATH, 'wb') as f:
    f.write(data)

print(f"Done. Replaced with {len(new_nodes)} bytes of new nodes.")
print("Added: 5 equip slots, 9 prep slots, 20 pack slots")
