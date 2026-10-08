extends SceneTree

func _initialize():
	call_deferred("render_cards")

func render_cards():
	root.size = Vector2i(1600, 840)
	var background = ColorRect.new()
	background.color = Color("292d2c")
	background.size = Vector2(1600, 840)
	root.add_child(background)
	var entries = JSON.parse_string(FileAccess.get_file_as_string("res://outputs/card_icons_expressive/manifest.json"))
	var scene = load("res://tscn/panel/BattleCard.tscn")
	var index = 0
	for entry in entries:
		var skill = load("res://resources/activeskills/" + entry.name + ".tres")
		var card = scene.instantiate()
		root.add_child(card)
		card.position = Vector2(40 + (index % 6) * 258, 35 + (index / 6) * 395)
		card.setup(skill, null, index)
		index += 1
	await process_frame
	await process_frame
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png("res://outputs/card_icons_expressive/cards-preview.png")
	print("CARD_ART_PREVIEW: 12 cards rendered")
	quit()
