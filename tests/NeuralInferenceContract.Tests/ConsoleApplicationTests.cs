namespace NeuralInferenceContract.Tests;

public sealed class ConsoleApplicationTests
{
    [Theory(DisplayName = "Поддерживаемые аргументы выводят результат без ошибок")]
    [InlineData("--help")]
    [InlineData("--list")]
    [InlineData("all")]
    [InlineData("success")]
    [InlineData("SUCCESS")]
    public void Supported_arguments_produce_output(string argument)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, ConsoleApplication.Run([argument], output, error));
        Assert.NotEmpty(output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact(DisplayName = "Запуск без аргументов выполняет все сценарии")]
    public void Default_execution_runs_every_scenario_including_unfinished_one()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, ConsoleApplication.Run([], output, error));
        Assert.Contains("Сценарий: success", output.ToString());
        Assert.Contains("Сценарий: no-initiator", output.ToString());
        Assert.Contains("Итог: Processing; расчёты не завершены.", output.ToString());
        Assert.Empty(error.ToString());
    }

    [Theory(DisplayName = "Неизвестный аргумент возвращает ошибку использования")]
    [InlineData("unknown")]
    [InlineData("--price")]
    public void Unknown_argument_returns_usage_error(string argument)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, ConsoleApplication.Run([argument], output, error));
        Assert.Empty(output.ToString());
        Assert.Contains("--list", error.ToString());
    }

    [Fact(DisplayName = "Лишние аргументы командной строки отклоняются")]
    public void Extra_arguments_are_rejected()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, ConsoleApplication.Run(["success", "all"], output, error));
        Assert.Empty(output.ToString());
        Assert.NotEmpty(error.ToString());
    }
}
