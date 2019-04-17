// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides options for hosting client-side Blazor applications in ASP.NET Core.
    /// </summary>
    public class BlazorHostingOptions
    {
        /// <summary>
        /// A list of full paths to client-side Blazor client assemblies.
        /// </summary>
        public ISet<string> ClientAssemblyPaths { get; } = new HashSet<string>();
    }
}
