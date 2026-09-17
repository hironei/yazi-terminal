--- @since 26.5.6

local ESCAPES = {
	["\\"] = "\\\\",
	["\""] = "\\\"",
	["\b"] = "\\b",
	["\f"] = "\\f",
	["\n"] = "\\n",
	["\r"] = "\\r",
	["\t"] = "\\t",
}

local function json_string(value)
	value = tostring(value):gsub("[%z\1-\31\\\"]", function(character)
		return ESCAPES[character] or string.format("\\u%04x", string.byte(character))
	end)
	return '"' .. value .. '"'
end

local function json_path(kind, value)
	if not value then
		return "null"
	end
	return "{\"kind\":" .. json_string(kind) .. ",\"value\":" .. json_string(value) .. "}"
end

local function json_path_array(kind, values)
	local encoded = {}
	for _, value in ipairs(values) do
		table.insert(encoded, json_path(kind, value))
	end
	return "[" .. table.concat(encoded, ",") .. "]"
end

local function trim(value)
	return value:gsub("^%s+", ""):gsub("%s+$", "")
end

local function unescape_double_quoted(value)
	return (value:gsub("\\(.)", function(character)
		return ({
			["\\"] = "\\",
			["\""] = "\"",
			["b"] = "\b",
			["f"] = "\f",
			["n"] = "\n",
			["r"] = "\r",
			["t"] = "\t",
		})[character] or "\\" .. character
	end))
end

local function quoted_value(line, name)
	local double_quoted = line:match("^" .. name .. "%s*=%s*\"(.*)\"%s*$")
	if double_quoted then
		return unescape_double_quoted(double_quoted)
	end
	return line:match("^" .. name .. "%s*=%s*'(.*)'%s*$")
end

local function array_values(value)
	local values = {}
	local index = 1
	local length = #value
	local function skip_whitespace()
		while index <= length and value:sub(index, index):match("%s") do
			index = index + 1
		end
	end
	local function finish_array()
		index = index + 1
		skip_whitespace()
		local trailing = value:sub(index, index)
		if trailing ~= "" and trailing ~= "#" then
			return {}, "invalid"
		end
		return #values > 0 and values or {}, #values > 0 and "valid" or "invalid"
	end

	skip_whitespace()
	if value:sub(index, index) ~= "[" then
		return {}, "invalid"
	end
	index = index + 1

	while true do
		skip_whitespace()
		local quote = value:sub(index, index)
		if quote == "]" then
			return finish_array()
		end
		if quote ~= "'" and quote ~= '"' then
			return {}, index > length and "incomplete" or "invalid"
		end

		index = index + 1
		local item = {}
		local closed = false
		while index <= length do
			local character = value:sub(index, index)
			if character == quote then
				closed = true
				index = index + 1
				break
			elseif quote == '"' and character == "\\"
				and index + 1 <= length then
				table.insert(item, character)
				table.insert(item, value:sub(index + 1, index + 1))
				index = index + 2
			else
				table.insert(item, character)
				index = index + 1
			end
		end
		if not closed then
			return {}, "incomplete"
		end

		local item_value = table.concat(item)
		if quote == '"' then
			item_value = unescape_double_quoted(item_value)
		end
		table.insert(values, item_value)
		skip_whitespace()
		local separator = value:sub(index, index)
		if separator == "," then
			index = index + 1
		elseif separator == "]" then
			return finish_array()
		elseif separator == "" then
			return {}, "incomplete"
		else
			return {}, "invalid"
		end
	end
end

