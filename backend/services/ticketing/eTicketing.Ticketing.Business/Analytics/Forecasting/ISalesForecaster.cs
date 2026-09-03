using eTicketing.Ticketing.Business.Reports;

namespace eTicketing.Ticketing.Business.Analytics.Forecasting;

/// <summary>
/// Projects the daily revenue/ticket series forward.
///
/// Deliberately synchronous and stateless: unlike the recommender's matrix factorization, an SSA
/// fit over at most 366 points costs milliseconds, so there is no trained artifact to persist, no
/// blob container and no nightly job — the model is fitted from the very series the request is
/// already reporting on and thrown away. See SsaSalesForecaster for why that is the right trade.
/// </summary>
public interface ISalesForecaster
{
    /// <summary>Forecasts <paramref name="horizon"/> days past the last day of
    /// <paramref name="history"/>. Never throws on thin data — it degrades through the source
    /// ladder instead, and reports which rung it landed on.</summary>
    ForecastBlock Forecast(IReadOnlyList<DailyPoint> history, int horizon);
}
