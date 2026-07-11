namespace ManagedDoom.Event;

public class DoomLoadLevel: FeatherMod.Events.Event
{
    public int episode { get; }
    public int map { get; }
    public string wadName { get; }

    public DoomLoadLevel(int episode, int map, string wadName)
    {
        this.episode = episode;
        this.map = map;
        this.wadName = wadName;
    }
}