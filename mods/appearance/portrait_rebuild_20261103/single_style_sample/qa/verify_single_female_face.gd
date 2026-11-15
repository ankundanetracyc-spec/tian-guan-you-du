extends SceneTree
var failed := false
func check(value: bool, message: String) -> void:
    if not value:
        failed = true
        push_error("FEMALE_FACE_CHECK_FAILED: " + message)
func _initialize() -> void:
    call_deferred("verify")
func verify() -> void:
    var mods = root.get_node("AppearanceModCatalog")
    var portraits = root.get_node("PortraitCatalog")
    mods.reload()
    var entries: Array = mods.get_parts("female", "face")
    check(entries.size() == 1, "exactly one female face")
    if entries.size() != 1:
        quit(1)
        return
    var face: Dictionary = entries[0]
    check(face.id == "female_face_03", "stable style id female_face_03")
    check(face.variant_index == 3, "stable saved style 3")
    check(face.display_name == "柔和卵面", "retained name")
    check(face.resource_path.ends_with("female/face_shape/04.png"), "retained PNG 04")
    check(ResourceLoader.exists(face.resource_path), "retained PNG loads")
    var fallback_checks := 0
    for seed_value in [1,42,31415]:
        var context := {"gender":"female", "seed":seed_value,"age":40}
        for request in ["0","1","2","3","female_face_00","female_face_01","female_face_02","female_face_03", ""]:
            var chosen: Dictionary = portraits.choose_part(context,"face",request)
            check(chosen.id == "female_face_03", "old style fallback " + request)
            check(chosen == portraits.choose_part(context,"face",request), "deterministic " + request)
            fallback_checks += 1
    var scene = load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    var gender: OptionButton = scene.find_child("GenderChoice",true,false)
    var selected_count := 0
    for sex_index in [0,1,0,1]:
        gender.select(sex_index)
        gender.emit_signal("item_selected",sex_index)
        var choice: OptionButton = scene.find_child("Variant_face_shape",true,false)
        if sex_index == 1:
            check(choice.item_count == 1, "female UI face count")
            check(choice.get_item_text(0) == "柔和卵面", "female UI label")
        for slot in ["hair_front","hair_back","eyebrows","eyes","nose","mouth","face_shape","clothing","face_accessory"]:
            var options: OptionButton = scene.find_child("Variant_"+slot,true,false)
            for option in options.item_count:
                options.select(option)
                options.emit_signal("item_selected",option)
                selected_count += 1
        scene.find_child("RandomizeParts",true,false).emit_signal("pressed")
        scene.find_child("ResetVariants",true,false).emit_signal("pressed")
        check(scene.find_child("PortraitPreview",true,false).texture != null,"composite texture")
    scene.find_child("ReferenceFemaleFace",true,false).emit_signal("pressed")
    var final_face: OptionButton = scene.find_child("Variant_face_shape",true,false)
    check(final_face.item_count == 1 and final_face.selected == 0,"reference button single retained face")
    check(scene.find_child("PortraitPreview",true,false).texture != null,"reference texture")
    var report := {"pass":not failed,"female_face_id":face.id,"path":face.resource_path,"old_style_fallback_checks":fallback_checks,"ui_selections_exercised":selected_count,"random_reset_reference_gender_switch":true,"headless":true,"scope":"runtime catalogs and real C# preview scene; no visual certification"}
    var out := FileAccess.open("res://mods/appearance/portrait_rebuild_20261103/single_style_sample/qa/female_face_runtime_check.json",FileAccess.WRITE)
    out.store_string(JSON.stringify(report,"  "))
    out.close()
    print("FEMALE_FACE_SINGLE_RUNTIME: ",JSON.stringify(report))
    scene.queue_free()
    await process_frame
    quit(1 if failed else 0)
