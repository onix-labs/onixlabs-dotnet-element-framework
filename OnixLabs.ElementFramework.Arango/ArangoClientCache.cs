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
using ArangoDBNetStandard;
using ArangoDBNetStandard.Transport.Http;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Process-wide cache of <see cref="ArangoDBClient"/> instances keyed by <c>(endpoint, database, username, password)</c>.
/// </summary>
/// <remarks>
/// Mirrors <c>Neo4jDriverCache</c> and <c>AgeDataSourceCache</c>: clients are HTTP-pooling under the hood, so a single client per (endpoint, db, credentials) tuple is the right granularity. The cache is process-wide, never evicts, and intentionally never disposes — like the other providers, this is fine in production (one endpoint, one set of credentials) and intentionally leaky under test runs that spin up many random ports.
/// </remarks>
internal static class ArangoClientCache
{
    private static readonly ConcurrentDictionary<CacheKey, ArangoDBClient> Clients = new();

    /// <summary>
    /// Gets or creates an <see cref="ArangoDBClient"/> for the supplied endpoint, database, and credentials.
    /// </summary>
    /// <param name="endpoint">The ArangoDB endpoint URI.</param>
    /// <param name="databaseName">The database name to bind the client to.</param>
    /// <param name="username">The username for basic auth.</param>
    /// <param name="password">The password for basic auth.</param>
    /// <returns>Returns the cached or newly created <see cref="ArangoDBClient"/>.</returns>
    public static ArangoDBClient GetOrCreate(Uri endpoint, string databaseName, string username, string password) =>
        Clients.GetOrAdd(
            new CacheKey(endpoint.AbsoluteUri, databaseName, username, password),
            static key => new ArangoDBClient(
                HttpApiTransport.UsingBasicAuth(new Uri(key.Endpoint), key.Database, key.Username, key.Password),
                new ArangoSerialization()));

    private readonly record struct CacheKey(string Endpoint, string Database, string Username, string Password);
}
