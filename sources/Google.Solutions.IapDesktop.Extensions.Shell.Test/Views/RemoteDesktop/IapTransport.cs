﻿//
// Copyright 2023 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using Google.Apis.Auth.OAuth2;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Runtime;
using Google.Solutions.Iap;
using Google.Solutions.Iap.Net;
using Google.Solutions.Iap.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport.Policies;
using Google.Solutions.IapDesktop.Extensions.Shell.Services.Session;
using Google.Solutions.Testing.Apis.Integration;
using System.Net;

namespace Google.Solutions.IapDesktop.Extensions.Shell.Test.Views.RemoteDesktop
{
    internal class IapTransport : DisposableBase, ITransport
    {
        public IapTunnel Tunnel { get; }

        public IProtocol Protocol { get; }

        public IPEndPoint Endpoint => this.Tunnel.LocalEndpoint;

        private IapTransport(IapTunnel tunnel, IProtocol protocol)
        {
            this.Tunnel = tunnel;
            this.Protocol = protocol;
        }

        public static IapTransport ForRdp(
            InstanceLocator instance, 
            ICredential credential)
        {
            var policy = new AllowAllPolicy();
            var listener = IapListener.CreateLocalListener(
                new IapClient(
                    credential,
                    instance,
                    3389,
                    IapClient.DefaultNetworkInterface,
                    TestProject.UserAgent),
                policy);

            var profile = new IapTunnel.Profile(
                RdpProtocol.Protocol,
                policy,
                instance,
                3389,
                new IPEndPoint(IPAddress.Loopback, listener.LocalPort));

            return new IapTransport(
                new IapTunnel(
                    listener,
                    profile,
                    IapTunnelFlags.None),
                RdpProtocol.Protocol);
        }

        protected override void Dispose(bool disposing)
        {
            this.Tunnel.Dispose();
            base.Dispose(disposing);
        }
    }
}
