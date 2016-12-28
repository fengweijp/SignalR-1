// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Tests
{
    public class HubActivatorTests
    {
        [Fact]
        public void HubCreatedIfNotResolvedFromServiceProvider()
        {
            Assert.NotNull(new HubActivator().Create<Hub<object>, object>(Mock.Of<IServiceProvider>()));
        }

        [Fact]
        public void HubCanBeResolvedFromServiceProvider()
        {
            var hub = Mock.Of<Hub<object>>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(Hub<object>))).Returns(hub);
            var resolvedHub = new HubActivator().Create<Hub<object>, object>(mockServiceProvider.Object);
            resolvedHub.OnConnectedAsync();

            Mock.Get(hub).Verify(h => h.OnConnectedAsync(), Times.Once);
        }
    }
}
