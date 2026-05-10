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

using Neo4j.Driver;
using Testcontainers.Neo4j;

namespace OnixLabs.ElementFramework.Neo4j.IntegrationTests;

/// <summary>
/// Represents an xUnit class fixture that owns a single Neo4j test container for the lifetime of a test class, so that all tests in that class share one running container.
/// </summary>
public sealed class Neo4jFixture : IAsyncLifetime
{
    private const int MaxConnectivityRetries = 10;
    private static readonly TimeSpan ConnectivityRetryDelay = TimeSpan.FromSeconds(5);

    private readonly Neo4jContainer container = new Neo4jBuilder("neo4j:5.26-community").Build();

    /// <summary>
    /// Gets the Bolt connection string for the running container.
    /// </summary>
    /// <value>The Bolt connection string used by the test context to connect to the shared Neo4j instance.</value>
    public string ConnectionString => container.GetConnectionString();

    /// <summary>
    /// Gets the auth token used by tests to connect to the shared Neo4j instance.
    /// </summary>
    /// <value>An <see cref="IAuthToken"/> matching the credentials configured on the container.</value>
    public IAuthToken AuthToken { get; } = AuthTokens.None;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await WaitForConnectivityAsync();
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        await container.StopAsync();
        await container.DisposeAsync();
    }

    /// <summary>
    /// Polls the Bolt endpoint until the driver can connect, throwing once the retry budget is exhausted.
    /// </summary>
    /// <returns>Returns a task that completes once connectivity has been verified.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the database is unreachable after <see cref="MaxConnectivityRetries"/> attempts.</exception>
    private async Task WaitForConnectivityAsync()
    {
        Uri boltUri = new(ConnectionString);
        for (int attempt = 1; attempt <= MaxConnectivityRetries; attempt++)
        {
            try
            {
                await using IDriver driver = GraphDatabase.Driver(boltUri, AuthToken);
                await driver.VerifyConnectivityAsync();
                return;
            }
            catch
            {
                await Task.Delay(ConnectivityRetryDelay);
            }
        }

        throw new InvalidOperationException("Graph database initialization failed within the expected time.");
    }
}
