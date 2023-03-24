// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.HtmlRendering;

namespace Microsoft.AspNetCore.Components.Web;

/// <summary>
/// 
/// </summary>
public sealed class HtmlRootComponent : HtmlComponent
{
    private readonly Task _quiescenceTask;

    internal HtmlRootComponent(HtmlRendererCore? renderer, int componentId, Task quiescenceTask)
        : base(renderer, componentId)
    {
        _quiescenceTask = quiescenceTask;
    }

    /// <summary>
    /// Obtains a <see cref="Task"/> that completes when the component hierarchy has completed asynchronous tasks such as loading.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes when the component hierarchy has completed asynchronous tasks such as loading.</returns>
    public Task WaitForQuiescenceAsync()
        => _quiescenceTask;
}
