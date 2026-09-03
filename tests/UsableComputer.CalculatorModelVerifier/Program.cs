using UsableComputer.Logic;

var tests = new (string Name, Action Test)[]
{
    ("chained operations", TestChainedOperations),
    ("decimal entry", TestDecimalEntry),
    ("clear", TestClear),
    ("negative result", TestNegativeResult),
    ("divide by zero", TestDivideByZero),
};

foreach (var test in tests)
{
    test.Test();
    Console.WriteLine($"PASS {test.Name}");
}

static void TestChainedOperations()
{
    var calculator = new CalculatorModel();
    calculator.InputDigit(1);
    calculator.InputDigit(2);
    Assert(calculator.ApplyOperator(CalculatorOperator.Add), "add should be accepted");
    calculator.InputDigit(3);
    Assert(calculator.ApplyOperator(CalculatorOperator.Subtract), "subtract should be accepted");
    calculator.InputDigit(4);
    Assert(calculator.PressEquals(), "equals should be accepted");
    Assert(calculator.DisplayText == "11", $"expected 11, got {calculator.DisplayText}");
}

static void TestDecimalEntry()
{
    var calculator = new CalculatorModel();
    calculator.InputDigit(2);
    calculator.InputDecimal();
    calculator.InputDigit(5);
    Assert(calculator.ApplyOperator(CalculatorOperator.Multiply), "multiply should be accepted");
    calculator.InputDigit(2);
    Assert(calculator.PressEquals(), "equals should be accepted");
    Assert(calculator.DisplayText == "5", $"expected 5, got {calculator.DisplayText}");
}

static void TestClear()
{
    var calculator = new CalculatorModel();
    calculator.InputDigit(9);
    calculator.Clear();
    Assert(calculator.DisplayText == "0", $"expected 0, got {calculator.DisplayText}");
    Assert(!calculator.HasError, "clear should remove errors");
}

static void TestNegativeResult()
{
    var calculator = new CalculatorModel();
    calculator.InputDigit(2);
    Assert(calculator.ApplyOperator(CalculatorOperator.Subtract), "subtract should be accepted");
    calculator.InputDigit(5);
    Assert(calculator.PressEquals(), "equals should be accepted");
    Assert(calculator.DisplayText == "-3", $"expected -3, got {calculator.DisplayText}");
}

static void TestDivideByZero()
{
    var calculator = new CalculatorModel();
    calculator.InputDigit(8);
    Assert(calculator.ApplyOperator(CalculatorOperator.Divide), "divide should be accepted");
    calculator.InputDigit(0);
    Assert(!calculator.PressEquals(), "divide by zero should be rejected");
    Assert(calculator.HasError, "divide by zero should set an error");
    Assert(calculator.ErrorMessage == "Cannot divide by zero.", "unexpected divide-by-zero message");
    Assert(calculator.DisplayText == "ERR", $"expected ERR, got {calculator.DisplayText}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
