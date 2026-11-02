using UnityEngine;

[CreateAssetMenu(fileName = "NewResourceType", menuName = "Game Data/Resource Type")]
public class ResourceType : ScriptableObject
{
    public string displayName = "Resource";
    public Color uiColor = Color.white;
    public Sprite icon;
}
