extends SceneTree
var failed := false
var checks := 0
func check(ok: bool, message: String) -> void:
    checks += 1
    if not ok:
        failed = true
        push_error(message)
func _initialize() -> void:
    call_deferred("verify")
func verify() -> void:
    var mods=root.get_node("AppearanceModCatalog")
    var portraits=root.get_node("PortraitCatalog")
    mods.reload()
    var scene=load("res://tscn/scene/CharacterAppearanceTest.tscn").instantiate()
    root.add_child(scene)
    await process_frame
    var gender_choice: OptionButton=scene.find_child("GenderChoice",true,false)
    for gender_index in 2:
        var gender="male" if gender_index==0 else "female"
        var entries: Array=mods.get_parts(gender,"clothes")
        check(entries.size()==7,gender+" clothes count")
        gender_choice.select(gender_index)
        gender_choice.emit_signal("item_selected",gender_index)
        var options: OptionButton=scene.find_child("Variant_clothing",true,false)
        check(options.item_count==7,gender+" UI count")
        for i in 7:
            var entry: Dictionary=portraits.choose_part({"gender":gender,"age":40,"seed":42},"clothes",str(i))
            check(entry.variant_index==i,gender+" stable saved index")
            check(entry.has("item_path") and ResourceLoader.exists(entry.item_path),gender+" equipment")
            var path: String=entry.resource_path
            var texture=load(path) as Texture2D
            check(texture!=null and texture.get_width()==1024 and texture.get_height()==1536,gender+" texture")
            var installed=Image.load_from_file(path)
            var candidate=Image.load_from_file("res://mods/appearance/clothing_polish_20261103/final/"+gender+"/clothing/%02d.png"%(i+1))
            check(installed.get_data()==candidate.get_data(),gender+" candidate pixels")
            options.select(i)
            options.emit_signal("item_selected",i)
            var preview=scene.find_child("PortraitPreview",true,false).texture
            check(preview!=null,gender+" composite")
            preview.get_image().save_png("res://mods/appearance/clothing_polish_20261103/installation/"+gender+"_%02d.png"%(i+1))
        scene.find_child("RandomizeParts",true,false).emit_signal("pressed")
        scene.find_child("ResetVariants",true,false).emit_signal("pressed")
        check(scene.find_child("PortraitPreview",true,false).texture!=null,"random/reset")
    var report={"pass":not failed,"checks":checks,"clothing_count":14,"equipment_and_saved_indices":true,"runtime_composites":14}
    var file=FileAccess.open("res://mods/appearance/clothing_polish_20261103/installation/runtime_check.json",FileAccess.WRITE)
    file.store_string(JSON.stringify(report,"  "))
    file.close()
    print("ALL_CLOTHING_INSTALL: ",JSON.stringify(report))
    scene.queue_free()
    await process_frame
    quit(1 if failed else 0)
