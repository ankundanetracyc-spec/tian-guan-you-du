extends RefCounted

# IDs and source paths are the actual active portrait catalog identities.
# These overhead SVG garments are purpose-made representations of those outfits;
# a front-facing portrait is never rotated into a supposed overhead texture.
const PACKAGE := "ink_three_variants_20261004"
const SOURCE_ROOT := "res://mods/appearance/ink_three_variants_20261004/male/clothing/"
const PROFILES := [
 {"id": "male_clothes_00", "name": "碧岚云叶袍", "cloth": "31584d", "shadow": "1e4038", "light": "648071", "lapel": "e0dbc4", "belt": "355348", "trim": "bba97c", "cuff": 14.0, "hem": 41.0, "motif": "leaf", "file": "01.png"},
 {"id": "male_clothes_01", "name": "玄青云竹袍", "cloth": "516779", "shadow": "354b60", "light": "7a8c96", "lapel": "deded0", "belt": "415566", "trim": "b7b8a4", "cuff": 13.0, "hem": 39.0, "motif": "bamboo", "file": "02.png"},
 {"id": "male_clothes_02", "name": "绛霞流云袍", "cloth": "813f34", "shadow": "572e29", "light": "a66b52", "lapel": "514238", "belt": "453c34", "trim": "bda381", "cuff": 15.0, "hem": 43.0, "motif": "cloud", "file": "03.png"},
 {"id": "male_clothes_03", "name": "玉虚·杂役常服", "cloth": "b0afa2", "shadow": "8c9188", "light": "d0cdbb", "lapel": "e6e1d0", "belt": "70858b", "trim": "c7c3ad", "cuff": 10.0, "hem": 33.0, "motif": "plain", "file": "04.png"},
 {"id": "male_clothes_04", "name": "玉虚·外门常服", "cloth": "738c9f", "shadow": "506b80", "light": "a1b2b8", "lapel": "e8e5d5", "belt": "596f80", "trim": "d0d5c9", "cuff": 14.0, "hem": 39.0, "motif": "cloud", "file": "05.png"},
 {"id": "male_clothes_05", "name": "玉虚·内门常服", "cloth": "e5e4d4", "shadow": "aebfb5", "light": "faf5e3", "lapel": "a2bbba", "belt": "7a9e9b", "trim": "8faeac", "cuff": 17.0, "hem": 43.0, "motif": "cloud", "file": "06.png"},
 {"id": "male_clothes_06", "name": "玉虚·真传常服", "cloth": "eeeadd", "shadow": "bac6c5", "light": "fff9e8", "lapel": "607d94", "belt": "9da9ac", "trim": "ac9d77", "cuff": 18.0, "hem": 46.0, "motif": "mountain", "file": "07.png"},
]
static var texture_cache: Dictionary = {}


func get_profile(id: String) -> Dictionary:
 for profile in PROFILES:
  if profile.id == id:
   return profile.duplicate(true)
 return {}


func source_path(id: String) -> String:
 var profile := get_profile(id)
 return SOURCE_ROOT + str(profile.file) if not profile.is_empty() else ""


func _texture(key: String, drawing: String, bounds: Rect2) -> Texture2D:
 if texture_cache.has(key):
  return texture_cache[key]
 var xml := '<svg xmlns="http://www.w3.org/2000/svg" width="%s" height="%s" viewBox="%s %s %s %s">%s</svg>' % [bounds.size.x, bounds.size.y, bounds.position.x, bounds.position.y, bounds.size.x, bounds.size.y, drawing]
 var image := Image.new()
 var error := image.load_svg_from_string(xml, 4.0)
 if error != OK:
  push_error("俯视衣料SVG载入失败: " + key)
  return null
 image.generate_mipmaps()
 var texture := ImageTexture.create_from_image(image)
 texture_cache[key] = texture
 return texture


