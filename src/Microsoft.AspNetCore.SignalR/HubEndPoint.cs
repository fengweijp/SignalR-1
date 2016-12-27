// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR
{
    public class HubEndPoint<THub> : HubEndPoint<THub, IClientProxy> where THub : Hub<IClientProxy>
    {
        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub> hubContext,
                           InvocationAdapterRegistry registry,
                           ILogger<HubEndPoint<THub>> logger,
                           IServiceScopeFactory serviceScopeFactory,
                           IHubActivator hubActivator)
            : base(lifetimeManager, hubContext, registry, logger, serviceScopeFactory, hubActivator)
        {
        }
    }

    public class HubEndPoint<THub, TClient> : EndPoint, IInvocationBinder where THub : Hub<TClient>
    {
        private readonly Dictionary<string, Func<Connection, InvocationDescriptor, Task<InvocationResultDescriptor>>> _callbacks
            = new Dictionary<string, Func<Connection, InvocationDescriptor, Task<InvocationResultDescriptor>>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type[]> _paramTypes = new Dictionary<string, Type[]>();

        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly IHubContext<THub, TClient> _hubContext;
        private readonly ILogger<HubEndPoint<THub, TClient>> _logger;
        private readonly InvocationAdapterRegistry _registry;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IHubActivator _hubActivator;

        public HubEndPoint(HubLifetimeManager<THub> lifetimeManager,
                           IHubContext<THub, TClient> hubContext,
                           InvocationAdapterRegistry registry,
                           ILogger<HubEndPoint<THub, TClient>> logger,
                           IServiceScopeFactory serviceScopeFactory,
                           IHubActivator hubActivator)
        {
            _lifetimeManager = lifetimeManager;
            _hubContext = hubContext;
            _registry = registry;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _hubActivator = hubActivator;

            DiscoverHubMethods();
        }

        public override async Task OnConnectedAsync(Connection connection)
        {
            // TODO: Dispatch from the caller
            await Task.Yield();
            Exception exception = null;
            try
            {
                await _lifetimeManager.OnConnectedAsync(connection);

                using (var scope = _serviceScopeFactory.CreateScope())
                using (var hubWrapper = _hubActivator.Create<THub, TClient>(scope.ServiceProvider))
                {
                    var hub = (THub)hubWrapper;
                    InitializeHub(hub, connection);
                    await hub.OnConnectedAsync();
                }

                await DispatchMessagesAsync(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(0, ex, "Error when processing requests.");
                exception = ex;
                connection.Channel.Input.Complete(exception);
                connection.Channel.Output.Complete(exception);
            }
            finally
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                using (var hubWrapper = _hubActivator.Create<THub, TClient>(scope.ServiceProvider))
                {
                    var hub = (THub)hubWrapper;
                    InitializeHub(hub, connection);
                    await hub.OnDisconnectedAsync(exception);
                }

                await _lifetimeManager.OnDisconnectedAsync(connection);
            }
        }

        private async Task DispatchMessagesAsync(Connection connection)
        {
            var stream = connection.Channel.GetStream();
            var invocationAdapter = _registry.GetInvocationAdapter(connection.Metadata.Get<string>("formatType"));

            while (true)
            {
                // TODO: Handle receiving InvocationResultDescriptor
                var invocationDescriptor = await invocationAdapter.ReadMessageAsync(stream, this) as InvocationDescriptor;

                // Is there a better way of detecting that a connection was closed?
                if (invocationDescriptor == null)
                {
                    break;
                }

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Received hub invocation: {invocation}", invocationDescriptor);
                }

                InvocationResultDescriptor result;
                Func<Connection, InvocationDescriptor, Task<InvocationResultDescriptor>> callback;
                if (_callbacks.TryGetValue(invocationDescriptor.Method, out callback))
                {
                    result = await callback(connection, invocationDescriptor);
                }
                else
                {
                    // If there's no method then return a failed response for this request
                    result = new InvocationResultDescriptor
                    {
                        Id = invocationDescriptor.Id,
                        Error = $"Unknown hub method '{invocationDescriptor.Method}'"
                    };

                    _logger.LogError("Unknown hub method '{method}'", invocationDescriptor.Method);
                }

                await invocationAdapter.WriteMessageAsync(result, stream);
            }
        }

        private void InitializeHub(THub hub, Connection connection)
        {
            hub.Clients = _hubContext.Clients;
            hub.Context = new HubCallerContext(connection);
            hub.Groups = new GroupManager<THub>(connection, _lifetimeManager);
        }

        private void DiscoverHubMethods()
        {
            var type = typeof(THub);

            foreach (var methodInfo in type.GetTypeInfo().DeclaredMethods.Where(m => IsHubMethod(m)))
            {
                var methodName = methodInfo.Name;

                if (_callbacks.ContainsKey(methodName))
                {
                    throw new NotSupportedException($"Duplicate definitions of '{methodInfo.Name}'. Overloading is not supported.");
                }

                var parameters = methodInfo.GetParameters();
                _paramTypes[methodName] = parameters.Select(p => p.ParameterType).ToArray();

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Hub method '{methodName}' is bound", methodName);
                }

                _callbacks[methodName] = async (connection, invocationDescriptor) =>
                {
                    var invocationResult = new InvocationResultDescriptor()
                    {
                        Id = invocationDescriptor.Id
                    };

                    using (var scope = _serviceScopeFactory.CreateScope())
                    using (var hubWrapper = _hubActivator.Create<THub, TClient>(scope.ServiceProvider))
                    {
                        var hub = (THub)hubWrapper;
                        InitializeHub(hub, connection);

                        try
                        {
                            var result = methodInfo.Invoke(hub, invocationDescriptor.Arguments);
                            var resultTask = result as Task;
                            if (resultTask != null)
                            {
                                await resultTask;
                                if (methodInfo.ReturnType.GetTypeInfo().IsGenericType)
                                {
                                    var property = resultTask.GetType().GetProperty("Result");
                                    invocationResult.Result = property?.GetValue(resultTask);
                                }
                            }
                            else
                            {
                                invocationResult.Result = result;
                            }
                        }
                        catch (TargetInvocationException ex)
                        {
                            _logger.LogError(0, ex, "Failed to invoke hub method");
                            invocationResult.Error = ex.InnerException.Message;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(0, ex, "Failed to invoke hub method");
                            invocationResult.Error = ex.Message;
                        }
                    }

                    return invocationResult;
                };
            };
        }

        private static bool IsHubMethod(MethodInfo m)
        {
            // TODO: Add more checks
            return m.IsPublic && !m.IsSpecialName;
        }

        Type IInvocationBinder.GetReturnType(string invocationId)
        {
            return typeof(object);
        }

        Type[] IInvocationBinder.GetParameterTypes(string methodName)
        {
            Type[] types;
            if (!_paramTypes.TryGetValue(methodName, out types))
            {
                throw new InvalidOperationException($"The hub method '{methodName}' could not be resolved.");
            }
            return types;
        }
    }
}
