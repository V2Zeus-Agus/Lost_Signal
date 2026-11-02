public class InteractCommand : ICommand
{
    private readonly PlayerInteractor interactor;

    public InteractCommand(PlayerInteractor interactor)
    {
        this.interactor = interactor;
    }

    public void Execute() => interactor.TryInteract();
}
