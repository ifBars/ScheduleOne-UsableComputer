using System.Globalization;
using System.Text;

namespace UsableComputer.Logic;

public enum CalculatorOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
}

/// <summary>
/// Small, deterministic calculator state machine. It never parses or executes expressions.
/// </summary>
public sealed class CalculatorModel
{
    private decimal _accumulator;
    private string _entry = "0";
    private CalculatorOperator? _pendingOperator;
    private bool _hasAccumulator;
    private bool _replaceEntry = true;
    private string? _errorMessage;

    public string DisplayText => _errorMessage == null ? _entry : "ERR";

    public string? ErrorMessage => _errorMessage;

    public bool HasError => _errorMessage != null;

    public static string SanitizeEntryText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "0";

        var builder = new StringBuilder(text.Length);
        var hasDecimal = false;
        var hasDigit = false;
        foreach (char character in text)
        {
            if (character >= '0' && character <= '9')
            {
                builder.Append(character);
                hasDigit = true;
                continue;
            }

            if (character == '-' && builder.Length == 0)
            {
                builder.Append(character);
                continue;
            }

            if (character == '.' && !hasDecimal)
            {
                if (builder.Length == 0)
                    builder.Append('0');
                else if (builder.Length == 1 && builder[0] == '-')
                    builder.Append('0');

                builder.Append('.');
                hasDecimal = true;
            }
        }

        if (builder.Length == 0)
            return "0";
        if (builder.Length == 1 && builder[0] == '-')
            return "-";
        if (!hasDigit && builder[^1] != '.')
            return "0";

        return builder.ToString();
    }

    public void Clear()
    {
        _accumulator = 0m;
        _entry = "0";
        _pendingOperator = null;
        _hasAccumulator = false;
        _replaceEntry = true;
        _errorMessage = null;
    }

    public void InputDigit(int digit)
    {
        if (digit is < 0 or > 9)
            throw new ArgumentOutOfRangeException(nameof(digit));

        if (_errorMessage != null)
            Clear();

        if (_replaceEntry)
        {
            if (!_pendingOperator.HasValue)
                _hasAccumulator = false;

            _entry = digit == 0 ? "0" : digit.ToString(CultureInfo.InvariantCulture);
            _replaceEntry = false;
            return;
        }

        if (_entry == "0")
        {
            _entry = digit == 0 ? "0" : digit.ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (_entry == "-0")
        {
            _entry = digit == 0 ? "-0" : "-" + digit.ToString(CultureInfo.InvariantCulture);
            return;
        }

        _entry += digit.ToString(CultureInfo.InvariantCulture);
    }

    public void InputDecimal()
    {
        if (_errorMessage != null)
            Clear();

        if (_replaceEntry)
        {
            if (!_pendingOperator.HasValue)
                _hasAccumulator = false;

            _entry = "0.";
            _replaceEntry = false;
            return;
        }

        if (_entry.IndexOf('.') < 0)
            _entry += ".";
    }

    public bool SetEntryText(string? text)
    {
        if (_errorMessage != null)
            Clear();

        string normalized = SanitizeEntryText(text);
        if (!IsTransientEntry(normalized) &&
            !decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out _))
        {
            return false;
        }

        if (_replaceEntry && !_pendingOperator.HasValue)
            _hasAccumulator = false;

        _entry = normalized;
        _replaceEntry = false;
        return true;
    }

    public bool ApplyOperator(CalculatorOperator calculatorOperator)
    {
        if (_errorMessage != null)
            return false;

        if (!_hasAccumulator)
        {
            if (!TryParseEntry(out _accumulator))
            {
                SetError("Enter a number.");
                return false;
            }

            _hasAccumulator = true;
        }
        else if (_pendingOperator.HasValue && !_replaceEntry && !ApplyPendingOperator())
        {
            return false;
        }

        _pendingOperator = calculatorOperator;
        _replaceEntry = true;
        return true;
    }

    public bool PressEquals()
    {
        if (_errorMessage != null)
            return false;

        if (!_hasAccumulator)
        {
            if (!TryParseEntry(out _accumulator))
            {
                SetError("Enter a number.");
                return false;
            }

            _hasAccumulator = true;
        }

        if (_pendingOperator.HasValue && !_replaceEntry && !ApplyPendingOperator())
            return false;

        _pendingOperator = null;
        _replaceEntry = true;
        return true;
    }

    private bool ApplyPendingOperator()
    {
        if (!_pendingOperator.HasValue || !TryParseEntry(out decimal rightHandSide))
        {
            SetError("Enter a number.");
            return false;
        }

        decimal result;
        switch (_pendingOperator.Value)
        {
            case CalculatorOperator.Add:
                result = _accumulator + rightHandSide;
                break;
            case CalculatorOperator.Subtract:
                result = _accumulator - rightHandSide;
                break;
            case CalculatorOperator.Multiply:
                result = _accumulator * rightHandSide;
                break;
            case CalculatorOperator.Divide:
                if (rightHandSide == 0m)
                {
                    SetError("Cannot divide by zero.");
                    return false;
                }

                result = _accumulator / rightHandSide;
                break;
            default:
                throw new InvalidOperationException("Unknown calculator operator.");
        }

        _accumulator = result;
        _entry = Format(result);
        _replaceEntry = true;
        return true;
    }

    private bool TryParseEntry(out decimal value)
    {
        if (IsTransientEntry(_entry))
        {
            value = 0m;
            return false;
        }

        return decimal.TryParse(
            _entry,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool IsTransientEntry(string value)
    {
        return value is "-" or "." or "-." ||
               value.EndsWith(".", StringComparison.Ordinal);
    }

    private static string Format(decimal value)
    {
        return value.ToString("G29", CultureInfo.InvariantCulture);
    }

    private void SetError(string message)
    {
        _errorMessage = message;
        _accumulator = 0m;
        _entry = "0";
        _pendingOperator = null;
        _hasAccumulator = false;
        _replaceEntry = true;
    }
}
