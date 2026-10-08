#!/usr/bin/env python3
"""Update equipment system from 3 slots to 5 slots."""
import os

ROOT = "E:/stacklands"

def main():
    # ===== 1. Update human_data.gd =====
    path = os.path.join(ROOT, "gdscripts/resources/human_data.gd")
    with open(path, 'rb') as f:
        data = f.read()

    old = b'@export var equipment_paths: Array[String] = []'
    idx = data.find(old)
    if idx >= 0:
        comment_start = data.rfind(b'\n', 0, idx)
        new = b'\n@export var weapon_path: String = ""\n@export var clothes_path: String = ""\n@export var ornament_1_path: String = ""\n@export var ornament_2_path: String = ""\n@export var vehicle_path: String = ""\n'
        data = data[:comment_start] + new + data[idx + len(old):]
        with open(path, 'wb') as f:
            f.write(data)
        print("Updated human_data.gd")
    else:
        print("human_data.gd: pattern not found")

    # ===== 2. Update character.gd =====
    path = os.path.join(ROOT, "gdscripts/entity/character.gd")
    with open(path, 'rb') as f:
        data = f.read()

    # 2a. EquipSlot enum
    old = b'enum EquipSlot {\n\tWEAPON = 0,\n\tARMOR = 1,\n\tACCESSORY = 2,\n}'
    new = b'enum EquipSlot {\n\tWEAPON = 0,\n\tCLOTHES = 1,\n\tORNAMENT_1 = 2,\n\tORNAMENT_2 = 3,\n\tVEHICLE = 4,\n}'
    if old in data:
        data = data.replace(old, new)
        print("Updated EquipSlot enum")
    else:
        print("EquipSlot enum not found")

    # 2b. EQUIP_SLOT_NAMES
    old = b'const EQUIP_SLOT_NAMES := {\n\tEquipSlot.WEAPON: "\xe6\xad\xa6\xe5\x99\xa8",\n\tEquipSlot.ARMOR: "\xe9\x98\xb2\xe5\x85\xb7",\n\tEquipSlot.ACCESSORY: "\xe9\xa5\xb0\xe5\x93\x81",\n}'
    new = b'const EQUIP_SLOT_NAMES := {\n\tEquipSlot.WEAPON: "\xe6\xad\xa6\xe5\x99\xa8",\n\tEquipSlot.CLOTHES: "\xe8\xa1\xa3\xe8\xa3\x85",\n\tEquipSlot.ORNAMENT_1: "\xe9\xa5\xb0\xe7\x89\xa9\xe2\x91\xa0",\n\tEquipSlot.ORNAMENT_2: "\xe9\xa5\xb0\xe7\x89\xa9\xe2\x91\xa1",\n\tEquipSlot.VEHICLE: "\xe5\xbe\xa1\xe5\x99\xa8",\n}'
    if old in data:
        data = data.replace(old, new)
        print("Updated EQUIP_SLOT_NAMES")
    else:
        print("EQUIP_SLOT_NAMES not found")

    # 2c. equipped_items init
    old = b'var equipped_items: Dictionary = {\n\tEquipSlot.WEAPON: null,\n\tEquipSlot.ARMOR: null,\n\tEquipSlot.ACCESSORY: null,\n}'
    new = b'var equipped_items: Dictionary = {\n\tEquipSlot.WEAPON: null,\n\tEquipSlot.CLOTHES: null,\n\tEquipSlot.ORNAMENT_1: null,\n\tEquipSlot.ORNAMENT_2: null,\n\tEquipSlot.VEHICLE: null,\n}'
    if old in data:
        data = data.replace(old, new)
        print("Updated equipped_items init")
    else:
        print("equipped_items init not found")

    # 2d. Bounds check
    data = data.replace(b'slot > EquipSlot.ACCESSORY', b'slot > EquipSlot.VEHICLE')
    print("Updated bounds check")

    # 2e. get_equipment_summary iteration
    old = b'for slot in [EquipSlot.WEAPON, EquipSlot.ARMOR, EquipSlot.ACCESSORY]:\n\t\tvar item: Equipment = equipped_items[slot]'
    new = b'for slot in [EquipSlot.WEAPON, EquipSlot.CLOTHES, EquipSlot.ORNAMENT_1, EquipSlot.ORNAMENT_2, EquipSlot.VEHICLE]:\n\t\tvar item: Equipment = equipped_items[slot]'
    if old in data:
        data = data.replace(old, new)
        print("Updated get_equipment_summary iteration")
    else:
        print("get_equipment_summary iteration not found, searching...")
        idx = data.find(b'EquipSlot.WEAPON, EquipSlot.ARMOR, EquipSlot.ACCESSORY')
        if idx >= 0:
            print(f"  Found at byte {idx}")

    # 2f. Save code
    old = b'for slot in [EquipSlot.WEAPON, EquipSlot.ARMOR, EquipSlot.ACCESSORY]:\n\t\tvar item: Equipment = equipped_items.get(slot)\n\t\tif item and is_instance_valid(item):\n\t\t\tvar item_data = item.get_item_data()\n\t\t\tif item_data:\n\t\t\t\tdata.add_child(item_data)\n\t\t\t\titem_data.set_meta("equip_slot", slot)\n\t\t\tvar ep: String = ""\n\t\t\tif item.has_meta("data_path"):\n\t\t\t\tep = item.get_meta("data_path", "")\n\t\t\tif ep == "" and item.entity_data and item.entity_data.resource_path != "":\n\t\t\t\tep = item.entity_data.resource_path\n\t\t\tif ep != "":\n\t\t\t\tif slot >= data.equipment_paths.size():\n\t\t\t\t\tdata.equipment_paths.resize(slot + 1)\n\t\t\t\tdata.equipment_paths[slot] = ep'
    new = b'for slot in [EquipSlot.WEAPON, EquipSlot.CLOTHES, EquipSlot.ORNAMENT_1, EquipSlot.ORNAMENT_2, EquipSlot.VEHICLE]:\n\t\tvar item: Equipment = equipped_items.get(slot)\n\t\tif item and is_instance_valid(item):\n\t\t\tvar item_data = item.get_item_data()\n\t\t\tif item_data:\n\t\t\t\tdata.add_child(item_data)\n\t\t\t\titem_data.set_meta("equip_slot", slot)\n\t\t\tvar ep: String = ""\n\t\t\tif item.has_meta("data_path"):\n\t\t\t\tep = item.get_meta("data_path", "")\n\t\t\tif ep == "" and item.entity_data and item.entity_data.resource_path != "":\n\t\t\t\tep = item.entity_data.resource_path\n\t\t\tif ep != "":\n\t\t\t\tmatch slot:\n\t\t\t\t\tEquipSlot.WEAPON: data.weapon_path = ep\n\t\t\t\t\tEquipSlot.CLOTHES: data.clothes_path = ep\n\t\t\t\t\tEquipSlot.ORNAMENT_1: data.ornament_1_path = ep\n\t\t\t\t\tEquipSlot.ORNAMENT_2: data.ornament_2_path = ep\n\t\t\t\t\tEquipSlot.VEHICLE: data.vehicle_path = ep'
    if old in data:
        data = data.replace(old, new)
        print("Updated save code")
    else:
        print("Save code not found")
        # Try to find it
        idx = data.find(b'for slot in [EquipSlot.WEAPON, EquipSlot.ARMOR, EquipSlot.ACCESSORY]:')
        while idx >= 0:
            print(f"  Found at byte {idx}")
            idx = data.find(b'for slot in [EquipSlot.WEAPON, EquipSlot.ARMOR, EquipSlot.ACCESSORY]:', idx + 1)

    # 2g. Load code #1
    old = b'for slot_key in data.equipment_paths:\n\t\t\tvar ep: String = data.equipment_paths[slot_key]\n\t\t\tif ep == "":\n\t\t\t\tcontinue\n\t\t\tvar idx := int(slot_key)\n\t\t\tif not ResourceLoader.exists(ep):\n\t\t\t\tcontinue\n\t\t\tvar equip_data = ResourceLoader.load(ep, "EquipmentData", ResourceLoader.CACHE_MODE_REUSE)\n\t\t\tif not equip_data:\n\t\t\t\tcontinue\n\t\t\tif equipped_items.has(idx) and equipped_items[idx] == null:\n\t\t\t\tvar equip := Equipment.new()\n\t\t\t\tequip.load_from_data(equip_data)\n\t\t\t\tadd_child(equip)\n\t\t\t\tequip.visible = false\n\t\t\t\tequip.process_mode = Node.PROCESS_MODE_DISABLED\n\t\t\t\tequipped_items[idx] = equip'
    new = b'for slot_field in ["weapon_path", "clothes_path", "ornament_1_path", "ornament_2_path", "vehicle_path"]:\n\t\t\tvar ep: String = data.get(slot_field, "")\n\t\t\tif ep == "":\n\t\t\t\tcontinue\n\t\t\tif not ResourceLoader.exists(ep):\n\t\t\t\tcontinue\n\t\t\tvar equip_data = ResourceLoader.load(ep, "EquipmentData", ResourceLoader.CACHE_MODE_REUSE)\n\t\t\tif not equip_data:\n\t\t\t\tcontinue\n\t\t\tvar slot: int\n\t\t\tmatch slot_field:\n\t\t\t\t"weapon_path": slot = EquipSlot.WEAPON\n\t\t\t\t"clothes_path": slot = EquipSlot.CLOTHES\n\t\t\t\t"ornament_1_path": slot = EquipSlot.ORNAMENT_1\n\t\t\t\t"ornament_2_path": slot = EquipSlot.ORNAMENT_2\n\t\t\t\t"vehicle_path": slot = EquipSlot.VEHICLE\n\t\t\t\t_: continue\n\t\t\tif equipped_items.has(slot) and equipped_items[slot] == null:\n\t\t\t\tvar equip := Equipment.new()\n\t\t\t\tequip.load_from_data(equip_data)\n\t\t\t\tadd_child(equip)\n\t\t\t\tequip.visible = false\n\t\t\t\tequip.process_mode = Node.PROCESS_MODE_DISABLED\n\t\t\t\tequipped_items[slot] = equip'
    if old in data:
        data = data.replace(old, new)
        print("Updated load code #1")
    else:
        print("Load code #1 not found")
        # Try to find partial match
        idx = data.find(b'for slot_key in data.equipment_paths')
        if idx >= 0:
            print(f"  Found at byte {idx}")

    # 2h. Full init code (equipped_items reset)
    old = b'EquipSlot.WEAPON: null,\n\t\tEquipSlot.ARMOR: null,\n\t\tEquipSlot.ACCESSORY: null,'
    new = b'EquipSlot.WEAPON: null,\n\t\tEquipSlot.CLOTHES: null,\n\t\tEquipSlot.ORNAMENT_1: null,\n\t\tEquipSlot.ORNAMENT_2: null,\n\t\tEquipSlot.VEHICLE: null,'
    if old in data:
        data = data.replace(old, new)
        print("Updated init reset")
    else:
        print("Init reset not found")

    with open(path, 'wb') as f:
        f.write(data)
    print("Done character.gd")

if __name__ == '__main__':
    main()
