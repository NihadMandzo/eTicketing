namespace eTicketing.Ticketing.Business.Analytics.Anomalies;

/// <summary>The SSA spike detector's output column: [Alert, Score, P-Value].</summary>
internal sealed class SpikePrediction
{
    [Microsoft.ML.Data.VectorType(3)]
    public double[] Prediction { get; set; } = [];
}
