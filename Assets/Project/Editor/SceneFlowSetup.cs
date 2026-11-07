#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Arma el flujo: MainMenu → (Nueva partida) → Tutorial (SampleScene) → (Continuar) → Mars.
///  - SampleScene queda como tutorial: sin guardado, reparar la nave = victoria con "Continuar".
///  - Mars es una copia del tutorial convertida en el juego real: sin victoria por reparar,
///    sin panel de intro, con los sistemas de la Fase 0 (guardado, planeta, ticks).
///  - El menú principal tiene "Continuar" (si hay guardado) y "Nueva partida".
/// </summary>
public static class SceneFlowSetup
{
    private const string ScenesFolder = "Assets/Scenes";
    private const string MenuScenePath = ScenesFolder + "/MainMenu.unity";
    private const string TutorialScenePath = ScenesFolder + "/SampleScene.unity";
    private const string GameSceneName = "Mars";
    private const string GameScenePath = ScenesFolder + "/" + GameSceneName + ".unity";

    [MenuItem("Tools/Lost Signal/Fase 0/0 - Crear flujo Tutorial → Mars")]
    public static void CreateFlow()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScenePath) == null)
        {
            EditorUtility.DisplayDialog("Flujo de escenas", $"No encontré {TutorialScenePath}.", "OK");
            return;
        }

        bool createGameScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null;
        if (!createGameScene &&
            !EditorUtility.DisplayDialog("Flujo de escenas",
                $"{GameSceneName}.unity ya existe. ¿Querés conservarla (solo se reconfiguran los ajustes) o recrearla desde el tutorial?",
                "Conservar", "Recrear desde el tutorial"))
        {
            AssetDatabase.DeleteAsset(GameScenePath);
            createGameScene = true;
        }

        if (createGameScene && !AssetDatabase.CopyAsset(TutorialScenePath, GameScenePath))
        {
            EditorUtility.DisplayDialog("Flujo de escenas", "No se pudo copiar la escena del tutorial.", "OK");
            return;
        }

        SetupBuildSettings();
        SetupTutorialScene();
        SetupGameScene();
        SetupMainMenu();

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MenuScenePath);

        Debug.Log("[Flujo] Listo: MainMenu → SampleScene (tutorial) → Mars (juego con guardado).");
        EditorUtility.DisplayDialog("Flujo de escenas",
            "Listo:\n• SampleScene = tutorial (sin guardado)\n• Mars = juego real con guardado\n• Menú: Continuar / Nueva partida\n\nDale Play desde MainMenu.",
            "OK");
    }

    // ---------- Escenas ----------

    private static void SetupBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(TutorialScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true)
        };
    }

    private static void SetupTutorialScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath);

        GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        SetBool(gm, "showIntroPanel", true);
        SetBool(gm, "endsOnShipRepaired", true);
        SetString(gm, "nextSceneOnVictory", GameSceneName);

        // El tutorial no guarda: si alguien le corrió la Fase 0, se sacan esos sistemas.
        GameObject core = GameObject.Find("CoreSystems");
        if (core != null) Object.DestroyImmediate(core);

        GameScreens screens = Object.FindFirstObjectByType<GameScreens>(FindObjectsInactive.Include);
        GameObject victory = GetReference<GameObject>(screens, "victoryPanel");
        if (victory != null)
        {
            SetText(victory.transform, "Title", "¡NAVE REPARADA!");
            SetText(victory.transform, "Body", "Despegaste del sitio del choque. Ahora empieza lo difícil: sobrevivir y explotar Marte.");

            Transform continueButton = EnsureButton(victory.transform, "ContinuarButton", "Continuar", screens.OnContinueButton);
            Transform restart = victory.transform.Find("ReiniciarButton");
            if (restart != null) CopyLayout(restart, continueButton);
            Reposition(continueButton, 0);
            Reposition(victory.transform.Find("ReiniciarButton"), 1);
            Reposition(victory.transform.Find("Menú principalButton"), 2);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupGameScene()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath);

        GameManager gm = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        SetBool(gm, "showIntroPanel", false);
        SetBool(gm, "endsOnShipRepaired", false); // en el juego real la nave no "gana": se vuelve el lanzamiento (Fase 5)
        SetString(gm, "nextSceneOnVictory", "");

        if (!Phase0Setup.ConfigureOpenScene())
            Debug.LogError("[Flujo] No se pudo configurar la Fase 0 en Mars (no hay Inventory).");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(MenuScenePath);

        MainMenu menu = Object.FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogError("[Flujo] No encontré MainMenu en la escena del menú.");
            return;
        }

        SetString(menu, "gameScene", GameSceneName);
        SetString(menu, "tutorialScene", "SampleScene");

        GameObject mainPanel = GetReference<GameObject>(menu, "mainPanel");
        if (mainPanel != null)
        {
            Transform continueButton = EnsureButton(mainPanel.transform, "ContinuarButton", "Continuar", menu.OnContinueButton);

            // "Jugar" pasa a ser "Nueva partida".
            Transform play = mainPanel.transform.Find("JugarButton");
            if (play == null) play = mainPanel.transform.Find("Nueva partidaButton");
            if (play != null)
            {
                play.name = "Nueva partidaButton";
                SetButtonLabel(play, "Nueva partida");
                Rewire(play, menu.OnNewGameButton);
            }

            Transform options = mainPanel.transform.Find("OpcionesButton");
            Transform quit = mainPanel.transform.Find("SalirButton");

            // Los botones del menú tienen anclajes personalizados. Se reparten los 4 botones
            // (Continuar, Nueva partida, Opciones, Salir) de forma pareja en el mismo espacio
            // que ocupaban los 3 originales: desde la altura de "Nueva partida" hasta la de "Salir".
            SetOffsetY(play, -60);
            SetOffsetY(options, -150);
            SetOffsetY(quit, -240);

            if (play != null && quit != null)
            {
                float height = ((RectTransform)mainPanel.transform).rect.height;
                RectTransform playRt = (RectTransform)play;
                float top = TopEdge(playRt, height);
                float bottom = TopEdge((RectTransform)quit, height);
                float step = (top - bottom) / 3f;

                Transform[] ordered = { continueButton, play, options, quit };
                for (int i = 0; i < ordered.Length; i++)
                {
                    if (ordered[i] == null) continue;
                    RectTransform rt = (RectTransform)ordered[i];
                    CopyLayout(playRt, rt);
                    float targetTop = top - i * step;
                    rt.anchoredPosition = new Vector2(playRt.anchoredPosition.x, targetTop - rt.anchorMax.y * height);
                    EditorUtility.SetDirty(rt);
                }
            }

            SetReference(menu, "continueButton", continueButton.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ---------- UI helpers (mismo estilo que LostSignalSetup) ----------

    private static Transform EnsureButton(Transform parent, string name, string label, UnityAction action)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            Rewire(existing, action);
            return existing;
        }

        GameObject go = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        go.name = name;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponentInChildren<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 32;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(300, 70);
        UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick, action);
        return go.transform;
    }

    private static void Rewire(Transform buttonTransform, UnityAction action)
    {
        Button button = buttonTransform.GetComponent<Button>();
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
    }

    // Mantiene la X de cada botón y solo los apila en Y.
    private static void Reposition(Transform buttonTransform, int index)
    {
        if (buttonTransform == null) return;
        RectTransform rt = (RectTransform)buttonTransform;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -60 - index * 90);
    }

    // Altura del borde superior del botón medida desde abajo del panel (pivot Y = 1).
    private static float TopEdge(RectTransform rt, float parentHeight) =>
        Mathf.Lerp(rt.anchorMin.y, rt.anchorMax.y, rt.pivot.y) * parentHeight + rt.anchoredPosition.y;

    private static void SetOffsetY(Transform t, float y)
    {
        if (t == null) return;
        RectTransform rt = (RectTransform)t;
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    private static void CopyLayout(Transform from, Transform to)
    {
        RectTransform a = (RectTransform)from;
        RectTransform b = (RectTransform)to;
        b.anchorMin = a.anchorMin;
        b.anchorMax = a.anchorMax;
        b.pivot = a.pivot;
        b.sizeDelta = a.sizeDelta;
        b.anchoredPosition = a.anchoredPosition;
    }

    private static void SetButtonLabel(Transform buttonTransform, string label)
    {
        TMP_Text text = buttonTransform.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
    }

    private static void SetText(Transform parent, string child, string value)
    {
        Transform t = parent.Find(child);
        TMP_Text text = t != null ? t.GetComponent<TMP_Text>() : null;
        if (text != null) text.text = value;
    }

    // ---------- Serialized helpers ----------

    private static T GetReference<T>(Object target, string field) where T : Object
    {
        if (target == null) return null;
        SerializedProperty p = new SerializedObject(target).FindProperty(field);
        return p != null ? p.objectReferenceValue as T : null;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        Modify(target, field, p => p.objectReferenceValue = value);
    }

    private static void SetBool(Object target, string field, bool value) => Modify(target, field, p => p.boolValue = value);

    private static void SetString(Object target, string field, string value) => Modify(target, field, p => p.stringValue = value);

    private static void Modify(Object target, string field, System.Action<SerializedProperty> change)
    {
        if (target == null)
        {
            Debug.LogError($"[Flujo] No se encontró el objeto para '{field}'.");
            return;
        }
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogError($"[Flujo] El campo '{field}' no existe en {target.GetType().Name}.");
            return;
        }
        change(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
