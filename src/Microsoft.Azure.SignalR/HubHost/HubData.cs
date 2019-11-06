// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Metadata that describes the <see cref="Hub"/> information associated with a specific endpoint.
    /// </summary>
    public class HubData
    {
        public HubData(Type hubType)
        {
            HubType = hubType;
        }

        /// <summary>
        /// The type of <see cref="Hub"/>.
        /// </summary>
        public Type HubType { get; }
    }
}