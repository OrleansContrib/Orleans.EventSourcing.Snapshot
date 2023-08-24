using System.Collections.Generic;
using Orleans;

namespace SimpleSample.GrainInterfaces.State;

[GenerateSerializer]
public class PersonState 
{
    [Id(0)]
    public string NickName { get; private set; } = "anonymous";

    [Id(1)]
    public List<string> HistorySaids { get; private set; } = new List<string>();

    public void Apply(PersonSaidEvent @event) 
    {
        HistorySaids.Add(@event.Said);
    }
        
    public void Apply(PersonNickNameUpdatedEvent @event) 
    {
        NickName = @event.NewNickName;
    }
}