local function parse_keymap_file(path)
	local file = io.open(path, "r")
	if not file then
		return {}
	end

	local commands = {}
	local current
	local in_manager_keymap = false
	local function save_current()
		if current and not current.invalid_run and current.runs and #current.runs > 0 then
			current.run = current.runs[1]
			table.insert(commands, current)
		end
	end

	for source_line in file:lines() do
		local line = trim(source_line)
		local section, keymap_position = line:match("^%[%[([%w_]+)%.([%w_]+)%]%]$")
		if section and (keymap_position == "prepend_keymap" or keymap_position == "append_keymap") then
			save_current()
			current = section == "mgr" and {} or nil
			in_manager_keymap = section == "mgr"
		elseif line:match("^%[%[") then
			save_current()
			current = nil
			in_manager_keymap = false
		elseif in_manager_keymap and current and line ~= "" and not line:match("^#") then
			if current.pending_run_array then
				current.pending_run_array = current.pending_run_array .. "\n" .. line
				local runs, status = array_values(current.pending_run_array)
				if status == "valid" then
					current.runs = runs
					current.pending_run_array = nil
				elseif status == "invalid" then
					current.invalid_run = true
					current.pending_run_array = nil
				end
			else
				local key = quoted_value(line, "on")
				if key then
					current.key = key
				else
					local key_array = line:match("^on%s*=%s*%[([^%]]*)%]")
					if key_array then
						local keys, status = array_values("[" .. key_array .. "]")
						if status == "valid" then
							current.key = table.concat(keys, " + ")
						end
					end
				end

				local description = quoted_value(line, "desc")
				if description then
					current.description = description
				end

				local run = quoted_value(line, "run")
				if run then
					current.runs = { run }
				else
					local run_array = line:match("^run%s*=%s*(%[.*)$")
					if run_array then
						local runs, status = array_values(run_array)
						if status == "valid" then
							current.runs = runs
						elseif status == "invalid" then
							current.invalid_run = true
						else
							current.pending_run_array = run_array
						end
					end
				end
			end
		end
	end
	save_current()
	file:close()
	return commands
end

local function config_home()
	local configured = os.getenv("YAZI_CONFIG_HOME")
	if configured and configured ~= "" then
		return configured
	end

	if package.config:sub(1, 1) == "\\" then
		local appdata = os.getenv("APPDATA")
		if appdata and appdata ~= "" then
			return appdata .. "/yazi/config"
		end
	end

	return (os.getenv("HOME") or "") .. "/.config/yazi"
end

local function get_all_commands()
	return parse_keymap_file(config_home() .. "/keymap.toml")
end

local function json_commands(commands)
	local encoded = {}
	local size = 2
	for _, command in ipairs(commands) do
		local run = command.run or ""
		local runs = command.runs or {}
		local description = command.description or ""
		local valid_runs = #runs > 0 and #runs <= 32
		for _, action in ipairs(runs) do
			if action == "" or #action > 4096 then
				valid_runs = false
				break
			end
		end
		if #run <= 4096 and #description <= 4096 and valid_runs then
			local run_items = {}
			for _, action in ipairs(runs) do
				table.insert(run_items, json_string(action))
			end
			local item = "{\"key\":" .. json_string(command.key or "")
				.. ",\"run\":" .. json_string(run)
				.. ",\"runs\":[" .. table.concat(run_items, ",") .. "]"
				.. ",\"description\":" .. json_string(description) .. "}"
			local separator = #encoded == 0 and 0 or 1
			if size + separator + #item > 60000 or #encoded >= 256 then
				break
			end
			table.insert(encoded, item)
			size = size + separator + #item
		end
	end
	return "[" .. table.concat(encoded, ",") .. "]"
end

local function json_snapshot(kind, state)
	return "{\"tab\":" .. tostring(state.tab)
		.. ",\"cwd\":" .. json_path(kind, state.cwd)
		.. ",\"hovered\":" .. json_path(kind, state.hovered)
		.. ",\"selected\":" .. json_path_array(kind, state.selected)
		.. "}"
end

local function json_state_update(kind, state)
	return "{\"present\":[\"tab\",\"cwd\",\"hovered\",\"selected\"]"
		.. ",\"tab\":" .. tostring(state.tab)
		.. ",\"cwd\":" .. json_path(kind, state.cwd)
		.. ",\"hovered\":" .. json_path(kind, state.hovered)
		.. ",\"selected\":" .. json_path_array(kind, state.selected)
		.. "}"
