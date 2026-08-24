ya = {
	sync = function(callback)
		return callback
	end,
	async = function()
	end,
	err = function()
	end,
	sleep = function()
	end,
}

local plugin = assert(loadfile(assert(arg[1])))()
local commands = plugin.parse_keymap_file(assert(arg[2]))

assert(#commands == 2, "only mgr prepend_keymap and append_keymap are supported")
assert(commands[1].key == "g")
assert(#commands[1].runs == 1 and commands[1].runs[1] == "cd C:\\workspace")
assert(commands[2].key == "z + r")
assert(#commands[2].runs == 2)
assert(commands[2].runs[1] == "plugin refresh")
assert(commands[2].runs[2] == "shell --confirm echo refreshed")
