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

using Neo4j.Driver;

namespace OnixLabs.ElementFramework;

/// <summary>
/// Provides extension methods for binding a graph context to a Neo4j endpoint.
/// </summary>
public static class GraphContextOptionsBuilderExtensions
{
    /// <summary>
    /// Binds the configured graph context to a Neo4j endpoint by constructing and supplying the Neo4j-specific provider implementations to the <see cref="GraphContextOptionsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="IDriver"/> for the supplied endpoint is shared process-wide. <b>Deadlock vector under captured sync contexts.</b> The Neo4j .NET driver (v6+) is async-only; this provider bridges the sync surface (<c>SaveChanges</c>, <c>BeginTransaction</c>, <c>FindById</c>, <c>AsEnumerable</c>, <c>RawStatement().Execute(...)</c>, etc.) to the async driver via <c>GetAwaiter().GetResult()</c>. Under hosts that capture a synchronization context — ASP.NET Classic, WinForms, WPF — calling these sync members can deadlock. Use the async surface (<c>SaveChangesAsync</c>, <c>BeginTransactionAsync</c>, <c>FindByIdAsync</c>, <c>AsAsyncEnumerable</c>, <c>RawStatement().ExecuteAsync(...)</c>) in those hosts. ASP.NET Core, console applications, and modern hosted-service consumers do not capture a synchronization context and are unaffected.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="connectionString">The Bolt connection URI (e.g. <c>bolt://localhost:7687</c> or <c>neo4j://...</c>).</param>
    /// <param name="authToken">The auth token to bind the driver with, or <see langword="null"/> for unauthenticated.</param>
    /// <returns>The same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connectionString"/> is null, empty, or whitespace.</exception>
    public static GraphContextOptionsBuilder UseNeo4j(this GraphContextOptionsBuilder builder, string connectionString, IAuthToken? authToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return builder.UseNeo4j(() => connectionString, authToken);
    }

    /// <summary>
    /// Binds the configured graph context to a Neo4j endpoint, deferring connection-string resolution and <see cref="IDriver"/> creation until the driver is first used.
    /// </summary>
    /// <remarks>
    /// Use this overload when the connection string is not known at <c>AddGraphContext</c> registration time — for example when the endpoint is provided by a Testcontainers instance whose port is only mapped after the container starts. Production wiring typically uses the eager overload because the connection string is known up front. <b>Deadlock vector under captured sync contexts.</b> See the eager overload's remarks.
    /// </remarks>
    /// <param name="builder">The <see cref="GraphContextOptionsBuilder"/> being configured.</param>
    /// <param name="connectionStringFactory">A factory that returns the Bolt connection URI on demand. Invoked once on first <see cref="IDriver"/> resolution; the result is cached process-wide.</param>
    /// <param name="authToken">The auth token to bind the driver with, or <see langword="null"/> for unauthenticated.</param>
    /// <returns>The same <see cref="GraphContextOptionsBuilder"/> to allow further chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="connectionStringFactory"/> is <see langword="null"/>.</exception>
    public static GraphContextOptionsBuilder UseNeo4j(this GraphContextOptionsBuilder builder, Func<string> connectionStringFactory, IAuthToken? authToken = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory);

        Lazy<IDriver> driver = new(DriverFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        CypherEmitter emitter = new();
        Neo4jResultMaterializer materializer = new();
        Neo4jGraphTransactionOpener opener = new(driver);
        Neo4jCypherExecutor executor = new(driver, opener);
        Neo4jTraversalTranslator translator = new(emitter, executor, materializer);

        return builder
            .UseStatementEmitter(emitter)
            .UseResultMaterializer(materializer)
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(translator);

        IDriver DriverFactory() => Neo4jDriverCache.GetOrCreate(connectionStringFactory(), authToken);
    }
}
