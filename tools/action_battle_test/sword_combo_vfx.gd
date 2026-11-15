extends Node2D
## Smooth crescent paint: one round brush body with a softer inner wash.
## Geometry and clocks still come from the original released sword stage.
const INK = preload("sword_brush_ink.gdshader")
const Sweep = preload("sword_sweep_motion.gd")
const GEOMETRY = preload("sword_combo_rules.gd")
var marks: Array[Dictionary] = []
var brush: Texture2D

func _brush() -> Texture2D:
 if brush!=null: return brush
 var image := Image.create(512,128,false,Image.FORMAT_RGBA8)
 for y in range(128):
  for x in range(512):
   var u := float(x)/511.0
   var v := float(y)/127.0
   var edge := smoothstep(0.0,0.10,v)*(1.0-smoothstep(0.80,1.0,v))
   var ends := smoothstep(0.0,0.045,u)*(1.0-smoothstep(0.965,1.0,u))
   var alpha := edge*ends
   var pigment := lerpf(0.76,0.96,smoothstep(0.0,0.40,v))
   image.set_pixel(x,y,Color(pigment,pigment,pigment,alpha))
 brush = ImageTexture.create_from_image(image)
 return brush

func emit(release: Dictionary, age := 0.0) -> void:
 var entry := release.duplicate(true)
 entry.view_depth = float(release.get("view_depth",Sweep.VIEW_DEPTH))
 entry.age = age
 entry.polygons = []
 for lane in range(2):
  var polygon := Polygon2D.new()
  polygon.antialiased = true
  polygon.z_as_relative = false
  polygon.z_index = 50
  polygon.texture = _brush()
  var material := ShaderMaterial.new()
  material.shader = INK
  material.set_shader_parameter("layer",float(lane))
  material.set_shader_parameter("effect_life",float(release.life))
  material.set_shader_parameter("reveal_seconds",float(release.reveal))
  material.set_shader_parameter("opacity",[0.17,0.94][lane])
  polygon.material = material
  add_child(polygon)
  entry.polygons.append(polygon)
 marks.append(entry)
 if marks.size()>16:
  var oldest: Dictionary = marks.pop_front()
  for node in oldest.polygons: node.queue_free()
 _update(entry)

func _path_sample(path: Array, u: float, key: String) -> Vector2:
 var at := clampf(u,0.0,1.0)*(path.size()-1)
 var index := mini(floori(at),path.size()-2)
 var first: Vector2 = path[index][key]
 var second: Vector2 = path[index+1][key]
 return first.cubic_interpolate(second,Vector2(path[maxi(index-1,0)][key]),Vector2(path[mini(index+2,path.size()-1)][key]),at-index)

func _clip_point(entry: Dictionary, point: Vector2) -> Vector2:
 var height := Vector2(0.0,float(entry.attack_height))
 var origin: Vector2 = entry.origin
 var depth: float = entry.view_depth
 var d: Vector2 = entry.direction
 var relative := Sweep.unproject_ground(point+height,depth)-origin
 var reach: float = entry.reach
 if str(entry.shape)=="capsule":
  var along := clampf(relative.dot(d),0.0,reach)
  var cap := along-clampf(along,GEOMETRY.CAPSULE_RADIUS,reach-GEOMETRY.CAPSULE_RADIUS)
  var width := sqrt(maxf(0.0,GEOMETRY.CAPSULE_RADIUS*GEOMETRY.CAPSULE_RADIUS-cap*cap))
  var ground := origin+d*along+d.orthogonal()*clampf(relative.dot(d.orthogonal()),-width,width)
  return Sweep.project_ground(ground,depth)-height
 # Stay microscopically inside the angular edge before float32 view mapping;
 # this is a drawing margin, never an extra contact tolerance or range change.
 var half := maxf(0.0,deg_to_rad(float(entry.angle)*0.5)-0.00001)
 var angle := clampf(d.angle_to(relative),-half,half)
 var ground := origin+Vector2.from_angle(d.angle()+angle)*minf(relative.length(),reach-0.001)
 return Sweep.project_ground(ground,depth)-height

