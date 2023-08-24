using Orleans;

namespace SimpleSample.GrainInterfaces.State;

public class PersonSaidEvent : Event
{
    [Id(3)]
    public string Said { get; set; }

    public PersonSaidEvent() : base("person.said")
    {
    }
}