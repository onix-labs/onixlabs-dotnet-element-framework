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
/// Represents the Neo4j implementation of <see cref="IGraphTransactionOpener"/> that opens fresh transactions on demand and holds the canonical ambient transaction for the bound context.
/// </summary>
/// <remarks>
/// Each successful <see cref="Open"/> creates a new <see cref="IAsyncSession"/> and <see cref="IAsyncTransaction"/> pair, wraps them in a <see cref="Neo4jGraphTransaction"/>, and stores the wrapper as the ambient transaction; the wrapper calls <see cref="ClearActive"/> on commit, rollback, or dispose. V1 is one-ambient-at-a-time: opening a second transaction while another is active throws <see cref="GraphTransactionAlreadyActiveException"/>. Sync paths bridge to async via <c>GetAwaiter().GetResult()</c>.
/// </remarks>
/// <param name="driver">A lazy handle to the shared <see cref="IDriver"/> for the bound Neo4j endpoint. Resolution defers to the moment a transaction is first opened, allowing the connection string to be deferred past <c>AddGraphContext</c> registration time.</param>
internal sealed class Neo4jGraphTransactionOpener(Lazy<IDriver> driver) : IGraphTransactionOpener
{
    private Neo4jGraphTransaction? active;

    /// <inheritdoc/>
    public IGraphTransaction? Active => active;

    /// <inheritdoc/>
    public IGraphTransaction Open() => OpenInternalAsync(CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<IGraphTransaction> OpenAsync(CancellationToken token = default) =>
        await OpenInternalAsync(token).ConfigureAwait(false);

    internal void ClearActive() => active = null;

    private async Task<Neo4jGraphTransaction> OpenInternalAsync(CancellationToken token)
    {
        if (active is not null)
            throw new GraphTransactionAlreadyActiveException(
                "An ambient graph transaction is already active for this context. Commit, roll back, or dispose it before opening another.");

        IAsyncSession session;
        IAsyncTransaction transaction;

        try
        {
            session = driver.Value.AsyncSession();
        }
        catch (Exception exception)
        {
            throw new GraphTransactionException("Failed to open a graph transaction against the Neo4j endpoint.", exception);
        }

        try
        {
            transaction = await session.BeginTransactionAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await session.CloseAsync().ConfigureAwait(false);
            throw new GraphTransactionException("Failed to open a graph transaction against the Neo4j endpoint.", exception);
        }

        Neo4jGraphTransaction wrapped = new(session, transaction, this);
        active = wrapped;
        return wrapped;
    }
}
