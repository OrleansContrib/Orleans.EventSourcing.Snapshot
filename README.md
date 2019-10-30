# Orleans.EventSourcing.Snapshot
Snapshot storage provider for orleans event sourcing

This provider stores grain state snapshots and the event sequence, using a standard storage provider that can be configured independently.

### Usage
Install from nuget:
`dotnet add package Orleans.EventSourcing.Snapshot`

Add this provider to SiloBuilder:

    builder.AddSnapshotStorageBasedLogConsistencyProviderAsDefault((op, name) => 
    {
        // Take snapshot every five events
        op.SnapshotStrategy = strategyInfo => strategyInfo.CurrentConfirmedVersion - strategyInfo.SnapshotVersion >= 5;
        op.UseIndependentEventStorage = true;

        // Should configure independent event storage when set UseIndependentEventStorage true
        op.ConfigureIndependentEventStorage = (services, name) =>
        {
            services.AddSingleton<IGrainEventStorage, SampleIndependentEventStorage>();
        };
    });
        
if you set `UseIndependentEventStorage` false, This provider use grain storage to store the snapshot and event sequence. Otherwise, This provider use grain storage to store the snapshot state, And use independent event storage that you configurated to store the event sequence.

This provider does support `RetrieveConfirmedEvents`. All events are always available, And where the event keep in denpend on your configuration.

### Use case detail you can see the sample project.
