extends SceneTree

var arena: Node2D
var checks := 0
func check(value: bool, note: String) -> void:
 checks += 1
 if not value:
  push_error(note)
  quit(1)
  assert(value,note)
func _initialize() -> void:
 call_deferred("run")
func run() -> void:
 arena=load("res://tscn/scene/ActionBattleOriginalWandererScene.tscn").instantiate()
 root.add_child(arena)
 await process_frame
 arena.set_process(false)
 arena.player=Vector2(1400,1000)
 arena.enemy=Vector2(1450,1000)
 arena.enemy_hp=100
 arena.hp=100
 arena.wolf_state="recover"
 arena.wolf_clock=999
 arena.pending_attack="拳掌"
 arena.attack_direction=Vector2.RIGHT
 arena.resolve_pending_attack()
 check(arena.enemy_hp==89,"One basic strike settles exactly once")
 check(arena.combat_texts.size()==1,"One basic hit emits one number")
 check(arena.combat_texts[0].value==11 and arena.combat_texts[0].side=="enemy","Number belongs to the actual target")
 arena.update_strike_fx(.1)
 check(arena.combat_texts.size()==1 and arena.enemy_hp==89,"Visual residue never emits additional damage or numbers")
 arena.combat_texts.clear()
 arena.enemy=Vector2(1800,1000)
 arena.pending_attack="拳掌"
 arena.resolve_pending_attack()
 check(arena.combat_texts.is_empty(),"A miss does not emit overhead text")
 arena.show_feedback("架招")
 arena.show_feedback("错步")
 check(arena.combat_texts.is_empty(),"Skill activation does not emit overhead text")
 arena.enemy=Vector2(1450,1000)
 arena.guard_window=.2
 arena.receive_wolf_bite()
 check(arena.hp==84,"Partial guard still settles remaining damage")
 check(arena.combat_texts.size()==2,"Partial guard gives one short cue plus one damage number")
 check(arena.combat_texts[0].kind=="block" and arena.combat_texts[1].value==16,"Guard result does not hide remaining damage")
 arena.add_combat_text(arena.player,0,"player")
 check(arena.combat_texts.size()==2,"Zero damage is silent")
 arena.add_combat_text(arena.player,0,"player","evade")
 arena.add_combat_text(arena.player,0,"player","evade")
 check(arena.combat_texts.size()==3,"Identical simultaneous short cues are not duplicated")
 var age: float=arena.combat_texts[0].age
 arena.configuring=true
 arena._process(.1)
 check(arena.combat_texts[0].age==age,"Configuration pauses text lifetime")
 arena.configuring=false
 arena.battle_hud.help_open=true
 arena._process(.1)
 check(arena.combat_texts[0].age==age,"Help pauses text lifetime")
 arena.battle_hud.help_open=false
 arena.update_combat_texts(.8)
 check(arena.combat_texts.is_empty(),"Expired text cleans itself up")
 arena.add_combat_text(arena.enemy,33,"enemy","damage",true)
 check(arena.combat_texts[0].critical,"Critical styling is recorded without an extra label")
 arena.restart_battle()
 check(arena.combat_texts.is_empty(),"Restart clears all floating text")
 print("COMBAT_TEXT_CHECKS ",checks," failures=0")
 arena.queue_free()
 await process_frame
 quit(0)