func _decoration(profile: Dictionary, shift: Vector2, size_factor: float) -> String:
 var paths := ""
 match str(profile.motif):
  "leaf", "bamboo":
   paths = '<path d="M-8 5 Q0 1 9 -4 M0 1 Q-4 -7 -7 -5 Q-6 -1 0 1 M3 0 Q5 -9 9 -8 Q10 -4 3 0 M-3 3 Q-8 7 -9 4" fill="none"/>'
  "mountain":
   paths = '<path d="M-10 5 L-5 -3 L-2 1 L3 -7 L10 5 M-6 5 L-2 0 M2 -4 L4 1 L6 -1 M-11 7 Q-4 4 0 7 Q5 9 11 6" fill="none"/>'
  "plain":
   paths = '<path d="M-8 3 Q-4 0 0 3 Q4 6 8 3" fill="none"/>'
  _:
   paths = '<path d="M-10 4 Q-4 8 0 3 Q5 -2 9 2 Q12 5 7 7 Q2 9 0 5 M-6 4 Q-9 -2 -3 -4 Q3 -6 5 -1 M-5 -2 Q0 -4 1 0" fill="none"/>'
 return '<g transform="translate(%s %s) scale(%s)" stroke="#%s" stroke-width="0.65" stroke-linecap="round" stroke-linejoin="round" opacity="0.82">%s</g>' % [shift.x, shift.y, size_factor, profile.trim, paths]


func garment_texture(id: String, piece: String, bounds: Rect2) -> Texture2D:
 var p := get_profile(id)
 if p.is_empty():
  return null
 var defs := '<defs><linearGradient id="cloth" x1="0" y1="0" x2="0.8" y2="1"><stop stop-color="#%s"/><stop offset="0.48" stop-color="#%s"/><stop offset="1" stop-color="#%s"/></linearGradient></defs>' % [p.light, p.cloth, p.shadow]
 var svg := defs
 match piece:
  "robe":
   var length := float(p.hem)
   svg += '<path d="M-12 -14 Q-28 -15 -%s -19 Q-%s -11 -%s -2 Q-%s 7 -%s 18 Q-26 17 -12 14 L-6 0 Z" fill="url(#cloth)" stroke="#%s" stroke-width="0.65"/>' % [length, length - 1, length - 3, length + 1, length, p.shadow]
   svg += '<path d="M-16 -10 Q-28 -9 -%s -13 M-14 -5 Q-28 -2 -%s 1 M-15 7 Q-28 12 -%s 13" fill="none" stroke="#%s" stroke-width="0.55" opacity="0.6"/>' % [length - 3, length - 2, length - 3, p.light]
   svg += '<path d="M-%s -18 Q-%s -8 -%s 0 Q-%s 9 -%s 17" fill="none" stroke="#%s" stroke-width="1.2" opacity="0.8"/>' % [length - 1, length - 2, length - 4, length - 2, length - 1, p.trim]
   if p.motif != "plain":
    svg += _decoration(p, Vector2(-length + 12, -9), 0.55)
    svg += _decoration(p, Vector2(-length + 12, 10), 0.55)
  "body":
   svg += '<path d="M-18 -14 Q-8 -20 0 -19 Q10 -18 17 -9 Q21 0 16 9 Q8 18 -1 19 Q-10 20 -18 14 Z" fill="url(#cloth)" stroke="#%s" stroke-width="0.7"/>' % p.shadow
   svg += '<path d="M-13 -14 Q-4 -8 6 1 Q-3 12 -14 15 L-18 11 Q-8 5 -2 0 Q-9 -6 -17 -11 Z" fill="#%s" stroke="#%s" stroke-width="0.6"/>' % [p.lapel, p.trim]
   svg += '<path d="M0 -15 Q3 -10 9 -6 M-15 -6 Q-13 -1 -16 7 M1 10 Q-5 15 -12 14" fill="none" stroke="#%s" stroke-width="0.65" opacity="0.65"/>' % p.light
   svg += '<path d="M-16 -15 Q-12 -1 -16 15" fill="none" stroke="#%s" stroke-width="5"/><path d="M-16 -15 Q-12 -1 -16 15" fill="none" stroke="#%s" stroke-width="0.6"/>' % [p.belt, p.trim]
   svg += _decoration(p, Vector2(-5, -12), 0.5)
  "left_sleeve", "right_sleeve":
   var width := float(p.cuff)
   var sign_y := 1 if piece == "left_sleeve" else -1
   svg += '<g transform="scale(1 %s)">' % sign_y
   svg += '<path d="M-8 -13 Q-12 -22 -5 -28 Q1 -35 12 -34 Q19 -32 28 -%s Q30 -22 30 -15 Q18 -11 11 -14 Q3 -17 -1 -12 Z" fill="url(#cloth)" stroke="#%s" stroke-width="0.7"/>' % [20 + width, p.shadow]
   svg += '<path d="M-3 -20 Q3 -28 12 -27 Q20 -24 28 -27 M3 -16 Q12 -19 21 -18 M2 -30 Q11 -30 21 -%s" fill="none" stroke="#%s" stroke-width="0.65" opacity="0.75"/>' % [18 + width, p.light]
   svg += '<path d="M28 -%s Q29 -24 29 -15" fill="none" stroke="#%s" stroke-width="3.0"/><path d="M27 -%s Q28 -24 28 -15" fill="none" stroke="#%s" stroke-width="0.6"/>' % [20 + width, p.lapel, 20 + width, p.trim]
   svg += _decoration(p, Vector2(17, -28), 0.75 if p.motif != "plain" else 0.5)
   svg += '</g>'
  "ribbon":
   svg += '<path d="M-15 -1 Q-25 1 -36 -2 L-40 2 Q-28 7 -15 3 Z M-15 2 Q-25 7 -42 10 L-39 14 Q-26 12 -14 5 Z" fill="#%s" stroke="#%s" stroke-width="0.65"/><path d="M-20 4 Q-29 8 -38 10" fill="none" stroke="#%s" stroke-width="0.65"/>' % [p.belt, p.shadow, p.trim]
 return _texture(id + "/" + piece, svg, bounds)


