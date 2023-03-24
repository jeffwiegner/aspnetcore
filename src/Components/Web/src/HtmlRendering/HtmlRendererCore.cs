// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Components.HtmlRendering;

internal sealed class HtmlRendererCore : Renderer
{
    private static readonly Task CanceledRenderTask = Task.FromCanceled(new CancellationToken(canceled: true));
    private Action<HtmlComponent>? _onComponentUpdated;

    private Task? _combinedQuiescenceTask;

    public HtmlRendererCore(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, IComponentActivator componentActivator)
        : base(serviceProvider, loggerFactory, componentActivator)
    {
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    public HtmlRootComponent BeginRenderingComponentAsync(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type componentType,
        ParameterView initialParameters)
    {
        var component = InstantiateComponent(componentType);
        var componentId = AssignRootComponentId(component);
        var quiescenceTask = RenderRootComponentAsync(componentId, initialParameters);

        if (quiescenceTask.IsFaulted)
        {
            ExceptionDispatchInfo.Capture(quiescenceTask.Exception.InnerException ?? quiescenceTask.Exception).Throw();
        }

        if (_combinedQuiescenceTask is null)
        {
            _combinedQuiescenceTask = quiescenceTask;
        }
        else
        {
            _combinedQuiescenceTask = Task.WhenAll(_combinedQuiescenceTask, quiescenceTask);
        }

        return new HtmlRootComponent(this, componentId, quiescenceTask);
    }

    protected override void HandleException(Exception exception)
        => ExceptionDispatchInfo.Capture(exception).Throw();

    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        if (_onComponentUpdated is not null)
        {
            var numUpdatedComponents = renderBatch.UpdatedComponents.Count;
            for (var i = 0; i < numUpdatedComponents; i++)
            {
                ref var diff = ref renderBatch.UpdatedComponents.Array[i];
                _onComponentUpdated(new HtmlComponent(this, diff.ComponentId));
            }
        }

        // By default we return a canceled task. This has the effect of making it so that the
        // OnAfterRenderAsync callbacks on components don't run by default.
        // This way, by default prerendering gets the correct behavior and other renderers
        // override the UpdateDisplayAsync method already, so those components can
        // either complete a task when the client acknowledges the render, or return a canceled task
        // when the renderer gets disposed.

        // We believe that returning a canceled task is the right behavior as we expect that any class
        // that subclasses this class to provide an implementation for a given rendering scenario respects
        // the contract that OnAfterRender should only be called when the display has successfully been updated
        // and the application is interactive. (Element and component references are populated and JavaScript interop
        // is available).

        return CanceledRenderTask;
    }

    internal new ArrayRange<RenderTreeFrame> GetCurrentRenderTreeFrames(int componentId)
        => base.GetCurrentRenderTreeFrames(componentId);

    public Task WaitForQuiescenceAsync(Action<HtmlComponent> onComponentUpdated)
    {
        Dispatcher.AssertAccess();

        if (_onComponentUpdated is not null)
        {
            // TODO: Support multiple
            // Or better still, make this API internal and then we can just choose not to support multiple
            // since it's not a usage pattern required by the framework.
            throw new InvalidOperationException($"{nameof(_onComponentUpdated)} is already set.");
        }

        _onComponentUpdated = onComponentUpdated;

        return _combinedQuiescenceTask ?? Task.CompletedTask;
    }
}
