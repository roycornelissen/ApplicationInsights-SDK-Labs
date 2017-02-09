﻿using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Wcf.Implementation;
using Microsoft.ApplicationInsights.Wcf.Tests.Integration;
using Microsoft.ApplicationInsights.Wcf.Tests.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.ServiceModel;

namespace Microsoft.ApplicationInsights.Wcf.Tests
{
    [TestClass]
    public class ClientTelemetryEndpointBehaviorTests
    {
        [TestMethod]
        public void BehaviorBuildsContractDescription_TwoWay()
        {
            using ( var host = new HostingContext<SimpleService, ISimpleService>() )
            {
                var configuration = new TelemetryConfiguration();
                var factory = new ChannelFactory<ISimpleService>(new NetTcpBinding(), host.GetServiceAddress());
                ISimpleService channel = null;
                try
                {
                    var desc = ClientTelemetryEndpointBehavior.BuildDescription(factory.Endpoint);
                    factory.Close();

                    Assert.IsNotNull(desc);
                    ClientOpDescription op;
                    var found = desc.TryLookupByAction("http://tempuri.org/ISimpleService/GetSimpleData", out op);
                    Assert.IsTrue(found);
                    Assert.AreEqual(false, op.IsOneWay);
                    Assert.AreEqual("GetSimpleData", op.Name);
                } catch
                {
                    factory.Abort();
                    if ( channel != null )
                    {
                        ((IClientChannel)channel).Abort();
                    }
                    throw;
                }
            }
        }

        [TestMethod]
        public void BehaviorAddsCustomBinding()
        {
            using ( var host = new HostingContext<SimpleService, ISimpleService>() )
            {
                var binding = new NetTcpBinding();
                binding.TransferMode = TransferMode.Streamed;
                var configuration = new TelemetryConfiguration();
                var factory = new ChannelFactory<ISimpleService>(binding, host.GetServiceAddress());
                ISimpleService channel = null;
                try
                {
                    var behavior = new ClientTelemetryEndpointBehavior(configuration);
#if NET40
                    factory.Endpoint.Behaviors.Add(behavior);
#else
                    factory.Endpoint.EndpointBehaviors.Add(behavior);
#endif

                    channel = factory.CreateChannel();
                    var innerChannel = GetInnerChannel(channel);
                    ((IClientChannel)channel).Close();
                    factory.Close();

                    Assert.IsInstanceOfType(innerChannel, typeof(ClientTelemetryRequestChannel), "Telemetry channel is is missing");
                } catch
                {
                    factory.Abort();
                    if ( channel != null )
                    {
                        ((IClientChannel)channel).Abort();
                    }
                    throw;
                }
            }
        }

        private System.ServiceModel.Channels.IChannel GetInnerChannel(object proxy)
        {
            // TransparentProxy -> ServiceChannelProxy -> ServiceChannel
            var realProxy = RemotingServices.GetRealProxy(proxy);
            var proxyType = realProxy.GetType();
            var field = proxyType.GetField("serviceChannel", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance);
            object serviceChannel = field.GetValue(realProxy);

            var prop = serviceChannel.GetType().GetProperty("InnerChannel", BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance);
            return prop.GetValue(serviceChannel, null) as System.ServiceModel.Channels.IChannel;
        }
    }
}
