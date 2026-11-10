using UnityEngine;

/// <summary>Generador de O2: presuriza el grupo de hábitats donde está.</summary>
public class OxygenGenerator : MonoBehaviour
{
    private Transform spinner;
    public bool Running { get; set; }

    public void Init(Transform spinningPart) => spinner = spinningPart;

    private void Update()
    {
        if (Running && spinner != null) spinner.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
    }
}
