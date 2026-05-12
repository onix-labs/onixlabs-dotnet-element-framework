// MIT License

// Copyright (c) 2020 ONIXLabs

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

namespace OnixLabs.ElementFramework.UnitTests;

[CollectionDefinition("Diagnostics-sensitive", DisableParallelization = true)]
public sealed class DiagnosticsSensitiveCollection;

[Collection("Diagnostics-sensitive")]
public class ElementFrameworkDiagnosticsTests
{
    private readonly GraphModel model = TestModel.Build();
    private readonly FakeStatementEmitter emitter = new();
    private readonly FakeGraphTransactionOpener opener = new();

    [Fact(DisplayName = "Flush emits a SaveChanges span with operation count and routing-mode tags on a successful auto-open flush")]
    public void FlushEmitsSaveChangesSpanAutoOpen()
    {
        using ActivityCollector activities = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));

        tracker.Flush();

        Activity span = activities.SingleByName("ElementFramework.SaveChanges");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(2, Convert.ToInt32(span.GetTagItem("elementframework.operation.count")));
        Assert.Equal("auto", span.GetTagItem("elementframework.transaction.mode"));
    }

    [Fact(DisplayName = "Flush within an ambient transaction tags the SaveChanges span with mode=ambient")]
    public void FlushTagsAmbientMode()
    {
        using ActivityCollector activities = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        opener.Open(); // installs an ambient transaction
        tracker.TrackAdd(Author.Create("Alice"));

        tracker.Flush();

        Activity span = activities.SingleByName("ElementFramework.SaveChanges");
        Assert.Equal("ambient", span.GetTagItem("elementframework.transaction.mode"));
    }

    [Fact(DisplayName = "Flush marks the SaveChanges span as Error and tags exception.type when execution throws")]
    public void FlushRecordsExceptionOnFailure()
    {
        using ActivityCollector activities = new();
        FakeRawStatementExecutor executor = new() { OnExecute = _ => throw new InvalidOperationException("boom") };
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        tracker.TrackAdd(Author.Create("Alice"));

        Assert.Throws<InvalidOperationException>(() => tracker.Flush());

        Activity span = activities.SingleByName("ElementFramework.SaveChanges");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("System.InvalidOperationException", span.GetTagItem("exception.type"));
    }

    [Fact(DisplayName = "Flush with no pending operations bumps the flushes counter with outcome=noop and emits no span")]
    public void EmptyFlushIsNoopMetric()
    {
        using ActivityCollector activities = new();
        using CounterCollector counters = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);

        tracker.Flush();

        Assert.Empty(activities.All);
        Assert.Equal(1L, counters.Sum("elementframework.savechanges.flushes", ("outcome", "noop")));
    }

    [Fact(DisplayName = "Successful flush ticks both flushes (outcome=success) and operations counters")]
    public void SuccessfulFlushTicksFlushesAndOperationsCounters()
    {
        using CounterCollector counters = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        tracker.TrackAdd(Author.Create("Alice"));
        tracker.TrackAdd(Author.Create("Bob"));
        tracker.TrackAdd(Author.Create("Charlie"));

        tracker.Flush();

        Assert.Equal(1L, counters.Sum("elementframework.savechanges.flushes", ("outcome", "success"), ("elementframework.transaction.mode", "auto")));
        Assert.Equal(3L, counters.Sum("elementframework.savechanges.operations", ("elementframework.transaction.mode", "auto")));
    }

    [Fact(DisplayName = "Failed flush ticks the flushes counter with outcome=failure and does NOT bump the operations counter")]
    public void FailedFlushTicksFailureCounterOnly()
    {
        using CounterCollector counters = new();
        FakeRawStatementExecutor executor = new() { OnExecute = _ => throw new InvalidOperationException("boom") };
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        tracker.TrackAdd(Author.Create("Alice"));

        Assert.Throws<InvalidOperationException>(() => tracker.Flush());

        Assert.Equal(1L, counters.Sum("elementframework.savechanges.flushes", ("outcome", "failure"), ("elementframework.transaction.mode", "auto")));
        Assert.Equal(0L, counters.SumAll("elementframework.savechanges.operations"));
    }

    [Fact(DisplayName = "GraphTransactionFactory.Open emits a BeginTransaction span")]
    public void OpenEmitsBeginTransactionSpan()
    {
        using ActivityCollector activities = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        GraphTransactionFactory factory = new(opener, tracker);

        using IGraphTransaction _ = factory.Open();

        Activity span = activities.SingleByName("ElementFramework.BeginTransaction");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
    }

    [Fact(DisplayName = "RollbackAwareGraphTransaction.Commit emits a Transaction.Commit span and bumps the terminals counter with outcome=committed")]
    public void CommitEmitsSpanAndTicksTerminal()
    {
        using ActivityCollector activities = new();
        using CounterCollector counters = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        GraphTransactionFactory factory = new(opener, tracker);
        IGraphTransaction transaction = factory.Open();

        transaction.Commit();

        Activity span = activities.SingleByName("ElementFramework.Transaction.Commit");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(1L, counters.Sum("elementframework.transactions.terminals", ("outcome", "committed")));
    }

    [Fact(DisplayName = "RollbackAwareGraphTransaction.Rollback emits a Transaction.Rollback span and ticks outcome=rolledback")]
    public void RollbackEmitsSpanAndTicksTerminal()
    {
        using ActivityCollector activities = new();
        using CounterCollector counters = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        GraphTransactionFactory factory = new(opener, tracker);
        IGraphTransaction transaction = factory.Open();

        transaction.Rollback();

        Activity span = activities.SingleByName("ElementFramework.Transaction.Rollback");
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Equal(1L, counters.Sum("elementframework.transactions.terminals", ("outcome", "rolledback")));
    }

    [Fact(DisplayName = "Dispose without a prior terminal ticks the terminals counter with outcome=disposed_without_terminal")]
    public void DisposeWithoutTerminalTicksCounter()
    {
        using CounterCollector counters = new();
        FakeRawStatementExecutor executor = new();
        ChangeTracker tracker = new(model, emitter, executor, opener, NullLogger<ChangeTracker>.Instance);
        GraphTransactionFactory factory = new(opener, tracker);
        IGraphTransaction transaction = factory.Open();

        transaction.Dispose();

        Assert.Equal(1L, counters.Sum("elementframework.transactions.terminals", ("outcome", "disposed_without_terminal")));
    }

    /// <summary>
    /// A scoped <see cref="ActivityListener"/> that captures every Activity emitted by the framework's
    /// <see cref="ElementFrameworkDiagnostics.SourceName"/> during the test. Disposing the collector
    /// removes the listener so subsequent tests don't see leftover activities.
    /// </summary>
    private sealed class ActivityCollector : IDisposable
    {
        private readonly ActivityListener listener;
        private readonly List<Activity> activities = [];
        private readonly object gate = new();
        private readonly Activity parent;

        public ActivityCollector()
        {
            parent = new Activity("TestRoot");
            parent.Start();

            listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == ElementFrameworkDiagnostics.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.TraceId != parent.TraceId) return;
                    lock (gate) activities.Add(activity);
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        public IReadOnlyList<Activity> All
        {
            get
            {
                lock (gate) return activities.ToArray();
            }
        }

        public Activity SingleByName(string operationName) =>
            Assert.Single(All, a => a.OperationName == operationName);

        public void Dispose()
        {
            listener.Dispose();
            parent.Stop();
            parent.Dispose();
        }
    }

    /// <summary>
    /// A scoped <see cref="MeterListener"/> that captures every <see cref="Counter{T}"/> increment
    /// emitted by the framework's <see cref="ElementFrameworkDiagnostics.MeterName"/> during the test.
    /// Exposes per-tag sums for assertions.
    /// </summary>
    private sealed class CounterCollector : IDisposable
    {
        private readonly MeterListener listener;
        private readonly List<(string Instrument, long Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> measurements = [];

        public CounterCollector()
        {
            listener = new MeterListener
            {
                InstrumentPublished = (instrument, l) =>
                {
                    if (instrument.Meter.Name == ElementFrameworkDiagnostics.MeterName)
                        l.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
                measurements.Add((instrument.Name, measurement, tags.ToArray())));
            listener.Start();
        }

        public long Sum(string instrumentName, params (string Key, string Value)[] requiredTags) =>
            measurements
                .Where(m => m.Instrument == instrumentName && requiredTags.All(rt => m.Tags.Any(t => t.Key == rt.Key && Equals(t.Value, rt.Value))))
                .Sum(m => m.Value);

        public long SumAll(string instrumentName) =>
            measurements.Where(m => m.Instrument == instrumentName).Sum(m => m.Value);

        public void Dispose() => listener.Dispose();
    }
}
