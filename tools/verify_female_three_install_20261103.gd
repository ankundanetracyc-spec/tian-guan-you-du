extends SceneTree
var failed := false
var count := 0
func check(ok: bool, message: String) -> void:
    count += 1
    if not ok:
        failed = true
        push_error(message)
func _initialize() -> void:
    call_deferred("verify")
func verify() -> void:
    var mods = root.get_node("AppearanceModCatalog")
    var catalog = root.get_node("PortraitCatalog")
    mods.reload()
    for part in ["hair_front","hair_back","brow","eye","nose","mouth"]:
        var entries: Array = mods.get_parts("female",part)
        check(entries.size()==3,part+" count")
        for entry in entries:
            check(entry.variant_index in [0,1,2],part+" variant")
            check(ResourceLoader.exists(entry.resource_path),part+" loads")
        for seed_value in [1,42,31415]:
            var context = {"gender":"female","seed":seed_value,"age":40}
            for requested in ["0","1","2","3","7","female_back_hair_07","female_eyes_03"]:
                var result: Dictionary = catalog.choose_part(context,part,requested)
                check(entries.has(result),part+" legacy fallback "+requested)
                check(result==catalog.choose_part(context,part,requested),part+" deterministic")
    var faces: Array = mods.get_parts("female","face")
    check(faces.size()==1 and faces[0].resource_path.ends_with("/04.png"),"face04 retained")
    var scene=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    var gender: OptionButton=scene.find_child("GenderChoice",true,false)
    gender.select(1)
    gender.emit_signal("item_selected",1)
    for part in ["hair_front","hair_back","eyebrows","eyes","nose","mouth"]:
        var choice: OptionButton=scene.find_child("Variant_"+part,true,false)
        check(choice.item_count==3,"UI "+part)
        for i in 3:
            choice.select(i)
            choice.emit_signal("item_selected",i)
            check(scene.find_child("PortraitPreview",true,false).texture!=null,"compose "+part)
    var front: OptionButton=scene.find_child("Variant_hair_front",true,false)
    var back: OptionButton=scene.find_child("Variant_hair_back",true,false)
    for f in 3:
        for b in 3:
            front.select(f)
            front.emit_signal("item_selected",f)
            back.select(b)
            back.emit_signal("item_selected",b)
            check(scene.find_child("PortraitPreview",true,false).texture!=null,"hair mix")
    scene.find_child("RandomizeParts",true,false).emit_signal("pressed")
    scene.find_child("ResetVariants",true,false).emit_signal("pressed")
    scene.find_child("ReferenceFemaleFace",true,false).emit_signal("pressed")
    check(scene.find_child("PortraitPreview",true,false).texture!=null,"reference")
    scene.find_child("PortraitPreview",true,false).texture.get_image().save_png("res://mods/appearance/portrait_rebuild_20261103/three_style_parts/installation/game_composite.png")
    gender.select(0)
    gender.emit_signal("item_selected",0)
    check(scene.find_child("PortraitPreview",true,false).texture!=null,"male retained")
    var report={"pass":not failed,"checks":count,"female_parts":18,"legacy_fallback":true,"hair_combinations":9,"real_preview_scene":true}
    var file=FileAccess.open("res://mods/appearance/portrait_rebuild_20261103/three_style_parts/installation/runtime_check.json",FileAccess.WRITE)
    file.store_string(JSON.stringify(report,"  "))
    file.close()
    print("FEMALE_THREE_INSTALL: ",JSON.stringify(report))
    scene.queue_free()
    await process_frame
    quit(1 if failed else 0)
