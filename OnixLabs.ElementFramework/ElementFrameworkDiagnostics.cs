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

namespace OnixLabs.ElementFramework;

/// <summary>
/// Exposes the diagnostic source names and instruments that the core framework emits for OpenTelemetry-compatible tracing and metrics.
/// </summary>
/// <remarks>
/// Consumers wire these up by adding the source / meter names to their OpenTelemetry pipeline — for example, <c>tracerProviderBuilder.AddSource(ElementFrameworkDiagnostics.SourceName)</c> and <c>meterProviderBuilder.AddMeter(ElementFrameworkDiagnostics.MeterName)</c>. The framework itself does not depend on the OpenTelemetry SDK; the <see cref="ActivitySource"/> and <see cref="Meter"/> APIs are in the .NET base class library starting from net5.0. <b>PII discipline:</b> the framework attaches only structural metadata to spans and counters — operation counts, transaction routing modes, traversal kinds, return alias names — never user-supplied parameter values, statement bodies, or property values.
/// </remarks>
public static class ElementFrameworkDiagnostics
{
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> the core framework emits spans through. Consumers subscribe to this name in their OpenTelemetry pipeline to receive framework-level traces such as <c>ElementFramework.SaveChanges</c>, <c>ElementFramework.BeginTransaction</c>, <c>ElementFramework.Transaction.Commit</c>, and <c>ElementFramework.Transaction.Rollback</c>.
    /// </summary>
    public const string SourceName = "OnixLabs.ElementFramework";

    /// <summary>
    /// The name of the <see cref="Meter"/> the core framework emits counters through. Consumers subscribe to this name in their OpenTelemetry pipeline to receive framework-level metrics.
    /// </summary>
    public const string MeterName = "OnixLabs.ElementFramework";

    /// <summary>
    /// The semantic version reported alongside the source and meter names. Bumped with the framework's package version.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// The <see cref="ActivitySource"/> instance the framework records spans through.
    /// </summary>
    internal static readonly ActivitySource Source = new(SourceName, Version);

    /// <summary>
    /// The <see cref="Meter"/> instance the framework records counters through.
    /// </summary>
    internal static readonly Meter Meter = new(MeterName, Version);

    /// <summary>
    /// Counts <c>SaveChanges</c> / <c>SaveChangesAsync</c> calls, tagged by outcome (<c>success</c> / <c>failure</c> / <c>noop</c>) and routing mode (<c>ambient</c> / <c>auto</c>).
    /// </summary>
    internal static readonly Counter<long> FlushesCounter = Meter.CreateCounter<long>("elementframework.savechanges.flushes");

    /// <summary>
    /// Counts the number of pending operations successfully flushed across all <c>SaveChanges</c> calls.
    /// </summary>
    internal static readonly Counter<long> FlushOperationsCounter = Meter.CreateCounter<long>("elementframework.savechanges.operations");

    /// <summary>
    /// Counts transaction terminals (<c>commit</c> / <c>rollback</c> / <c>disposed_without_terminal</c>) tagged by outcome.
    /// </summary>
    internal static readonly Counter<long> TransactionTerminalsCounter = Meter.CreateCounter<long>("elementframework.transactions.terminals");
}
