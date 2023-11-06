﻿//
// Copyright 2020 Google LLC
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

using Google.Apis.CloudOSLogin.v1;
using Google.Apis.CloudOSLogin.v1.Data;
using Google.Apis.Discovery;
using Google.Apis.Requests;
using Google.Apis.Services;
using Google.Apis.Util;
using Google.Solutions.Apis.Auth;
using Google.Solutions.Apis.Auth.Gaia;
using Google.Solutions.Apis.Auth.Iam;
using Google.Solutions.Apis.Client;
using Google.Solutions.Apis.Diagnostics;
using Google.Solutions.Apis.Locator;
using Google.Solutions.Common.Diagnostics;
using Google.Solutions.Common.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Google.Solutions.Apis.Compute
{
    /// <summary>
    /// Client for OS Login API.
    /// </summary>
    public interface IOsLoginClient : IClient
    {
        /// <summary>
        /// Import user's public key to OS Login.
        /// </summary>
        /// <param name="keyType">Key type (for ex, 'ssh-rsa')</param>
        /// <param name="keyBlob">SSH1/Base64-encoded public key</param>
        Task<LoginProfile> ImportSshPublicKeyAsync(
            ProjectLocator project,
            string keyType,
            string keyBlob,
            TimeSpan validity,
            CancellationToken token);

        /// <summary>
        /// Read user's profile and published SSH keys.
        /// </summary>
        Task<LoginProfile> GetLoginProfileAsync(
           ProjectLocator project,
           CancellationToken token);

        /// <summary>
        /// Delete existing authorized key.
        /// </summary>
        Task DeleteSshPublicKeyAsync(
            string fingerprint,
            CancellationToken cancellationToken);
    }

    public class OsLoginClient : ApiClientBase, IOsLoginClient
    {
        private readonly IAuthorization authorization;
        private readonly CloudOSLoginService service;

        public OsLoginClient(
            ServiceEndpoint<OsLoginClient> endpoint,
            IAuthorization authorization,
            UserAgent userAgent)
            : base(endpoint, authorization, userAgent)
        {
            this.authorization = authorization.ExpectNotNull(nameof(authorization));
            this.service = new CloudOSLoginService(this.Initializer);
        }

        public static ServiceEndpoint<OsLoginClient> CreateEndpoint(
            ServiceRoute? route = null)
        {
            return new ServiceEndpoint<OsLoginClient>(
                route ?? ServiceRoute.Public,
                "https://oslogin.googleapis.com/");
        }

        //---------------------------------------------------------------------
        // IOsLoginClient.
        //---------------------------------------------------------------------

        public async Task<LoginProfile> ImportSshPublicKeyAsync(
            ProjectLocator project,
            string keyType,
            string keyBlob,
            TimeSpan validity,
            CancellationToken token)
        {
            project.ExpectNotNull(nameof(project));
            keyType.ExpectNotEmpty(nameof(keyType));
            keyBlob.ExpectNotEmpty(nameof(keyBlob));

            Debug.Assert(!keyType.Contains(' '));

            var gaiaSession = this.authorization.Session as IGaiaOidcSession
                ?? throw new OsLoginNotSupportedForWorkloadIdentityException();

            using (ApiTraceSource.Log.TraceMethod().WithParameters(project))
            {
                var expiryTimeUsec = new DateTimeOffset(DateTime.UtcNow.Add(validity))
                    .ToUnixTimeMilliseconds() * 1000;

                var userEmail = gaiaSession.Email;
                Debug.Assert(userEmail != null);

                var request = this.service.Users.ImportSshPublicKey(
                    new SshPublicKey()
                    {
                        Key = $"{keyType} {keyBlob}",
                        ExpirationTimeUsec = expiryTimeUsec
                    },
                    $"users/{userEmail}");
                request.ProjectId = project.ProjectId;

                try
                {
                    var response = await request
                        .ExecuteAsync(token)
                        .ConfigureAwait(false);

                    //
                    // Creating the profile succeeded (if it didn't exist
                    // yet -- but we still need to check if the key was actually
                    // added.
                    //
                    // If the 'Allow users to manage their SSH public keys
                    // via the OS Login API' policy is disabled (in Cloud Identity),
                    // then adding the key won't work.
                    //
                    if (response.LoginProfile.SshPublicKeys
                        .EnsureNotNull()
                        .Any(kvp => kvp.Value.Key.Contains(keyBlob)))
                    {
                        return response.LoginProfile;
                    }
                    else
                    {
                        //
                        // Key wasn't added.
                        //
                        throw new ResourceAccessDeniedException(
                            "You do not have sufficient permissions to publish an SSH " +
                            "key to OS Login",
                            HelpTopics.ManagingOsLogin,
                            new GoogleApiException("oslogin", response.Details ?? string.Empty));
                    }
                }
                catch (GoogleApiException e) when (e.IsAccessDenied())
                {
                    //
                    // Likely reason: The user account is a consumer account or
                    // an administrator has disabled POSIX account/SSH key information
                    // updates in the Admin Console.
                    //
                    throw new ResourceAccessDeniedException(
                        "You do not have sufficient permissions to use OS Login: " +
                        e.Error?.Message ?? "access denied",
                        HelpTopics.ManagingOsLogin,
                        e);
                }
            }
        }

        public async Task<LoginProfile> GetLoginProfileAsync(
            ProjectLocator project,
            CancellationToken token)
        {
            using (ApiTraceSource.Log.TraceMethod().WithParameters(project))
            {
                var gaiaSession = this.authorization.Session as IGaiaOidcSession
                    ?? throw new OsLoginNotSupportedForWorkloadIdentityException();

                var request = this.service.Users.GetLoginProfile(
                    $"users/{gaiaSession.Email}");
                request.ProjectId = project.ProjectId;

                try
                {
                    return await request
                        .ExecuteAsync(token)
                        .ConfigureAwait(false);
                }
                catch (GoogleApiException e) when (e.IsAccessDenied())
                {
                    throw new ResourceAccessDeniedException(
                        "You do not have sufficient permissions to use OS Login: " +
                        e.Error?.Message ?? "access denied",
                        HelpTopics.ManagingOsLogin,
                        e);
                }
            }
        }

        public async Task DeleteSshPublicKeyAsync(
            string fingerprint,
            CancellationToken cancellationToken)
        {
            using (ApiTraceSource.Log.TraceMethod().WithParameters(fingerprint))
            {
                var gaiaSession = this.authorization.Session as IGaiaOidcSession
                    ?? throw new OsLoginNotSupportedForWorkloadIdentityException();

                try
                {
                    var userEmail = gaiaSession.Email;
                    Debug.Assert(userEmail != null);

                    await this.service.Users.SshPublicKeys
                        .Delete($"users/{userEmail}/sshPublicKeys/{fingerprint}")
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (GoogleApiException e) when (e.IsAccessDenied())
                {
                    throw new ResourceAccessDeniedException(
                        "You do not have sufficient permissions to use OS Login: " +
                        e.Error?.Message ?? "access denied",
                        HelpTopics.ManagingOsLogin,
                        e);
                }
            }
        }

        public async Task<string?> SignPublicKeyAsync(
            ZoneLocator zone,
            string publicKey,
            CancellationToken cancellationToken)
        {
            using (ApiTraceSource.Log.TraceMethod().WithParameters(zone))
            {
                try
                {
                    var request = new BetaSignSshPublicKeyRequest(
                        this.service,
                        new BetaSignSshPublicKeyRequestData()
                        {
                            SshPublicKey = publicKey
                        },
                        $"users/{this.authorization.Session.Username}/projects/{zone.ProjectId}/locations/{zone.Name}");

                    var response = await request
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);

                    return response.SignedSshPublicKey;
                }
                catch (GoogleApiException e) when (
                    e.Error != null && 
                    e.Error.Code == 400 && 
                    e.Error.Message != null &&
                    e.Error.Message.Contains("google.posix_username"))
                {
                    throw new ExternalIdpNotConfiguredForOsLoginException(
                        "Your IdP configuration doesn't support the use of OS Login",
                        e);
                }
                catch (GoogleApiException e) when (e.IsAccessDenied())
                {
                    throw new ResourceAccessDeniedException(
                        "You do not have sufficient permissions to use OS Login: " +
                        e.Error?.Message ?? "access denied",
                        HelpTopics.ManagingOsLogin,
                        e);
                }
            }
        }

        public async Task<IList<SecurityKey>> ListSecurityKeysAsync(
            ProjectLocator project,
            CancellationToken cancellationToken)
        {
            using (ApiTraceSource.Log.TraceMethod().WithParameters(project))
            {
                var gaiaSession = this.authorization.Session as IGaiaOidcSession
                    ?? throw new OsLoginNotSupportedForWorkloadIdentityException();

                try
                {
                    var request = new BetaGetLoginProfileRequest(
                        this.service,
                        $"users/{gaiaSession.Username}")
                    {
                        ProjectId = project.Name,
                        View = BetaGetLoginProfileRequest.ViewEnum.SECURITYKEY
                    };

                    var response = await request
                        .ExecuteAsync(cancellationToken)
                        .ConfigureAwait(false);

                    return response.SecurityKeys ?? new List<SecurityKey>();
                }
                catch (GoogleApiException e) when (e.IsAccessDenied())
                {
                    throw new ResourceAccessDeniedException(
                        "You do not have sufficient permissions to use OS Login: " +
                        e.Error?.Message ?? "access denied",
                        HelpTopics.ManagingOsLogin,
                        e);
                }
            }
        }

        //---------------------------------------------------------------------
        // v1beta1 entities. These can be removed once the methods have been
        // promoted to v1.
        //---------------------------------------------------------------------

        #region Request entities

        private class BetaSignSshPublicKeyResponseData : IDirectResponseSchema
        {
            [JsonProperty("signedSshPublicKey")]
            public virtual string? SignedSshPublicKey { get; set; }

            public virtual string? ETag { get; set; }
        }

        private class BetaSignSshPublicKeyRequestData : IDirectResponseSchema
        {
            [JsonProperty("sshPublicKey")]
            public virtual string? SshPublicKey { get; set; }

            public virtual string? ETag { get; set; }
        }

        private class BetaSignSshPublicKeyRequest 
            : CloudOSLoginBaseServiceRequest<BetaSignSshPublicKeyResponseData>
        {
            [RequestParameter("parent")]
            public virtual string Parent { get; private set; }
            private BetaSignSshPublicKeyRequestData Body { get; set; }
            public override string MethodName => "signSshPublicKey";
            public override string HttpMethod => "POST";
            public override string RestPath => "v1beta/{+parent}:signSshPublicKey";

            public BetaSignSshPublicKeyRequest(
                IClientService service, 
                BetaSignSshPublicKeyRequestData body, 
                string parent)
                : base(service)
            {
                this.Parent = parent;
                this.Body = body;
                InitParameters();
            }

            protected override object GetBody()
            {
                return this.Body;
            }

            protected override void InitParameters()
            {
                base.InitParameters();
                this.RequestParameters.Add("parent", new Parameter
                {
                    Name = "parent",
                    IsRequired = true,
                    ParameterType = "path",
                    DefaultValue = null,
                    Pattern = "^users/[^/]+/projects/[^/]+/locations/[^/]+$"
                });
            }
        }

        private class BetaLoginProfile : IDirectResponseSchema
        {
            /// <summary>The registered security key credentials for a user.</summary>
            [JsonProperty("securityKeys")]
            public virtual IList<SecurityKey>? SecurityKeys { get; set; }

            public virtual string? ETag { get; set; }
        }

        public class SecurityKey : IDirectResponseSchema
        {
            [JsonProperty("deviceNickname")]
            public virtual string? DeviceNickname { get; set; }

            [JsonProperty("privateKey")]
            public virtual string? PrivateKey { get; set; }

            [JsonProperty("publicKey")]
            public virtual string? PublicKey { get; set; }

            [JsonProperty("universalTwoFactor")]
            public virtual UniversalTwoFactor? UniversalTwoFactor { get; set; }

            [JsonProperty("webAuthn")]
            public virtual WebAuthn? WebAuthn { get; set; }

            public virtual string? ETag { get; set; }
        }

        public class UniversalTwoFactor : IDirectResponseSchema
        {
            [JsonProperty("appId")]
            public virtual string? AppId { get; set; }

            public virtual string? ETag { get; set; }
        }

        public class WebAuthn : IDirectResponseSchema
        {
            [JsonProperty("rpId")]
            public virtual string? RpId { get; set; }

            public virtual string? ETag { get; set; }
        }

        private class BetaGetLoginProfileRequest : CloudOSLoginBaseServiceRequest<BetaLoginProfile>
        {
            public enum ViewEnum
            {
                [StringValue("SECURITY_KEY")]
                SECURITYKEY
            }

            [RequestParameter("name")]
            public virtual string Name { get; private set; }

            [RequestParameter("projectId")]
            public virtual string? ProjectId { get; set; }

            [RequestParameter("systemId")]
            public virtual string? SystemId { get; set; }

            [RequestParameter("view")]
            public virtual ViewEnum? View { get; set; }

            public override string MethodName => "getLoginProfile";

            public override string HttpMethod => "GET";

            public override string RestPath => "v1beta/{+name}/loginProfile";

            public BetaGetLoginProfileRequest(IClientService service, string name)
                : base(service)
            {
                this.Name = name;
                InitParameters();
            }

            protected override void InitParameters()
            {
                base.InitParameters();
                this.RequestParameters.Add("name", new Parameter
                {
                    Name = "name",
                    IsRequired = true,
                    ParameterType = "path",
                    DefaultValue = null,
                    Pattern = "^users/[^/]+$"
                });
                this.RequestParameters.Add("projectId", new Parameter
                {
                    Name = "projectId",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null
                });
                this.RequestParameters.Add("systemId", new Parameter
                {
                    Name = "systemId",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null
                });
                this.RequestParameters.Add("view", new Parameter
                {
                    Name = "view",
                    IsRequired = false,
                    ParameterType = "query",
                    DefaultValue = null,
                    Pattern = null
                });
            }
        }

        #endregion
    }

    internal class OsLoginNotSupportedForWorkloadIdentityException :
        NotSupportedForWorkloadIdentityException, IExceptionWithHelpTopic
    {
        public OsLoginNotSupportedForWorkloadIdentityException() 
            : base(
                "This project or VM instance uses OS Login, but OS Login is " +
                "currently not supported by workforce identity federation.")
        {
        }
    }

    public class ExternalIdpNotConfiguredForOsLoginException :
        ClientException, IExceptionWithHelpTopic
    {
        public IHelpTopic? Help { get; }

        public ExternalIdpNotConfiguredForOsLoginException(
            string message, 
            Exception inner)
            : base(message, inner)
        {
            this.Help = HelpTopics.UseOsLoginWithWorkforceIdentity;
        }
    }
}
