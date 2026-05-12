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
using ArangoDBNetStandard.Transport.Http;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace OnixLabs.ElementFramework.Arango.IntegrationTests;

/// <summary>
/// Represents an xUnit class fixture that owns a single ArangoDB test container for the lifetime of a test class, so that all tests in that class share one running container.
/// </summary>
public sealed class ArangoFixture : IAsyncLifetime
{
    private const string Image = "arangodb/arangodb:3.12";
    private const int ArangoPort = 8529;
    private const string RootUser = "root";
    private const string RootPassword = "test";
    public const string SystemDatabase = "_system";
    private const int MaxConnectivityRetries = 30;
    private static readonly TimeSpan ConnectivityRetryDelay = TimeSpan.FromSeconds(2);

    private readonly IContainer container = new ContainerBuilder(Image)
        .WithEnvironment("ARANGO_ROOT_PASSWORD", RootPassword)
        .WithPortBinding(ArangoPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("is ready for business"))
        .Build();

    /// <summary>
    /// Gets the HTTP endpoint URI of the running ArangoDB container.
    /// </summary>
    public Uri Endpoint => new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(ArangoPort)).Uri;

    /// <summary>
    /// Gets the username used to authenticate to the ArangoDB container.
    /// </summary>
    public string Username => RootUser;

    /// <summary>
    /// Gets the password used to authenticate to the ArangoDB container.
    /// </summary>
    public string Password => RootPassword;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await WaitForConnectivityAsync();
    }

    public async Task DisposeAsync()
    {
        await container.StopAsync();
        await container.DisposeAsync();
    }

    private async Task WaitForConnectivityAsync()
    {
        for (int attempt = 1; attempt <= MaxConnectivityRetries; attempt++)
        {
            try
            {
                using HttpApiTransport transport = HttpApiTransport.UsingBasicAuth(Endpoint, SystemDatabase, RootUser, RootPassword);
                using ArangoDBClient client = new(transport);
                await client.Database.GetCurrentDatabaseInfoAsync();
                return;
            }
            catch
            {
                await Task.Delay(ConnectivityRetryDelay);
            }
        }

        throw new InvalidOperationException("ArangoDB container did not become reachable within the expected time.");
    }
}
