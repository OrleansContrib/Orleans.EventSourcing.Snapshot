using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Orleans.EventSourcing.Snapshot;
using Orleans.Runtime;
using SimpleSample.GrainInterfaces.State;

namespace SimpleSample.Silo;

internal delegate Event Deserialize(BinaryData data, Type type);

public class BlobStorage : IGrainEventStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IMemoryCache _memoryCache;
    private readonly IReadOnlyDictionary<string, Type> _typeMapping;
    private readonly Deserialize _toObjectFromJson;

    public BlobStorage(BlobServiceClient blobServiceClient, IMemoryCache memoryCache)
    {
        _blobServiceClient = blobServiceClient;
        _memoryCache = memoryCache;
        _typeMapping = typeof(Event)
            .Assembly
            .GetExportedTypes()
            .Where(x => !x.IsAbstract && !x.IsInterface && x.IsClass && x != typeof(Event) &&
                        typeof(Event).IsAssignableFrom(x))
            .Select(x => x.GetConstructor(Type.EmptyTypes))
            .Select(x => (Event) x!.Invoke(null))
            .ToDictionary(x => x.TypeKey, x => x.GetType());
        
        var method = typeof(BinaryData)
            .GetMethods()
            .First(x => x.Name == nameof(BinaryData.ToObjectFromJson));

        _toObjectFromJson = (data, type) =>
        {
            var genericMethod = method.MakeGenericMethod(type);
            var result = (Event)genericMethod.Invoke(data, new object[] { null });
            return result;
        };
    }

    public async Task SaveEvents<TEvent>(string grainTypeName, GrainId grainReference, IEnumerable<TEvent> events, int expectedVersion)
    {
        var container = await GetContainer(grainTypeName, grainReference);

        var startNewEvent = await EventsCount(grainTypeName, grainReference);

        var tasks = events
            .Select((e, i) => Upload(startNewEvent + i, container, e))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private static async Task Upload<TEvent>(int i, BlobContainerClient container, TEvent @event)
    {
        var blobClient = container.GetBlobClient($"{i}.json");

        await using var writeStream = await blobClient.OpenWriteAsync(true);
        await JsonSerializer.SerializeAsync(writeStream, @event, @event!.GetType());
    }

    public async Task<List<TEvent>> ReadEvents<TEvent>(string grainTypeName, GrainId grainReference, int start, int count)
    {
        var container = await GetContainer(grainTypeName, grainReference);

        var tasks = Enumerable.Range(start, count)
            .Select(i => Download<TEvent>(i, container))
            .ToArray();
        await Task.WhenAll(tasks);

        return tasks
            .Select(x => x.Result)
            .Where(x => x.Index > -1)
            .OrderBy(x => x.Index)
            .Select(x => x.Event)
            .ToList();
    }

    private async Task<(int Index, TEvent Event)> Download<TEvent>(int i, BlobContainerClient container)
    {
        var blobClient = container.GetBlobClient($"{i}.json");
        var exists = await blobClient.ExistsAsync();

        if (!exists.Value)
        {
            return (-1, default)!;
        }

        var content = await blobClient.DownloadContentAsync();
        var @event = content.Value.Content.ToObjectFromJson<Event>();

        var type = _typeMapping[@event.TypeKey];
        var result = (TEvent) (object) _toObjectFromJson(content.Value.Content, type);

        return (i, result);
    }

    public async Task<int> EventsCount(string grainTypeName, GrainId grainReference)
    {
        var container = await GetContainer(grainTypeName, grainReference);

        return container.GetBlobs().Count();
    }

    private async Task<BlobContainerClient> GetContainer(string grainTypeName, GrainId grainReference)
    {
        var containerName = $"{grainReference.Type.ToString()}/{grainReference.Key.ToString()}"
            .Replace("/", "-")
            .Replace("*", "")
            [..(grainReference.Type.ToString().Length + grainReference.Key.ToString().Length + 1)]
            .ToLowerInvariant();
        var container = _blobServiceClient.GetBlobContainerClient(containerName);

        if (!_memoryCache.TryGetValue(containerName, out _))
        {
            await container.CreateIfNotExistsAsync();
            _memoryCache.Set(containerName, true, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                Size = 1
            });
        }

        return container;
    }
}