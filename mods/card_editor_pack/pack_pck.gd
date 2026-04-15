extends SceneTree

func _init() -> void:
	var out_path = "build/card_editor.pck"
	var args = OS.get_cmdline_user_args()
	if args.is_empty():
		args = OS.get_cmdline_args()
	for i in range(args.size()):
		if args[i] == "--out" and i + 1 < args.size():
			out_path = args[i + 1]
			break

	var packer = PCKPacker.new()
	var err = packer.pck_start(out_path)
	if err != OK:
		push_error("Failed to start PCK packer: %s" % err)
		quit(1)
		return

	_add_file(packer, "mod_manifest.json")
	_add_dir(packer, "mods/card_editor")
	_add_dir(packer, "card_editor/localization")

	packer.flush()
	quit()

func _add_dir(packer, rel_dir: String) -> void:
	var dir = DirAccess.open("res://%s" % rel_dir)
	if dir == null:
		push_error("Missing directory: %s" % rel_dir)
		return
	dir.list_dir_begin()
	while true:
		var name = dir.get_next()
		if name == "":
			break
		if name.begins_with("."):
			continue
		if dir.current_is_dir():
			_add_dir(packer, "%s/%s" % [rel_dir, name])
		else:
			_add_file(packer, "%s/%s" % [rel_dir, name])
	dir.list_dir_end()

func _add_file(packer, rel_path: String) -> void:
	# Skip import metadata files — the compiled .ctex they reference is never in the PCK.
	# Without a .import redirect, Godot loads raw JPG/PNG/WebP directly at runtime.
	if rel_path.ends_with(".import"):
		return
	var res_path = "res://%s" % rel_path
	var abs_path = ProjectSettings.globalize_path(res_path)
	var err = packer.add_file(res_path, abs_path)
	if err != OK:
		push_error("Failed to add %s (%s): %s" % [rel_path, abs_path, err])
