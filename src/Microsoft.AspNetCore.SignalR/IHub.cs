// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR
{
    interface IHub<TClient> : IDisposable
    {
        IHubConnectionContext<TClient> Clients { get; set; }
        HubCallerContext Context { get; set; }
        IGroupManager Groups { get; set; }
        Task OnConnectedAsync();
        Task OnDisconnectedAsync(Exception exception);
        object Invoke(MethodInfo methodInfo, object[] arguments);
    }
}