end

local function json_heartbeat(tab, revision)
	return "{\"present\":[\"tab\"],\"tab\":" .. tostring(tab)
		.. ",\"heartbeat\":true,\"revision\":" .. tostring(revision) .. "}"
end

local function json_envelope(instance_id, sequence, kind, payload)
	return "{\"protocol\":\"yazi-desktop-host/1\""
		.. ",\"instanceId\":" .. json_string(instance_id)
		.. ",\"sequence\":" .. tostring(sequence)
		.. ",\"kind\":" .. json_string(kind)
		.. ",\"payload\":" .. payload
		.. "}"
end

local get_state = ya.sync(function()
	local current = cx.active.current
	local selected = {}
	for _, url in pairs(cx.active.selected) do
		table.insert(selected, tostring(url))
	end
	table.sort(selected)

	return {
		tab = cx.tabs.idx,
		cwd = tostring(current.cwd),
		hovered = current.hovered and tostring(current.hovered.url) or nil,
		selected = selected,
	}
end)

local function states_equal(left, right)
	if not left or left.tab ~= right.tab or left.cwd ~= right.cwd or left.hovered ~= right.hovered
		or #left.selected ~= #right.selected then
		return false
	end

	for index, value in ipairs(left.selected) do
		if value ~= right.selected[index] then
			return false
		end
	end
	return true
end

local function setup(state, opts)
	if state.started then
		return
	end

	local pipe = opts.pipe or os.getenv("YAZI_DESKTOP_HOST_PIPE")
	local instance_id = opts.instance_id or os.getenv("YAZI_DESKTOP_HOST_INSTANCE_ID")
	local path_kind = opts.path_kind or "filesystem"
	local interval = opts.interval or 0.1
	local retry_interval = opts.retry_interval or 1
	if not pipe or not instance_id then
		ya.err("yazi-desktop-host requires YAZI_DESKTOP_HOST_PIPE and YAZI_DESKTOP_HOST_INSTANCE_ID")
		return
	end

	state.started = true
	ya.async(function()
		local function send(fd, sequence, kind, payload)
			local ok, write_err = fd:write_all(json_envelope(instance_id, sequence, kind, payload) .. "\n")
			if not ok then
				ya.err("yazi-desktop-host bridge write failed", write_err)
				return false
			end
			local flushed, flush_err = fd:flush()
			if not flushed then
				ya.err("yazi-desktop-host bridge flush failed", flush_err)
				return false
			end
			return true
		end

		while true do
			local fd, err = fs.access():write(true):open(Url(pipe))
			if not fd then
				ya.err("yazi-desktop-host could not open the bridge pipe", err)
				ya.sleep(retry_interval)
			else
				local sequence = 0
				local last_state
				local last_state_sequence
				local connected = send(fd, sequence, "hello", "{\"capabilities\":[\"snapshot\",\"state\",\"commands\",\"heartbeat\"]"
					.. ",\"commands\":" .. json_commands(get_all_commands()) .. "}")
				if connected then
					while true do
						local snapshot = get_state()
						sequence = sequence + 1
						local changed = not states_equal(last_state, snapshot)
						local kind = not last_state and "snapshot" or "state"
						local payload = kind == "snapshot"
							and json_snapshot(path_kind, snapshot)
							or changed
							and json_state_update(path_kind, snapshot)
							or json_heartbeat(snapshot.tab, last_state_sequence)
						if not send(fd, sequence, kind, payload) then
							connected = false
							break
						end
						if kind == "snapshot" or changed then
							last_state = snapshot
							last_state_sequence = sequence
						end
						ya.sleep(interval)
					end
				end
				if not connected then
					ya.sleep(retry_interval)
				end
			end
		end
	end)
end

return {
	setup = setup,
	parse_keymap_file = parse_keymap_file,
}
