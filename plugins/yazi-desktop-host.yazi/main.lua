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
				local last_snapshot
				local connected = send(fd, sequence, "hello", "{\"capabilities\":[\"snapshot\",\"state\"]}")
				if connected then
					while true do
						local snapshot = get_state()
						local encoded_snapshot = json_snapshot(path_kind, snapshot)
						if encoded_snapshot ~= last_snapshot then
							sequence = sequence + 1
							local kind = last_snapshot and "state" or "snapshot"
							local payload = kind == "snapshot"
								and encoded_snapshot
								or json_state_update(path_kind, snapshot)
							if not send(fd, sequence, kind, payload) then
								connected = false
								break
							end
							last_snapshot = encoded_snapshot
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
}
