namespace ManagedDoom.Event;

public class DoomPickupWeapon: FeatherMod.Events.Event
{
    public WeaponType weapon { get; }
    public bool isDroppedFromEnemy { get; }

    public DoomPickupWeapon(WeaponType weapon, bool isDrop)
    {
        this.weapon = weapon;
        this.isDroppedFromEnemy = isDrop;
    }
}