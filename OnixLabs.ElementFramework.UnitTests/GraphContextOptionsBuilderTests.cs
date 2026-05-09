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

using Microsoft.Extensions.Logging.Abstractions;

namespace OnixLabs.ElementFramework.UnitTests;

public class GraphContextOptionsBuilderTests
{
    private static GraphContextServices NoopBootstrap(GraphContext context, GraphContextOptions options) => null!;

    [Fact(DisplayName = "Build returns options carrying every configured service when all required services are supplied")]
    public void BuildReturnsOptionsWithAllServices()
    {
        FakeStatementEmitter emitter = new();
        FakeResultMaterializer materializer = new();
        FakeRawStatementExecutor executor = new();
        FakeGraphTransactionOpener opener = new();
        FakeTraversalTranslator translator = new();
        NullLoggerFactory loggerFactory = NullLoggerFactory.Instance;

        GraphContextOptions options = new GraphContextOptionsBuilder()
            .UseStatementEmitter(emitter)
            .UseResultMaterializer(materializer)
            .UseRawStatementExecutor(executor)
            .UseGraphTransactionOpener(opener)
            .UseTraversalTranslator(translator)
            .UseLoggerFactory(loggerFactory)
            .Build(NoopBootstrap);

        Assert.Same(emitter, options.StatementEmitter);
        Assert.Same(materializer, options.ResultMaterializer);
        Assert.Same(executor, options.RawStatementExecutor);
        Assert.Same(opener, options.GraphTransactionOpener);
        Assert.Same(translator, options.TraversalTranslator);
        Assert.Same(loggerFactory, options.LoggerFactory);
    }

    [Fact(DisplayName = "Build leaves LoggerFactory null when UseLoggerFactory is not called")]
    public void BuildLeavesLoggerFactoryNullByDefault()
    {
        GraphContextOptions options = ConfiguredBuilder().Build(NoopBootstrap);

        Assert.Null(options.LoggerFactory);
    }

