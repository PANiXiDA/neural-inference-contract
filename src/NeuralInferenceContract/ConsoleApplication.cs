using NeuralInferenceContract.Model;
using NeuralInferenceContract.Scenarios;

namespace NeuralInferenceContract;

public static class ConsoleApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length > 1)
        {
            error.WriteLine("Ожидается имя одного сценария.");
            WriteUsage(error);
            return 2;
        }

        var selection = args.Length == 0 ? "all" : args[0].ToLowerInvariant();
        if (selection is "--help" or "-h")
        {
            WriteUsage(output);
            return 0;
        }

        var terms = new ContractTerms();
        var scenarios = ScenarioCatalog.Create(terms);
        if (selection == "--list")
        {
            foreach (var scenario in scenarios)
            {
                output.WriteLine($"{scenario.Name}: {scenario.Description}");
            }

            return 0;
        }

        var selected = selection == "all"
            ? scenarios
            : scenarios.Where(scenario => scenario.Name == selection).ToArray();

        if (selected.Count == 0)
        {
            error.WriteLine($"Неизвестный сценарий: {args[0]}");
            WriteUsage(error);
            return 2;
        }

        output.WriteLine($"Цена токена: {terms.PricePerToken}; лимит: {terms.MaxTokens}; резерв: {terms.Reservation}.");
        foreach (var scenario in selected)
        {
            WriteScenario(output, scenario, ScenarioRunner.Run(scenario, terms));
        }

        return 0;
    }

    private static void WriteScenario(TextWriter output, ContractScenario scenario, ScenarioRun run)
    {
        output.WriteLine();
        output.WriteLine($"Сценарий: {scenario.Name}");
        output.WriteLine(scenario.Description);
        output.WriteLine($"Начальное состояние: {run.InitialState.Stage}");

        foreach (var step in run.Steps)
        {
            var state = step.Result.State;
            var status = step.Result.Accepted ? "принято" : "отклонено";
            output.WriteLine($"[{step.Number:00}] {step.Event.Kind} / {step.Event.Actor}: "
                + $"{step.PreviousState.Stage} -> {state.Stage} ({status})");
            output.WriteLine($"     {step.Result.Description}");
            output.WriteLine($"     Резерв: {state.Escrow}; провайдеру: {state.ProviderPaid}; "
                + $"возврат: {state.UserRefunded}; срок: {DeadlineText(state)}; выход: {step.Result.Output}.");
        }

        output.WriteLine($"Итог: {run.FinalState.Stage}; "
            + (run.FinalState.IsFinal ? "расчёты завершены." : "расчёты не завершены."));
    }

    private static string DeadlineText(ContractState state) =>
        state.IsFinal || state.Stage == ContractStage.Idle ? "нет активного срока"
            : state.DeadlineReached ? "истёк" : "не истёк";

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("Использование: NeuralInferenceContract [all | имя-сценария | --list | --help]");
        output.WriteLine("Без аргументов выполняются все сценарии. Начальная конфигурация: p=1, U=2.");
    }
}
