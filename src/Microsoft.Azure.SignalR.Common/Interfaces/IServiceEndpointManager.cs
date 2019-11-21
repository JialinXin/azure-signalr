// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointManager
    {
        IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint);

        ServiceEndpoint[] Endpoints { get; }

        IReadOnlyList<HubServiceEndpoint> GetEndpoints(string hub);

        void AddServiceEndpoint(ServiceEndpoint serviceEndpoint);

        void RemoveServiceEndpoint(ServiceEndpoint serviceEndpoint);

        HubServiceEndpoint GenerateHubServiceEndpoint(string hub, ServiceEndpoint serviceEndpoint);

        IMultiEndpointServiceConnectionContainer GetServiceConnectionContainer(string hub);

        // TODO: Move to ScaleService if we don't public the IScaleService
        void AddServiceConnectionContainer(string hub, IMultiEndpointServiceConnectionContainer container);

        IEnumerable<string> GetHubs();
    }
}