    [Fact(DisplayName = "Build throws InvalidOperationException naming the missing UseStatementEmitter call")]
    public void BuildThrowsWhenStatementEmitterMissing()
    {
        GraphContextOptionsBuilder builder = new GraphContextOptionsBuilder()
            .UseResultMaterializer(new FakeResultMaterializer())
            .UseRawStatementExecutor(new FakeRawStatementExecutor())
            .UseGraphTransactionOpener(new FakeGraphTransactionOpener())
            .UseTraversalTranslator(new FakeTraversalTranslator());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.Build(NoopBootstrap));
        Assert.Contains(nameof(GraphContextOptionsBuilder.UseStatementEmitter), exception.Message);
    }

    [Fact(DisplayName = "Build throws InvalidOperationException naming the missing UseResultMaterializer call")]
    public void BuildThrowsWhenResultMaterializerMissing()
    {
        GraphContextOptionsBuilder builder = new GraphContextOptionsBuilder()
            .UseStatementEmitter(new FakeStatementEmitter())
            .UseRawStatementExecutor(new FakeRawStatementExecutor())
            .UseGraphTransactionOpener(new FakeGraphTransactionOpener())
            .UseTraversalTranslator(new FakeTraversalTranslator());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.Build(NoopBootstrap));
        Assert.Contains(nameof(GraphContextOptionsBuilder.UseResultMaterializer), exception.Message);
    }

    [Fact(DisplayName = "Build throws InvalidOperationException naming the missing UseRawStatementExecutor call")]
    public void BuildThrowsWhenRawStatementExecutorMissing()
    {
        GraphContextOptionsBuilder builder = new GraphContextOptionsBuilder()
            .UseStatementEmitter(new FakeStatementEmitter())
            .UseResultMaterializer(new FakeResultMaterializer())
            .UseGraphTransactionOpener(new FakeGraphTransactionOpener())
            .UseTraversalTranslator(new FakeTraversalTranslator());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.Build(NoopBootstrap));
        Assert.Contains(nameof(GraphContextOptionsBuilder.UseRawStatementExecutor), exception.Message);
    }

    [Fact(DisplayName = "Build throws InvalidOperationException naming the missing UseGraphTransactionOpener call")]
    public void BuildThrowsWhenGraphTransactionOpenerMissing()
    {
        GraphContextOptionsBuilder builder = new GraphContextOptionsBuilder()
            .UseStatementEmitter(new FakeStatementEmitter())
            .UseResultMaterializer(new FakeResultMaterializer())
            .UseRawStatementExecutor(new FakeRawStatementExecutor())
            .UseTraversalTranslator(new FakeTraversalTranslator());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.Build(NoopBootstrap));
        Assert.Contains(nameof(GraphContextOptionsBuilder.UseGraphTransactionOpener), exception.Message);
    }

    [Fact(DisplayName = "Build throws InvalidOperationException naming the missing UseTraversalTranslator call")]
    public void BuildThrowsWhenTraversalTranslatorMissing()
    {
        GraphContextOptionsBuilder builder = new GraphContextOptionsBuilder()
            .UseStatementEmitter(new FakeStatementEmitter())
            .UseResultMaterializer(new FakeResultMaterializer())
            .UseRawStatementExecutor(new FakeRawStatementExecutor())
            .UseGraphTransactionOpener(new FakeGraphTransactionOpener());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.Build(NoopBootstrap));
        Assert.Contains(nameof(GraphContextOptionsBuilder.UseTraversalTranslator), exception.Message);
    }

    [Fact(DisplayName = "Build throws ArgumentNullException when bootstrap is null")]
    public void BuildThrowsForNullBootstrap()
    {
        GraphContextOptionsBuilder builder = ConfiguredBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.Build(null!));
    }

    [Fact(DisplayName = "UseStatementEmitter throws ArgumentNullException for null")]
    public void UseStatementEmitterThrowsForNull() =>
        Assert.Throws<ArgumentNullException>(() => new GraphContextOptionsBuilder().UseStatementEmitter(null!));

    [Fact(DisplayName = "UseResultMaterializer throws ArgumentNullException for null")]
    public void UseResultMaterializerThrowsForNull() =>
        Assert.Throws<ArgumentNullException>(() => new GraphContextOptionsBuilder().UseResultMaterializer(null!));

    [Fact(DisplayName = "UseRawStatementExecutor throws ArgumentNullException for null")]
    public void UseRawStatementExecutorThrowsForNull() =>
        Assert.Throws<ArgumentNullException>(() => new GraphContextOptionsBuilder().UseRawStatementExecutor(null!));

    [Fact(DisplayName = "UseGraphTransactionOpener throws ArgumentNullException for null")]
    public void UseGraphTransactionOpenerThrowsForNull() =>
        Assert.Throws<ArgumentNullException>(() => new GraphContextOptionsBuilder().UseGraphTransactionOpener(null!));

    [Fact(DisplayName = "UseTraversalTranslator throws ArgumentNullException for null")]
    public void UseTraversalTranslatorThrowsForNull() =>
        Assert.Throws<ArgumentNullException>(() => new GraphContextOptionsBuilder().UseTraversalTranslator(null!));

    [Fact(DisplayName = "UseLoggerFactory throws ArgumentNullException for null")]
    public void UseLoggerFactoryThrowsForNull() =>
        Assert.Throws<ArgumentNullException>(() => new GraphContextOptionsBuilder().UseLoggerFactory(null!));

    private static GraphContextOptionsBuilder ConfiguredBuilder() => new GraphContextOptionsBuilder()
        .UseStatementEmitter(new FakeStatementEmitter())
        .UseResultMaterializer(new FakeResultMaterializer())
        .UseRawStatementExecutor(new FakeRawStatementExecutor())
        .UseGraphTransactionOpener(new FakeGraphTransactionOpener())
        .UseTraversalTranslator(new FakeTraversalTranslator());
}
