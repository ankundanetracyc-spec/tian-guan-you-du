extends "action_battle_feel_test.gd"

const CONTACT_SPACE = preload("human_contact_space.gd")
var npcs: Array[Node2D] = []
var elevation := 0.0
var simulation_paused := true
var has_started := false
var show_hitboxes := false
var hitbox_overlay: Node2D

func _ready() -> void:
 original_wanderer_enabled = true
 original_wanderer_art_folder = "yuxu_fullbody_v15"
 super._ready()
 skill_slots = ["痛击", "架招", "催血", "聚炁", ""]
 var hud_parent := battle_hud.get_parent()
 battle_hud.hide()
 battle_hud.set_process(false)
 battle_hud.set_process_input(false)
 battle_hud.queue_free()
 battle_hud = load(_action_test_path("human_sparring_hud.gd")).new()
 battle_hud.battle = self
 hud_parent.add_child(battle_hud)
 for index in range(1, int(attribute_runtime.get_unit_count())):
  var npc: Node2D = load(_action_test_path("human_sparring_fighter.gd")).new()
  add_child(npc)
  npc.initialize(self, index)
  npcs.append(npc)
 restart_battle()
 hitbox_overlay = load(_action_test_path("human_sparring_hitboxes.gd")).new()
 hitbox_overlay.arena = self
 hitbox_overlay.z_index = 4095
 add_child(hitbox_overlay)

func _load_enemy_art() -> void:
 pass

func _setup_skinned_wolf() -> void:
 pass

func update_skinned_wolf(_delta: float) -> void:
 pass

func combat_paused() -> bool:
 return simulation_paused or super.combat_paused()

func attack_plane_height() -> float:
 return CONTACT_SPACE.hand_height(self)

func body_effect_origin() -> Vector2:
 return player - Vector2(0, elevation)

func attack_aim_direction() -> Vector2:
 return CONTACT_SPACE.pointer_direction(self, get_global_mouse_position())

func update_skinned_player() -> void:
 super.update_skinned_player()
 if original_wanderer != null: original_wanderer.position = body_effect_origin()
 if skinned_player != null: skinned_player.position = body_effect_origin()

func set_simulation_paused(value: bool) -> void:
 simulation_paused = value
 if not value: has_started = true
 held_basic_attack = false
 clear_combo_input()
 for npc in npcs:
  npc.held_basic_attack = false
  npc.clear_combo_input()

func _notification(what: int) -> void:
 super._notification(what)
 if what == NOTIFICATION_WM_WINDOW_FOCUS_OUT: set_simulation_paused(true)

func _input(event: InputEvent) -> void:
 if event is InputEventKey and event.pressed and not event.echo:
  if event.keycode == KEY_ESCAPE:
   if battle_hud.help_open: battle_hud.help_open = false
   elif configuring: configuring = false
   else: attribute_runtime.request_close()
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_P and not configuring and not battle_hud.help_open:
   set_simulation_paused(not simulation_paused)
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_T and not configuring and not battle_hud.help_open:
   restart_battle()
   get_viewport().set_input_as_handled()
   return
  if event.keycode == KEY_F2:
   show_hitboxes = not show_hitboxes
   get_viewport().set_input_as_handled()
   return
  if event.keycode in [KEY_G, KEY_H]: return
 if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
  held_basic_attack = false
 if simulation_paused: return
 if event is InputEventMouseButton and battle_hud.pointer_over_ui(): return
 super._input(event)

func _process(delta: float) -> void:
 if hitbox_overlay != null: hitbox_overlay.queue_redraw()
 if combat_paused():
  held_basic_attack = false
  clear_combo_input()
  visual_motion_delta = 0.0
  update_skinned_player()
  for npc in npcs:
   npc.held_basic_attack = false
   npc.clear_combo_input()
   npc.visual_motion_delta = 0.0
   npc.update_skinned_player()
  queue_redraw()
  return
 super._process(delta)
 if original_wanderer != null: original_wanderer.z_index = int(player.y)
 if skill_vfx != null: skill_vfx.z_index = int(player.y) + 6

func update_wolf(delta: float, _previous_player: Vector2 = Vector2.INF) -> void:
 for npc in npcs:
  npc.advance_fighter(delta)
  if npc.skill_vfx != null: npc.skill_vfx.z_index = int(npc.player.y) + 6
 refresh_health()

func refresh_health() -> void:
 var state: Dictionary = attribute_runtime.get_state()
 hp = state.hp
 enemy_hp = state.enemy_hp
 var closest := INF
 for npc in npcs:
  var unit: Dictionary = attribute_runtime.get_unit_state(npc.unit_index)
  npc.hp = unit.hp
  npc.enemy_hp = hp
  if npc.hp > 0 and player.distance_squared_to(npc.player) < closest:
   enemy = npc.player
   closest = player.distance_squared_to(npc.player)

func player_is_preparing_toward(point: Vector2, target: Node2D = null) -> bool:
 if pending_attack not in ["拳掌", "痛击"]: return false
 var reach: float = fist_combo.reach if pending_attack == "拳掌" and fist_combo != null else float(SKILL_RULES["痛击"].reach)
 var direction: Vector2 = attack_aim_direction() if pending_attack == "拳掌" else attack_direction
 return contact_contains(player, direction, reach, point) and (target == null or CONTACT_SPACE.intersects_height(target, attack_plane_height()))

