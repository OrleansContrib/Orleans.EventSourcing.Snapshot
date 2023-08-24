using System;
using Orleans;

namespace SimpleSample.GrainInterfaces.State;

[GenerateSerializer]
public class Event
{
    public Event(string typeKey)
    {
        Id = Guid.NewGuid();
        TypeKey = typeKey;
        Created = DateTime.UtcNow;
    }

    [Id(0)]
    public DateTime Created { get; set; }
    [Id(1)]
    public string TypeKey { get; set; }
    [Id(2)]
    public Guid Id { get; set; }
};