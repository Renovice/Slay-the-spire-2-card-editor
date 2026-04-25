extends SceneTree

func _init() -> void:
	var out_path = "build/card_editor.pck"
	var root_path = ""
	var args = OS.get_cmdline_user_args()
	if args.is_empty():
		args = OS.get_cmdline_args()
	for i in range(args.size()):
		if args[i] == "--out" and i + 1 < args.size():
			out_path = args[i + 1]
		if args[i] == "--root" and i + 1 < args.size():
			root_path = args[i + 1]

	var packer = PCKPacker.new()
	var err = packer.pck_start(out_path)
	if err != OK:
		push_error("Failed to start PCK packer: %s" % err)
		quit(1)
		return

	var failed = false
	failed = _add_file(packer, "mod_manifest.json", root_path) or failed
	failed = _add_dir(packer, "mods/card_editor", root_path) or failed
	# Localization is shipped next to the DLL, not inside the PCK. That keeps
	# Korean/Chinese translation files editable without rebuilding this package.
	if failed:
		quit(1)
		return

	packer.flush()
	quit()

func _add_dir(packer, rel_dir: String, root_path: String) -> bool:
	var dir = DirAccess.open(_source_path(rel_dir, root_path))
	if dir == null:
		push_error("Missing directory: %s" % rel_dir)
		return true
	var failed = false
	dir.list_dir_begin()
	while true:
		var name = dir.get_next()
		if name == "":
			break
		if name.begins_with("."):
			continue
		if dir.current_is_dir():
			failed = _add_dir(packer, "%s/%s" % [rel_dir, name], root_path) or failed
		else:
			failed = _add_file(packer, "%s/%s" % [rel_dir, name], root_path) or failed
	dir.list_dir_end()
	return failed

func _add_file(packer, rel_path: String, root_path: String) -> bool:
	# Skip import metadata files - the compiled .ctex they reference is never in the PCK.
	# Without a .import redirect, Godot loads raw JPG/PNG/WebP directly at runtime.
	if rel_path.ends_with(".import"):
		return false
	var res_path = "res://%s" % rel_path
	var abs_path = _source_path(rel_path, root_path)
	var err = packer.add_file(res_path, abs_path)
	if err != OK:
		push_error("Failed to add %s (%s): %s" % [rel_path, abs_path, err])
		return true
	return false

func _source_path(rel_path: String, root_path: String) -> String:
	if root_path.is_empty():
		return ProjectSettings.globalize_path("res://%s" % rel_path)
	return root_path.path_join(rel_path)