func contact_contains(origin: Vector2, direction: Vector2, reach: float, point: Vector2) -> bool:
 var relative := point - origin
 return relative.length() <= reach and direction.dot(relative) > 0.0

func resolve_pending_attack(elapsed_after_end := 0.0) -> void:
 resolve_fighter_attack(self, elapsed_after_end)

func resolve_fighter_attack(source: Node2D, elapsed_after_end := 0.0) -> void:
 var release: Dictionary = source.prepare_attack_release(elapsed_after_end)
 if release.is_empty(): return
 var source_index: int = 0 if source == self else source.unit_index
 var targets: Array = npcs if source == self else [self]
 var effect: Dictionary = release.effect
 var attack_height: float = source.attack_plane_height()
 effect.attack_height = attack_height
 var impacts: Array[Vector2] = []
 source.basic_strike_hit = false
 for target in targets:
  if target.hp <= 0: continue
  if not contact_contains(source.player, source.strike_direction, source.attack_reach, target.player): continue
  if not CONTACT_SPACE.intersects_height(target, attack_height): continue
  var target_index: int = 0 if target == self else target.unit_index
  var side := "player" if target_index == 0 else "npc_%d" % target_index
  if target.invulnerable > 0.0:
   add_combat_text(target.player, 0, side, "evade")
   if impact_feedback != null: impact_feedback.report(source,target,{"evaded":true},str(release.name),"本体",attack_height)
   continue
  var result: Dictionary = attribute_runtime.resolve_contact(source_index, target_index, release.name, int(release.damage), target.guard_window > 0.0)
  if result.get("inactive", true): continue
  if impact_feedback != null: impact_feedback.report(source,target,result,str(release.name),"本体",attack_height)
  if result.evaded:
   add_combat_text(target.player, 0, side, "evade")
  else:
   impacts.append(target.player)
   if result.guarded:
    target.finish_guard()
    target.start_guard_visual(true, source.player - target.player)
    add_combat_text(target.player, int(result.blocked), side, "block")
   if int(result.damage) > 0:
    target.hurt_visual = 0.2
    add_combat_text(target.player, int(result.damage), side, "damage", bool(result.critical))
  refresh_health()
 source.basic_strike_hit = not impacts.is_empty()
 effect.hit = source.basic_strike_hit
 effect.impact_positions = impacts
 if not impacts.is_empty():
  effect.impact = impacts[0]
  source.basic_strike_impact = impacts[0]
 source.strike_fx.append(effect)
 if hp <= 0 or enemy_hp <= 0: clear_finished_battle()

func add_combat_text(at: Vector2, value: int, side: String, kind := "damage", critical := false) -> void:
 if kind == "damage" and value <= 0: return
 var lane := 0
 for mark in combat_texts:
  if str(mark.side) == side and float(mark.age) < 0.28: lane += 1
 combat_texts.append({"point":at + Vector2(0, -135), "value":value, "side":side,
  "kind":kind, "critical":critical, "age":0.0, "lifetime":0.76, "lane":lane % 5})

func clear_finished_battle() -> void:
 super.clear_finished_battle()
 for npc in npcs: npc.clear_finished_battle()

func restart_battle() -> void:
 elevation = 0.0
 super.restart_battle()
 player = TRIAL_WORLD_SIZE * Vector2(0.5, 0.56)
 for npc in npcs:
  npc.load_state()
  npc.player = player + Vector2.from_angle(npc.approach_angle) * 360.0
  npc.visual_motion_delta = 0.0
  npc.visual_motion_velocity = Vector2.ZERO
  npc.update_skinned_player()
 refresh_health()
 set_simulation_paused(true)
 has_started = false
 update_skinned_player()
 _update_trial_camera(0.0, true)
 queue_redraw()

func get_camera_units() -> Array[Dictionary]:
 var units: Array[Dictionary] = []
 if hp > 0: units.append({"position":player, "head_height":150.0 + elevation, "alive":true})
 for npc in npcs:
  if npc.hp > 0: units.append({"position":npc.player, "head_height":150.0 + npc.elevation, "alive":true})
 return units

func draw_enemy() -> void:
 var font := ThemeDB.fallback_font
 draw_set_transform(player, 0.0, Vector2(1.0, 0.34))
 draw_arc(Vector2.ZERO, 17.0, 0.0, TAU, 32, Color("6c9b8a"), 1.5, true)
 draw_set_transform(Vector2.ZERO)
 for npc in npcs:
  draw_set_transform(npc.player, 0.0, Vector2(1.0, 0.34))
  draw_arc(Vector2.ZERO, 17.0, 0.0, TAU, 32, Color("a45b52") if npc.hp > 0 else Color("807b70"), 1.2, true)
  draw_set_transform(Vector2.ZERO)
  var bar := Rect2(npc.player + Vector2(-32, -109), Vector2(64, 5))
  draw_rect(bar, Color("493b34"))
  draw_rect(Rect2(bar.position, Vector2(bar.size.x * clampf(float(npc.hp) / maxi(npc.max_hp, 1), 0.0, 1.0), 5)), Color("b05d50"))
  var label: String = "陪练%d" % npc.unit_index
  var width := font.get_string_size(label, HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x
  draw_string(font, npc.player + Vector2(-width * 0.5, -116), label, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("293c39"))
