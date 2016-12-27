// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.SignalR
{
    public class HubWrapper<THub, TClient> : IDisposable
        where THub : Hub<TClient>
    {
        private readonly THub _hub;
        private readonly bool _shouldDispose;

        public HubWrapper(THub hub, bool shouldDispose)
        {
            _hub = hub;
            _shouldDispose = shouldDispose;
        }

        public static explicit operator THub(HubWrapper<THub, TClient> wrapper)
        {
            return wrapper._hub;
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
