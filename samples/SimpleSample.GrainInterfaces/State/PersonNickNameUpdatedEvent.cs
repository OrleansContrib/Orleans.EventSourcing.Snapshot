using Orleans;

namespace SimpleSample.GrainInterfaces.State;

public class PersonNickNameUpdatedEvent : Event
{
    [Id(3)]
    public string NewNickName { get; set; }

    public PersonNickNameUpdatedEvent() : base("person.nickNameUpdated")
    {
    }
}