using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.LogConsistency;
using Orleans.Providers;
using Orleans.Runtime;
using System;

namespace Orleans.EventSourcing.Snapshot.Hosting
{
    public static class SnapshotStorageSiloBuilderExtensions
    {
        public static ISiloHostBuilder AddSnapshotStorageBasedLogConsistencyProviderAsDefault(
            this ISiloHostBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions)
        {
            return builder.AddSnapshotStorageBasedLogConsistencyProvider(
                configureSnapshotStorageOptions, ProviderConstants.DEFAULT_STORAGE_PROVIDER_NAME);
        }

        public static ISiloHostBuilder AddSnapshotStorageBasedLogConsistencyProvider(
            this ISiloHostBuilder builder,
            Action<SnapshotStorageOptions, string> configureSnapshotStorageOptions,
            string name = "SnapshotStorage")
        {
            var snapshotStorageOptions = new SnapshotStorageOptions();
            configureSnapshotStorageOptions?.Invoke(snapshotStorageOptions, name);

            return builder.ConfigureServices(services => services
                .AddSnapshotStorageBasedLogConsistencyProvider(snapshotStorageOptions, name));
        }

        public static ISiloBuilder AddLogStorageBasedLogConsistencyProviderAsDefault(
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
