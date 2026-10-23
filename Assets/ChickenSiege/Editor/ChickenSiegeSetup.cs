using System.Collections.Generic;
using Fusion;
using Starter;
using Starter.Shooter;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CLASE 2 · Crea los assets de la clase con un clic (para el proyecto de referencia del profesor
/// o para quien se quede atrás en la UI). En clase, los alumnos pueden montarlos a mano siguiendo el guion.
///   Tools → Chicken Siege → 1 · Menú: dificultad y lista de salas   (prefab Common/UIGameMenu)
///   Tools → Chicken Siege → 2 · Castillo y barra de vida            (escena 03_Shooter abierta)
///   Tools → Chicken Siege → 3 · Marcador (Tab)                       (escena 03_Shooter abierta)
///   Tools → Chicken Siege → 4 · Fin de partida                       (escena 03_Shooter abierta)
/// Solo monta UI y objetos de escena: el código de red lo escribimos en clase.
/// Los campos que se añaden en clase (UIGameMenu.DifficultyDropdown/SessionList, Castle.ShakeRoot)
/// se asignan por nombre, así este script compila antes de escribirlos.
/// </summary>
public static class ChickenSiegeSetup
{
    private const string Root = "Assets/ChickenSiege";
    private const string MenuPrefabPath = "Assets/Common/UIGameMenu.prefab";
    private const string RowPrefabPath = Root + "/Prefabs/SessionRow.prefab";

    // ─────────────────────────────────────────────── 1 · Menú

    [MenuItem("Tools/Chicken Siege/1 · Menú: dificultad y lista de salas")]
    private static void SetupMenu()
    {
        EnsureFolder(Root + "/Prefabs");

        var root = PrefabUtility.LoadPrefabContents(MenuPrefabPath);
        try
        {
            var menu = root.GetComponent<UIGameMenu>();
            var panel = root.transform.Find("Panel") as RectTransform;
            var room = panel != null ? panel.Find("Room") as RectTransform : null;
            if (menu == null || panel == null || room == null)
            {
                Debug.LogError("[Chicken Siege] No encuentro UIGameMenu/Panel/Room en el prefab.");
                return;
            }

            var menuSerialized = new SerializedObject(menu);
            var dropdownProperty = menuSerialized.FindProperty("DifficultyDropdown");
            var sessionListProperty = menuSerialized.FindProperty("SessionList");
            if (dropdownProperty == null || sessionListProperty == null)
            {
                Debug.LogError("[Chicken Siege] Añadid primero a UIGameMenu los campos:\n" +
                               "public TMP_Dropdown DifficultyDropdown;\npublic UISessionList SessionList;");
                return;
            }

            var roomInput = room.GetComponent<TMP_InputField>();
            var font = roomInput != null && roomInput.textComponent != null ? roomInput.textComponent.font : null;
            float fontSize = roomInput != null && roomInput.textComponent != null ? roomInput.textComponent.fontSize : 24f;
            var roomImage = room.GetComponent<Image>();
            var sprite = roomImage != null ? roomImage.sprite : null;

            // Si ya se ejecutó antes, rehacemos
            DestroyChild(panel, "Difficulty");
            DestroyChild(panel, "SessionList");

            // ── Dropdown de dificultad junto al campo de sala
            const float roomWidth = 334f;
            const float ddWidth = 110f;
            const float gap = 10f;
            float newRoomWidth = roomWidth - ddWidth - gap;
            room.sizeDelta = new Vector2(newRoomWidth, room.sizeDelta.y);
            room.anchoredPosition = new Vector2(-roomWidth / 2f + newRoomWidth / 2f, room.anchoredPosition.y);

            var res = new TMP_DefaultControls.Resources { standard = sprite, background = sprite, inputField = sprite };
            var ddGo = TMP_DefaultControls.CreateDropdown(res);
            ddGo.name = "Difficulty";
            var ddRect = (RectTransform)ddGo.transform;
            ddRect.SetParent(panel, false);
            ddRect.anchorMin = ddRect.anchorMax = new Vector2(0.5f, 0.5f);
            ddRect.sizeDelta = new Vector2(ddWidth, room.sizeDelta.y);
            ddRect.anchoredPosition = new Vector2(roomWidth / 2f - ddWidth / 2f, room.anchoredPosition.y);
            if (roomImage != null)
            {
                var ddImage = ddGo.GetComponent<Image>();
                ddImage.sprite = roomImage.sprite;
                ddImage.type = roomImage.type;
                ddImage.color = roomImage.color;
            }

            var dropdown = ddGo.GetComponent<TMP_Dropdown>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(Difficulty.Names));
            dropdown.value = Difficulty.Default;
            if (dropdown.captionText != null)
            {
                dropdown.captionText.fontSize = fontSize * 0.8f;
                if (font != null) dropdown.captionText.font = font;
            }
            if (dropdown.itemText != null)
            {
                dropdown.itemText.fontSize = fontSize * 0.8f;
                if (font != null) dropdown.itemText.font = font;
            }

            // Etiqueta "Dificultad" encima, copiando la de Room
            var roomLabel = room.Find("Label");
            if (roomLabel != null)
            {
                var label = Object.Instantiate(roomLabel.gameObject, ddRect);
                label.name = "Label";
                var lr = (RectTransform)label.transform;
                lr.sizeDelta = new Vector2(ddWidth, lr.sizeDelta.y);
                lr.anchoredPosition = new Vector2(ddWidth / 2f, lr.anchoredPosition.y);
                var lt = label.GetComponent<TMP_Text>();
                if (lt != null) lt.text = "Dificultad";

                var roomLabelRect = (RectTransform)roomLabel;
                roomLabelRect.sizeDelta = new Vector2(newRoomWidth, roomLabelRect.sizeDelta.y);
                roomLabelRect.anchoredPosition = new Vector2(newRoomWidth / 2f, roomLabelRect.anchoredPosition.y);
            }

            // ── Fila de la lista (prefab)
            var rowPrefab = CreateSessionRowPrefab(font, sprite);

            // ── Lista de salas a la derecha del panel
            var svGo = DefaultControls.CreateScrollView(new DefaultControls.Resources { background = sprite, standard = sprite });
            svGo.name = "SessionList";
            var svRect = (RectTransform)svGo.transform;
            svRect.SetParent(panel, false);
            svRect.anchorMin = svRect.anchorMax = new Vector2(0.5f, 0.5f);
            svRect.pivot = new Vector2(0.5f, 0.5f);
            svRect.sizeDelta = new Vector2(420f, panel.sizeDelta.y - 60f);
            svRect.anchoredPosition = new Vector2(panel.sizeDelta.x / 2f + 30f + 210f, -30f);
            var svImage = svGo.GetComponent<Image>();
            if (svImage != null) svImage.color = new Color(0f, 0f, 0f, 0.6f);

            var scroll = svGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            if (scroll.horizontalScrollbar != null)
            {
                Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
                scroll.horizontalScrollbar = null;
            }

            var content = scroll.content;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var title = NewText(svRect, "Title", "Salas abiertas", font, 26f, TextAlignmentOptions.Left);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = new Vector2(0f, 6f);

            var empty = NewText(svRect, "EmptyLabel", "No hay salas: ¡cread una!", font, 22f, TextAlignmentOptions.Center);
            Stretch(empty.rectTransform, 0f);
            empty.color = new Color(1f, 1f, 1f, 0.6f);

            var list = svGo.AddComponent<UISessionList>();
            list.Content = content;
            list.RowPrefab = rowPrefab;
            list.EmptyLabel = empty.gameObject;

            dropdownProperty.objectReferenceValue = dropdown;
            sessionListProperty.objectReferenceValue = list;
            menuSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, MenuPrefabPath);
            Debug.Log("[Chicken Siege] Menú listo: dropdown de dificultad, lista de salas y prefab " + RowPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static SessionRow CreateSessionRowPrefab(TMP_FontAsset font, Sprite sprite)
    {
        var go = new GameObject("SessionRow", typeof(RectTransform));
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = new Color(1f, 1f, 1f, 0.12f);

        var layout = go.AddComponent<LayoutElement>();
        layout.minHeight = 50f;
        layout.preferredHeight = 50f;

        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset(12, 8, 6, 6);
        h.spacing = 8f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        var nameText = RowText(go.transform, "Name", "Sala", font, 2f);
        var playersText = RowText(go.transform, "Players", "0/10", font, 0.8f);
        var difficultyText = RowText(go.transform, "Difficulty", "Normal", font, 1f);

        var res = new TMP_DefaultControls.Resources { standard = sprite };
        var buttonGo = TMP_DefaultControls.CreateButton(res);
        buttonGo.name = "Join";
        buttonGo.transform.SetParent(go.transform, false);
        var buttonLayout = buttonGo.AddComponent<LayoutElement>();
        buttonLayout.preferredWidth = 96f;
        buttonLayout.minWidth = 96f;
        var buttonText = buttonGo.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = "Unirse";
            buttonText.fontSize = 20f;
            buttonText.color = Color.black;
            if (font != null) buttonText.font = font;
        }

        var row = go.AddComponent<SessionRow>();
        row.NameText = nameText;
        row.PlayersText = playersText;
        row.DifficultyText = difficultyText;
        row.JoinButton = buttonGo.GetComponent<Button>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, RowPrefabPath);
        Object.DestroyImmediate(go);
        return prefab.GetComponent<SessionRow>();
    }

    private static TextMeshProUGUI RowText(Transform parent, string name, string text, TMP_FontAsset font, float flexible)
    {
        var t = NewText(parent, name, text, font, 20f, TextAlignmentOptions.MidlineLeft);
        t.overflowMode = TextOverflowModes.Ellipsis;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = flexible;
        le.minWidth = 40f;
        return t;
    }

    // ─────────────────────────────────────────────── 2 · Castillo

    [MenuItem("Tools/Chicken Siege/2 · Castillo y barra de vida (escena 03_Shooter)")]
    private static void SetupCastle()
    {
        var spawner = Object.FindFirstObjectByType<ChickenSpawner>();
        if (spawner == null)
        {
            Debug.LogError("[Chicken Siege] Abrid primero la escena 03_Shooter.");
            return;
        }

        if (Object.FindFirstObjectByType<Castle>() != null)
        {
            Debug.LogWarning("[Chicken Siege] Ya hay un castillo en la escena. Borradlo si queréis crearlo de nuevo.");
            return;
        }

        EnsureFolder(Root + "/Materials");
        var wallMat = Mat("CastleWall", new Color(0.85f, 0.6f, 0.47f));
        var roofMat = Mat("CastleRoof", new Color(0.25f, 0.72f, 0.79f));
        var ruinMat = Mat("CastleRuins", new Color(0.35f, 0.35f, 0.37f));

        // Los pollos aparecen en un anillo alrededor del origen del mundo y vuelan hacia el spawner:
        // ponemos el castillo en el suelo, debajo del spawner.
        var basePos = new Vector3(spawner.transform.position.x, 0f, spawner.transform.position.z);
        if (Physics.Raycast(new Vector3(basePos.x, 500f, basePos.z), Vector3.down, out var hit, 1000f))
        {
            basePos.y = hit.point.y;
        }

        var castleGo = new GameObject("Castle");
        Undo.RegisterCreatedObjectUndo(castleGo, "Chicken Siege Castle");
        castleGo.transform.position = basePos;

        var collider = castleGo.AddComponent<BoxCollider>();
        collider.size = new Vector3(10f, 15f, 10f);
        collider.center = new Vector3(0f, 7.5f, 0f);

        var model = new GameObject("Model");
        model.transform.SetParent(castleGo.transform, false);
        Part(PrimitiveType.Cube, model.transform, "Keep", new Vector3(0f, 3f, 0f), new Vector3(8f, 6f, 8f), wallMat);
        Part(PrimitiveType.Cube, model.transform, "Top", new Vector3(0f, 7f, 0f), new Vector3(4f, 2f, 4f), roofMat);
        foreach (var x in new[] { -4f, 4f })
        foreach (var z in new[] { -4f, 4f })
        {
            Part(PrimitiveType.Cylinder, model.transform, "Tower", new Vector3(x, 5f, z), new Vector3(2.4f, 5f, 2.4f), wallMat);
            Part(PrimitiveType.Cube, model.transform, "TowerTop", new Vector3(x, 10.4f, z), new Vector3(2.8f, 0.8f, 2.8f), roofMat);
        }

        var ruins = new GameObject("Ruins");
        ruins.transform.SetParent(castleGo.transform, false);
        Part(PrimitiveType.Cube, ruins.transform, "Rubble", new Vector3(0f, 0.75f, 0f), new Vector3(9f, 1.5f, 9f), ruinMat);
        Part(PrimitiveType.Cube, ruins.transform, "Rubble", new Vector3(-3f, 1.8f, 2f), new Vector3(2f, 2f, 2f), ruinMat).transform.localRotation = Quaternion.Euler(10f, 30f, 5f);
        Part(PrimitiveType.Cube, ruins.transform, "Rubble", new Vector3(3f, 1.6f, -2.5f), new Vector3(2.5f, 1.5f, 1.5f), ruinMat).transform.localRotation = Quaternion.Euler(-8f, 70f, 0f);
        ruins.SetActive(false);

        // Objeto de escena: su State Authority es el master client
        var networkObject = castleGo.AddComponent<NetworkObject>();
        networkObject.Flags |= NetworkObjectFlags.MasterClientObject;

        var health = castleGo.AddComponent<Health>();
        health.InitialHealth = 200;
        health.DeathTime = 0f;
        health.VisualRoot = model;
        health.DeathRoot = ruins;

        var castle = castleGo.AddComponent<Castle>();
        castle.Health = health;
        var castleSerialized = new SerializedObject(castle);
        var shakeRoot = castleSerialized.FindProperty("ShakeRoot"); // existe tras el ejercicio 2
        if (shakeRoot != null)
        {
            shakeRoot.objectReferenceValue = model.transform;
            castleSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Spawner: los pollos vuelan hacia el centro del castillo
        Undo.RecordObject(spawner.transform, "Chicken Siege Spawner");
        Undo.RecordObject(spawner, "Chicken Siege Spawner");
        spawner.transform.position = basePos + Vector3.up * 5f;
        spawner.SpawnHeightMin = basePos.y + 3f;
        spawner.SpawnHeightMax = basePos.y + 12f;
        spawner.DirectionDispersion = 5f;
        var spawnerSerialized = new SerializedObject(spawner);
        var targetProperty = spawnerSerialized.FindProperty("Target"); // campo de la solución: apuntar al castillo
        if (targetProperty != null)
        {
            targetProperty.objectReferenceValue = collider;
            spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(spawner);

        // 10 jugadores en la instancia del menú de esta escena
        var menu = Object.FindFirstObjectByType<UIGameMenu>();
        if (menu != null)
        {
            Undo.RecordObject(menu, "Chicken Siege Menu");
            menu.MaxPlayerCount = 10;
            PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
        }

        CreateCastleUI(castle);

        var scene = castleGo.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = castleGo;

        Debug.Log($"[Chicken Siege] Castillo creado en {basePos}. Spawner en {spawner.transform.position}. Escena guardada.");
    }

    private static void CreateCastleUI(Castle castle)
    {
        var uiShooter = Object.FindFirstObjectByType<UIShooter>();
        Canvas canvas = null;
        if (uiShooter != null)
        {
            canvas = uiShooter.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = uiShooter.GetComponentInChildren<Canvas>();
        }
        if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[Chicken Siege] No encuentro un Canvas: la barra del castillo no se creó.");
            return;
        }

        var font = uiShooter != null && uiShooter.ChickenCount != null ? uiShooter.ChickenCount.font : null;

        // Barra arriba en el centro
        var bar = new GameObject("CastleBar", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(bar, "Chicken Siege UI");
        var barRect = (RectTransform)bar.transform;
        barRect.SetParent(canvas.transform, false);
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 1f);
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(520f, 44f);
        barRect.anchoredPosition = new Vector2(0f, -30f);

        var background = NewImage(barRect, "Background", new Color(0.12f, 0.05f, 0.05f, 0.85f));
        Stretch(background.rectTransform, 0f);

        var fill = NewImage(barRect, "Fill", new Color(0.88f, 0.22f, 0.24f, 1f));
        Stretch(fill.rectTransform, 4f);
        fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        var text = NewText(barRect, "Text", "Castillo 200/200", font, 24f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 0f);

        // Cartel de derrota, oculto
        var defeat = new GameObject("DefeatGroup", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(defeat, "Chicken Siege UI");
        var defeatRect = (RectTransform)defeat.transform;
        defeatRect.SetParent(canvas.transform, false);
        Stretch(defeatRect, 0f);
        var dim = defeat.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = false;
        var defeatText = NewText(defeatRect, "Text", "¡El castillo cayó!", font, 80f, TextAlignmentOptions.Center);
        Stretch(defeatText.rectTransform, 0f);
        defeatText.color = new Color(0.95f, 0.3f, 0.3f, 1f);
        defeat.SetActive(false);

        var ui = bar.AddComponent<UICastle>();
        ui.Castle = castle;
        ui.CanvasGroup = bar.GetComponent<CanvasGroup>();
        ui.HealthFill = fill;
        ui.HealthText = text;
        ui.DefeatGroup = defeat;
    }

    // ─────────────────────────────────────────────── 3 · Marcador

    [MenuItem("Tools/Chicken Siege/3 · Marcador (Tab) (escena 03_Shooter)")]
    private static void SetupScoreboard()
    {
        var uiShooter = Object.FindFirstObjectByType<UIShooter>();
        var gameManager = Object.FindFirstObjectByType<GameManager>();
        var spawner = Object.FindFirstObjectByType<ChickenSpawner>();
        if (uiShooter == null || gameManager == null)
        {
            Debug.LogError("[Chicken Siege] Abrid primero la escena 03_Shooter.");
            return;
        }

        if (Object.FindFirstObjectByType<UIScoreboard>() != null)
        {
            Debug.LogWarning("[Chicken Siege] Ya hay un marcador en la escena.");
            return;
        }

        var canvas = uiShooter.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = uiShooter.GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Chicken Siege] No encuentro el Canvas del Shooter.");
            return;
        }

        var font = uiShooter.ChickenCount != null ? uiShooter.ChickenCount.font : null;

        var board = new GameObject("Scoreboard", typeof(RectTransform), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(board, "Chicken Siege Scoreboard");
        var rect = (RectTransform)board.transform;
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(640f, 460f);
        rect.anchoredPosition = Vector2.zero;

        var background = NewImage(rect, "Background", new Color(0.06f, 0.08f, 0.11f, 0.88f));
        Stretch(background.rectTransform, 0f);

        var title = NewText(rect, "Title", "Marcador", font, 36f, TextAlignmentOptions.Center);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 60f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -12f);

        var list = NewText(rect, "List", "Jugador<pos=80%>Pollos", font, 26f, TextAlignmentOptions.TopLeft);
        Stretch(list.rectTransform, 0f);
        list.rectTransform.offsetMin = new Vector2(36f, 24f);
        list.rectTransform.offsetMax = new Vector2(-36f, -84f);
        list.richText = true;

        var scoreboard = board.AddComponent<UIScoreboard>();
        scoreboard.GameManager = gameManager;
        scoreboard.CanvasGroup = board.GetComponent<CanvasGroup>();
        scoreboard.CanvasGroup.alpha = 0f;
        scoreboard.CanvasGroup.blocksRaycasts = false;
        scoreboard.ListText = list;
        scoreboard.MasterReference = spawner != null ? spawner.GetComponent<NetworkObject>() : null;

        var scene = board.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = board;
        Debug.Log("[Chicken Siege] Marcador creado: mantened Tab en partida.");
    }

    // ─────────────────────────────────────────────── 4 · Fin de partida

    [MenuItem("Tools/Chicken Siege/4 · Fin de partida (escena 03_Shooter)")]
    private static void SetupGameOver()
    {
        var uiCastle = Object.FindFirstObjectByType<UICastle>();
        if (uiCastle == null || uiCastle.DefeatGroup == null)
        {
            Debug.LogError("[Chicken Siege] Primero: Tools → Chicken Siege → 2 (castillo y barra de vida).");
            return;
        }

        var scoreboard = Object.FindFirstObjectByType<UIScoreboard>();
        if (scoreboard == null)
            Debug.LogWarning("[Chicken Siege] No hay marcador: ejecutad también Tools → Chicken Siege → 3 para verlo al terminar.");

        var defeat = (RectTransform)uiCastle.DefeatGroup.transform;
        Undo.RegisterFullObjectHierarchyUndo(defeat.gameObject, "Chicken Siege Game Over");
        DestroyChild(defeat, "Countdown");
        DestroyChild(defeat, "BackButton");

        // El título arriba, para dejar sitio al marcador en el centro
        var title = defeat.Find("Text") as RectTransform;
        TMP_FontAsset font = null;
        if (title != null)
        {
            title.anchorMin = new Vector2(0f, 0.8f);
            title.anchorMax = new Vector2(1f, 0.97f);
            title.offsetMin = title.offsetMax = Vector2.zero;
            var titleText = title.GetComponent<TMP_Text>();
            if (titleText != null) font = titleText.font;
        }

        var countdown = NewText(defeat, "Countdown", "Volviendo al menú en 8…", font, 28f, TextAlignmentOptions.Center);
        countdown.rectTransform.anchorMin = new Vector2(0f, 0.03f);
        countdown.rectTransform.anchorMax = new Vector2(1f, 0.09f);
        countdown.rectTransform.offsetMin = countdown.rectTransform.offsetMax = Vector2.zero;
        countdown.color = new Color(1f, 1f, 1f, 0.75f);

        var buttonGo = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources { standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd") });
        buttonGo.name = "BackButton";
        var buttonRect = (RectTransform)buttonGo.transform;
        buttonRect.SetParent(defeat, false);
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.12f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(320f, 64f);
        buttonRect.anchoredPosition = Vector2.zero;
        var buttonText = buttonGo.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = "Volver al menú";
            buttonText.fontSize = 26f;
            buttonText.color = new Color(0.1f, 0.1f, 0.1f);
            if (font != null) buttonText.font = font;
        }

        Undo.RecordObject(uiCastle, "Chicken Siege Game Over");
        uiCastle.Scoreboard = scoreboard;
        uiCastle.Menu = Object.FindFirstObjectByType<UIGameMenu>();
        uiCastle.CountdownText = countdown;
        uiCastle.BackButton = buttonGo.GetComponent<Button>();
        EditorUtility.SetDirty(uiCastle);

        // El marcador por encima del fondo oscuro del cartel
        if (scoreboard != null && scoreboard.transform.parent == defeat.parent)
            scoreboard.transform.SetSiblingIndex(defeat.GetSiblingIndex() + 1);

        var scene = uiCastle.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Chicken Siege] Fin de partida listo: cartel, marcador, cuenta atrás y botón para volver al menú.");
    }

    // ─────────────────────────────────────────────── utilidades

    private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // solo vale el collider de la raíz
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static Material Mat(string name, Color color)
    {
        var path = $"{Root}/Materials/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static TextMeshProUGUI NewText(Transform parent, string name, string text, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = alignment;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void DestroyChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null) Object.DestroyImmediate(child.gameObject);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
