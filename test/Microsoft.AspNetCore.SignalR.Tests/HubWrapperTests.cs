// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Moq;
using Moq.Protected;
using Xunit;

namespace Microsoft.AspNetCore.SignalR.Tests
{
    public class HubWrapperTests
    {
        [Fact]
        public void HubWrapperDisposesHubsCreatedByActivator()
        {
            var mockHub = new Mock<Hub<object>>();
            mockHub.Protected().Setup("Dispose", It.IsAny<bool>());
            new HubWrapper<Hub<object>, object>(mockHub.Object, true).Dispose();
            mockHub.Protected().Verify("Dispose", Times.Once(), true);
        }

        [Fact]
        public void HubWrapperDoesNotDisposeHubsResolvedFromServiceProvider()
        {
            var mockHub = new Mock<Hub<object>>();
            mockHub.Protected().Setup("Dispose", It.IsAny<bool>());
            new HubWrapper<Hub<object>, object>(mockHub.Object, false).Dispose();
            mockHub.Protected().Verify("Dispose", Times.Never(), true);
        }
    }
}
