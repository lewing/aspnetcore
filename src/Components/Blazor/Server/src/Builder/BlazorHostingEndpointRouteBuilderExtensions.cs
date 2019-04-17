// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides extension methods for hosting client-side Blazor applications with routing.
    /// </summary>
    public static class BlazorHostingEndpointRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder MapFallbackToClientSideBlazor<TClientApp>(this IEndpointRouteBuilder endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            return MapFallbackToClientSideBlazor(endpoints, typeof(TClientApp).Assembly.Location);
        }

        public static IEndpointConventionBuilder MapFallbackToClientSideBlazor(this IEndpointRouteBuilder endpoints, string clientAssemblyFilePath)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            var config = BlazorConfig.Read(clientAssemblyFilePath);

            // We want to serve "index.html" from whichever directory contains it in this priority order:
            // 1. Client app "dist" directory
            // 2. Client app "wwwroot" directory
            // 3. Server app "wwwroot" directory
            var directory = endpoints.ServiceProvider.GetRequiredService<IWebHostEnvironment>().WebRootPath;
            var indexHtml = config.FindIndexHtmlFile();
            if (indexHtml != null)
            {
                directory = Path.GetDirectoryName(indexHtml);
            }

            var options = new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(directory),
                OnPrepareResponse = CacheHeaderSettings.SetCacheHeaders,
            };

            return endpoints.MapFallbackToFile("index.html", options);
        }
    }
}
