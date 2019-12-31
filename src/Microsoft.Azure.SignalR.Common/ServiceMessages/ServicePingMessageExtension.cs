// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    using System;
    using ServicePingMessage = Microsoft.Azure.SignalR.Protocol.PingMessage;

    public static class ServicePingMessageExtension
    {
        public static bool TryGetServiceStatusPingMessage(this ServicePingMessage message, out ServiceStatusPingMessage serviceStatus)
        {
            if (message.TryGetValue(ServiceStatusPingMessage.Key, out var value))
            {
                serviceStatus = new ServiceStatusPingMessage(value);
                return true;
            }

            serviceStatus = null;
            return false;
        }

        public static bool TryGetValue(this ServicePingMessage pingMessage, string key, out string value)
        {
            if (pingMessage == null)
            {
                value = null;
                return false;
            }

            int index = 0;
            while (index < pingMessage.Messages.Length - 1)
            {
                var item1 = pingMessage.Messages[index];
                var item2 = pingMessage.Messages[index + 1];
                if (item1 == key)
                {
                    value = item2;
                    return true;
                }

                index += 2;
            }

            value = null;
            return false;
        }

        public static bool TryGetServers(this ServicePingMessage message, out string serverIds, long timeout = 160000000)
        {
            if (message.TryGetValue(ServiceStatusPingMessage.ServersKey, out var serverContext ))
            {
                var values = serverContext.Split(':');
                if (values.Length == 2 
                    && long.TryParse(values[1], out var updatedTime) 
                    && DateTime.UtcNow.Ticks - updatedTime <= timeout)
                {
                    serverIds = values[0];
                    return true;
                }
            }

            serverIds = null;
            return false;
        }
    }
}
