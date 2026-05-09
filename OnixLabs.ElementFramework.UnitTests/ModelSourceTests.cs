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

using Microsoft.Extensions.DependencyInjection;

namespace OnixLabs.ElementFramework.UnitTests;

public class ModelSourceTests
{
    [Fact(DisplayName = "ModelFor returns the same model instance for distinct contexts of the same CLR type")]
    public void ModelForCachesByContextType()
    {
        ModelSourceContextA first = NewContext<ModelSourceContextA>();
        ModelSourceContextA second = NewContext<ModelSourceContextA>();

        IGraphModel firstModel = ModelSource.ModelFor(first);
        IGraphModel secondModel = ModelSource.ModelFor(second);

        Assert.Same(firstModel, secondModel);
    }

    [Fact(DisplayName = "ModelFor returns distinct models for different context CLR types")]
    public void ModelForReturnsDistinctModelsAcrossTypes()
    {
        ModelSourceContextA contextA = NewContext<ModelSourceContextA>();
        ModelSourceContextB contextB = NewContext<ModelSourceContextB>();

        IGraphModel modelA = ModelSource.ModelFor(contextA);
        IGraphModel modelB = ModelSource.ModelFor(contextB);

        Assert.NotSame(modelA, modelB);
        Assert.NotNull(modelA.GetNode(typeof(Author)));
        Assert.NotNull(modelB.GetNode(typeof(Post)));
    }

    [Fact(DisplayName = "Building a context with an invalid model surfaces ModelConfigurationException at first resolution")]
    public void InvalidModelSurfacesConfigurationException()
    {
        Assert.Throws<ModelConfigurationException>(() => NewContext<ModelSourceContextWithUnregisteredEndpoint>());
    }

    private static T NewContext<T>() where T : GraphContext
    {
        ServiceCollection services = new();
        services.AddGraphContext<T>(builder => builder
            .UseStatementEmitter(new FakeStatementEmitter())
            .UseResultMaterializer(new FakeResultMaterializer())
            .UseRawStatementExecutor(new FakeRawStatementExecutor())
            .UseGraphTransactionOpener(new FakeGraphTransactionOpener())
            .UseTraversalTranslator(new FakeTraversalTranslator()));
        return services.BuildServiceProvider().GetRequiredService<T>();
    }
}

internal sealed class ModelSourceContextA(GraphContextOptions options) : GraphContext(options)
{
    protected internal override void OnModelCreating(IGraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<Author>().HasKey(a => a.Id);
    }
}

internal sealed class ModelSourceContextB(GraphContextOptions options) : GraphContext(options)
{
    protected internal override void OnModelCreating(IGraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<Post>().HasKey(p => p.Id);
    }
}

internal sealed class ModelSourceContextWithUnregisteredEndpoint(GraphContextOptions options) : GraphContext(options)
{
    protected internal override void OnModelCreating(IGraphModelBuilder modelBuilder)
    {
        modelBuilder.Node<Author>().HasKey(a => a.Id);
        modelBuilder.Relationship<Author, Wrote, Post>();
    }
}
