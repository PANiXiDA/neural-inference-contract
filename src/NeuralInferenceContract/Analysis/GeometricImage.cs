namespace NeuralInferenceContract.Analysis;

public sealed record GeometricPoint(
    int Length, double X, double Y, int StateIndex, int Output, bool InClosedComponent, bool IsCritical);

public static class GeometricImage
{
    public static IReadOnlyList<GeometricPoint> Build(CompositionGraph graph, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxLength, 24);

        var points = new List<GeometricPoint>(maxLength);
        var stateIndex = 0;
        var x = 0d;
        var y = 0d;
        var xWeight = 1d;
        var yWeight = 1d;

        for (var length = 1; length <= maxLength; length++)
        {
            var edge = graph.Edges[stateIndex];
            x += xWeight;
            y += (edge.Result.Output + 1) * yWeight;
            stateIndex = edge.To;
            var inComponent = stateIndex >= graph.CycleStart;

            points.Add(new GeometricPoint(
                length, x, y, stateIndex, edge.Result.Output, inComponent, inComponent && graph.IsCritical));

            xWeight /= 2;
            yWeight /= 3;
        }

        return points.AsReadOnly();
    }
}
