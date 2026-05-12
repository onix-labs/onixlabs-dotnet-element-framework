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

using ArangoDBNetStandard;
using ArangoDBNetStandard.CursorApi.Models;
using ArangoDBNetStandard.Transport.Http;

namespace OnixLabs.ElementFramework.Arango.IntegrationTests;

/// <summary>
/// Wire-level smoke test that proves the Testcontainers ArangoDB fixture and the .NET client library can round-trip an AQL query end-to-end. Not part of the conformance suite — exists only as the first proof that the provider's surrounding infrastructure works before we add framework code on top.
/// </summary>
public sealed class ArangoSmokeTests(ArangoFixture fixture) : IClassFixture<ArangoFixture>
{
    [Fact(DisplayName = "AQL RETURN 1 round-trips end-to-end through the .NET client")]
    public async Task AqlReturnOneRoundTrips()
    {
        using HttpApiTransport transport = HttpApiTransport.UsingBasicAuth(fixture.Endpoint, ArangoFixture.SystemDatabase, fixture.Username, fixture.Password);
        using ArangoDBClient client = new(transport);

        CursorResponse<long> cursor = await client.Cursor.PostCursorAsync<long>("RETURN 1");

        Assert.Single(cursor.Result);
        Assert.Equal(1L, cursor.Result.First());
    }
}
