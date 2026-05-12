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

using Microsoft.Extensions.DependencyInjection;
using OnixLabs.ElementFramework.Conformance;
using OnixLabs.ElementFramework.Conformance.TestFixtures.BlogApplication;
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.InMemory.IntegrationTests;

public sealed class InMemoryGraphContextIntegrationTests(ITestOutputHelper output) : AbstractGraphContextIntegrationTests(output)
{
    private const string DatabaseName = "onixlabs-element-framework-inmemory-tests";

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddGraphContext<BlogGraphContext>(builder => builder.UseInMemory(DatabaseName));

    protected override Task ResetGraphAsync()
    {
        InMemoryStoreRegistry.Reset(DatabaseName);
        return Task.CompletedTask;
    }

    // Cypher-specific raw-statement assertions in the conformance suite are skipped here;
    // the in-memory provider only understands op-coded statements from its own emitter.

    [Fact(DisplayName = "RawStatement Execute should return result rows", Skip = "In-memory provider does not execute Cypher.")]
    public override void RawStatementExecuteShouldReturnResultRows() => base.RawStatementExecuteShouldReturnResultRows();

    [Fact(DisplayName = "RawStatement.ExecuteAsync should yield result rows", Skip = "In-memory provider does not execute Cypher.")]
    public override Task RawStatementExecuteAsyncShouldYieldRows() => base.RawStatementExecuteAsyncShouldYieldRows();

    [Fact(DisplayName = "RawStatement.Execute writes should be visible to the OGM read APIs", Skip = "In-memory provider does not execute Cypher.")]
    public override void RawStatementExecuteShouldPersistMutationVisibleToOgm() => base.RawStatementExecuteShouldPersistMutationVisibleToOgm();

    [Fact(DisplayName = "RawStatement.Execute with a multi-row return should yield every row", Skip = "In-memory provider does not execute Cypher.")]
    public override void RawStatementExecuteShouldYieldMultipleRows() => base.RawStatementExecuteShouldYieldMultipleRows();
}
