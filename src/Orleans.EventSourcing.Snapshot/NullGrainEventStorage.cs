using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orleans.EventSourcing.Snapshot
{
    public class NullGrainEventStorage : IGrainEventStorage
    {
        public Task<int> EventsCount(string grainTypeName, GrainReference grainReference)
        {
            throw new NotSupportedException();
        }

        public Task<List<TEvent>> RetrieveEvents<TEvent>(string grainTypeName, GrainReference grainReference, int fromVersion, int toVersion)
        {
            throw new NotSupportedException();
        }

        public Task Save<TEvent>(string grainTypeName, GrainReference grainReference, IEnumerable<TEvent> events)
        {
            throw new NotSupportedException();
        }
    }
}
