@tool
extends Control

@export var bookmark_width: float = 200.0:
	set(value):
		bookmark_width = value
		_apply_runtime_layout()

@export var bookmark_height: float = 110.0:
	set(value):
		bookmark_height = value
		_apply_runtime_layout()

@export var position_x_offset_from_back_button: float = 0.0:
	set(value):
		position_x_offset_from_back_button = value
		_apply_runtime_layout()

@export var position_y_offset_from_back_button: float = 12.0:
	set(value):
		position_y_offset_from_back_button = value
		_apply_runtime_layout()

@export var min_gap_from_bottom_controls: float = 10.0:
	set(value):
		min_gap_from_bottom_controls = value
		_apply_runtime_layout()

@export var clamp_to_bottom_controls: bool = false:
	set(value):
		clamp_to_bottom_controls = value
		_apply_runtime_layout()

@export var label_left: float = 17.0:
	set(value):
		label_left = value
		_apply_runtime_layout()

@export var label_top: float = 12.0:
	set(value):
		label_top = value
		_apply_runtime_layout()

@export var label_right: float = -49.0:
	set(value):
		label_right = value
		_apply_runtime_layout()

@export var label_bottom: float = -14.0:
	set(value):
		label_bottom = value
		_apply_runtime_layout()

func _ready() -> void:
	_apply_runtime_layout()

func _process(_delta: float) -> void:
	if Engine.is_editor_hint():
		_apply_runtime_layout()

func _apply_runtime_layout() -> void:
	var back_button := get_node_or_null("Sidebar/BackButtonPreview") as Control
	var bookmark := get_node_or_null("Sidebar/BookmarkPreview") as Control
	var bottom_bounds := get_node_or_null("Sidebar/BottomControlsBounds") as Control
	var label := get_node_or_null("Sidebar/BookmarkPreview/BookmarkLabel") as Control
	var readout := get_node_or_null("RuntimeReadout") as Label
	if back_button == null or bookmark == null or bottom_bounds == null or label == null:
		return

	var desired_x := back_button.offset_left + position_x_offset_from_back_button
	var desired_y := back_button.offset_top + position_y_offset_from_back_button
	var max_y := bottom_bounds.offset_top - bookmark_height - min_gap_from_bottom_controls
	var final_y := desired_y
	if clamp_to_bottom_controls:
		final_y = min(desired_y, max_y)

	bookmark.custom_minimum_size = Vector2(bookmark_width, bookmark_height)
	bookmark.offset_left = desired_x
	bookmark.offset_top = final_y
	bookmark.offset_right = desired_x + bookmark_width
	bookmark.offset_bottom = final_y + bookmark_height

	label.offset_left = label_left
	label.offset_top = label_top
	label.offset_right = bookmark_width + label_right
	label.offset_bottom = bookmark_height + label_bottom

	if readout != null:
		readout.text = "Runtime placement preview\nDesired X: %.1f\nDesired Y: %.1f\nFinal Y: %.1f\nMax Y: %.1f\nClamp: %s" % [desired_x, desired_y, final_y, max_y, str(clamp_to_bottom_controls)]
