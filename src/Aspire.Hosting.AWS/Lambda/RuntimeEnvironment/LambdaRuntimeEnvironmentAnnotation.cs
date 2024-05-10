// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.AWS.Lambda.RuntimeEnvironment;

internal sealed class LambdaRuntimeEnvironmentAnnotation : IResourceAnnotation
{
    public EndpointReference[] EndpointReferences { get; }
    public string? PathAndQuery { get; init; }

    public LambdaRuntimeEnvironmentAnnotation(params EndpointReference[] endpointReferences)
    {
        EndpointReferences = endpointReferences;
    }
}
