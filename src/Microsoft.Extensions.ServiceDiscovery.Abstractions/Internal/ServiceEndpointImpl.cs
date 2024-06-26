// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.Extensions.ServiceDiscovery.Internal;

internal sealed class ServiceEndpointImpl(EndPoint endPoint, IFeatureCollection? features = null) : ServiceEndpoint
{
    public override EndPoint EndPoint { get; } = endPoint;

    public override IFeatureCollection Features { get; } = features ?? new FeatureCollection();

    public override string? ToString() => EndPoint switch
    {
        IPEndPoint ip when ip.Port == 0 && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 => $"[{ip.Address}]",
        IPEndPoint ip when ip.Port == 0 => $"{ip.Address}",
        DnsEndPoint dns when dns.Port == 0 => $"{dns.Host}",
        DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
        _ => EndPoint.ToString()!
    };
}
