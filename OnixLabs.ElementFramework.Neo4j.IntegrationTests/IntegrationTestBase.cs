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
using Xunit.Abstractions;

namespace OnixLabs.ElementFramework.Neo4j.IntegrationTests;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    private IServiceScope scope = null!;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
        ServiceCollection services = new();
        ConfigureServices(services);
        Provider = services.BuildServiceProvider();
    }

    protected ITestOutputHelper Output { get; }

    protected IServiceProvider Provider { get; }

    protected IServiceProvider Scope => scope.ServiceProvider;

    public async Task InitializeAsync()
    {
        scope = Provider.CreateScope();
        await OnInitializeAsync();
    }

    public async Task DisposeAsync()
    {
        scope.Dispose();
        if (Provider is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
        else if (Provider is IDisposable disposable) disposable.Dispose();
        await OnDisposeAsync();
    }

    protected abstract void ConfigureServices(IServiceCollection services);

    protected virtual Task OnInitializeAsync() => Task.CompletedTask;

    protected virtual Task OnDisposeAsync() => Task.CompletedTask;
}
