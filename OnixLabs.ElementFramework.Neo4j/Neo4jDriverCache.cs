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

using System.Collections.Concurrent;
using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Provides a process-wide cache of <see cref="IDriver"/> instances keyed by connection string and auth token.
/// </summary>
/// <remarks>
/// Neo4j drivers are designed for application-singleton reuse — they manage their own connection pool internally and creating one per call leaks pooled connections. Tests that spin up many Testcontainer instances are the practical motivation: each container has a unique connection string so the cache grows one entry per container, and drivers are reused across the many <c>AddGraphContext</c> calls within a single fixture. Auth-token equality is reference-based because <see cref="IAuthToken"/> does not override equality; if two callers construct equivalent auth tokens via separate <c>AuthTokens.Basic(...)</c> calls and pass them into <c>UseNeo4j</c>, two drivers will be created.
/// </remarks>
internal static class Neo4jDriverCache
{
    private static readonly ConcurrentDictionary<DriverKey, IDriver> Drivers = new();

    /// <summary>
    /// Returns the cached <see cref="IDriver"/> for the supplied connection string and auth token, creating one on first call.
    /// </summary>
    /// <param name="connectionString">The Bolt connection URI (e.g. <c>bolt://localhost:7687</c> or <c>neo4j://...</c>).</param>
    /// <param name="authToken">The auth token to bind the driver with, or <see langword="null"/> for unauthenticated.</param>
    /// <returns>The shared <see cref="IDriver"/> instance for the supplied key.</returns>
    public static IDriver GetOrCreate(string connectionString, IAuthToken? authToken) => Drivers.GetOrAdd(
        new DriverKey(connectionString, authToken),
        static key => GraphDatabase.Driver(key.ConnectionString, key.AuthToken ?? AuthTokens.None)
    );

    private readonly record struct DriverKey(string ConnectionString, IAuthToken? AuthToken);
}
