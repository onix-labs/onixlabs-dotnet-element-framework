// All Rights Reserved License
//
// 1. Grant of License
// Subject to the terms and conditions of this License, ONIXLabs ("Licensor") hereby grants to you a limited, non-exclusive, non-transferable, non-sublicensable license to use the Software for commercial, private, and paid purposes. This license does not include any rights to modify, distribute, or create derivative works of the Software.
//
// 2. Permitted Uses
// You are permitted to:
//  - Use the Software for commercial purposes.
//  - Use the Software for private purposes.
//  - Use the Software for paid purposes.
//  - Exercise any patent rights associated with the Software, solely in connection with your use of the Software as permitted under this License.
//
// 3. Restrictions
// You are not permitted to:
//  - Modify, alter, or create any derivative works of the Software.
//  - Distribute, sublicense, lease, rent, or otherwise transfer the Software to any third party.
//  - Use the Software without obtaining a proper license for paid use.
//  - Use the Software in any way that infringes upon the trademarks, service marks, or trade names of the Licensor.
//  - Use the Software in any manner that could cause it to be considered open-source software or otherwise subject to an open-source license.
//
// 4. No Free Use
// This license does not permit any free use of the Software. Any use of the Software without a paid license is strictly prohibited.
//
// 5. No Liability
// To the maximum extent permitted by applicable law, the Software is provided "as is" and "as available" without warranty of any kind, express or implied, including but not limited to the implied warranties of merchantability, fitness for a particular purpose, and non-infringement. In no event shall the Licensor be liable for any damages whatsoever arising out of the use of or inability to use the Software, even if the Licensor has been advised of the possibility of such damages.
//
// 6. No Warranty
// The Licensor makes no warranty that the Software will meet your requirements, be uninterrupted, secure, or error-free. The Licensor disclaims all warranties with respect to the Software, whether express or implied, including but not limited to any warranties of merchantability, fitness for a particular purpose, and non-infringement.
//
// 7. Termination
// This license is effective until terminated. Your rights under this license will terminate automatically without notice if you fail to comply with any term of this license. Upon termination, you must immediately cease all use of the Software and destroy all copies of the Software in your possession or control.
//
// 8. Governing Law
// This license will be governed by and construed in accordance with the laws of [Your Jurisdiction], without regard to its conflict of laws principles.
//
// 9. Entire Agreement
// This license constitutes the entire agreement between you and the Licensor concerning the Software and supersedes all prior or contemporaneous communications, agreements, or understandings, whether oral or written, concerning the subject matter hereof.
//
// By using the Software, you acknowledge that you have read and understood this license and agree to be bound by its terms and conditions.

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
