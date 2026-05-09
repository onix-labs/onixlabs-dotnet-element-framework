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
