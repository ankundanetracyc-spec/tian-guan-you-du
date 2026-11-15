extends Node2D

## Independent art trial. The pawn, save data and formal combat are never changed.
const Avatar = preload("pawn_four_way_avatar.gd")
const Adapter = preload("pawn_appearance_profile.gd")
const Vfx = preload("pawn_paper_vfx.gd")
const SIZE := Vector2i(1280,800)
const NAMES := {"fist":"拳掌 · 掌风","heavy":"痛击 · 沉劲","guard":"化解 · 护劲",
	"blood":"血气 · 朱墨劲息","qi":"聚炁 · 凝息","wave":"血浪 · 赤潮"}
const INK := Color("5e5146")
const TITLES := ["拳掌","痛击","化解","血气","聚炁","血浪"]
var adapter: RefCounted
var profile: Dictionary
var actor: Node2D
var target: Node2D
var bank: Node2D
var small_actors: Array[Node2D]=[]
var small_banks: Array[Node2D]=[]
var title: Label
var direction_label: Label
var buttons: Array[Button]=[]
var selected := "fist"
var aim := Vector2.RIGHT
var clock := 0.0
var cycle := 0.0
var paused := false
var rotate_aim := false
var mouse_aim := false
var target_hurt := 1.0
var font := SystemFont.new()
var ui: CanvasLayer
var scripted_capture := false
var saved_window: Dictionary={}

func _ready() -> void:
	font.font_names=PackedStringArray(["Microsoft YaHei","Noto Sans CJK SC"])
	var window:=get_window()
	saved_window={"mode":window.mode,"size":window.size,"position":window.position,"title":window.title,
		"scale_size":window.content_scale_size,"scale_mode":window.content_scale_mode,"scale_aspect":window.content_scale_aspect}
	window.mode=Window.MODE_WINDOWED
	get_window().title="水墨劲势 · 圆袖人物试样"
	get_window().size=SIZE
	get_window().content_scale_size=SIZE
	get_window().content_scale_mode=Window.CONTENT_SCALE_MODE_CANVAS_ITEMS
	get_window().content_scale_aspect=Window.CONTENT_SCALE_ASPECT_KEEP
	adapter=Adapter.new()
	adapter.build(self)
	profile=adapter.get_demo_sample_profile()
	actor=_pawn(Vector2(338,482),2.8)
	target=_pawn(Vector2(518,482),2.8)
	target.modulate=Color("e5d4c2")
	bank=Vfx.new()
	add_child(bank)
	bank.bind_actor(actor)
	for i in range(4):
		var pawn:=_pawn(Vector2(164+i*245,751),1.12)
		var effect:=Vfx.new()
		add_child(effect)
		effect.bind_actor(pawn)
		small_actors.append(pawn)
		small_banks.append(effect)
	_build_ui()
	select_effect("fist")

func _pawn(at: Vector2, size: float) -> Node2D:
	var pawn:=Avatar.new()
	add_child(pawn)
	assert(pawn.build())
	pawn.set_appearance(profile)
	pawn.position=at
	pawn.scale=Vector2.ONE*size
	pawn.z_index=int(at.y)
	pawn.set_direction(Vector2.RIGHT)
	return pawn

func _style() -> StyleBoxFlat:
	var style:=StyleBoxFlat.new()
	style.bg_color=Color("f4ead8")
	style.border_color=Color("c8baa4")
	style.set_border_width_all(1)
	style.set_corner_radius_all(10)
	style.content_margin_left=14
	style.content_margin_right=14
	return style

func _label(text: String, at: Vector2, size: int, color := INK) -> Label:
	var label:=Label.new()
	label.text=text
	label.position=at
	label.add_theme_font_override("font",font)
	label.add_theme_font_size_override("font_size",size)
	label.add_theme_color_override("font_color",color)
	ui.add_child(label)
	return label

func _button(text: String, at: Vector2, width: float, callback: Callable) -> Button:
	var button:=Button.new()
	button.text=text
	button.position=at
	button.size=Vector2(width,42)
	button.add_theme_font_override("font",font)
	button.add_theme_font_size_override("font_size",18)
	button.add_theme_color_override("font_color",INK)
	button.add_theme_stylebox_override("normal",_style())
	var hover:=_style()
	hover.bg_color=Color("e7ddc9")
	button.add_theme_stylebox_override("hover",hover)
	button.add_theme_stylebox_override("pressed",hover)
	button.pressed.connect(callback)
	ui.add_child(button)
	return button

func _build_ui() -> void:
	ui=CanvasLayer.new()
	ui.layer=30
	add_child(ui)
	_label("水墨劲势",Vector2(36,22),32)
	_label("特效试样",Vector2(204,35),18,Color("9c8066"))
	_label("墨势  /  干湿层次  /  水平流向",Vector2(36,75),17,Color("8d7b68"))
	title=_label("",Vector2(62,140),25)
	direction_label=_label("",Vector2(62,184),16,Color("8d7b68"))
	_label("样式",Vector2(947,136),22)
	for i in range(Vfx.KINDS.size()):
		var kind: String=Vfx.KINDS[i]
		buttons.append(_button("%d   %s" % [i+1,TITLES[i]],Vector2(943,182+i*54),285,select_effect.bind(kind)))
	_button("重播",Vector2(943,530),134,replay)
	_button("暂停 / 继续",Vector2(1090,530),138,toggle_pause)
	_button("旋转方向",Vector2(943,584),134,toggle_rotation)
	_button("返回",Vector2(1090,584),138,go_back)
	_label("← → 调方向   /   1—6 换样式",Vector2(63,570),16,Color("8d7b68"))
	_label("四向同场",Vector2(62,645),18)
	for i in range(4):
		_label(["向右","向下","向左","向上"][i],Vector2(204+i*245,729),15,Color("8d7b68"))

