public class FireCommand : ICommand
{
    private readonly Weapon weapon;

    public FireCommand(Weapon weapon)
    {
        this.weapon = weapon;
    }

    public void Execute() => weapon.TryFire();
}
