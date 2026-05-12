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

namespace OnixLabs.ElementFramework.Neo4j.IntegrationTests;

public sealed class Neo4jGraphContextIntegrationTests(ITestOutputHelper output, Neo4jFixture fixture)
    : AbstractGraphContextIntegrationTests(output), IClassFixture<Neo4jFixture>
{
    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddGraphContext<BlogGraphContext>(builder =>
            builder.UseNeo4j(() => fixture.ConnectionString, fixture.AuthToken));

    protected override string? ProviderSourceName => Neo4jDiagnostics.SourceName;
}
