using System;
using UnityEngine;

/// <summary>
/// Le da a un objeto de la escena un id persistente y junta el estado
/// de todos sus componentes ISaveable.
/// </summary>
[DisallowMultipleComponent]
public class SaveableEntity : MonoBehaviour
{
    [SerializeField] private string id;

    public string Id => id;

    public EntityRecord Capture()
    {
        var record = new EntityRecord { id = id };
        foreach (ISaveable saveable in GetComponents<ISaveable>())
        {
            record.components.Add(new ComponentRecord
            {
                type = saveable.GetType().Name,
                json = saveable.CaptureState()
            });
        }
        return record;
    }

    public void Restore(EntityRecord record, ItemDatabase database)
    {
        foreach (ISaveable saveable in GetComponents<ISaveable>())
        {
            string typeName = saveable.GetType().Name;
            ComponentRecord component = record.components.Find(c => c.type == typeName);
            if (component != null) saveable.RestoreState(component.json, database);
        }
    }

#if UNITY_EDITOR
    public void AssignNewId() => id = Guid.NewGuid().ToString("N");

    private void OnValidate()
    {
        // Solo en objetos de escena: los prefabs en el Project quedan sin id.
        if (string.IsNullOrEmpty(id) && gameObject.scene.IsValid())
        {
            AssignNewId();
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
