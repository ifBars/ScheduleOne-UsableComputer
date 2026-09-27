-- Paste into App Studio. Counts are kept separately in each host save.
return {
  api_version = 1,
  id = "shift-tally",
  title = "Shift Tally",
  icon = "notes",
  render = function()
    local count = tonumber(computer.storage.get("completed")) or 0
    return {
      { kind = "heading", text = "Shift Tally" },
      { kind = "text", text = "Mark completed tasks during your shift." },
      { kind = "stat", label = "Tasks completed", value = tostring(count) },
      { kind = "button", text = "Complete a task", action = function()
          local latest = tonumber(computer.storage.get("completed")) or 0
          computer.storage.set("completed", tostring(latest + 1))
        end },
      { kind = "button", text = "Start a new tally", action = function()
          computer.storage.delete("completed")
        end },
    }
  end
}
