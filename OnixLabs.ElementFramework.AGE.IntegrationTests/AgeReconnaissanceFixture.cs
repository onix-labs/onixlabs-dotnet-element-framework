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

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace OnixLabs.ElementFramework.AGE.IntegrationTests;

/// <summary>
/// Represents an xUnit class fixture that owns a single Apache AGE test container for the lifetime of a test class, so that all tests in that class share one running container.
/// </summary>
/// <remarks>
/// We use the generic <see cref="ContainerBuilder"/> rather than <c>Testcontainers.PostgreSql</c> because the AGE image already bundles its own initialization script. The AGE image listens on the standard 5432 port and accepts the credentials baked into its entrypoint (postgres/postgres on database <c>postgres</c>).
/// </remarks>
public sealed class AgeReconnaissanceFixture : IAsyncLifetime
{
    private const string Image = "apache/age:release_PG16_1.6.0";
    private const string Username = "postgres";
    private const string Password = "postgres";
    private const string Database = "postgres";
    private const int PostgresPort = 5432;
    private const int MaxConnectivityRetries = 30;
    private static readonly TimeSpan ConnectivityRetryDelay = TimeSpan.FromSeconds(2);

    private readonly IContainer container = new ContainerBuilder(Image)
        .WithEnvironment("POSTGRES_USER", Username)
        .WithEnvironment("POSTGRES_PASSWORD", Password)
        .WithEnvironment("POSTGRES_DB", Database)
        .WithPortBinding(PostgresPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-U", Username, "-d", Database))
        .Build();

    /// <summary>
    /// Gets the Npgsql connection string for the running AGE container.
    /// </summary>
    public string ConnectionString => new NpgsqlConnectionStringBuilder
    {
        Host = container.Hostname,
        Port = container.GetMappedPublicPort(PostgresPort),
        Username = Username,
        Password = Password,
        Database = Database,
        IncludeErrorDetail = true
    }.ConnectionString;

    /// <summary>
    /// Builds a data source that lets unmapped Postgres types (including <c>ag_catalog.agtype</c>)
    /// flow through as text, so we can read and write agtype payloads without registering a
    /// custom type handler.
    /// </summary>
    public NpgsqlDataSource CreateDataSource()
    {
        NpgsqlDataSourceBuilder builder = new(ConnectionString);
        builder.EnableUnmappedTypes();
        return builder.Build();
    }

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
                await using NpgsqlConnection connection = new(ConnectionString);
                await connection.OpenAsync();
                await using NpgsqlCommand command = new("SELECT 1", connection);
                await command.ExecuteScalarAsync();
                return;
            }
            catch
            {
                await Task.Delay(ConnectivityRetryDelay);
            }
        }

        throw new InvalidOperationException("Apache AGE container did not become reachable within the expected time.");
    }
}