func _blade_ribbon(entry: Dictionary, lane: int, head: float, tail: float) -> PackedVector2Array:
 var path: Array = entry.blade_path
 var shape := PackedVector2Array()
 var thinning := 1.0-0.42*smoothstep(float(entry.reveal),float(entry.life),float(entry.age))
 if str(entry.shape)=="capsule":
  # A thrust needs a longitudinal spear of ink along the blade, not the tiny
  # area swept sideways by its almost stationary tip (which looks like a flag).
  var front := _path_sample(path,head,"tip")
  var grip := _path_sample(path,0.5,"grip")
  var normal := Sweep.unproject_ground(front-grip,float(entry.view_depth)).normalized().orthogonal()
  normal = Sweep.project_ground(normal,float(entry.view_depth))
  for side in [1.0,-1.0]:
   for iteration in range(49):
    var fraction := float(iteration if side>0.0 else 48-iteration)/48.0
    var point := grip.lerp(front,lerpf(tail,1.0,fraction))
    var width: float = float(entry.stroke)*(0.36 if lane==0 else 0.62)*thinning
    width *= pow(maxf(0.0,sin(fraction*PI)),0.85)
    shape.append(_clip_point(entry,point+normal*width*side))
  return shape
 for side in [0,1]:
  for iteration in range(49):
   var fraction := float(iteration if side==0 else 48-iteration)/48.0
   var u := lerpf(tail,head,fraction)
   var tip := _path_sample(path,u,"tip")
   var grip := _path_sample(path,u,"grip")
   var blade_ground := Sweep.unproject_ground(grip-tip,float(entry.view_depth))
   var base_width: float = float(entry.stroke)*(0.72 if lane==0 else 1.22)
   # The short two cuts paint the outer half of the real blade's swept face,
   # rather than a thin outline of only its tip. This gives a readable
   # horizontal sheet even for the narrower 80/120-degree attack sectors.
   if lane==1 and int(entry.index)<2:
    base_width = maxf(base_width,blade_ground.length()*0.48)
   var width := base_width*thinning
   width *= pow(maxf(0.0,sin(fraction*PI)),0.72)
   var inward := blade_ground.normalized()*minf(width,blade_ground.length()*0.5)
   # Rotate in the true horizontal plane, then project its fixed world depth.
   # All displayed ground anchors/markers use this same camera, without
   # squashing the upright character or changing the real attack footprint.
   inward = Sweep.project_ground(inward,float(entry.view_depth))
   var point := tip+inward*float(side)
   shape.append(_clip_point(entry,point))
 return shape

func _ribbon(entry: Dictionary, lane: int, head: float, tail: float) -> PackedVector2Array:
 var shape := PackedVector2Array()
 if entry.has("blade_path") and entry.blade_path.size()>1:
  return _blade_ribbon(entry,lane,head,tail)
 var span := maxf(0.005,head-tail)
 var thinning := 1.0-0.55*smoothstep(float(entry.reveal)*0.5,float(entry.life),float(entry.age))
 var reach: float = entry.reach
 var direction: Vector2 = entry.direction
 var origin: Vector2 = entry.origin
 var d := direction
 var normal := d.orthogonal()
 if str(entry.shape)=="capsule":
  for side in [1.0,-1.0]:
   for iteration in range(33):
    var fraction := float(iteration if side>0.0 else 32-iteration)/32.0
    var taper := pow(maxf(0.0,sin(fraction*PI)),0.66)
    var width: float = taper*float(entry.stroke)*[1.0,0.82][lane]*thinning
    var x := reach*lerpf(tail,head,fraction)
    # Reveal grows the painted length, never the capsule's fixed round cap.
    # Near O or R even a short, thick brush must stay inside that original cap.
    var cap_distance := x-clampf(x,GEOMETRY.CAPSULE_RADIUS,reach-GEOMETRY.CAPSULE_RADIUS)
    var cap_width := sqrt(maxf(0.0,GEOMETRY.CAPSULE_RADIUS*GEOMETRY.CAPSULE_RADIUS-cap_distance*cap_distance))
    var across := clampf(side*width,-cap_width,cap_width)
    shape.append(origin+d*x+normal*across)
 else:
  var half := deg_to_rad(float(entry.angle)*0.5)
  var reverse := int(entry.index)==1
  var active_half := half*span
  var middle := lerpf(half,-half,(tail+head)*0.5) if reverse else lerpf(-half,half,(tail+head)*0.5)
  # Retain release-space anchoring while the old ink contracts very slightly
  # inward. The stroke never acquires new reach or follows its caster.
  var outer := reach-1.0-float(entry.stroke)*0.12*smoothstep(float(entry.reveal)*0.6,float(entry.life),float(entry.age))
  var ratio := 1.80 if int(entry.index)<2 else 1.55
  var depth: float = float(entry.stroke)*(2.20 if lane==0 else ratio)*sin(span*PI*0.5)*thinning
  # Two circles meet at the current arc's tips. Both contours stay round;
  # no radial waviness, asymmetric bulges or detached hairline are added.
  depth = minf(depth,outer*(1.0-cos(active_half))*0.85)
  var shift := (2.0*outer*depth-depth*depth)/maxf(0.000001,2.0*(outer*(1.0-cos(active_half))-depth))
  var inner_squared := outer*outer+shift*shift+2.0*outer*shift*cos(active_half)
  for side in [1.0,-1.0]:
   for iteration in range(49):
    var fraction := float(iteration if side>0.0 else 48-iteration)/48.0
    var u := lerpf(tail,head,fraction)
    var theta := lerpf(half,-half,u) if reverse else lerpf(-half,half,u)
    var local_angle := theta-middle
    var inside := -shift*cos(local_angle)+sqrt(maxf(0.0,inner_squared-shift*shift*pow(sin(local_angle),2.0)))
    var radius := outer if side>0.0 else minf(outer,maxf(0.0,inside))
    shape.append(origin+Vector2.from_angle(d.angle()+theta)*radius)
 for index in range(shape.size()):
  shape[index] = Sweep.project_ground(shape[index],float(entry.view_depth))-Vector2(0.0,float(entry.get("attack_height",58.0)))
 return shape

