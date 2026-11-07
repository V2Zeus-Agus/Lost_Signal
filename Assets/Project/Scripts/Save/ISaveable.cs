/// <summary>
/// Componente que guarda su propio estado. Necesita un SaveableEntity en el mismo GameObject
/// (el menú Lost Signal > Fase 0 > Reasignar IDs de guardado lo agrega solo).
/// </summary>
public interface ISaveable
{
    string CaptureState();
    void RestoreState(string json, ItemDatabase database);
}