func select_effect(kind: String) -> void:
	selected=kind
	cycle=0.0
	bank.clear()
	for item in small_banks: item.clear()
	if title!=null: title.text=NAMES[kind]
	for i in range(buttons.size()): buttons[i].modulate=Color("b98364") if Vfx.KINDS[i]==kind else Color.WHITE
	_set_aim(aim)
	replay()

func replay() -> void:
	bank.clear()
	bank.play(selected,aim)
	target_hurt=0.0
	cycle=0.0
	for i in range(4):
		var direction:=Vector2.from_angle(i*PI*0.5)
		small_actors[i].set_direction(direction)
		small_banks[i].clear()
		small_banks[i].play(selected,direction)

func toggle_pause() -> void: paused=not paused
func toggle_rotation() -> void:
	mouse_aim=false
	rotate_aim=not rotate_aim

func _set_aim(direction: Vector2) -> void:
	if direction.is_zero_approx(): return
	aim=direction.normalized()
	actor.set_direction(aim)
	var ground:=Vector2(aim.x,aim.y*Vfx.Shapes.VIEW_DEPTH)
	target.position=actor.position+ground*64.0*actor.scale.x
	target.z_index=int(target.position.y)
	target.set_direction(-aim)
	target.visible=selected in ["fist","heavy","blood","wave"]
	if direction_label!=null: direction_label.text="朝向  %d°" % posmod(roundi(rad_to_deg(aim.angle())),360)

func _process(delta: float) -> void:
	delta=minf(delta,0.05)
	if not paused:
		clock+=delta
		cycle+=delta
		target_hurt+=delta
		if mouse_aim:
			var point:=get_global_mouse_position()-actor.position+Vector2(0,bank.actor_height()*actor.scale.y)
			_set_aim(Vector2(point.x,point.y/Vfx.Shapes.VIEW_DEPTH))
		elif rotate_aim:
			_set_aim(Vector2.from_angle(clock*0.7))
		else: _set_aim(aim)
		if cycle>1.9: replay()
	actor.advance(delta if not paused else 0.0,Vector2.ZERO)
	target.advance(delta if not paused else 0.0,Vector2.ZERO)
	# A private mannequin response accompanies the art sample, with no HP or combat.
	if selected in ["fist","heavy","blood","wave"]:
		var strength:=1.0-smoothstep(0.21,0.56,target_hurt)
		strength*=smoothstep(0.12,0.19,target_hurt)
		target.visual.rotation=aim.x*(0.12 if selected=="heavy" else 0.07)*strength
	bank.advance(delta,paused)
	for i in range(4):
		small_actors[i].advance(delta if not paused else 0.0,Vector2.ZERO)
		small_banks[i].advance(delta,paused)
	queue_redraw()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index==MOUSE_BUTTON_LEFT and event.pressed and event.position.x<900 and event.position.y<620:
		mouse_aim=true
		var point:=get_global_mouse_position()-actor.position+Vector2(0,bank.actor_height()*actor.scale.y)
		_set_aim(Vector2(point.x,point.y/Vfx.Shapes.VIEW_DEPTH))
		replay()
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode>=KEY_1 and event.keycode<=KEY_6: select_effect(Vfx.KINDS[event.keycode-KEY_1])
		elif event.keycode==KEY_LEFT: mouse_aim=false;_set_aim(aim.rotated(-PI/8));replay()
		elif event.keycode==KEY_RIGHT: mouse_aim=false;_set_aim(aim.rotated(PI/8));replay()
		elif event.keycode==KEY_SPACE: toggle_pause()
		elif event.keycode==KEY_ESCAPE: go_back()

func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO,Vector2(SIZE)),Color("f0e5d2"))
	for rect in [Rect2(34,119,864,492),Rect2(924,119,322,510),Rect2(34,631,1212,146)]:
		draw_style_box(_style(),rect)
	# A quiet ground ellipse keeps the raised spell plane distinct from the floor.
	draw_ellipse_at(Vector2(429,487),Vector2(263,39),Color("e9decb"))
	draw_line(Vector2(62,546),Vector2(870,546),Color("d8cbb5"),1.0,true)

func draw_ellipse_at(at: Vector2, radius: Vector2, color: Color) -> void:
	var points:=PackedVector2Array()
	for i in range(64): points.append(at+Vector2(cos(i*TAU/64.0),sin(i*TAU/64.0))*radius)
	draw_colored_polygon(points,color)

func go_back() -> void:
	if scripted_capture: return
	get_tree().change_scene_to_file("res://tscn/scene/StartMenu.tscn")

func _exit_tree() -> void:
	if adapter!=null: adapter.unbind()
	if not saved_window.is_empty():
		var window:=get_window()
		window.title=saved_window.title
		window.content_scale_size=saved_window.scale_size
		window.content_scale_mode=saved_window.scale_mode
		window.content_scale_aspect=saved_window.scale_aspect
		window.mode=saved_window.mode
		if window.mode==Window.MODE_WINDOWED:
			window.size=saved_window.size
			window.position=saved_window.position
