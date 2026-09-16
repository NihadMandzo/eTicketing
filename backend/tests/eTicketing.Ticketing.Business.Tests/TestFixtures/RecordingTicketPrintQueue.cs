using eTicketing.Contracts.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;
using eTicketing.Contracts.Events;
using eTicketing.Shared.Messaging;
using eTicketing.Shared.TicketPdf;
using eTicketing.Ticketing.Business.Analytics.Anomalies;
using eTicketing.Ticketing.Business.Analytics.Forecasting;
using eTicketing.Ticketing.Business.Analytics.Insights;
using eTicketing.Ticketing.Business.Analytics.Narrative;
using eTicketing.Ticketing.Business.Analytics.Segmentation;
using eTicketing.Ticketing.Business.Analytics;
using eTicketing.Ticketing.Business.External;
using eTicketing.Ticketing.Business.GateDevices;
using eTicketing.Ticketing.Business.Integration;
using eTicketing.Ticketing.Business.Purchases;
using eTicketing.Ticketing.Business.ReadModels;
using eTicketing.Ticketing.Business.Reports;
using eTicketing.Ticketing.Business.Sectors;
using eTicketing.Ticketing.Business.Security;
using eTicketing.Ticketing.Business.Subscriptions;
using eTicketing.Ticketing.Business.TicketPrint;
using eTicketing.Ticketing.Business.Tickets;
using eTicketing.Ticketing.Business.Time;
using eTicketing.Ticketing.Data.Entities;
using eTicketing.Ticketing.Data.Repositories;
using eTicketing.Ticketing.Data;

namespace eTicketing.Ticketing.Business.Tests.TestFixtures;

/// <summary>The real queue is a Channel drained by a hosted service; tests only need to know which
/// batch ids were handed to it.</summary>
public sealed class RecordingTicketPrintQueue : ITicketPrintQueue
{
    public List<Guid> Enqueued { get; } = [];

    public void Enqueue(Guid batchId) => Enqueued.Add(batchId);
}
