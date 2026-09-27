local tasks = {
  { key = "supplies", text = "Check supply stock" },
  { key = "deliveries", text = "Review active deliveries" },
  { key = "customers", text = "Review dealer customer assignments" },
}

return {
  api_version = 1,
  id = "shift-checklist",
  title = "Shift Checklist",
  icon = "notes",
  render = function()
    local rows = {
      { kind = "heading", text = "Before the next shift" },
      { kind = "text", text = "Mark each task after checking it in the game." },
    }
    for _, task in ipairs(tasks) do
      local key = task.key
      rows[#rows + 1] = {
        kind = "checkbox", text = task.text,
        value = computer.storage.get(key) == "done",
        action = function(checked)
          if checked then computer.storage.set(key, "done")
          else computer.storage.delete(key) end
        end,
      }
    end
    rows[#rows + 1] = { kind = "button", text = "Reset checklist", action = function()
      for _, task in ipairs(tasks) do computer.storage.delete(task.key) end
    end }
    return rows
  end,
}
