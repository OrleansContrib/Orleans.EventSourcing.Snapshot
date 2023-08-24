using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orleans.EventSourcing.Snapshot
{
    public class NullGrainEventStorage : IGrainEventStorage
    {
        public Task<int> EventsCount(string grainTypeName, GrainId grainReference)
        {
            throw new NotSupportedException();
        }

        public Task<List<TEvent>> ReadEvents<TEvent>(string grainTypeName, GrainId grainReference, int start, int count)
        {
            throw new NotSupportedException();
        }

        public Task SaveEvents<TEvent>(string grainTypeName, GrainId grainReference, IEnumerable<TEvent> events, int expectedVersion)
        {
            throw new NotImplementedException();
        }
    }
}
