using NeuralInferenceContract.Analysis;
using NeuralInferenceContract.Automata;
using NeuralInferenceContract.Export;
using NeuralInferenceContract.Model;
using NeuralInferenceContract.Scenarios;

namespace NeuralInferenceContract;

public static class ConsoleApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length > 0 && args[0].Equals("--probability", StringComparison.OrdinalIgnoreCase))
        {
            return AnalyzeProbability(args, output, error);
        }

        if (args.Length > 0 && args[0].Equals("--analyze", StringComparison.OrdinalIgnoreCase))
        {
            return Analyze(args, output, error);
        }

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

    private static int Analyze(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length is < 2 or > 3)
        {
            WriteUsage(error);
            return 2;
        }

        var terms = new ContractTerms();
        var scenario = ScenarioCatalog.Create(terms)
            .FirstOrDefault(candidate => candidate.Name.Equals(args[1], StringComparison.OrdinalIgnoreCase));
        if (scenario is null)
        {
            error.WriteLine($"Неизвестный сценарий: {args[1]}");
            return 2;
        }

        var environment = EnvironmentAutomaton.FromEvents(scenario.Events);
        var graph = CompositionGraph.Build(new ContractComposition(environment, terms));
        if (args.Length == 3)
        {
            try
            {
                CompositionExport.Write(args[2], scenario.Name, graph, GeometricImage.Build(graph, maxLength: 8));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                error.WriteLine($"Ошибка экспорта: {exception.Message}");
                return 1;
            }
        }

        var entry = graph.States[graph.CycleStart];
        output.WriteLine($"Сценарий: {scenario.Name}");
        output.WriteLine($"Достижимо состояний: {graph.States.Count}; переходов: {graph.Edges.Count}.");
        output.WriteLine($"Замкнутая компонента: {(graph.IsCritical ? "критическая" : "некритическая")}; "
            + $"шаг входа: {graph.CycleStart}; состояний: {graph.CycleLength}.");
        output.WriteLine($"Состояние при входе: {entry.Contract.Stage}; резерв: {entry.Contract.Escrow}.");
        if (graph.CycleStart > 0)
        {
            var path = graph.Edges.Take(graph.CycleStart).Select(edge => edge.Event.Kind);
            output.WriteLine($"Путь к компоненте: {string.Join(" → ", path)}.");
        }

        return 0;
    }

    private static int AnalyzeProbability(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length > 2)
        {
            WriteUsage(error);
            return 2;
        }

        var terms = new ContractTerms();
        var analysis = ProbabilityAnalysis.Build(terms);
        if (args.Length == 2)
        {
            try
            {
                ProbabilityExport.Write(args[1], analysis);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                error.WriteLine($"Ошибка экспорта: {exception.Message}");
                return 1;
            }
        }

        output.WriteLine("Вероятностная среда: независимые равновероятные двоичные решения.");
        foreach (var path in analysis.Paths)
        {
            output.WriteLine(FormattableString.Invariant(
                $"{path.Environment.Code}: P={path.Environment.Probability}; {path.Outcome}; резерв={path.EntryState.Escrow}."));
        }

        output.WriteLine(FormattableString.Invariant(
            $"Оплата: {analysis.SettledProbability}; возврат: {analysis.RefundedProbability}; блокировка: {analysis.LockedProbability}."));
        output.WriteLine(FormattableString.Invariant(
            $"Сумма вероятностей: {analysis.TotalProbability}; средний заблокированный резерв: {analysis.ExpectedEscrow}."));
        output.WriteLine("Влияние вероятности участия инициатора (остальные вероятности равны 0.5):");
        foreach (var probability in new[] { 0m, 0.5m, 1m })
        {
            var comparison = ProbabilityAnalysis.Build(terms, new EnvironmentProbabilities(timeoutInitiator: probability));
            output.WriteLine(FormattableString.Invariant(
                $"a={probability}: оплата={comparison.SettledProbability}; возврат={comparison.RefundedProbability}; блокировка={comparison.LockedProbability}."));
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
        output.WriteLine("Анализ сплетения: NeuralInferenceContract --analyze имя-сценария [каталог экспорта]");
        output.WriteLine("Вероятностный анализ: NeuralInferenceContract --probability [каталог экспорта]");
        output.WriteLine("Без аргументов выполняются все сценарии. Начальная конфигурация: p=1, U=2.");
    }
}
