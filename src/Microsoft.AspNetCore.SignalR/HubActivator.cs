// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.SignalR
{
    public class HubActivator : IHubActivator
    {
        public HubWrapper<THub, TClient> Create<THub, TClient>(IServiceProvider provider)
            where THub : Hub<TClient>
        {
            var hub = provider.GetService<THub>();
            var created = false;

            if (hub == null)
            {
                hub = ActivatorUtilities.CreateInstance<THub>(provider);
                created = true;
            }

            return new HubWrapper<THub, TClient>(hub, created);
        }
    }
}
