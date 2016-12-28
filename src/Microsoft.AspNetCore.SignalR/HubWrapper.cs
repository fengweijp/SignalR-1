// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR
{
    public class HubWrapper<THub, TClient> : IHub<TClient>
        where THub : Hub<TClient>
    {
        private readonly THub _hub;
        private readonly bool _shouldDispose;

        public HubWrapper(THub hub, bool shouldDispose)
        {
            if (hub == null)
            {
                throw new ArgumentNullException(nameof(hub));
            }

            _hub = hub;
            _shouldDispose = shouldDispose;
        }

        public IHubConnectionContext<TClient> Clients
        {
            get
            {
                return _hub.Clients;
            }
            set
            {
                _hub.Clients = value;
            }
        }

        public HubCallerContext Context
        {
            get
            {
                return _hub.Context;
            }
            set
            {
                _hub.Context = value;
            }
        }

        public IGroupManager Groups
        {
            get
            {
                return _hub.Groups;
            }
            set
            {
                _hub.Groups = value;
            }
        }

        public Task OnConnectedAsync()
        {
            return _hub.OnConnectedAsync();
        }

        public Task OnDisconnectedAsync(Exception exception)
        {
            return _hub.OnDisconnectedAsync(exception);
        }

        public object Invoke(MethodInfo methodInfo, object[] arguments)
        {
            return _hub.Invoke(methodInfo, arguments);
        }

        public void Dispose()
        {
            if (_shouldDispose)
            {
                _hub.Dispose();
            }
        }
    }
}
