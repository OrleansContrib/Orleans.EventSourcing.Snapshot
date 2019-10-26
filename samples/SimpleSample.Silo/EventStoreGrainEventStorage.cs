using EventStore.ClientAPI;
using JsonNet.PrivateSettersContractResolvers;
using Newtonsoft.Json;
using Orleans.EventSourcing.Snapshot;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleSample.Silo
{
    public class EventStoreGrainEventStorage : IGrainEventStorage
    {
        private static readonly JsonSerializerSettings _jsonDefaultSettings = new JsonSerializerSettings
        {
            ContractResolver = new PrivateSetterContractResolver(),
            DefaultValueHandling = DefaultValueHandling.Include,
            TypeNameHandling = TypeNameHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor
        };

        private IEventStoreConnection _eventStoreConnection;
        private Func<string, Type> _stringToType;
        private Func<Type, string> _typeToString;

        public EventStoreGrainEventStorage(IEventStoreConnection eventStoreConnection)
        {
            _eventStoreConnection = eventStoreConnection;
            _stringToType = Type.GetType;
            _typeToString = type => type.AssemblyQualifiedName;
        }

        public EventStoreGrainEventStorage WithTypeResolver(Func<Type, string> typeToString, Func<string, Type> stringToType)
        {
            _stringToType = stringToType;
            _typeToString = typeToString;
            return this;
        }

        public async Task<int> EventsCount(string grainTypeName, GrainReference grainReference)
        {
            var (_, lastEventNumber) = await ReadLastEvent(grainTypeName, grainReference);

            return lastEventNumber + 1;
        }

        public async Task<List<TEvent>> ReadEvents<TEvent>(
            string grainTypeName,
            GrainReference grainReference,
            int start,
            int count)
        {
            if (count <= 0)
            {
                return new List<TEvent>();
            }

            var events = new List<TEvent>();
            int maxReadSize = 4096;

            long nextPageStart;
            int lastIndex = 0;
            int runningCount = 0;
            string streamName = GetStreamName(grainTypeName, grainReference);

            do
            {
                var eventsLeft = count - runningCount;
                var pageSize = eventsLeft < maxReadSize ? eventsLeft : maxReadSize;

                var slice = await _eventStoreConnection
                    .ReadStreamEventsForwardAsync(streamName, start, pageSize, false);

                if (slice.Status == SliceReadStatus.StreamDeleted || slice.Status == SliceReadStatus.StreamNotFound)
                {
                    return new List<TEvent>();
                }

                runningCount += slice.Events.Length;
                nextPageStart = !slice.IsEndOfStream ? slice.NextEventNumber : -1;

                var sliceEvents = slice.Events.Select(x => DeserializeEvent(x)).ToList();
                events.AddRange(sliceEvents.Select(x => x.Event).Cast<TEvent>());
                lastIndex = sliceEvents.Any() ? sliceEvents.Last().EventNumber : lastIndex;
            }
            while (nextPageStart != -1 && runningCount < count);

            return events;
        }

        public Task SaveEvents<TEvent>(
            string grainTypeName,
            GrainReference grainReference,
            IEnumerable<TEvent> events,
            int expectedVersion)
        {
            if (events?.Any() == true)
            {
                var streamName = GetStreamName(grainTypeName, grainReference);
                var eventTypeName = _typeToString(typeof(TEvent));
                var eventData = events.Select(e => SerializeEvent(e));

                return _eventStoreConnection.AppendToStreamAsync(streamName, ExpectedVersion.Any, eventData);
            }

            return Task.CompletedTask;
        }

        private async Task<(object Event, int EventNumber)> ReadLastEvent(string grainTypeName, GrainReference grainReference)
        {
            var streamName = GetStreamName(grainTypeName, grainReference);
            var slice = await _eventStoreConnection
                .ReadStreamEventsBackwardAsync(streamName, StreamPosition.End, 1, true);

            if (slice.Status == SliceReadStatus.StreamDeleted
                || slice.Status == SliceReadStatus.StreamNotFound
                || slice.Events.Length == 0)
            {
                return (null, -1);
            }

            return DeserializeEvent(slice.Events.First());
        }

        private string GetStreamName(string grainTypeName, GrainReference grainReference)
        {
            return $"{grainTypeName}{grainReference.ToShortKeyString()}";
        }

        private EventData SerializeEvent(object @event)
        {
            var eventJson = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(@event, _jsonDefaultSettings));

            var metadata = new EventMetadata() { ClrTypeName = _typeToString(@event.GetType()) };
            var metadataJson = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata, _jsonDefaultSettings));

            return new EventData(Guid.NewGuid(), @event.GetType().Name, true, eventJson, metadataJson);
        }

        private (object Event, int EventNumber) DeserializeEvent(ResolvedEvent @event)
        {
            var metadataJson = Encoding.UTF8.GetString(@event.Event.Metadata);
            var metadata = JsonConvert.DeserializeObject<EventMetadata>(metadataJson);

            var eventJson = Encoding.UTF8.GetString(@event.Event.Data);
            var eventType = _stringToType(metadata.ClrTypeName);

            return (JsonConvert.DeserializeObject(eventJson, eventType, _jsonDefaultSettings), (int)@event.Event.EventNumber);
        }
    }

    public class EventMetadata
    {
        public string ClrTypeName { get; set; }
    }
}
