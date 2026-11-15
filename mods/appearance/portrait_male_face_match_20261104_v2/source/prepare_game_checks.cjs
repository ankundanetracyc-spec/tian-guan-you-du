const fs=require('fs');
let text=fs.readFileSync('tools/verify_male_reference_20261104.gd','utf8');
text=text.replace('portrait_male_reference_20261104_v1','portrait_male_face_match_20261104_v2');
text=text.replace(/const SKIN_MEASUREMENT_REPORT[\s\S]*?(?=var checks)/,'const REFERENCE03_SKIN_TARGETS := PACK + "qa/reference03_skin_targets.json"\n');
text=text.replace('var skin_alpha_changed_pixels := -1','var face_alpha_changed_pixels := -1').replace('var skin_bbox_unchanged := false','var face_candidate_pixels_match := false').replace('var ear_occlusion: Dictionary = {}\n','');
const faceCheck=`func verify_face_match(report: Dictionary) -> void:
	var candidate := load_image(PACK + "male/face_shape/01.png")
	var current := load_image(ACTIVE + "male/face_shape/01.png")
	check(candidate.get_size() == Vector2i(1024, 1536), "Candidate replacement face canvas")
	check(current.get_size() == candidate.get_size(), "Installed face has candidate canvas")
	face_candidate_pixels_match = current.get_data() == candidate.get_data()
	check(face_candidate_pixels_match, "Installed complete face pixels exactly match new candidate")
	check(not current.is_invisible(), "Replacement face is visible")
	check(int(report.skin_reference_column) == 3, "Third reference face is the specified skin material target")
	check(FileAccess.file_exists(REFERENCE03_SKIN_TARGETS), "Third-face skin target measurements exist")
	if FileAccess.file_exists(REFERENCE03_SKIN_TARGETS):
		check(JSON.parse_string(FileAccess.get_file_as_string(REFERENCE03_SKIN_TARGETS)) is Dictionary, "Third-face skin target report is valid JSON")
	# Skin geometry is allowed to change. Record alpha differences for review only.
	var before_file := PACK + "source/face_before.png"
	if FileAccess.file_exists(before_file):
		var before := load_image(before_file)
		if before.get_size() == current.get_size():
			var old_data: PackedByteArray = before.get_data()
			var new_data: PackedByteArray = current.get_data()
			face_alpha_changed_pixels = 0
			for index in range(3, old_data.size(), 4):
				if old_data[index] != new_data[index]:
					face_alpha_changed_pixels += 1


`;
text=text.replace(/func alpha_bbox[\s\S]*?(?=func verify_retired_styles)/,faceCheck);
text=text.replace(/func ear_patch_measurement[\s\S]*?(?=func verify\(\))/,'');
text=text.replace('report.records.size() == 14, "Six male hair, seven features and one face registered"','report.records.size() == 8, "Seven revised male features and one complete replacement face registered"');
text=text.replace('report.baseline_sha256.size() == 30, "22 female PNGs, seven male clothes and one empty male accessory preserved"','report.baseline_sha256.size() == 36, "22 female PNGs, six male hair, seven male clothes and empty accessory preserved"');
text=text.replace('verify_skin_geometry()\n\tmeasure_ear_layer_diagnostics()','verify_face_match(report)\n\tcheck(FileAccess.get_sha256(ACTIVE + "appearance_parts.json") == str(report.config_sha256), "Appearance configuration bytes preserved")\n\tcheck(FileAccess.get_sha256(str(report.reference_resource_path)) == str(report.reference_sha256), "Specified reference image unchanged")');
const canvasCheck='\t\tcheck(image.get_size() == Vector2i(1024, 1536) and not image.is_invisible(), "Visible male RGBA part " + str(record.resource_path))';
text=text.replace(canvasCheck,canvasCheck+'\n\t\tcheck(image.get_data() == load_image(str(record.candidate_path)).get_data(), "Installed component pixels match candidate " + str(record.resource_path))');
text=text.replace('for part in [0, 1, 2, 3, 4, 5]:','for part in [0, 1, 2, 3, 4, 5, 6]:');
text=text.replace('"skin_alpha_changed_pixels": skin_alpha_changed_pixels, "skin_bbox_unchanged": skin_bbox_unchanged, "skin_measurement_report": SKIN_MEASUREMENT_REPORT, "ear_alpha_diagnostics": ear_occlusion','"face_candidate_pixels_match": face_candidate_pixels_match, "face_alpha_changed_pixels_diagnostic": face_alpha_changed_pixels, "face_geometry_may_change": true, "skin_reference_column": 3, "reference03_skin_target_report": REFERENCE03_SKIN_TARGETS, "visual_material_acceptance": "Runtime pixel equality confirms installation and composition, not a visual match to the source skin or feature shape."');
text=text.replace('MALE_REFERENCE_20261104: ','MALE_FACE_MATCH_20261104: ');
fs.writeFileSync('tools/verify_male_face_match_20261104.gd',text);
let preview=fs.readFileSync('tools/preview_male_reference_20261104.gd','utf8').replace('portrait_male_reference_20261104_v1','portrait_male_face_match_20261104_v2');
preview=preview.replace('for part in ["hair_front", "hair_back", "eyebrows"]:\n\t\tvar choice: OptionButton = scene.find_child("Variant_" + part, true, false)\n\t\tchoice.select(1)\n\t\tchoice.emit_signal("item_selected", 1)','for part in ["hair_front", "hair_back", "eyebrows"]:\n\t\tvar choice: OptionButton = scene.find_child("Variant_" + part, true, false)\n\t\tchoice.select(2)\n\t\tchoice.emit_signal("item_selected", 2)\n\tvar eyes: OptionButton = scene.find_child("Variant_eyes", true, false)\n\teyes.select(1)\n\teyes.emit_signal("item_selected", 1)');
preview=preview.replace('MALE_REFERENCE_PREVIEW_CAPTURE: ','MALE_FACE_MATCH_PREVIEW_CAPTURE: ');
fs.writeFileSync('tools/preview_male_face_match_20261104.gd',preview);

