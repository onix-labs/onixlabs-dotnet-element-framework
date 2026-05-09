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
/// Represents the Neo4j implementation of <see cref="IRawStatementExecutor"/> that runs raw Cypher queries against the bound endpoint with ambient-transaction routing.
/// </summary>
/// <remarks>
/// When <see cref="IGraphTransactionOpener.Active"/> is set, the executor runs the query through the active transaction's <see cref="IAsyncTransaction.RunAsync(string, object)"/>; otherwise it opens a fresh auto-commit <see cref="IAsyncSession"/> per call. Result rows are materialized eagerly so faults during the driver round-trip surface immediately rather than at enumeration time. The sync surface bridges to the async driver via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
/// <param name="driver">A lazy handle to the shared <see cref="IDriver"/> for the bound Neo4j endpoint, used for auto-commit fallback. Resolution defers to first execute, allowing the connection string to be deferred past <c>AddGraphContext</c> registration time.</param>
/// <param name="transactionOpener">The <see cref="IGraphTransactionOpener"/> whose <see cref="IGraphTransactionOpener.Active"/> indicates the routing target.</param>
internal sealed class Neo4jCypherExecutor(Lazy<IDriver> driver, IGraphTransactionOpener transactionOpener) : IRawStatementExecutor
{
    /// <inheritdoc/>
    public IEnumerable<IReadOnlyDictionary<string, object?>> Execute(string statement, IReadOnlyDictionary<string, object?> parameters)
    {
        try
        {
            return RunAsync(statement, parameters, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            throw new RawStatementException(
                "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(string statement, IReadOnlyDictionary<string, object?> parameters, CancellationToken token = default) =>
        ExecuteAsyncCore(statement, parameters, token);

    private async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsyncCore(string statement, IReadOnlyDictionary<string, object?> parameters, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows;
        try
        {
            rows = await RunAsync(statement, parameters, token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new RawStatementException(
                "Failed to execute the supplied Cypher statement against the Neo4j endpoint.", exception);
        }

        foreach (IReadOnlyDictionary<string, object?> row in rows)
            yield return row;
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RunAsync(string cypher, IReadOnlyDictionary<string, object?> parameters, CancellationToken token)
    {
        Dictionary<string, object?> driverParameters = parameters.ToDictionary(kv => kv.Key, kv => PropertySerializer.Serialize(kv.Value));

        if (transactionOpener.Active is Neo4jGraphTransaction ambient)
        {
            IResultCursor cursor = await ambient.Transaction.RunAsync(cypher, driverParameters).ConfigureAwait(false);
            return await MaterializeAsync(cursor, token).ConfigureAwait(false);
        }

        await using IAsyncSession session = driver.Value.AsyncSession();
        IResultCursor autoCursor = await session.RunAsync(cypher, driverParameters).ConfigureAwait(false);
        return await MaterializeAsync(autoCursor, token).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> MaterializeAsync(IResultCursor cursor, CancellationToken token)
    {
        List<IRecord> records = await cursor.ToListAsync(token).ConfigureAwait(false);
        List<IReadOnlyDictionary<string, object?>> rows = new(records.Count);
        foreach (IRecord record in records)
        {
            Dictionary<string, object?> row = new(record.Values.Count);
            foreach (KeyValuePair<string, object> entry in record.Values) row[entry.Key] = entry.Value;
            rows.Add(row);
        }
        return rows;
    }
}
