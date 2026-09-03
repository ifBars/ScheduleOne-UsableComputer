using System.Collections.Generic;

namespace UsableComputer.Scripting;

internal sealed class LuaAppTemplate
{
    internal LuaAppTemplate(string name, string source)
    {
        Name = name;
        Source = source;
    }

    internal string Name { get; }
    internal string Source { get; }
}

internal static class LuaAppTemplates
{
    internal static IReadOnlyList<LuaAppTemplate> All { get; } = new[]
    {
        new LuaAppTemplate("Notes", """
            local note = "Replace this text with your own app state or instructions."

            return {
              id = "example-notes",
              title = "Notes Example",
              icon = "notes",
              width = 520,
              height = 360,
              render = function()
                local player = computer.player()
                return {
                  { kind = "heading", text = "Notes" },
                  { kind = "text", text = note },
                  { kind = "text", text = "Current location: " .. player.current_property },
                  { kind = "text", text = "Region: " .. player.region },
                }
              end
            }
            """),
        new LuaAppTemplate("Calculator", """
            return {
              id = "example-calculator",
              title = "Calculator Example",
              icon = "calculator",
              width = 420,
              height = 340,
              render = function()
                local money = computer.money()
                local liquid = money.cash + money.online
                return {
                  { kind = "heading", text = "Money Calculator" },
                  { kind = "stat", label = "Cash + bank", value = computer.currency(liquid) },
                  { kind = "stat", label = "Cash share", value = string.format("%.1f%%", liquid > 0 and money.cash / liquid * 100 or 0) },
                  { kind = "stat", label = "Net worth gap", value = computer.currency(money.net_worth - liquid) },
                }
              end
            }
            """),
        new LuaAppTemplate("About", """
            return {
              id = "example-about",
              title = "About Example",
              icon = "about",
              width = 430,
              height = 300,
              render = function()
                local player = computer.player()
                return {
                  { kind = "heading", text = "Player Profile" },
                  { kind = "stat", label = "Name", value = player.name },
                  { kind = "stat", label = "Health", value = string.format("%.0f / %.0f", player.health, player.max_health) },
                  { kind = "stat", label = "Region", value = player.region },
                  { kind = "text", text = "This data is read live through the sandboxed computer API." },
                }
              end
            }
            """),
        new LuaAppTemplate("Journal", """
            return {
              id = "example-journal",
              title = "Journal Example",
              icon = "journal",
              width = 620,
              height = 430,
              render = function()
                local quests = computer.quests()
                local rows = { { kind = "heading", text = "Active Quests" } }
                if #quests == 0 then
                  rows[#rows + 1] = { kind = "text", text = "No active quests." }
                end
                for i = 1, #quests do
                  local quest = quests[i]
                  rows[#rows + 1] = { kind = "stat", label = quest.title, value = quest.tracked and "Tracked" or quest.state }
                  rows[#rows + 1] = { kind = "text", text = quest.subtitle .. " · " .. quest.entry_count .. " objectives" }
                end
                return rows
              end
            }
            """),
        new LuaAppTemplate("Products", """
            return {
              id = "example-products",
              title = "Products Example",
              icon = "products",
              width = 700,
              height = 470,
              render = function()
                local products = computer.products()
                local rows = { { kind = "heading", text = "Discovered Products" } }
                for i = 1, #products do
                  local product = products[i]
                  local flags = product.listed and "Listed" or "Not listed"
                  if product.favourited then flags = flags .. " · Favourite" end
                  rows[#rows + 1] = { kind = "stat", label = product.name, value = computer.currency(product.market_value) }
                  rows[#rows + 1] = { kind = "text", text = product.product_type .. " · " .. flags }
                end
                if #products == 0 then rows[#rows + 1] = { kind = "text", text = "No products discovered yet." } end
                return rows
              end
            }
            """),
        new LuaAppTemplate("App Studio", """
            return {
              id = "example-api-explorer",
              title = "API Explorer",
              icon = "studio",
              width = 600,
              height = 400,
              render = function()
                local money = computer.money()
                return {
                  { kind = "heading", text = "Computer API Explorer" },
                  { kind = "text", text = "Available snapshots: money, player, properties, employees, products, quests." },
                  { kind = "stat", label = "Owned properties", value = tostring(#computer.properties()) },
                  { kind = "stat", label = "Employee locations", value = tostring(#computer.employees()) },
                  { kind = "stat", label = "Discovered products", value = tostring(#computer.products()) },
                  { kind = "stat", label = "Active quests", value = tostring(#computer.quests()) },
                  { kind = "stat", label = "Net worth", value = computer.currency(money.net_worth) },
                }
              end
            }
            """),
        new LuaAppTemplate("Settings", """
            return {
              id = "example-settings",
              title = "Settings Example",
              icon = "settings",
              width = 520,
              height = 360,
              render = function()
                local player = computer.player()
                local properties = computer.properties()
                return {
                  { kind = "heading", text = "Gameplay Preferences" },
                  { kind = "text", text = "Use Lua locals for app-specific settings; host desktop settings remain permission-protected." },
                  { kind = "stat", label = "Current region", value = player.region },
                  { kind = "stat", label = "Property context", value = player.current_property },
                  { kind = "stat", label = "Owned properties", value = tostring(#properties) },
                }
              end
            }
            """),
    };
}
