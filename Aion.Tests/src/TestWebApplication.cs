using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Home;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aion.Tests;

// ReSharper disable once ClassNeverInstantiated.Global
public class TestWebApplication : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
    }


}