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

using Google.Solutions.Apis.Compute;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Util;
using Google.Solutions.IapDesktop.Application.Services.Adapters;
using Google.Solutions.IapDesktop.Core.ClientModel.Protocol;
using Google.Solutions.IapDesktop.Core.ClientModel.Transport;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.IapDesktop.Extensions.Shell.Services.Session
{
    public abstract class SessionContextBase<TCredential, TParameters>
        : ISessionContext<TCredential, TParameters>
        where TCredential : ISessionCredential
    {
        private readonly IIapTransportFactory iapTransportFactory;
        private readonly IDirectTransportFactory directTransportFactory;
        private readonly IComputeEngineAdapter computeEngineAdapter;

        protected async Task<ITransport> ConnectTransportAsync(
            IProtocol protocol,
            SessionTransportType transportType,
            ushort port,
            TimeSpan connectionTimeout,
            CancellationToken cancellationToken)
        {
            switch (transportType)
            {
                case SessionTransportType.IapTunnel:
                    return await Transport
                        .CreateIapTransportAsync(
                            this.iapTransportFactory,
                            protocol,
                            this.Instance,
                            port,
                            connectionTimeout)
                        .ConfigureAwait(false);

                case SessionTransportType.Vpc:
                    return await Transport
                        .CreateVpcTransportAsync(
                            this.directTransportFactory,
                            protocol,
                            this.computeEngineAdapter,
                            this.Instance,
                            port,
                            cancellationToken)
                        .ConfigureAwait(false);

                default:
                    throw new ArgumentException("Unrecognized transport type");
            }
        }

        protected SessionContextBase(
            IIapTransportFactory iapTransportFactory,
            IDirectTransportFactory directTransportFactory,
            IComputeEngineAdapter computeEngineAdapter,
            InstanceLocator instance,
            TParameters parameters)
        {
            this.iapTransportFactory = iapTransportFactory.ExpectNotNull(nameof(SessionContextBase<TCredential, TParameters>.iapTransportFactory));
            this.directTransportFactory = directTransportFactory.ExpectNotNull(nameof(directTransportFactory));
            this.computeEngineAdapter = computeEngineAdapter.ExpectNotNull(nameof(computeEngineAdapter));
            this.Instance = instance.ExpectNotNull(nameof(instance));
            this.Parameters = parameters.ExpectNotNull(nameof(parameters));
        }

        //---------------------------------------------------------------------
        // ISessionContext.
        //---------------------------------------------------------------------

        public InstanceLocator Instance { get; }

        public TParameters Parameters { get; }

        public abstract Task<TCredential> AuthorizeCredentialAsync(
            CancellationToken cancellationToken);

        public abstract Task<ITransport> ConnectTransportAsync(
            CancellationToken cancellationToken);

        public virtual void Dispose()
        {
        }
    }
}
