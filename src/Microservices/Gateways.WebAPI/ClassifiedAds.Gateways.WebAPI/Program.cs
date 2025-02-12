﻿using ClassifiedAds.Gateways.WebAPI.ConfigurationOptions;
using ClassifiedAds.Gateways.WebAPI.HttpMessageHandlers;
using ClassifiedAds.Infrastructure.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Configuration.File;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.Gateways.Ocelot;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var services = builder.Services;
        var configuration = builder.Configuration;

        builder.WebHost.UseClassifiedAdsLogger(configuration =>
        {
            return new LoggingOptions();
        });

        var appSettings = new AppSettings();
        configuration.Bind(appSettings);

        services.AddOcelot()
            .AddDelegatingHandler<DebuggingHandler>(true);

        services.PostConfigure<FileConfiguration>(fileConfiguration =>
        {
            foreach (var route in appSettings.Ocelot.Routes.Select(x => x.Value))
            {
                var uri = new Uri(route.Downstream);

                foreach (var pathTemplate in route.UpstreamPathTemplates)
                {

                    fileConfiguration.Routes.Add(new FileRoute
                    {
                        UpstreamPathTemplate = pathTemplate,
                        DownstreamPathTemplate = pathTemplate,
                        DownstreamScheme = uri.Scheme,
                        DownstreamHostAndPorts = new List<FileHostAndPort>
                        {
                            new FileHostAndPort{ Host = uri.Host, Port = uri.Port }
                        }
                    });
                }
            }

            foreach (var route in fileConfiguration.Routes)
            {
                if (string.IsNullOrWhiteSpace(route.DownstreamScheme))
                {
                    route.DownstreamScheme = configuration["Ocelot:DefaultDownstreamScheme"];
                }

                if (string.IsNullOrWhiteSpace(route.DownstreamPathTemplate))
                {
                    route.DownstreamPathTemplate = route.UpstreamPathTemplate;
                }
            }
        });

        // Configure the HTTP request pipeline.
        var app = builder.Build();

        app.UseWebSockets();
        await app.UseOcelot();

        app.Run();
    }
}