func _update(entry: Dictionary) -> void:
 var progress: float = clampf(float(entry.age)/float(entry.life),0.0,1.0)
 entry.sweep_head = Sweep.head(float(entry.age),float(entry.reveal))
 entry.sweep_tail = minf(Sweep.tail(float(entry.age),float(entry.reveal),float(entry.life)),float(entry.sweep_head)-0.005)
 for lane in range(entry.polygons.size()):
  var polygon: Polygon2D = entry.polygons[lane]
  var head: float = entry.sweep_head
  var tail: float = entry.sweep_tail
  if lane==0:
   head = Sweep.head(maxf(0.0,float(entry.age)-0.018),float(entry.reveal))
   tail = Sweep.tail(maxf(0.0,float(entry.age)-0.018),float(entry.reveal),float(entry.life))*0.72
  tail = minf(tail,head-0.005)
  polygon.polygon = _ribbon(entry,lane,head,tail)
  polygon.visible = progress<1.0
  var vertices := polygon.polygon.size()/2
  # A concave crescent must be textured as consecutive strips. Triangulating
  # the whole outline creates long diagonals that pinch the wash into kinks.
  var strips: Array[PackedInt32Array] = []
  for index in range(vertices-1):
   var inner := vertices*2-1-index
   var next_inner := inner-1
   for triangle in [PackedInt32Array([index,index+1,inner]),PackedInt32Array([index+1,next_inner,inner])]:
    var a: Vector2 = polygon.polygon[triangle[0]]
    var b: Vector2 = polygon.polygon[triangle[1]]
    var c: Vector2 = polygon.polygon[triangle[2]]
    if absf((b-a).cross(c-a))>0.000001: strips.append(triangle)
  polygon.polygons = strips
  var uv := PackedVector2Array()
  # U belongs to the released sweep, not to the remaining cropped crescent.
  # Old pigment stays at its own place while the head advances and tail dries.
  for index in range(vertices): uv.append(Vector2(lerpf(tail,head,float(index)/(vertices-1))*511.0,0.0))
  for index in range(vertices): uv.append(Vector2(lerpf(tail,head,1.0-float(index)/(vertices-1))*511.0,127.0))
  polygon.uv = uv
  var material: ShaderMaterial = polygon.material
  material.set_shader_parameter("effect_age",float(entry.age))
  material.set_shader_parameter("dissolve",smoothstep(0.78,1.0,progress))
  material.set_shader_parameter("opacity",[0.17,0.94][lane]*(1.0-smoothstep(0.72,1.0,progress)))

func advance(delta: float) -> void:
 for index in range(marks.size()-1,-1,-1):
  var entry: Dictionary = marks[index]
  entry.age = float(entry.age)+delta
  if float(entry.age)>=float(entry.life):
   for node in entry.polygons: node.queue_free()
   marks.remove_at(index)
  else: _update(entry)

func clear() -> void:
 for entry in marks:
  for node in entry.polygons: node.queue_free()
 marks.clear()
