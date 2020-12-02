﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;

namespace Microsoft.Azure.SignalR.Management
{
    //todo public later
    internal class ContextOptions
    {
        //Users not allowed to configure it
        internal string ProductInfo { get; set; }

        public ServiceEndpoint[] ServiceEndpoints { get; set; }

        public string ApplicationName { get; set; }

        public int ConnectionCount { get; set; } = 2;

        public IWebProxy Proxy { get; set; }

        internal ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Persistent;

        internal void ValidateOptions()
        {
            if (ServiceEndpoints.Length == 0)
            {
                throw new InvalidOperationException($"Service endpoint(s) is/are not configured.");
            }
        }
    }
}