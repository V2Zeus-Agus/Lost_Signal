/// <summary>Algo que se simula a ritmo fijo (máquinas, generadores, taladros).</summary>
public interface ITickable
{
    /// <param name="deltaTime">Siempre TickManager.TickInterval.</param>
    void Tick(float deltaTime);
}
