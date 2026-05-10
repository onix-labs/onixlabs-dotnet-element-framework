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

namespace OnixLabs.ElementFramework.UnitTests;

internal sealed class Author
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Age { get; set; }
}

internal sealed class Post
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
}

internal sealed class Comment
{
    public Guid Id { get; set; }
    public string Body { get; set; } = "";
}

internal sealed class Wrote
{
    public DateTimeOffset WrittenAt { get; set; }
}

internal sealed class CommentOn;

internal sealed class Unregistered
{
    public Guid Id { get; set; }
}

internal sealed class StringKeyed
{
    public string? Identifier { get; set; }
    public string Body { get; set; } = "";
}

internal static class TestModel
{
    public static GraphModel Build()
    {
        GraphModelBuilder builder = new();
        builder.Node<Author>().HasKey(a => a.Id);
        builder.Node<Post>().HasKey(p => p.Id);
        builder.Node<Comment>().HasKey(c => c.Id);
        builder.Relationship<Author, Wrote, Post>();
        builder.Relationship<Comment, CommentOn, Post>();
        return builder.Build();
    }
}

internal sealed class FakeStatementEmitter : IStatementEmitter
{
    public DataStatement EmitAdd<T>(IGraphModel model, T node) =>
        new($"ADD {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitUpdate<T>(IGraphModel model, T node) =>
        new($"UPDATE {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitRemove<T>(IGraphModel model, T node) =>
        new($"REMOVE {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitMerge<T>(IGraphModel model, T node) =>
        new($"MERGE {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitConnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEdge edge, TEnd end) =>
        new($"CONNECT {typeof(TEdge).Name}", new Dictionary<string, object?>());

    public DataStatement EmitDisconnect<TStart, TEdge, TEnd>(IGraphModel model, TStart start, TEnd end) =>
        new($"DISCONNECT {typeof(TEdge).Name}", new Dictionary<string, object?>());

    public DataStatement EmitFindById<T>(IGraphModel model, object key) =>
        new($"FIND {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitExists<T>(IGraphModel model, object key) =>
        new($"EXISTS {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitAsEnumerableNodes<T>(IGraphModel model) =>
        new($"ALL NODES {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitAsEnumerableEdges<T>(IGraphModel model) =>
        new($"ALL EDGES {typeof(T).Name}", new Dictionary<string, object?>());

    public DataStatement EmitTraversal(IGraphModel model, TraversalAst ast) =>
        new("TRAVERSAL", new Dictionary<string, object?>());
}

internal sealed class FakeRawStatementExecutor : IRawStatementExecutor
{
    public List<string> Executed { get; } = [];
    public Func<string, IEnumerable<IReadOnlyDictionary<string, object?>>>? OnExecute { get; set; }

    public IEnumerable<IReadOnlyDictionary<string, object?>> Execute(string statement, IReadOnlyDictionary<string, object?> parameters)
    {
        Executed.Add(statement);
        return OnExecute?.Invoke(statement) ?? [];
    }

    public IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ExecuteAsync(string statement, IReadOnlyDictionary<string, object?> parameters, CancellationToken token = default)
    {
        Executed.Add(statement);
        IEnumerable<IReadOnlyDictionary<string, object?>> rows = OnExecute?.Invoke(statement) ?? [];
        return ToAsync(rows);
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object?>> ToAsync(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        foreach (IReadOnlyDictionary<string, object?> row in rows)
        {
            yield return row;
            await Task.Yield();
        }
    }
}

internal sealed class FakeGraphTransactionOpener : IGraphTransactionOpener
{
    public List<string> Events { get; } = [];
    public IGraphTransaction? Active { get; private set; }

    public IGraphTransaction Open()
    {
        Events.Add("Open");
        FakeGraphTransaction transaction = new(this);
        Active = transaction;
        return transaction;
    }

    public Task<IGraphTransaction> OpenAsync(CancellationToken token = default)
    {
        Events.Add("OpenAsync");
        FakeGraphTransaction transaction = new(this);
        Active = transaction;
        return Task.FromResult<IGraphTransaction>(transaction);
    }

    internal void Clear() => Active = null;
}

internal sealed class FakeGraphTransaction(FakeGraphTransactionOpener owner) : IGraphTransaction
{
    public void Commit()
    {
        owner.Events.Add("Commit");
        owner.Clear();
    }

    public Task CommitAsync(CancellationToken token = default)
    {
        owner.Events.Add("CommitAsync");
        owner.Clear();
        return Task.CompletedTask;
    }

    public void Rollback()
    {
        owner.Events.Add("Rollback");
        owner.Clear();
    }

    public Task RollbackAsync(CancellationToken token = default)
    {
        owner.Events.Add("RollbackAsync");
        owner.Clear();
        return Task.CompletedTask;
    }

    public void Dispose() => owner.Events.Add("Dispose");

    public ValueTask DisposeAsync()
    {
        owner.Events.Add("DisposeAsync");
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeTraversalTranslator : ITraversalTranslator
{
    public TraversalAst? LastAst { get; private set; }
    public Func<TraversalAst, IEnumerable<object>>? OnTranslate { get; set; }

    public IEnumerable<TResult> Translate<TResult>(IGraphModel model, TraversalAst ast)
    {
        LastAst = ast;
        return (OnTranslate?.Invoke(ast) ?? []).Cast<TResult>();
    }

    public IAsyncEnumerable<TResult> TranslateAsync<TResult>(IGraphModel model, TraversalAst ast, CancellationToken token = default)
    {
        LastAst = ast;
        return ToAsync<TResult>(OnTranslate?.Invoke(ast) ?? []);
    }

    private static async IAsyncEnumerable<TResult> ToAsync<TResult>(IEnumerable<object> rows)
    {
        foreach (object row in rows)
        {
            yield return (TResult)row;
            await Task.Yield();
        }
    }
}

internal sealed class FakeResultMaterializer : IResultMaterializer
{
    public const string NodeAlias = "n";
    public const string EdgeAlias = "r";
    public const string CountAlias = "count";

    public T MaterializeNode<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row) =>
        (T)row[NodeAlias]!;

    public T MaterializeEdge<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row) =>
        (T)row[EdgeAlias]!;

    public T MaterializeNodeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) =>
        (T)row[alias]!;

    public T MaterializeEdgeAt<T>(IGraphModel model, IReadOnlyDictionary<string, object?> row, string alias) =>
        (T)row[alias]!;

    public bool ReadExists(IReadOnlyDictionary<string, object?> row) =>
        row[CountAlias] is long and > 0;
}
