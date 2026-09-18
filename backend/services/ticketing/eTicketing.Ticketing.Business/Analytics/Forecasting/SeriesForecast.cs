namespace eTicketing.Ticketing.Business.Analytics.Forecasting;

internal sealed class SeriesForecast
{
    public float[] Forecast { get; set; } = [];
    public float[] LowerBound { get; set; } = [];
    public float[] UpperBound { get; set; } = [];
}
