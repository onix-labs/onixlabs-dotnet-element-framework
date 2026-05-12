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
/// Exposes the diagnostic source names and instruments that the ArangoDB provider emits for OpenTelemetry-compatible tracing and metrics.
/// </summary>
/// <remarks>
/// Consumers wire these up by adding the source / meter names to their OpenTelemetry pipeline. The framework's Arango provider spans are higher-level (parameter count, ambient-vs-auto-commit routing, traversal kind); ArangoDBNetStandard does not currently emit its own wire-layer <see cref="ActivitySource"/>, so observability for the HTTP exchange would need to come from an HTTP-client instrumentation library separately. <b>PII discipline:</b> spans attach only the parameter count, never values or full statement bodies.
/// </remarks>
public static class ArangoDiagnostics
{
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> the Arango provider emits spans through.
    /// </summary>
    public const string SourceName = "OnixLabs.ElementFramework.Arango";

    /// <summary>
    /// The name of the <see cref="Meter"/> the Arango provider emits counters through.
    /// </summary>
    public const string MeterName = "OnixLabs.ElementFramework.Arango";

    /// <summary>
    /// The semantic version reported alongside the source and meter names.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// The <see cref="ActivitySource"/> instance the Arango provider records spans through.
    /// </summary>
    internal static readonly ActivitySource Source = new(SourceName, Version);

    /// <summary>
    /// The <see cref="Meter"/> instance the Arango provider records counters through.
    /// </summary>
    internal static readonly Meter Meter = new(MeterName, Version);

    /// <summary>
    /// Counts statement executions, tagged by transaction routing (<c>ambient</c> / <c>auto</c>) and outcome (<c>success</c> / <c>failure</c>).
    /// </summary>
    internal static readonly Counter<long> StatementsCounter = Meter.CreateCounter<long>("elementframework.arango.statements");
}
