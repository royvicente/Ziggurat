﻿using System;
using Ziggurat;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsumerService<TMessage, TService>(
        this IServiceCollection services,
        Action<MiddlewareOptions<TMessage>> setupAction)
        where TService : class, IConsumerService<TMessage>
        where TMessage : IMessage
    {
        services
            .AddScoped<TService>()
            .AddScoped<IConsumerService<TMessage>>(t =>
                new PipelineHandler<TMessage>(
                    t,
                    t.GetRequiredService<TService>())
            );


        var options = new MiddlewareOptions<TMessage>();
        setupAction(options);

        foreach (var extension in options.Extensions)
            extension(services);

        return services;
    }
}