// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Mime;
using Microsoft.AspNetCore.Blazor.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class BlazorHostingServiceCollectionExtensions
    {
        public static IServiceCollection AddBlazorHosting(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            AddBlazorHostingCore(services);
            return services;
        }

        public static IServiceCollection AddBlazorHosting<TClientApp>(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.Configure<BlazorHostingOptions>(options =>
            {
                options.ClientAssemblyPaths.Add(typeof(TClientApp).Assembly.Location);
            });

            AddBlazorHostingCore(services);
            return services;
        }

        public static IServiceCollection AddBlazorHosting(this IServiceCollection services, Action<BlazorHostingOptions> configure)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (configure != null)
            {
                services.Configure(configure);
            }

            AddBlazorHostingCore(services);
            return services;
        }

        private static void AddBlazorHostingCore(IServiceCollection services)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<StaticFileOptions>, BlazorHostingConfigureStaticFileOptions>());
        }

        private class BlazorHostingConfigureStaticFileOptions : IPostConfigureOptions<StaticFileOptions>
        {
            private readonly IOptions<BlazorHostingOptions> _options;
            private readonly IWebHostEnvironment _environment;

            public BlazorHostingConfigureStaticFileOptions(IOptions<BlazorHostingOptions> options, IWebHostEnvironment environment)
            {
                _options = options;
                _environment = environment;
            }

            public void PostConfigure(string name, StaticFileOptions options)
            {
                if (name != Microsoft.Extensions.Options.Options.DefaultName)
                {
                    return;
                }

                if (_options.Value.ClientAssemblyPaths.Count > 0)
                {
                    var fileProviders = new List<IFileProvider>();

                    var enableDebugging = false;

                    foreach (var filePath in _options.Value.ClientAssemblyPaths)
                    {
                        // TODO: Make the .blazor.config file contents sane
                        // Currently the items in it are bizarre and don't relate to their purpose,
                        // hence all the path manipulation here. We shouldn't be hardcoding 'dist' here either.
                        var config = BlazorConfig.Read(filePath);

                        enableDebugging |= config.EnableDebugging;

                        // First, match the request against files in the client app dist directory
                        fileProviders.Add(new PhysicalFileProvider(config.DistPath));

                        // * Before publishing, we serve the wwwroot files directly from source
                        //   (and don't require them to be copied into dist).
                        //   In this case, WebRootPath will be nonempty if that directory exists.
                        // * After publishing, the wwwroot files are already copied to 'dist' and
                        //   will be served by the above middleware, so we do nothing here.
                        //   In this case, WebRootPath will be empty (the publish process sets this).
                        if (!string.IsNullOrEmpty(config.WebRootPath))
                        {
                            fileProviders.Add(new PhysicalFileProvider(config.WebRootPath));
                        }
                    }

                    // Unwrap composites to avoid making a long chain.
                    if (options.FileProvider is CompositeFileProvider composite)
                    {
                        fileProviders.AddRange(composite.FileProviders);
                    }
                    else if (options.FileProvider != null)
                    {
                        fileProviders.Add(options.FileProvider);
                    }
                    else
                    {
                        fileProviders.Add(_environment.WebRootFileProvider);
                    }

                    options.FileProvider = new CompositeFileProvider(fileProviders);

                    // We can't modify an IFileContentTypeProvider, so we have to decorate.
                    var contentTypeProvider = new FileExtensionContentTypeProvider();
                    AddMapping(contentTypeProvider, ".dll", MediaTypeNames.Application.Octet);
                    if (enableDebugging)
                    {
                        AddMapping(contentTypeProvider, ".pdb", MediaTypeNames.Application.Octet);
                    }

                    if (options.ContentTypeProvider == null)
                    {
                        options.ContentTypeProvider = contentTypeProvider;
                    }
                    else
                    {
                        options.ContentTypeProvider = new DelegatingContentTypeProvider(contentTypeProvider, options.ContentTypeProvider);
                    }

                    var original = options.OnPrepareResponse;
                    options.OnPrepareResponse = (context) =>
                    {
                        original(context);
                        CacheHeaderSettings.SetCacheHeaders(context);
                    };
                }

                static void AddMapping(FileExtensionContentTypeProvider provider, string name, string mimeType)
                {
                    if (!provider.Mappings.ContainsKey(name))
                    {
                        provider.Mappings.Add(name, mimeType);
                    }
                }
            }

            private class DelegatingContentTypeProvider : IContentTypeProvider
            {
                private readonly IContentTypeProvider[] _providers;

                public DelegatingContentTypeProvider(params IContentTypeProvider[] providers)
                {
                    _providers = providers;
                }

                public bool TryGetContentType(string subpath, out string contentType)
                {
                    var providers = _providers;
                    for (var i = 0; i < providers.Length; i++)
                    {
                        var provider = providers[i];
                        if (provider.TryGetContentType(subpath, out contentType))
                        {
                            return true;
                        }
                    }

                    contentType = null;
                    return false;
                }
            }
        }
    }
}
