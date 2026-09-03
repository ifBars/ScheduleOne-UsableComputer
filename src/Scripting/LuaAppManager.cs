using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MelonLoader.Utils;
using MoonSharp.Interpreter;
using S1API.Entities;
using S1API.Money;
using S1API.Property;
using UsableComputer.API;
using UsableComputer.Logic;
using UsableComputer.Native;
using UsableComputer.UI;

namespace UsableComputer.Scripting;

internal static class LuaAppManager
{
    private const int MaximumSourceLength = 65_536;
    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9._-]{2,63}$", RegexOptions.CultureInvariant);
    private static readonly Dictionary<string, LuaAppDefinition> Definitions = new(StringComparer.Ordinal);
    private static string? _lastSource;

    internal static string AppsDirectory => Path.Combine(
        MelonEnvironment.UserDataDirectory,
        "UsableComputer",
        "Apps");

    internal static string StarterSource => """
        return {
          id = "my-dashboard",
          title = "My Dashboard",
          icon = "studio",
          width = 560,
          height = 340,

          render = function()
            local money = computer.money()
            local player = computer.player()
            local properties = computer.properties()

            return {
              { kind = "heading", text = "My Dashboard" },
              { kind = "stat", label = "Cash", value = computer.currency(money.cash) },
              { kind = "stat", label = "Bank", value = computer.currency(money.online) },
              { kind = "stat", label = "Net worth", value = computer.currency(money.net_worth) },
              { kind = "text", text = "Region: " .. player.region },
              { kind = "text", text = "Owned properties: " .. #properties },
            }
          end
        }
        """;

    internal static void Initialize()
    {
        Directory.CreateDirectory(AppsDirectory);
        ValidateBundledTemplates();
        foreach (string path in Directory.EnumerateFiles(AppsDirectory, "*.lua").OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string source = File.ReadAllText(path);
                if (TryCompile(source, path, out LuaAppDefinition? definition, out string error))
                {
                    Register(definition!);
                    _lastSource ??= source;
                }
                else
                {
                    MelonLoader.MelonLogger.Warning($"[{Constants.ModName}] Lua app '{path}' was skipped: {error}");
                }
            }
            catch (Exception exception)
            {
                MelonLoader.MelonLogger.Warning($"[{Constants.ModName}] Could not read Lua app '{path}': {exception.Message}");
            }
        }
    }

    private static void ValidateBundledTemplates()
    {
        foreach (LuaAppTemplate template in LuaAppTemplates.All)
        {
            string sourcePath = $"built-in-template:{template.Name}";
            if (!TryCompile(template.Source, sourcePath, out _, out string error))
            {
                MelonLoader.MelonLogger.Error(
                    $"[{Constants.ModName}] Bundled Lua template '{template.Name}' is invalid: {error}");
            }
        }
    }

    internal static string GetEditorSource() => _lastSource ?? StarterSource;

    internal static bool TryValidateBundledTemplatesAtRuntime(out string message)
    {
        foreach (LuaAppTemplate template in LuaAppTemplates.All)
        {
            if (!TryCompile(template.Source, $"built-in-template:{template.Name}", out LuaAppDefinition? definition, out message))
                return false;

            try
            {
                DynValue rows = LuaExecutionBudget.RunFunction(
                    definition!.Script,
                    definition.Render,
                    $"{template.Name} template render");
                if (rows.Type != DataType.Table)
                {
                    message = $"{template.Name} render did not return a row table.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                message = $"{template.Name} render failed: {exception.Message}";
                return false;
            }
        }

        message = $"Validated {LuaAppTemplates.All.Count} bundled templates.";
        return true;
    }

    internal static bool TrySaveAndRegister(string source, out string message)
    {
        string temporaryPath = Path.Combine(AppsDirectory, "unsaved.lua");
        if (!TryCompile(source, temporaryPath, out LuaAppDefinition? definition, out message))
            return false;

        string fileName = definition!.Id.Replace('.', '_') + ".lua";
        string path = Path.Combine(AppsDirectory, fileName);
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetFullPath(AppsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            message = "The app ID produced an invalid save path.";
            return false;
        }

        File.WriteAllText(fullPath, source);
        Register(new LuaAppDefinition(
            definition.Id,
            definition.Title,
            definition.Icon,
            definition.WindowSize,
            definition.Script,
            definition.Render,
            source,
            fullPath));
        _lastSource = source;
        message = $"Saved and loaded {definition.Title}.";
        return true;
    }

    internal static void Shutdown()
    {
        foreach (LuaAppDefinition definition in Definitions.Values.ToArray())
            DesktopAppRegistry.Unregister(definition.RegistryId);
        Definitions.Clear();
        _lastSource = null;
    }

    private static void Register(LuaAppDefinition definition)
    {
        if (Definitions.TryGetValue(definition.Id, out LuaAppDefinition? existing))
            DesktopAppRegistry.Unregister(existing.RegistryId);

        Definitions[definition.Id] = definition;
        DesktopAppRegistry.Register(new DesktopAppDescriptor(
            definition.RegistryId,
            definition.Title,
            "",
            definition.WindowSize,
            UnityEngine.Vector2.zero,
            context => new LuaDesktopAppSession(context, definition),
            definition.ResolveIcon));
    }

    private static bool TryCompile(
        string source,
        string sourcePath,
        out LuaAppDefinition? definition,
        out string error)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(source))
        {
            error = "Enter a Lua app definition first.";
            return false;
        }
        if (source.Length > MaximumSourceLength)
        {
            error = $"Lua apps are limited to {MaximumSourceLength / 1024} KiB.";
            return false;
        }

        try
        {
            var script = new Script(CoreModules.Preset_HardSandbox);
            RegisterComputerApi(script);
            DynValue result = LuaExecutionBudget.RunSource(script, source, sourcePath);
            if (result.Type != DataType.Table)
                throw new ScriptRuntimeException("The file must return an app table.");

            Table table = result.Table;
            string id = ReadRequiredString(table, "id", 64).ToLowerInvariant();
            if (!IdPattern.IsMatch(id))
                throw new ScriptRuntimeException("id must be 3-64 lowercase letters, numbers, dots, underscores, or hyphens.");
            string title = ReadRequiredString(table, "title", 40);
            string icon = ReadOptionalString(table, "icon", "generic", 24);
            float width = ReadNumber(table, "width", 520f, 320f, 700f);
            float height = ReadNumber(table, "height", 340f, 220f, 470f);
            DynValue render = table.Get("render");
            if (render.Type != DataType.Function && render.Type != DataType.ClrFunction)
                throw new ScriptRuntimeException("render must be a function that returns an array of rows.");

            definition = new LuaAppDefinition(
                id,
                title,
                icon,
                new UnityEngine.Vector2(width, height),
                script,
                render,
                source,
                sourcePath);
            error = string.Empty;
            return true;
        }
        catch (SyntaxErrorException exception)
        {
            error = exception.DecoratedMessage ?? exception.Message;
            return false;
        }
        catch (ScriptRuntimeException exception)
        {
            error = exception.DecoratedMessage ?? exception.Message;
            return false;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void RegisterComputerApi(Script script)
    {
        var computer = new Table(script);
        computer.Set("money", DynValue.NewCallback((_, _) => DynValue.NewTable(CreateMoneyTable(script))));
        computer.Set("player", DynValue.NewCallback((_, _) => DynValue.NewTable(CreatePlayerTable(script))));
        computer.Set("properties", DynValue.NewCallback((_, _) => DynValue.NewTable(CreatePropertiesTable(script))));
        computer.Set("employees", DynValue.NewCallback((_, _) => DynValue.NewTable(CreateEmployeesTable(script))));
        computer.Set("products", DynValue.NewCallback((_, _) => DynValue.NewTable(CreateProductsTable(script))));
        computer.Set("quests", DynValue.NewCallback((_, _) => DynValue.NewTable(CreateQuestsTable(script))));
        computer.Set("currency", DynValue.NewCallback((_, args) =>
        {
            double value = args.Count > 0 ? args[0].CastToNumber() ?? 0d : 0d;
            return DynValue.NewString($"${value:0.##}");
        }));
        script.Globals.Set("computer", DynValue.NewTable(computer));
    }

    private static Table CreateMoneyTable(Script script)
    {
        var table = new Table(script);
        table.Set("cash", DynValue.NewNumber(Money.GetCashBalance()));
        table.Set("online", DynValue.NewNumber(Money.GetOnlineBalance()));
        table.Set("net_worth", DynValue.NewNumber(Money.GetNetWorth()));
        return table;
    }

    private static Table CreatePlayerTable(Script script)
    {
        var table = new Table(script);
        Player? player = Player.Local;
        table.Set("name", DynValue.NewString(player?.Name ?? "Player"));
        table.Set("health", DynValue.NewNumber(player?.CurrentHealth ?? 0f));
        table.Set("max_health", DynValue.NewNumber(player?.MaxHealth ?? 0f));
        table.Set("region", DynValue.NewString(player?.CurrentRegion.ToString() ?? "Unknown"));
        table.Set("current_property", DynValue.NewString(player?.CurrentProperty?.PropertyName ?? string.Empty));
        return table;
    }

    private static Table CreatePropertiesTable(Script script)
    {
        var table = new Table(script);
        int index = 1;
        foreach (PropertyWrapper property in PropertyManager.GetOwnedProperties())
        {
            var row = new Table(script);
            row.Set("name", DynValue.NewString(property.PropertyName));
            row.Set("code", DynValue.NewString(property.PropertyCode));
            row.Set("price", DynValue.NewNumber(property.Price));
            row.Set("employee_count", DynValue.NewNumber(property.EmployeeCount));
            row.Set("employee_capacity", DynValue.NewNumber(property.EmployeeCapacity));
            table.Set(index++, DynValue.NewTable(row));
        }
        return table;
    }

    private static Table CreateEmployeesTable(Script script)
    {
        var table = new Table(script);
        int index = 1;
        foreach (PropertyWrapper property in PropertyManager.GetOwnedProperties())
        {
            var row = new Table(script);
            row.Set("property", DynValue.NewString(property.PropertyName));
            row.Set("count", DynValue.NewNumber(property.EmployeeCount));
            row.Set("capacity", DynValue.NewNumber(property.EmployeeCapacity));
            table.Set(index++, DynValue.NewTable(row));
        }
        return table;
    }

    private static Table CreateProductsTable(Script script)
    {
        var table = new Table(script);
        int index = 1;
        foreach (ProductViewModel product in ProductManagerNativeAdapter.ReadDiscoveredProducts())
        {
            var row = new Table(script);
            row.Set("id", DynValue.NewString(product.Id));
            row.Set("name", DynValue.NewString(product.Name));
            row.Set("product_type", DynValue.NewString(product.ProductType));
            row.Set("market_value", DynValue.NewNumber(product.MarketValue));
            row.Set("listed", DynValue.NewBoolean(product.IsListed));
            row.Set("favourited", DynValue.NewBoolean(product.IsFavourited));
            table.Set(index++, DynValue.NewTable(row));
        }
        return table;
    }

    private static Table CreateQuestsTable(Script script)
    {
        var table = new Table(script);
        int index = 1;
        foreach (JournalQuestViewModel quest in JournalNativeAdapter.ReadActiveQuests())
        {
            var row = new Table(script);
            row.Set("id", DynValue.NewString(quest.Id));
            row.Set("title", DynValue.NewString(quest.Title));
            row.Set("subtitle", DynValue.NewString(quest.Subtitle));
            row.Set("description", DynValue.NewString(quest.Description));
            row.Set("state", DynValue.NewString(quest.State));
            row.Set("tracked", DynValue.NewBoolean(quest.IsTracked));
            row.Set("entry_count", DynValue.NewNumber(quest.Entries.Count));
            table.Set(index++, DynValue.NewTable(row));
        }
        return table;
    }

    private static string ReadRequiredString(Table table, string field, int maxLength)
    {
        DynValue value = table.Get(field);
        if (value.Type != DataType.String || string.IsNullOrWhiteSpace(value.String))
            throw new ScriptRuntimeException($"{field} is required and must be text.");
        string result = value.String.Trim();
        if (result.Length > maxLength)
            throw new ScriptRuntimeException($"{field} may be at most {maxLength} characters.");
        return result;
    }

    private static string ReadOptionalString(Table table, string field, string fallback, int maxLength)
    {
        DynValue value = table.Get(field);
        if (value.IsNil())
            return fallback;
        if (value.Type != DataType.String)
            throw new ScriptRuntimeException($"{field} must be text.");
        string result = value.String.Trim();
        if (result.Length > maxLength)
            throw new ScriptRuntimeException($"{field} may be at most {maxLength} characters.");
        return result;
    }

    private static float ReadNumber(Table table, string field, float fallback, float minimum, float maximum)
    {
        DynValue value = table.Get(field);
        if (value.IsNil())
            return fallback;
        if (value.Type != DataType.Number || double.IsNaN(value.Number) || double.IsInfinity(value.Number))
            throw new ScriptRuntimeException($"{field} must be a finite number.");
        return UnityEngine.Mathf.Clamp((float)value.Number, minimum, maximum);
    }
}
