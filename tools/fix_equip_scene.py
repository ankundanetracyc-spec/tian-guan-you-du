#!/usr/bin/env python3
"""Fix duplicated EquipContainer in InfoPanel.tscn and insert proper nodes."""
import os

PATH = "E:/stacklands/tscn/panel/InfoPanel.tscn"

with open(PATH, 'rb') as f:
    content = f.read()

# Find ALL occurrences of EquipContainer
idx1 = content.find(b'[node name="EquipContainer"')
idx2 = content.find(b'[node name="EquipContainer"', idx1 + 50)

print(f"First EquipContainer at: {idx1}")
print(f"Second EquipContainer at: {idx2}")

# Find the next sibling after the SECOND EquipContainer
# Look for next [node at same hierarchy level
next_sib = content.find(b'\n[node', idx2 + 10)
# Adjust for the \n before [node
if next_sib > 0:
    next_sib += 1  # include the newline
else:
    next_sib = content.find(b'[node', idx2 + 50)
    # Walk forward to find one that's NOT a child of EquipContainer
    while next_sib > 0:
        eol = content.find(b'\n', next_sib)
        parent_line = content[next_sib:eol]
        if b'ScrollBody/ContentPanel/EquipContainer' not in parent_line:
            break
        next_sib = content.find(b'[node', next_sib + 10)

# Build the replacement nodes
new = []
new.append(b'[node name="EquipContainer" type="ScrollContainer" parent="ScrollBody/ContentPanel" unique_id=1554373122]')
new.append(b'unique_name_in_owner = true')
new.append(b'visible = false')
new.append(b'layout_mode = 1')
new.append(b'anchors_preset = 15')
new.append(b'anchor_right = 1.0')
new.append(b'anchor_bottom = 1.0')
new.append(b'offset_left = -712.0')
new.append(b'offset_top = -8.0')
new.append(b'offset_right = -388.0')
new.append(b'offset_bottom = -78.0')
new.append(b'grow_horizontal = 2')
new.append(b'grow_vertical = 2')
new.append(b'horizontal_scroll_mode = 0')
new.append(b'')
new.append(b'[node name="EquipContent" type="VBoxContainer" parent="ScrollBody/ContentPanel/EquipContainer"]')
new.append(b'layout_mode = 2')
new.append(b'size_flags_horizontal = 3')
new.append(b'size_flags_vertical = 3')
new.append(b'theme_override_constants/separation = 6')
new.append(b'')
new.append(b'# Equipment section')
new.append(b'[node name="EquipTitle" type="Label" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'theme_override_colors/font_color = Color(0.3, 0.25, 0.18, 1)')
new.append(b'theme_override_font_sizes/font_size = 15')
new.append(b'text = "\u2014 \u88c5\u5907 \u2014"')
new.append(b'horizontal_alignment = 1')
new.append(b'')
new.append(b'[node name="EquipSlots" type="HBoxContainer" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'size_flags_vertical = 3')
new.append(b'theme_override_constants/separation = 4')
new.append(b'alignment = 1')
new.append(b'')
new.append(b'[node name="WeaponSlot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]')
new.append(b'layout_mode = 2')
new.append(b'[node name="ClothesSlot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]')
new.append(b'layout_mode = 2')
new.append(b'[node name="Ornament1Slot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]')
new.append(b'layout_mode = 2')
new.append(b'[node name="Ornament2Slot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]')
new.append(b'layout_mode = 2')
new.append(b'[node name="VehicleSlot" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/EquipSlots" instance=ExtResource("8_packslot")]')
new.append(b'layout_mode = 2')
new.append(b'')
new.append(b'[node name="EquipSep" type="HSeparator" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'')
new.append(b'# Battle prep section')
new.append(b'[node name="PrepTitle" type="Label" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'theme_override_colors/font_color = Color(0.3, 0.25, 0.18, 1)')
new.append(b'theme_override_font_sizes/font_size = 15')
new.append(b'text = "\u2014 \u6218\u5907 \u2014"')
new.append(b'horizontal_alignment = 1')
new.append(b'')
new.append(b'[node name="PrepGrid" type="GridContainer" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'size_flags_vertical = 3')
new.append(b'theme_override_constants/h_separation = 4')
new.append(b'theme_override_constants/v_separation = 4')
new.append(b'columns = 3')
new.append(b'alignment = 1')
new.append(b'')
for i in range(9):
    new.append(f'[node name="PrepSlot{i}" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PrepGrid" instance=ExtResource("8_packslot")]'.encode())
    new.append(b'layout_mode = 2')
new.append(b'')
new.append(b'[node name="PackSep" type="HSeparator" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'')
new.append(b'# Backpack section')
new.append(b'[node name="PackTitle" type="Label" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'theme_override_colors/font_color = Color(0.3, 0.25, 0.18, 1)')
new.append(b'theme_override_font_sizes/font_size = 15')
new.append(b'text = "\u2014 \u884c\u56ca \u2014"')
new.append(b'horizontal_alignment = 1')
new.append(b'')
new.append(b'[node name="PackGrid" type="GridContainer" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent"]')
new.append(b'layout_mode = 2')
new.append(b'size_flags_vertical = 3')
new.append(b'theme_override_constants/h_separation = 4')
new.append(b'theme_override_constants/v_separation = 4')
new.append(b'columns = 5')
new.append(b'alignment = 1')
new.append(b'')
for i in range(20):
    new.append(f'[node name="PackSlot{i}" parent="ScrollBody/ContentPanel/EquipContainer/EquipContent/PackGrid" instance=ExtResource("8_packslot")]'.encode())
    new.append(b'layout_mode = 2')

new_content = b'\n'.join(new)

# Replace from idx1 to next_sib
print(f"Replacing from byte {idx1} to {next_sib}")
print(f"Old length: {next_sib - idx1}, New length: {len(new_content)}")

content = content[:idx1] + new_content + content[next_sib:]

with open(PATH, 'wb') as f:
    f.write(content)

print(f"Done. File size: {len(content)} bytes")
print(f"Nodes: 5 equip + 9 prep + 20 pack = 34 slots")
