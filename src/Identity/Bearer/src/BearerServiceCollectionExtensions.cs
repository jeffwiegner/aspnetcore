// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Identity;

/// <summary>
/// Contains extension methods to <see cref="IServiceCollection"/> for configuring identity services.
/// </summary>
public static class BearerServiceCollectionExtensions
{
    /// <summary>
    /// Adds and configures the identity system for the specified User and Token types. Role services are not added
    /// by default but can be added with <see cref="IdentityBuilder.AddRoles{TRole}"/>.
    /// </summary>
    /// <typeparam name="TUser">The type representing a User in the system.</typeparam>
    /// <typeparam name="TToken">The type representing a Token in the system.</typeparam>
    /// <param name="services">The services available in the application.</param>
    /// <param name="setupAction">An action to configure the <see cref="IdentityOptions"/>.</param>
    /// <returns>An <see cref="IdentityBuilder"/> for creating and configuring the identity system.</returns>
    public static IdentityBearerTokenBuilder AddIdentityCore<TUser, TToken>(this IServiceCollection services, Action<IdentityOptions> setupAction)
        where TUser : class
        where TToken : class
        => services.AddIdentityCore<TUser>(setupAction).AddBearerTokens<TToken>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static AuthenticationBuilder AddBearerServerAuthentication(this IServiceCollection services)
    {
        // Our default scheme is cookies
        var authenticationBuilder = services.AddAuthentication(IdentityConstants.BearerCookieScheme);

        // Add the default authentication cookie that will be used between the front end and
        // the backend.
        authenticationBuilder.AddCookie(IdentityConstants.BearerCookieScheme);

        return authenticationBuilder;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IdentityBuilder AddDefaultIdentityBearer<TUser>(this IServiceCollection services)
        where TUser : class
    => services.AddDefaultIdentityBearer<TUser, IdentityToken>(_ => { });

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    /// <typeparam name="TToken"></typeparam>
    /// <param name="services"></param>
    /// <param name="setupAction"></param>
    /// <returns></returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
    public static IdentityBuilder AddDefaultIdentityBearer<TUser, TToken>(this IServiceCollection services,
        Action<IdentityOptions> setupAction)
        where TUser : class
        where TToken : class
    {
        services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddCookie(IdentityConstants.BearerCookieScheme)
            .AddScheme<BearerSchemeOptions, IdentityBearerHandler>(IdentityConstants.BearerScheme, o => { });

        services.AddOptions<IdentityBearerOptions>().Configure<IAuthenticationConfigurationProvider>((o, cp) =>
        {
            // We're reading the authentication configuration for the Bearer scheme
            var bearerSection = cp.GetSchemeConfiguration(IdentityConstants.BearerScheme);

            // An example of what the expected schema looks like
            // "Authentication": {
            //     "Schemes": {
            //       "Identity.Bearer": {
            //         "Audience": "",
            //         "Issuer": "",
            //         "SigningKeys": [ { "Issuer": .., "Payload": base64Key, "Length": 32 } ]
            //       }
            //     }
            //   }

//            var section = bearerSection.GetSection("SigningKeys:0");

            o.Issuer = bearerSection["Issuer"] ?? throw new InvalidOperationException("Issuer is not specified");
            //            var signingKeyBase64 = section["Payload"] ?? throw new InvalidOperationException("Signing key is not specified");

            //var signingKeyBytes = Convert.FromBase64String(signingKeyBase64);

            // An example of what the expected signing keys (JWKs) looks like
            //"SigningCredentials": {
            //  "kty": "oct",
            //  "alg": "HS256",
            //  "kid": "randomguid",
            //  "k": "(G+KbPeShVmYq3t6w9z$C&F)J@McQfTj"
            //}

            // TODO: should this support a JWKS (set of keys?)
            // TODO: This should go into some other key manager API
            var jwkSection = bearerSection.GetRequiredSection("SigningCredentials");
            o.SigningCredentials = jwkSection.Get<JsonWebKey>();

            //o.SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(signingKeyBytes),
            //        SecurityAlgorithms.HmacSha256Signature);

            // TODO: Resolve multiple audiences read vs write??
            o.Audiences = bearerSection.GetSection("Audiences").GetChildren()
                        .Where(s => !string.IsNullOrEmpty(s.Value))
                        .Select(s => s.Value!)
                        .ToList();
        });

        services.Configure<IdentityOptions>(o => o.Stores.SchemaVersion = IdentityVersions.Version2);
        return services.AddIdentityCore<TUser, TToken>(setupAction).IdentityBuilder;
    }
}
