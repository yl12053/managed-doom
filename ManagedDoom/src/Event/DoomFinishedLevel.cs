namespace ManagedDoom.Event;

public class DoomFinishedLevel: FeatherMod.Events.Event
{
    public string wadName { get; }
    public int episode { get; }
    public int map { get; }

    public DoomFinishedLevel(string wadName, int episode, int map)
    {
        this.wadName = wadName;
        this.episode = episode;
        this.map = map;
    }
}