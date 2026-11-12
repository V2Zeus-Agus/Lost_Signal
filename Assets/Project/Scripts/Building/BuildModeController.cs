using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Modo construcción (tecla B), estilo Planet Crafter. Máquina de estados (patrón State):
///  Apagado → Menú (elegís qué construir) → Colocando (holograma verde/rojo) / Desarmando.
/// Colocando: clic construye, R gira, clic derecho vuelve al menú, Esc o B sale.
/// Los hábitats se pegan solos al costado de otro y se unen por dentro.
/// </summary>
[DefaultExecutionOrder(-20)] // antes que PlayerInputHandler: el Esc de cancelar no tiene que pausar
public class BuildModeController : MonoBehaviour
{
    [SerializeField] private BaseBuildingManager manager;
    [SerializeField] private Inventory inventory;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Collider ground;
    [SerializeField] private float placeRange = 16f;
    [SerializeField] private float removeRange = 10f;

    private interface IBuildState
    {
        void Enter();
        void Tick();
        void Exit();
    }

    private IBuildState current;
    private OffState off;
    private MenuState menu;
    private PlacingState placing;
    private RemovingState removing;

    private Canvas hintCanvas;
    private TMP_Text hintText;

    public bool IsActive => current != null && current != off;

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<BaseBuildingManager>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        if (playerCamera == null && inventory != null) playerCamera = inventory.GetComponentInChildren<Camera>();
        if (ground == null) ground = GameObject.Find("Ground")?.GetComponent<Collider>();

