extends Node2D

@export var start_point: Vector2 = Vector2(400, 40)
@export var end_point: Vector2 = Vector2(400, 700)

@export var auto_strike: bool = true
@export var strike_rate: float = 9.0
@export var visible_time: float = 0.055

@export var segments: int = 22
@export var displacement: float = 110.0
@export var branches: int = 11
@export var branch_chance: float = 0.48
@export var branch_min_length: float = 70.0
@export var branch_max_length: float = 220.0

@export var core_width: float = 2.0
@export var body_width: float = 8.0
@export var glow_width: float = 42.0

@export var intensity: float = 1.0

var rng := RandomNumberGenerator.new()
var strike_timer := 0.0
var life_timer := 0.0
var is_visible_strike := false

var additive_material := CanvasItemMaterial.new()


func _ready() -> void:
	rng.randomize()

	additive_material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	strike()


func _process(delta: float) -> void:
	if not auto_strike:
		return

	if is_visible_strike:
		life_timer += delta

		if life_timer >= visible_time:
			hide_strike()
	else:
		strike_timer += delta

		if strike_timer >= 1.0 / max(strike_rate, 0.01):
			strike()


func _input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		strike()


func strike() -> void:
	clear_bolt()

	visible = true
	is_visible_strike = true
	life_timer = 0.0
	strike_timer = 0.0

	var main_points := make_main_bolt(start_point, end_point)

	# Big blue fake bloom
	add_child(make_line(
		main_points,
		glow_width,
		Color(0.05, 0.22, 1.0, 0.18 * intensity),
		false
	))

	# Smaller cyan glow
	add_child(make_line(
		main_points,
		body_width * 3.2,
		Color(0.18, 0.65, 1.0, 0.42 * intensity),
		false
	))

	# Hot body
	add_child(make_line(
		main_points,
		body_width,
		Color(0.55, 0.86, 1.0, 0.95 * intensity),
		false
	))

	# White-hot needle core
	add_child(make_line(
		main_points,
		core_width,
		Color(1.0, 0.97, 0.78, 1.0),
		false
	))

	make_branches(main_points)


func hide_strike() -> void:
	is_visible_strike = false
	visible = false
	clear_bolt()


func clear_bolt() -> void:
	for child in get_children():
		child.queue_free()


func make_main_bolt(a: Vector2, b: Vector2) -> PackedVector2Array:
	var points := PackedVector2Array()

	var dir := b - a
	var normal := Vector2(-dir.y, dir.x).normalized()

	var drift := 0.0

	for i in range(segments + 1):
		var t := float(i) / float(segments)
		var p := a.lerp(b, t)

		if i != 0 and i != segments:
			var envelope := sin(t * PI)

			var target := rng.randf_range(-displacement, displacement)
			drift = lerp(drift, target, 0.78)

			var snap := rng.randf_range(-displacement * 0.28, displacement * 0.28)
			p += normal * (drift + snap) * envelope

		points.append(p)

	return points


func make_branch_bolt(a: Vector2, b: Vector2, amount: float) -> PackedVector2Array:
	var points := PackedVector2Array()

	var count := rng.randi_range(2, 5)

	var dir := b - a
	var normal := Vector2(-dir.y, dir.x).normalized()

	for i in range(count + 1):
		var t := float(i) / float(count)
		var p := a.lerp(b, t)

		if i != 0 and i != count:
			var envelope := sin(t * PI)
			p += normal * rng.randf_range(-amount, amount) * envelope

		points.append(p)

	return points


func make_branches(main_points: PackedVector2Array) -> void:
	var made := 0

	for i in range(2, main_points.size() - 3):
		if made >= branches:
			return

		if rng.randf() > branch_chance:
			continue

		var root := main_points[i]

		var prev := main_points[i - 1]
		var next := main_points[i + 1]

		var tangent := (next - prev).normalized()
		var normal := Vector2(-tangent.y, tangent.x)

		if rng.randf() < 0.5:
			normal *= -1.0

		var length := rng.randf_range(branch_min_length, branch_max_length)

		var direction := (
			normal * rng.randf_range(0.85, 1.25)
			+ tangent * rng.randf_range(-0.25, 0.55)
		).normalized()

		var end := root + direction * length
		var branch_points := make_branch_bolt(root, end, 38.0)

		var power := rng.randf_range(0.35, 0.8)

		add_child(make_line(
			branch_points,
			glow_width * 0.45,
			Color(0.05, 0.22, 1.0, 0.12 * power * intensity),
			true
		))

		add_child(make_line(
			branch_points,
			body_width * 1.5,
			Color(0.18, 0.65, 1.0, 0.38 * power * intensity),
			true
		))

		add_child(make_line(
			branch_points,
			body_width * 0.45,
			Color(0.55, 0.86, 1.0, 0.88 * power * intensity),
			true
		))

		add_child(make_line(
			branch_points,
			core_width * 0.75,
			Color(1.0, 0.97, 0.78, 0.95 * power),
			true
		))

		made += 1


func make_line(points: PackedVector2Array, line_width: float, color: Color, taper: bool) -> Line2D:
	var line := Line2D.new()

	line.points = points
	line.width = line_width
	line.default_color = color
	line.material = additive_material

	line.joint_mode = Line2D.LINE_JOINT_SHARP
	line.begin_cap_mode = Line2D.LINE_CAP_NONE
	line.end_cap_mode = Line2D.LINE_CAP_NONE
	line.antialiased = true

	var curve := Curve.new()

	if taper:
		curve.add_point(Vector2(0.0, 1.0))
		curve.add_point(Vector2(0.55, 0.7))
		curve.add_point(Vector2(1.0, 0.0))
	else:
		curve.add_point(Vector2(0.0, 0.15))
		curve.add_point(Vector2(0.08, 1.0))
		curve.add_point(Vector2(0.88, 0.85))
		curve.add_point(Vector2(1.0, 0.05))

	line.width_curve = curve

	return line