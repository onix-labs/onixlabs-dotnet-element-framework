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