        off = new OffState(this);
        menu = new MenuState(this);
        placing = new PlacingState(this);
        removing = new RemovingState(this);
        BuildHint();
        SetState(off);
    }

    private void OnDisable()
    {
        UIModal.FireBlocked = false;
    }

    private void Update()
    {
        bool playing = GameManager.Instance != null && GameManager.Instance.IsPlaying;
        if (!playing)
        {
            if (current != off && current != menu) SetState(off);
            return;
        }
        current.Tick();
    }

    /// <summary>Abre el menú de construcción (lo mismo que apretar B). Lo usa la consola de desarrollador.</summary>
    public void OpenMenu()
    {
        if (manager != null && current == off && !UIModal.IsOpen) SetState(menu);
    }

    private void SetState(IBuildState next)
    {
        current?.Exit();
        current = next;
        stateEnteredAt = Time.unscaledTime;
        current.Enter();
    }

    private void Hint(string text)
    {
        hintCanvas.enabled = !string.IsNullOrEmpty(text);
        hintText.text = text;
    }

    private void BuildHint()
    {
        hintCanvas = RuntimeUI.CreateCanvas("BuildHintCanvas", transform, 15);
        UnityEngine.UI.Image bg = RuntimeUI.Image("Hint", hintCanvas.transform, new Color(0f, 0f, 0f, 0.55f), false);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 210f);
        rt.sizeDelta = new Vector2(1100f, 84f);
        hintText = RuntimeUI.Text("Text", bg.transform, "", 21, TextAlignmentOptions.Center);
        RuntimeUI.Stretch(hintText.rectTransform, 8f);
        hintCanvas.enabled = false;
    }

    private static bool Pressed(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
    private static bool LeftClick => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    // El clic que eligió algo en el menú no tiene que construir/desarmar en el mismo instante.
    private const float ClickGraceSeconds = 0.25f;
    private float stateEnteredAt;
    private bool InGrace => Time.unscaledTime - stateEnteredAt < ClickGraceSeconds;
    private static bool RightClick => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

    /// <summary>Esc o B: salir del modo construcción sin pausar el juego.</summary>
    private bool ExitPressed()
    {
        if (Pressed(Key.Escape) || Pressed(Key.B))
        {
            UIModal.ConsumeEscape();
            SetState(off);
            return true;
        }
        return false;
    }

    // ======================================================================
    // Estados
    // ======================================================================

    private class OffState : IBuildState
    {
        private readonly BuildModeController c;
        public OffState(BuildModeController controller) => c = controller;

        public void Enter()
        {
            UIModal.FireBlocked = false;
            c.Hint(null);
        }

        public void Tick()
        {
            if (Pressed(Key.B) && !UIModal.IsOpen && c.manager != null) c.SetState(c.menu);
        }

        public void Exit() { }
    }

    private class MenuState : IBuildState
    {
        private readonly BuildModeController c;
        public MenuState(BuildModeController controller) => c = controller;

        public void Enter()
        {
            c.Hint(null);
            BuildMenuPanel.Show(c.manager, c.inventory,
                definition => { c.placing.Definition = definition; c.SetState(c.placing); },
                () => c.SetState(c.removing),
                () => c.SetState(c.off));
        }

        public void Tick() { }

        public void Exit() => BuildMenuPanel.HideSilently();
    }

    private class PlacingState : IBuildState
    {
        private readonly BuildModeController c;
        private GameObject hologram;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private int rotationSteps;
        private bool lastValid = true;

        public BuildableDefinition Definition;

        public PlacingState(BuildModeController controller) => c = controller;

        public void Enter()
        {
            UIModal.FireBlocked = true;
            rotationSteps = 0;
            hologram = BuildPieceFactory.Create(Definition, c.manager.Catalog, true);
            renderers.Clear();
            renderers.AddRange(hologram.GetComponentsInChildren<Renderer>());
            lastValid = true;
            SetValid(false);
        }

        public void Exit()
        {
            if (hologram != null) Object.Destroy(hologram);
            hologram = null;
            UIModal.FireBlocked = false;
        }

        private void SetValid(bool valid)
        {
            if (valid == lastValid) return;
            lastValid = valid;
            Material m = valid ? c.manager.Catalog.HologramValid : c.manager.Catalog.HologramInvalid;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats; // los modelos importados pueden tener varios materiales
            }
        }

        public void Tick()
        {
            if (UIModal.IsOpen) return; // con la consola o el inventario abiertos no se construye
            if (c.ExitPressed()) return;
            if (RightClick) { c.SetState(c.menu); return; }
            if (Pressed(Key.R)) rotationSteps++;

            bool hasPose = BuildPlacement.TryGetPose(Definition, c.playerCamera.transform, c.ground, c.placeRange, rotationSteps,
                out Vector3 position, out Quaternion rotation, out bool snapped, out string problem);

            hologram.SetActive(hasPose);
            if (hasPose) hologram.transform.SetPositionAndRotation(position, rotation);

            if (problem == null && !c.manager.IsUnlocked(Definition)) problem = "Bloqueado: necesitás una base con aire";
            bool affordable = CostText.CanAfford(c.inventory, Definition.cost);
            bool valid = hasPose && problem == null && affordable;
            SetValid(valid);

            string status = !hasPose ? "<color=#ff8866>Apuntá al suelo</color>"
                          : problem != null ? $"<color=#ff8866>{problem}</color>"
                          : !affordable ? $"<color=#ff8866>Te falta: {CostText.Missing(c.inventory, Definition.cost)}</color>"
                          : snapped && Definition.kind == BuildableKind.Door ? "<color=#7fff9a>Encaja en la pared: abre una entrada</color>"
                          : snapped ? "<color=#7fff9a>Se une al hábitat de al lado</color>"
                          : "<color=#7fff9a>Listo para construir</color>";
            string rotateHint = Definition.kind == BuildableKind.Habitat ? "[R] Girar 90°" : "[R] Girar";
            c.Hint($"<b>{Definition.displayName}</b> · {CostText.Format(Definition.cost)}\n{status}   " +
                   $"<size=18><color=#aab>[Clic] Construir · {rotateHint} · [Clic der.] Menú · [B/Esc] Salir</color></size>");

            if (LeftClick && valid && !c.InGrace)
                new PlacePieceCommand(c.manager, Definition, c.inventory, position, rotation).Execute();
        }
    }

    private class RemovingState : IBuildState
    {
        private readonly BuildModeController c;
        public RemovingState(BuildModeController controller) => c = controller;

        public void Enter() => UIModal.FireBlocked = true;
        public void Exit() => UIModal.FireBlocked = false;

        public void Tick()
        {
            if (UIModal.IsOpen) return;
            if (c.ExitPressed()) return;
            if (RightClick) { c.SetState(c.menu); return; }

            Transform cam = c.playerCamera.transform;
            PlacedPiece piece = null;
            if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, c.removeRange, ~0, QueryTriggerInteraction.Ignore))
                piece = hit.collider.GetComponentInParent<PlacedPiece>();

            string controls = "<size=18><color=#aab>[Clic] Desarmar · [Clic der.] Menú · [B/Esc] Salir</color></size>";
            if (piece == null || piece.Definition == null)
            {
                c.Hint("<b>Desarmar</b> · apuntá a algo que hayas construido\n" + controls);
                return;
            }

            bool can = piece.CanRemove(out string reason);
            string refund = DevCheats.InfiniteMaterials ? "No devuelve nada (mats infinitos)" : $"Devuelve: {CostText.Format(piece.Definition.cost)}";
            string status = can ? $"<color=#7fff9a>{refund}</color>" : $"<color=#ff8866>{reason}</color>";
            c.Hint($"<b>Desarmar {piece.Definition.displayName}</b> · {status}\n{controls}");

            if (LeftClick && can && !c.InGrace) new RemovePieceCommand(c.manager, piece, c.inventory).Execute();
        }
    }
}
