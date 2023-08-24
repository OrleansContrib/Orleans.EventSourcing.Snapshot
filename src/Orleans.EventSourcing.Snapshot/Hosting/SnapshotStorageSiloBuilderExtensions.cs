using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using Orleans.Runtime.LogConsistency;

namespace Orleans.EventSourcing.Snapshot.Hosting
{
    public static class SnapshotStorageSiloBuilderExtensions
    {
        public static ISiloBuilder AddSnapshotStorageBasedLogConsistencyProviderAsDefault(
            this ISiloBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions)
        {
            return builder.AddSnapshotStorageBasedLogConsistencyProvider(
                configureSnapshotStorageOptions, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME);
        }

        public static ISiloBuilder AddSnapshotStorageBasedConsistencyProviderAsDefault(
            this ISiloBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions)
        {
            return builder.AddSnapshotStorageBasedLogConsistencyProvider(
                configureSnapshotStorageOptions, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME);
        }

 
        public static ISiloBuilder AddSnapshotStorageBasedLogConsistencyProvider(
            this ISiloBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions,
            string name = "SnapshotStorage")
        {
            var snapshotStorageOptions = new SnapshotStorageOptions();
            configureSnapshotStorageOptions?.Invoke(snapshotStorageOptions, name);

            return builder.ConfigureServices(services => services
                .AddSnapshotStorageBasedLogConsistencyProvider(snapshotStorageOptions, name));
        }

        internal static IServiceCollection AddSnapshotStorageBasedLogConsistencyProvider(
            this IServiceCollection services,
            SnapshotStorageOptions options,
            string name)
        {
            services.AddSnapshotStorageLogConsistencyOptions(options.UseIndependentEventStorage, name);

            if (options.UseIndependentEventStorage)
            {
                options.ConfigureIndependentEventStorage?.Invoke(services, name);
            }

            services
                .AddSingletonNamedService(name, (sp, n) => LogConsistencyProviderFactory.Create(sp, n, options.SnapshotStrategy))
                .TryAddSingleton(sp => sp.GetServiceByName<ILogViewAdaptorFactory>(ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME));
            
            // https://github.com/dotnet/orleans/issues/8157
            services.TryAddSingleton<Factory<IGrainContext, ILogConsistencyProtocolServices>>(serviceProvider =>
            {
                var factory = ActivatorUtilities.CreateFactory(typeof(ProtocolServices), new[] { typeof(IGrainContext) });
                return arg1 => (ILogConsistencyProtocolServices)factory(serviceProvider, new object[] { arg1 });
            });

            return services;
        }

        internal static IServiceCollection AddSnapshotStorageLogConsistencyOptions(
            this IServiceCollection services,
            bool useIndependentEventStorage,
            string name)
        {
            services.AddOptions<SnapshotStorageLogConsistencyOptions>(name)
               .Configure(options => options.UseIndependentEventStorage = useIndependentEventStorage);

            services.ConfigureNamedOptionForLogging<SnapshotStorageLogConsistencyOptions>(name);

            return services;
        }
    }
}
