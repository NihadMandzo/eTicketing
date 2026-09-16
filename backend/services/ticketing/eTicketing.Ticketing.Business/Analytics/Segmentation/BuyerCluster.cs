using eTicketing.Ticketing.Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using static eTicketing.Ticketing.Business.Reports.ReportMath;

namespace eTicketing.Ticketing.Business.Analytics.Segmentation;

internal sealed class BuyerCluster
{
    [ColumnName("PredictedLabel")]
    public uint ClusterId { get; set; }
}