func basic_texture(piece: String, bounds: Rect2) -> Texture2D:
 var svg := ""
 match piece:
  "head":
   # Preserve the existing fixed bun; no hairstyle selections are introduced.
   svg = '<ellipse cx="10" cy="0" rx="11.4" ry="10.7" fill="#313b34" stroke="#26322c" stroke-width="0.6"/><path d="M18 -5 Q24 -3 24 0 Q22 3 18 5" fill="#d6bea0" stroke="#9b8b71" stroke-width="0.5"/><ellipse cx="0" cy="0" rx="5.7" ry="6" fill="#28362f"/><path d="M0 -5 L1 5" stroke="#aaa181" stroke-width="1.6"/><path d="M2 -8 Q8 -11 17 -6 M2 -5 Q10 -9 18 -3 M3 8 Q10 10 17 5" fill="none" stroke="#6e7565" stroke-width="0.6" opacity="0.7"/>'
  "left_hand":
   svg = '<path d="M27 -23 Q32 -26 36 -22 Q38 -19 35 -17 Q30 -15 27 -18 Z" fill="#d6bea2" stroke="#897f69" stroke-width="0.65"/><path d="M31 -23 L34 -21 M31 -20 L35 -19" stroke="#aa9679" stroke-width="0.5"/>'
  "right_hand":
   svg = '<g transform="scale(1 -1)"><path d="M27 -23 Q32 -26 36 -22 Q38 -19 35 -17 Q30 -15 27 -18 Z" fill="#d6bea2" stroke="#897f69" stroke-width="0.65"/><path d="M31 -23 L34 -21 M31 -20 L35 -19" stroke="#aa9679" stroke-width="0.5"/></g>'
  "left_foot", "right_foot":
   var y := -11 if piece == "left_foot" else 11
   svg = '<rect x="-37" y="%s" width="17" height="7" rx="3.5" fill="#39483d" stroke="#28392f" stroke-width="0.6"/><path d="M-33 %s L-23 %s" stroke="#aaaf93" stroke-width="0.8"/>' % [y - 3.5, y - 1.5, y - 1.5]
 return _texture("basic/" + piece, svg, bounds)
