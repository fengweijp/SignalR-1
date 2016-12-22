﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Tests
{
    public class HubEndpointTests
    {
        [Fact]
        public async Task HubsAreDisposed()
        {
            var trackDispose = new TrackDispose();
            var serviceProvider = CreateServiceProvider(s => s.AddSingleton(trackDispose));

            var endPoint = serviceProvider.GetService<HubEndPoint<TestHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                await endPointTask;

                Assert.Equal(2, trackDispose.DisposeCount);
            }
        }

        [Fact]
        public async Task HubMethodCanReturnValueFromTask()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                // return Task<int>
                await SendRequest(connectionWrapper.HttpConnection, adapter, "TaskValueMethod");
                var res = await ReadConnectionOutputAsync<InvocationResultDescriptor>(connectionWrapper.HttpConnection);
                Assert.Equal(42L, res.Result);

                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                await endPointTask;
            }
        }

        [Fact]
        public async Task HubMethodCanReturnValue()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                // return int
                await SendRequest(connectionWrapper.HttpConnection, adapter, "ValueMethod");
                var res = await ReadConnectionOutputAsync<InvocationResultDescriptor>(connectionWrapper.HttpConnection);
                Assert.Equal(43L, res.Result);

                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                await endPointTask;
            }
        }

        [Fact]
        public async Task HubMethodCanBeStatic()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                // return int
                await SendRequest(connectionWrapper.HttpConnection, adapter, "StaticMethod");
                var res = await ReadConnectionOutputAsync<InvocationResultDescriptor>(connectionWrapper.HttpConnection);
                Assert.Equal("fromStatic", res.Result);

                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                await endPointTask;
            }
        }

        [Fact]
        public async Task HubMethodCanBeVoid()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                // return int
                await SendRequest(connectionWrapper.HttpConnection, adapter, "VoidMethod");
                var res = await ReadConnectionOutputAsync<InvocationResultDescriptor>(connectionWrapper.HttpConnection);
                Assert.Equal(null, res.Result);

                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                await endPointTask;
            }
        }

        [Fact]
        public async Task HubMethodWithMultiParam()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                // return int
                await SendRequest(connectionWrapper.HttpConnection, adapter, "MultiParamMethod", (byte)32, 42, 'm', "string");
                var res = await ReadConnectionOutputAsync<InvocationResultDescriptor>(connectionWrapper.HttpConnection);
                Assert.Equal((long)(32 + 42 + 'm'), res.Result);

                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                await endPointTask;
            }
        }

        [Fact]
        public async Task CannotCallOverriddenBaseHubMethod()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connectionWrapper = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connectionWrapper.Connection);

                await connectionWrapper.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                // return int
                await SendRequest(connectionWrapper.HttpConnection, adapter, "OnDisconnectedAsync");

                await Task.Delay(1000);
                // kill the connection
                connectionWrapper.Connection.Channel.Dispose();

                try
                {
                    await endPointTask;
                    Assert.True(false);
                }
                catch (Exception)
                {
                    // TODO
                }
            }
        }

        [Fact]
        public async Task BroadcastHubMethod_SendsToAllClients()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var firstConnection = new ConnectionWrapper())
            using (var secondConnection = new ConnectionWrapper())
            {
                var firstEndPointTask = endPoint.OnConnectedAsync(firstConnection.Connection);
                var secondEndPointTask = endPoint.OnConnectedAsync(secondConnection.Connection);

                await Task.WhenAll(firstConnection.HttpConnection.Input.ReadingStarted, secondConnection.HttpConnection.Input.ReadingStarted);

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                await SendRequest(firstConnection.HttpConnection, adapter, "BroadcastMethod", "test");

                foreach (var res in await Task.WhenAll(
                    ReadConnectionOutputAsync<InvocationDescriptor>(firstConnection.HttpConnection),
                    ReadConnectionOutputAsync<InvocationDescriptor>(secondConnection.HttpConnection)))
                {
                    Assert.Equal("Broadcast", res.Method);
                    Assert.Equal(1, res.Arguments.Length);
                    Assert.Equal("test", res.Arguments[0]);
                }

                // kill the connections
                firstConnection.Connection.Channel.Dispose();
                secondConnection.Connection.Channel.Dispose();
                
                await Task.WhenAll(firstEndPointTask, secondEndPointTask);
            }
        }

        [Fact]
        public async Task HubsCanAddAndSendToGroup()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var firstConnection = new ConnectionWrapper())
            using (var secondConnection = new ConnectionWrapper())
            {
                var firstEndPointTask = endPoint.OnConnectedAsync(firstConnection.Connection);
                var secondEndPointTask = endPoint.OnConnectedAsync(secondConnection.Connection);

                await Task.WhenAll(firstConnection.HttpConnection.Input.ReadingStarted, secondConnection.HttpConnection.Input.ReadingStarted);

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                await SendRequest_IgnoreReceive(firstConnection.HttpConnection, adapter, "GroupSendMethod", "testGroup", "test");
                // check that 'secondConnection' hasn't received the group send
                Assert.False(secondConnection.HttpConnection.Output.ReadAsync().IsCompleted);

                await SendRequest_IgnoreReceive(secondConnection.HttpConnection, adapter, "GroupAddMethod", "testGroup");

                await SendRequest(firstConnection.HttpConnection, adapter, "GroupSendMethod", "testGroup", "test");
                // check that 'firstConnection' hasn't received the group send
                Assert.False(firstConnection.HttpConnection.Output.ReadAsync().IsCompleted);

                // check that 'secondConnection' has received the group send
                var res = await ReadConnectionOutputAsync<InvocationDescriptor>(secondConnection.HttpConnection);
                Assert.Equal("Send", res.Method);
                Assert.Equal(1, res.Arguments.Length);
                Assert.Equal("test", res.Arguments[0]);

                // kill the connections
                firstConnection.Connection.Channel.Dispose();
                secondConnection.Connection.Channel.Dispose();

                await Task.WhenAll(firstEndPointTask, secondEndPointTask);
            }
        }

        [Fact]
        public async Task RemoveFromGroupWhenNotInGroupDoesNotFail()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var connection = new ConnectionWrapper())
            {
                var endPointTask = endPoint.OnConnectedAsync(connection.Connection);

                await connection.HttpConnection.Input.ReadingStarted;

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var writer = invocationAdapter.GetInvocationAdapter("json");

                await SendRequest_IgnoreReceive(connection.HttpConnection, writer, "GroupRemoveMethod", "testGroup");

                // kill the connection
                connection.Connection.Channel.Dispose();

                await endPointTask;
            }
        }

        [Fact]
        public async Task HubsCanSendToUser()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var firstConnection = new ConnectionWrapper())
            using (var secondConnection = new ConnectionWrapper())
            {
                var firstEndPointTask = endPoint.OnConnectedAsync(firstConnection.Connection);
                var secondEndPointTask = endPoint.OnConnectedAsync(secondConnection.Connection);

                await Task.WhenAll(firstConnection.HttpConnection.Input.ReadingStarted, secondConnection.HttpConnection.Input.ReadingStarted);

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                await SendRequest_IgnoreReceive(firstConnection.HttpConnection, adapter, "ClientSendMethod", secondConnection.Connection.User.Identity.Name, "test");

                // check that 'secondConnection' has received the group send
                var res = await ReadConnectionOutputAsync<InvocationDescriptor>(secondConnection.HttpConnection);
                Assert.Equal("Send", res.Method);
                Assert.Equal(1, res.Arguments.Length);
                Assert.Equal("test", res.Arguments[0]);

                // kill the connections
                firstConnection.Connection.Channel.Dispose();
                secondConnection.Connection.Channel.Dispose();

                await Task.WhenAll(firstEndPointTask, secondEndPointTask);
            }
        }

        [Fact]
        public async Task HubsCanSendToConnection()
        {
            var serviceProvider = CreateServiceProvider();

            var endPoint = serviceProvider.GetService<HubEndPoint<MethodHub>>();

            using (var firstConnection = new ConnectionWrapper())
            using (var secondConnection = new ConnectionWrapper())
            {
                var firstEndPointTask = endPoint.OnConnectedAsync(firstConnection.Connection);
                var secondEndPointTask = endPoint.OnConnectedAsync(secondConnection.Connection);

                await Task.WhenAll(firstConnection.HttpConnection.Input.ReadingStarted, secondConnection.HttpConnection.Input.ReadingStarted);

                var invocationAdapter = serviceProvider.GetService<InvocationAdapterRegistry>();
                var adapter = invocationAdapter.GetInvocationAdapter("json");

                await SendRequest_IgnoreReceive(firstConnection.HttpConnection, adapter, "ConnectionSendMethod", secondConnection.Connection.ConnectionId, "test");

                // check that 'secondConnection' has received the group send
                var res = await ReadConnectionOutputAsync<InvocationDescriptor>(secondConnection.HttpConnection);
                Assert.Equal("Send", res.Method);
                Assert.Equal(1, res.Arguments.Length);
                Assert.Equal("test", res.Arguments[0]);

                // kill the connections
                firstConnection.Connection.Channel.Dispose();
                secondConnection.Connection.Channel.Dispose();

                await Task.WhenAll(firstEndPointTask, secondEndPointTask);
            }
        }

        private class MethodHub : Hub
        {
            public Task GroupRemoveMethod(string groupName)
            {
                return Groups.RemoveAsync(groupName);
            }

            public Task ClientSendMethod(string userId, string message)
            {
                return Clients.User(userId).InvokeAsync("Send", message);
            }

            public Task ConnectionSendMethod(string connectionId, string message)
            {
                return Clients.Client(connectionId).InvokeAsync("Send", message);
            }

            public Task GroupAddMethod(string groupName)
            {
                return Groups.AddAsync(groupName);
            }

            public Task GroupSendMethod(string groupName, string message)
            {
                return Clients.Group(groupName).InvokeAsync("Send", message);
            }

            public Task BroadcastMethod(string message)
            {
                return Clients.All.InvokeAsync("Broadcast", message);
            }

            public Task<int> TaskValueMethod()
            {
                return Task.FromResult(42);
            }

            public int ValueMethod()
            {
                return 43;
            }

            static public string StaticMethod()
            {
                return "fromStatic";
            }

            public void VoidMethod()
            {
            }

            public int MultiParamMethod(byte b, int i, char c, string s)
            {
                return c + i + b;
            }

            public override Task OnDisconnectedAsync()
            {
                return TaskCache.CompletedTask;
            }
        }

        private class TestHub : Hub
        {
            private TrackDispose _trackDispose;

            public TestHub(TrackDispose trackDispose)
            {
                _trackDispose = trackDispose;
            }

            protected override void Dispose(bool dispose)
            {
                if (dispose)
                {
                    _trackDispose.DisposeCount++;
                }
            }
        }

        private class TrackDispose
        {
            public int DisposeCount = 0;
        }

        public async Task SendRequest(HttpConnection connection, IInvocationAdapter writer, string method, params object[] args)
        {
            if (connection == null)
            {
                throw new ArgumentNullException();
            }

            var stream = new MemoryStream();
            await writer.WriteMessageAsync(new InvocationDescriptor
            {
                Arguments = args,
                Method = method
            }, stream);

            var buffer = connection.Input.Alloc();
            buffer.Write(stream.ToArray());
            await buffer.FlushAsync();
        }

        public async Task SendRequest_IgnoreReceive(HttpConnection connection, IInvocationAdapter writer, string method, params object[] args)
        {
            await SendRequest(connection, writer, method, args);
            var methodResult = await connection.Output.ReadAsync();
            connection.Output.AdvanceReader(methodResult.Buffer.End, methodResult.Buffer.End);
        }

        private async Task<T> ReadConnectionOutputAsync<T>(HttpConnection connection)
        {
            // TODO: other formats?
            var methodResult = await connection.Output.ReadAsync();
            var serializer = new JsonSerializer();
            var res = serializer.Deserialize<T>(new JsonTextReader(new StreamReader(new MemoryStream(methodResult.Buffer.ToArray()))));
            connection.Output.AdvanceReader(methodResult.Buffer.End, methodResult.Buffer.End);

            return res;
        }

        private IServiceProvider CreateServiceProvider(Action<ServiceCollection> addServices = null)
        {
            var services = new ServiceCollection();
            services.AddOptions()
                .AddLogging()
                .AddSignalR();

            addServices?.Invoke(services);

            return services.BuildServiceProvider();
        }

        private class ConnectionWrapper : IDisposable
        {
            private static int ID;
            private PipelineFactory _factory;
            private HttpConnection _httpConnection;

            public Connection Connection;
            public HttpConnection HttpConnection => (HttpConnection)Connection.Channel;

            public ConnectionWrapper(string format = "json")
            {
                _factory = new PipelineFactory();
                _httpConnection = new HttpConnection(_factory);

                var connectionManager = new ConnectionManager();

                Connection = connectionManager.AddNewConnection(_httpConnection).Connection;
                Connection.Metadata["formatType"] = format;
                Connection.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, Interlocked.Increment(ref ID).ToString()) }));
            }

            public void Dispose()
            {
                Connection.Channel.Dispose();
                _httpConnection.Dispose();
                _factory.Dispose();
            }
        }
    }
}
