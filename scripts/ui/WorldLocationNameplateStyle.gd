extends RefCounted

# 大世界地点与宗门峰名共用纸签及九宫格边角，不另造近似底板。
const PAPER_TEXTURE: Texture2D = preload("res://textures/world/bai_chuan/acceptance_20261030/nameplates/blank_48x152.png")

static func create(highlighted: bool = false, texture: Texture2D = null) -> StyleBoxTexture:
	var style := StyleBoxTexture.new()
	style.texture = texture if texture != null else PAPER_TEXTURE
	style.texture_margin_left = 7
	style.texture_margin_right = 7
	style.texture_margin_top = 10
	style.texture_margin_bottom = 10
	style.modulate_color = Color(1.04, 1.01, 0.94) if highlighted else Color.WHITE
	style.content_margin_left = 5
	style.content_margin_right = 5
	style.content_margin_top = 6
	style.content_margin_bottom = 6
	return style
