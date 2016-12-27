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
            Assert.NotNull(
                (Hub<object>)new HubActivator().Create<Hub<object>, object>(Mock.Of<IServiceProvider>()));
        }

        [Fact]
        public void HubCanBeResolvedFromServiceProvider()
        {
            var hub = Mock.Of<Hub<object>>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockServiceProvider.Setup(sp => sp.GetService(typeof(Hub<object>))).Returns(hub);

            Assert.Same(hub,
                (Hub<object>)new HubActivator().Create<Hub<object>, object>(mockServiceProvider.Object));
        }
    }
}
