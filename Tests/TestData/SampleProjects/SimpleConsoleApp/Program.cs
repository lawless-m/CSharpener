namespace SimpleConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        var calculator = new Calculator();
        var result = calculator.Add(5, 3);
        Console.WriteLine($"5 + 3 = {result}");

        // This method is used
        ProcessInput("test");
    }

    // This method is used by Main
    private static void ProcessInput(string input)
    {
        Console.WriteLine($"Processing: {input}");
        ValidateInput(input);
    }

    // This method is used by ProcessInput
    private static bool ValidateInput(string input)
    {
        return !string.IsNullOrEmpty(input);
    }

    // This method is UNUSED - should be detected
    private static void UnusedMethod()
    {
        Console.WriteLine("This method is never called");
    }

    // This method is UNUSED - should be detected
    private static int UnusedCalculation(int a, int b)
    {
        return a * b * 100;
    }
}

public class Calculator
{
    // This method is used
    public int Add(int a, int b)
    {
        return a + b;
    }

    // This method is used by Add through a property
    public int LastResult { get; private set; }

    // This method is UNUSED - should be detected
    public int Subtract(int a, int b)
    {
        return a - b;
    }

    // This private method is UNUSED - should be detected with high confidence
    private int Multiply(int a, int b)
    {
        return a * b;
    }

    // This method is UNUSED - should be detected
    private void LogOperation(string operation)
    {
        Console.WriteLine($"Operation: {operation}");
    }
}

public class UnusedHelper
{
    // This entire class is unused

    public void DoSomething()
    {
        Console.WriteLine("Doing something");
    }

    public int Calculate(int x)
    {
        return x * 2;
    }

    private void InternalMethod()
    {
        Console.WriteLine("Internal");
    }
}
