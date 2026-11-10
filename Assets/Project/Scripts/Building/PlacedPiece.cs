using UnityEngine;

/// <summary>Estado extra de una pieza que hay que guardar (p. ej. el contenido de un cofre).</summary>
public interface IPieceState
{
    string CaptureExtra();
    void RestoreExtra(string json, ItemDatabase database);
}

/// <summary>Marca una pieza construida por el jugador.</summary>
public class PlacedPiece : MonoBehaviour
{
    public BuildableDefinition Definition { get; private set; }

    public void Init(BuildableDefinition definition) => Definition = definition;

    /// <summary>Si se puede desarmar ahora; si no, por qué.</summary>
    public virtual bool CanRemove(out string reason)
    {
        reason = null;
        StorageContainer storage = GetComponent<StorageContainer>();
        if (storage != null && storage.Container.TotalCount > 0)
        {
            reason = "Vaciá el cofre antes de desarmarlo";
            return false;
        }
        Habitat habitat = GetComponent<Habitat>();
        if (habitat != null && BaseBuildingManager.Instance != null && BaseBuildingManager.Instance.HasPiecesInside(habitat))
        {
            reason = "Sacá lo que hay adentro del hábitat primero";
            return false;
        }
        return true;
    }
}
