using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

/// <summary>
/// Idempotent, re-runnable build steps for Pawbound. Each phase can be invoked
/// from the "Pawbound" menu in the Editor or headlessly via
/// <c>-executeMethod PawboundBuilder.BuildPhaseA</c>.
///
/// The runtime gameplay code lives in normal MonoBehaviours; this file only
/// generates/derives assets (sprite import settings, animation clips, animator
/// controllers) and wires up the scene, so that work is reproducible.
/// </summary>
public static class PawboundBuilder
{
    // ---- constants -------------------------------------------------------------
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string KnightAnimDir = "Assets/Animations/Knight";
    const string KnightControllerPath = KnightAnimDir + "/Knight.controller";

    const string EnemyAnimDir = "Assets/Animations/Enemy";
    const string SkeletonControllerPath = EnemyAnimDir + "/Skeleton.controller";
    const string HazardAnimDir = "Assets/Animations/Hazard";
    const string FireballControllerPath = HazardAnimDir + "/Fireball.controller";
    const string PrefabDir = "Assets/Prefabs";
    const string SkeletonPrefabPath = PrefabDir + "/Enemy_Skeleton.prefab";
    const string FireballPrefabPath = PrefabDir + "/Fireball.prefab";

    const float GroundSurfaceY = -4.0f;   // world Y of the top of the ground collider
    const float KnightScale = 4.5f;       // ~26x32 px @ PPU 100 -> ~1.2 x 1.45 world units
    const float KnightStartX = -6.0f;
    const float SkeletonScale = 3.6f;     // ~36x47 px @ PPU 100 -> ~1.3 x 1.7 world units
    const float FireballScale = 3.0f;     // ~34x19 px @ PPU 100 -> ~1.0 x 0.6 world units

    static readonly string[] FireballFrames =
    {
        "Assets/FB001.png",
        "Assets/FB002.png",
        "Assets/FB003.png",
        "Assets/FB004.png",
        "Assets/FB005.png",
    };

    static readonly string[] KnightSheets =
    {
        "Assets/noBKG_KnightIdle_strip.png",
        "Assets/noBKG_KnightRun_strip.png",
        "Assets/noBKG_KnightJumpAndFall_strip.png",
        "Assets/noBKG_KnightAttack_strip.png",
        "Assets/noBKG_KnightDeath_strip.png",
        "Assets/noBKG_KnightRoll_strip.png",
        "Assets/noBKG_KnightShield_strip.png",
    };

    static readonly string[] SkeletonSheets =
    {
        "Assets/Skeleton_01_White_Idle.png",
        "Assets/Skeleton_01_White_Walk.png",
        "Assets/Skeleton_01_White_Attack1.png",
        "Assets/Skeleton_01_White_Attack2.png",
        "Assets/Skeleton_01_White_Hurt.png",
        "Assets/Skeleton_01_White_Die.png",
    };

    // =========================================================================
    //  PHASE A  -  Knight player, animator, movement, ground, parallax, camera
    // =========================================================================
    [MenuItem("Pawbound/Build Phase A  (Knight + Movement + Ground + Parallax)")]
    public static void BuildPhaseA()
    {
        try
        {
            EnsureTagsAndLayers();
            FixKnightSpriteImport();
            BuildKnightAnimator();
            ConfigurePhaseAScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Phase A build COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Phase A build FAILED: " + e);
            throw;
        }
    }

    // =========================================================================
    //  PHASE B  -  Skeleton enemy: animator, prefab, spawner
    // =========================================================================
    [MenuItem("Pawbound/Build Phase B  (Skeleton Enemy + Spawning)")]
    public static void BuildPhaseB()
    {
        try
        {
            EnsureTagsAndLayers();
            FixSkeletonSpriteImport();
            BuildSkeletonAnimator();
            BuildSkeletonPrefab();
            ConfigurePhaseBScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Phase B build COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Phase B build FAILED: " + e);
            throw;
        }
    }

    static void FixSkeletonSpriteImport()
    {
        foreach (var path in SkeletonSheets)
            SetSpriteSheetPivot(path, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0f));
        Debug.Log("[Pawbound] Skeleton sprite pivots set to bottom-centre.");
    }

    static void BuildSkeletonAnimator()
    {
        EnsureFolder(EnemyAnimDir);

        var idle = LoadFrames("Assets/Skeleton_01_White_Idle.png");
        var walk = LoadFrames("Assets/Skeleton_01_White_Walk.png");
        var attack = LoadFrames("Assets/Skeleton_01_White_Attack1.png");
        var hurt = LoadFrames("Assets/Skeleton_01_White_Hurt.png");
        var die = LoadFrames("Assets/Skeleton_01_White_Die.png");

        var idleClip = MakeSpriteClip(EnemyAnimDir + "/Skeleton_Idle.anim", idle, 10f, true);
        var walkClip = MakeSpriteClip(EnemyAnimDir + "/Skeleton_Walk.anim", walk, 12f, true);
        var attackClip = MakeSpriteClip(EnemyAnimDir + "/Skeleton_Attack.anim", attack, 14f, false);
        var hurtClip = MakeSpriteClip(EnemyAnimDir + "/Skeleton_Hurt.anim", hurt, 12f, false);
        var deathClip = MakeSpriteClip(EnemyAnimDir + "/Skeleton_Death.anim", Slice(die, 0, 12), 10f, false);

        if (File.Exists(SkeletonControllerPath)) AssetDatabase.DeleteAsset(SkeletonControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(SkeletonControllerPath);

        ac.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;
        var sWalk = sm.AddState("Walk"); sWalk.motion = walkClip;
        var sIdle = sm.AddState("Idle"); sIdle.motion = idleClip;
        var sAttack = sm.AddState("Attack"); sAttack.motion = attackClip;
        var sHurt = sm.AddState("Hurt"); sHurt.motion = hurtClip;
        var sDeath = sm.AddState("Death"); sDeath.motion = deathClip;
        sm.defaultState = sWalk;

        AddTransition(sWalk, sIdle, c => c.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving"));
        AddTransition(sIdle, sWalk, c => c.AddCondition(AnimatorConditionMode.If, 0f, "Moving"));

        var anyAttack = sm.AddAnyStateTransition(sAttack);
        anyAttack.canTransitionToSelf = false;
        anyAttack.hasExitTime = false;
        anyAttack.duration = 0.05f;
        anyAttack.hasFixedDuration = true;
        anyAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

        var attackDone = sAttack.AddTransition(sWalk);
        attackDone.hasExitTime = true; attackDone.exitTime = 0.95f;
        attackDone.duration = 0.1f; attackDone.hasFixedDuration = true;

        var anyHurt = sm.AddAnyStateTransition(sHurt);
        anyHurt.canTransitionToSelf = false;
        anyHurt.hasExitTime = false;
        anyHurt.duration = 0.05f;
        anyHurt.hasFixedDuration = true;
        anyHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");

        var hurtDone = sHurt.AddTransition(sWalk);
        hurtDone.hasExitTime = true; hurtDone.exitTime = 0.9f;
        hurtDone.duration = 0.1f; hurtDone.hasFixedDuration = true;

        var anyDeath = sm.AddAnyStateTransition(sDeath);
        anyDeath.canTransitionToSelf = false;
        anyDeath.hasExitTime = false;
        anyDeath.duration = 0.05f;
        anyDeath.hasFixedDuration = true;
        anyDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pawbound] Skeleton.controller built (Walk/Idle/Attack/Hurt/Death).");
    }

    static void BuildSkeletonPrefab()
    {
        EnsureFolder(PrefabDir);

        var idleFrames = LoadFrames("Assets/Skeleton_01_White_Idle.png");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SkeletonControllerPath);
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        var go = new GameObject("Enemy_Skeleton");
        go.transform.localScale = new Vector3(SkeletonScale, SkeletonScale, 1f);
        if (enemyLayer >= 0) go.layer = enemyLayer;
        TrySetTag(go, "Enemy");

        var sr = go.AddComponent<SpriteRenderer>();
        if (idleFrames.Length > 0) sr.sprite = idleFrames[0];
        sr.sortingLayerName = "player";
        sr.sortingOrder = 0;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        rb.gravityScale = 0f;

        float s = SkeletonScale;
        var col = go.AddComponent<CapsuleCollider2D>();
        col.isTrigger = true;
        col.direction = CapsuleDirection2D.Vertical;
        col.size = new Vector2(0.55f / s, 1.0f / s);   // ~0.55 x 1.0 world units
        col.offset = new Vector2(0f, 0.55f / s);        // sits from feet up ~1.0 unit

        var se = go.AddComponent<SkeletonEnemy>();
        var seSO = new SerializedObject(se);
        seSO.FindProperty("animator").objectReferenceValue = animator;
        seSO.FindProperty("bodyCollider").objectReferenceValue = col;
        seSO.ApplyModifiedPropertiesWithoutUndo();

        if (File.Exists(SkeletonPrefabPath)) AssetDatabase.DeleteAsset(SkeletonPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, SkeletonPrefabPath);
        UnityEngine.Object.DestroyImmediate(go);

        Debug.Log("[Pawbound] Enemy_Skeleton.prefab built.");
    }

    static void ConfigurePhaseBScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = GameObject.Find("Player");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPrefabPath);

        var spawnerGO = GameObject.Find("SkeletonSpawner");
        if (spawnerGO == null) spawnerGO = new GameObject("SkeletonSpawner");
        spawnerGO.transform.position = Vector3.zero;

        var spawner = GetOrAdd<SkeletonSpawner>(spawnerGO);
        var so = new SerializedObject(spawner);
        so.FindProperty("skeletonPrefab").objectReferenceValue = prefab;
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("groundY").floatValue = GroundSurfaceY;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Scene configured for Phase B and saved.");
    }

    // =========================================================================
    //  PHASE C  -  Fireball sky hazard: animator, prefab, spawner
    // =========================================================================
    [MenuItem("Pawbound/Build Phase C  (Fireball Sky Hazard + Spawning)")]
    public static void BuildPhaseC()
    {
        try
        {
            EnsureTagsAndLayers();
            FixFireballSpriteImport();
            BuildFireballAnimator();
            BuildFireballPrefab();
            ConfigurePhaseCScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Phase C build COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Phase C build FAILED: " + e);
            throw;
        }
    }

    static void FixFireballSpriteImport()
    {
        foreach (var path in FireballFrames)
            SetSpriteSheetPivot(path, SpriteAlignment.Center, new Vector2(0.5f, 0.5f));
        Debug.Log("[Pawbound] Fireball sprite pivots set to centre.");
    }

    static void BuildFireballAnimator()
    {
        EnsureFolder(HazardAnimDir);

        var frames = LoadFramesMulti(FireballFrames);
        var clip = MakeSpriteClip(HazardAnimDir + "/Fireball.anim", frames, 14f, true);

        if (File.Exists(FireballControllerPath)) AssetDatabase.DeleteAsset(FireballControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(FireballControllerPath);

        var sm = ac.layers[0].stateMachine;
        var fly = sm.AddState("Fly");
        fly.motion = clip;
        sm.defaultState = fly;

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pawbound] Fireball.controller built (single looping Fly state).");
    }

    static void BuildFireballPrefab()
    {
        EnsureFolder(PrefabDir);

        var frames = LoadFramesMulti(FireballFrames);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(FireballControllerPath);
        int hazardLayer = LayerMask.NameToLayer("Hazard");

        var go = new GameObject("Fireball");
        go.transform.localScale = new Vector3(FireballScale, FireballScale, 1f);
        if (hazardLayer >= 0) go.layer = hazardLayer;
        TrySetTag(go, "Hazard");

        var sr = go.AddComponent<SpriteRenderer>();
        if (frames.Length > 0) sr.sprite = frames[0];
        sr.sortingLayerName = "player";
        sr.sortingOrder = 2;

        var animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
        rb.gravityScale = 0f;

        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.30f / FireballScale;   // ~0.30 world units

        go.AddComponent<Fireball>();

        if (File.Exists(FireballPrefabPath)) AssetDatabase.DeleteAsset(FireballPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(go, FireballPrefabPath);
        UnityEngine.Object.DestroyImmediate(go);

        Debug.Log("[Pawbound] Fireball.prefab built.");
    }

    static void ConfigurePhaseCScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = GameObject.Find("Player");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireballPrefabPath);

        var spawnerGO = GameObject.Find("FireballSpawner");
        if (spawnerGO == null) spawnerGO = new GameObject("FireballSpawner");
        spawnerGO.transform.position = Vector3.zero;

        var spawner = GetOrAdd<FireballSpawner>(spawnerGO);
        var so = new SerializedObject(spawner);
        so.FindProperty("fireballPrefab").objectReferenceValue = prefab;
        so.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
        so.FindProperty("groundY").floatValue = GroundSurfaceY;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Scene configured for Phase C and saved.");
    }

    // =========================================================================
    //  PHASE D  -  Parallax fix + Main Menu + Game flow + Score + Game Over
    // =========================================================================
    const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Pawbound/Build Phase D  (Menu + Game Flow + Score + Parallax Fix)")]
    public static void BuildPhaseD()
    {
        try
        {
            EnsureTagsAndLayers();
            BuildMainMenuScene();
            ConfigureGameplaySceneForFlow();
            SetBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Phase D build COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Phase D build FAILED: " + e);
            throw;
        }
    }

    // ---- build settings --------------------------------------------------
    static void SetBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true),
        };
        Debug.Log("[Pawbound] Build settings: [MainMenu, SampleScene].");
    }

    // ---- UI helpers ----------------------------------------------------
    static Font UIFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static GameObject MakeCanvas(string name)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return go;
    }

    static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static Text UIText(string name, Transform parent, string content, int size,
                       TextAnchor anchor, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = UIFont();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static (GameObject go, Button button, Text label) UIButton(
        string name, Transform parent, string content, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.10f, 0.10f, 0.14f, 0.92f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.6f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.4f, 1f);
        btn.colors = colors;

        var label = UIText(name + "Label", go.transform, content, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Stretch((RectTransform)label.transform);

        return (go, btn, label);
    }

    static Sprite FirstSprite(string path)
        => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    // ---- Main Menu scene ----------------------------------------------
    static void BuildMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.09f, 1f);
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        camGO.AddComponent<UniversalAdditionalCameraData>();

        EnsureEventSystem();

        var canvasGO = MakeCanvas("MenuCanvas");

        // Background image (composite scene art) behind the menu.
        var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bg.transform.SetParent(canvasGO.transform, false);
        Stretch((RectTransform)bg.transform);
        var bgImg = bg.GetComponent<Image>();
        var bgSprite = FirstSprite("Assets/orig.png") ?? FirstSprite("Assets/1.png");
        if (bgSprite != null) { bgImg.sprite = bgSprite; bgImg.type = Image.Type.Simple; bgImg.preserveAspect = false; }
        bgImg.color = bgSprite != null ? new Color(0.7f, 0.7f, 0.8f, 1f) : new Color(0.08f, 0.09f, 0.14f, 1f);
        bgImg.raycastTarget = false;

        // Dark scrim for text legibility.
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scrim.transform.SetParent(canvasGO.transform, false);
        Stretch((RectTransform)scrim.transform);
        scrim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        scrim.GetComponent<Image>().raycastTarget = false;

        var title = UIText("Title", canvasGO.transform, "PAWBOUND", 130,
                           TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.45f), FontStyle.Bold);
        var titleRT = (RectTransform)title.transform;
        titleRT.anchorMin = titleRT.anchorMax = new Vector2(0.5f, 0.72f);
        titleRT.sizeDelta = new Vector2(1400f, 220f);

        var subtitle = UIText("Subtitle", canvasGO.transform, "2D ENDLESS RUNNER", 44,
                              TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.95f), FontStyle.Bold);
        var subRT = (RectTransform)subtitle.transform;
        subRT.anchorMin = subRT.anchorMax = new Vector2(0.5f, 0.60f);
        subRT.sizeDelta = new Vector2(1200f, 90f);

        var (startGO, startBtn, _) = UIButton("StartButton", canvasGO.transform, "START GAME", new Vector2(440f, 96f), 40);
        var startRT = (RectTransform)startGO.transform;
        startRT.anchorMin = startRT.anchorMax = new Vector2(0.5f, 0.36f);
        startRT.anchoredPosition = Vector2.zero;

        var menuControllerGO = new GameObject("MainMenu");
        var menuController = menuControllerGO.AddComponent<MainMenuController>();
        var mcSO = new SerializedObject(menuController);
        mcSO.FindProperty("gameplaySceneName").stringValue = "SampleScene";
        mcSO.ApplyModifiedPropertiesWithoutUndo();

        UnityEventTools.AddPersistentListener(startBtn.onClick, new UnityAction(menuController.StartGame));

        EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        Debug.Log("[Pawbound] MainMenu.unity built.");
    }

    // ---- Gameplay scene: parallax fix + managers + HUD + Game Over ------
    static void ConfigureGameplaySceneForFlow()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var mainCam = GameObject.Find("Main Camera");
        var knight = UnityEngine.Object.FindFirstObjectByType<KnightController>();
        var skeletonSpawner = UnityEngine.Object.FindFirstObjectByType<SkeletonSpawner>();
        var fireballSpawner = UnityEngine.Object.FindFirstObjectByType<FireballSpawner>();

        // --- parallax: retarget to the rendered camera, set depth factors, un-squash ---
        float[] factors = { 1.0f, 0.8f, 0.55f, 0.2f }; // 1_0 sky .. 4_0 near
        foreach (var bc in UnityEngine.Object.FindObjectsByType<BackgroundController>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(bc);
            so.FindProperty("cam").objectReferenceValue = mainCam != null ? mainCam.transform : null;
            so.FindProperty("infiniteHorizontal").boolValue = true;

            string n = bc.gameObject.name;
            if (n.StartsWith("1_0")) so.FindProperty("parallaxEffect").floatValue = factors[0];
            else if (n.StartsWith("2_0")) so.FindProperty("parallaxEffect").floatValue = factors[1];
            else if (n.StartsWith("3_0")) so.FindProperty("parallaxEffect").floatValue = factors[2];
            else if (n.StartsWith("4_0")) so.FindProperty("parallaxEffect").floatValue = factors[3];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Un-squash: use the (intended) X scale on both axes so layers fill the frame.
            var t = bc.transform;
            float s = Mathf.Abs(t.localScale.x) > 0.01f ? t.localScale.x : 1f;
            t.localScale = new Vector3(s, s, 1f);
            EditorUtility.SetDirty(bc);
        }

        // --- managers ---
        var gmGO = GameObject.Find("GameManager") ?? new GameObject("GameManager");
        var scoreManager = GetOrAdd<ScoreManager>(gmGO);
        var gameManager = GetOrAdd<GameManager>(gmGO);

        // --- UI ---
        EnsureEventSystem();
        var canvasGO = GameObject.Find("GameplayCanvas") ?? MakeCanvas("GameplayCanvas");

        // HUD
        var hudGO = FindChildByName(canvasGO.transform, "HUD");
        if (hudGO == null)
        {
            hudGO = new GameObject("HUD", typeof(RectTransform));
            hudGO.transform.SetParent(canvasGO.transform, false);
            Stretch((RectTransform)hudGO.transform);
        }
        var scoreLabel = FindComponentInChildren<Text>(hudGO.transform, "ScoreLabel");
        if (scoreLabel == null)
        {
            scoreLabel = UIText("ScoreLabel", hudGO.transform, "SCORE  00000", 40,
                                TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            var rt = (RectTransform)scoreLabel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(40f, -28f);
            rt.sizeDelta = new Vector2(600f, 70f);
        }
        var hud = GetOrAdd<HUDController>(hudGO);
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        hudSO.FindProperty("scoreText").objectReferenceValue = scoreLabel;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // Game Over panel
        var goPanel = FindChildByName(canvasGO.transform, "GameOverPanel");
        if (goPanel == null)
        {
            goPanel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            goPanel.transform.SetParent(canvasGO.transform, false);
            Stretch((RectTransform)goPanel.transform);
            goPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var over = UIText("OverTitle", goPanel.transform, "GAME OVER", 110,
                              TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.4f), FontStyle.Bold);
            var overRT = (RectTransform)over.transform;
            overRT.anchorMin = overRT.anchorMax = new Vector2(0.5f, 0.66f);
            overRT.sizeDelta = new Vector2(1200f, 200f);

            var finalScore = UIText("FinalScore", goPanel.transform, "SCORE  00000", 52,
                                    TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var fsRT = (RectTransform)finalScore.transform;
            fsRT.anchorMin = fsRT.anchorMax = new Vector2(0.5f, 0.52f);
            fsRT.sizeDelta = new Vector2(900f, 90f);

            var (restartGO, restartBtn, _) = UIButton("RestartButton", goPanel.transform, "RESTART", new Vector2(420f, 90f), 38);
            var rRT = (RectTransform)restartGO.transform;
            rRT.anchorMin = rRT.anchorMax = new Vector2(0.5f, 0.36f);
            rRT.anchoredPosition = Vector2.zero;

            var (menuGO, menuBtn, _) = UIButton("MenuButton", goPanel.transform, "MAIN MENU", new Vector2(420f, 90f), 38);
            var mRT = (RectTransform)menuGO.transform;
            mRT.anchorMin = mRT.anchorMax = new Vector2(0.5f, 0.24f);
            mRT.anchoredPosition = Vector2.zero;

            UnityEventTools.AddPersistentListener(restartBtn.onClick, new UnityAction(gameManager.Restart));
            UnityEventTools.AddPersistentListener(menuBtn.onClick, new UnityAction(gameManager.ReturnToMainMenu));

            var goScoreText = finalScore;
            var panel = GetOrAdd<GameOverPanel>(goPanel);
            var pSO = new SerializedObject(panel);
            pSO.FindProperty("scoreText").objectReferenceValue = goScoreText;
            pSO.ApplyModifiedPropertiesWithoutUndo();
        }
        var gameOverPanel = goPanel.GetComponent<GameOverPanel>();
        goPanel.SetActive(false);

        // --- wire GameManager ---
        var gmSO = new SerializedObject(gameManager);
        gmSO.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
        gmSO.FindProperty("gameplaySceneName").stringValue = "SampleScene";
        gmSO.FindProperty("knight").objectReferenceValue = knight;
        gmSO.FindProperty("skeletonSpawner").objectReferenceValue = skeletonSpawner;
        gmSO.FindProperty("fireballSpawner").objectReferenceValue = fireballSpawner;
        gmSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        gmSO.FindProperty("hudRoot").objectReferenceValue = hudGO;
        gmSO.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        gmSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Gameplay scene: parallax retargeted, managers + HUD + Game Over wired.");
    }

    static GameObject FindChildByName(Transform parent, string name)
    {
        var t = parent.Find(name);
        return t != null ? t.gameObject : null;
    }

    static T FindComponentInChildren<T>(Transform parent, string name) where T : Component
    {
        foreach (var c in parent.GetComponentsInChildren<T>(true))
            if (c.gameObject.name == name) return c;
        return null;
    }

    // =========================================================================
    //  PHASE E (visual)  -  Parallax cohesion pass (art-informed depth)
    // =========================================================================
    //
    // Visual depth read from the actual sprite content:
    //   1.png  = flat blue sky gradient .......... FARTHEST
    //   2.png  = big white clouds (transp. edges) . DISTANT
    //   3.png  = distant treeline + green meadow .. MIDDLE (meadow sits behind 4)
    //   4.png  = foreground trees + detailed grass  NEAR / gameplay ground
    //
    // Factors use the existing camera-delta model (parallaxEffect: 1 = static,
    // 0 = world-locked). Two cohesive planes: a calm sky pair (0.90 / 0.78) and a
    // near-world-locked ground pair (0.35 / 0.06) so nothing reads as an isolated
    // sliding image. 4_0 is moved behind the player sorting layer so its trees
    // never cover the Knight / Skeletons / Fireballs.
    //
    [MenuItem("Pawbound/Polish Background  (Parallax Cohesion Pass)")]
    public static void PolishBackground()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var mainCam = GameObject.Find("Main Camera");

            // name -> (parallaxEffect, uniform scale, world Y, sorting layer, sorting order)
            var cfg = new Dictionary<string, (float pe, float scale, float y, string sort, int order)>
            {
                { "1_0", (0.90f, 1.10f,  0.0f, "1SKY",        0)  },
                { "2_0", (0.78f, 1.21f,  1.0f, "2clouds",     0)  },
                { "3_0", (0.35f, 1.21f, -2.1f, "3Background", 0)  },
                { "4_0", (0.06f, 1.21f, -2.4f, "3Background", 10) },
            };

            foreach (var bc in UnityEngine.Object.FindObjectsByType<BackgroundController>(FindObjectsSortMode.None))
            {
                if (!cfg.TryGetValue(bc.gameObject.name, out var c)) continue;

                var so = new SerializedObject(bc);
                so.FindProperty("parallaxEffect").floatValue = c.pe;
                so.FindProperty("infiniteHorizontal").boolValue = true;
                if (mainCam != null) so.FindProperty("cam").objectReferenceValue = mainCam.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                var t = bc.transform;
                t.localScale = new Vector3(c.scale, c.scale, 1f);   // uniform -> pixel proportions preserved
                var p = t.position; p.y = c.y; t.position = p;

                foreach (var sr in bc.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sortingLayerName = c.sort;
                    sr.sortingOrder = c.order;
                }
                EditorUtility.SetDirty(bc);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Pawbound] Background cohesion pass applied and scene saved.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Background polish FAILED: " + e);
            throw;
        }
    }

    // =========================================================================
    //  SCORE UPGRADE  -  persistent HI-SCORE in HUD, Game Over, Main Menu
    // =========================================================================
    [MenuItem("Pawbound/Upgrade Score UI  (Persistent HI-SCORE)")]
    public static void BuildScoreUpgrade()
    {
        try
        {
            UpgradeMainMenuHiScore();
            UpgradeGameplayScoreUI();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Score UI upgrade COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Score UI upgrade FAILED: " + e);
            throw;
        }
    }

    static readonly Color HiScoreGrey = new Color(0.75f, 0.78f, 0.85f);

    static void UpgradeMainMenuHiScore()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        var canvas = GameObject.Find("MenuCanvas");
        if (canvas == null) { Debug.LogWarning("[Pawbound] MenuCanvas not found."); return; }

        var hi = FindComponentInChildren<Text>(canvas.transform, "HiScoreText");
        if (hi == null)
        {
            hi = UIText("HiScoreText", canvas.transform, "HI-SCORE 00000", 34,
                        TextAnchor.MiddleCenter, HiScoreGrey, FontStyle.Bold);
            var rt = (RectTransform)hi.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.48f);
            rt.sizeDelta = new Vector2(900f, 70f);
        }

        var mc = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
        if (mc != null)
        {
            var so = new SerializedObject(mc);
            so.FindProperty("hiScoreText").objectReferenceValue = hi;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] MainMenu HI-SCORE line added and wired.");
    }

    static void UpgradeGameplayScoreUI()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var scoreManager = UnityEngine.Object.FindFirstObjectByType<ScoreManager>();
        var canvas = GameObject.Find("GameplayCanvas");
        if (canvas == null) { Debug.LogWarning("[Pawbound] GameplayCanvas not found."); return; }

        // --- HUD: move SCORE to top-right, add HI-SCORE beneath it ---
        var hud = FindChildByName(canvas.transform, "HUD");
        var scoreLabel = FindComponentInChildren<Text>(hud.transform, "ScoreLabel");
        if (scoreLabel != null)
        {
            var rt = (RectTransform)scoreLabel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -28f);
            rt.sizeDelta = new Vector2(700f, 60f);
            scoreLabel.alignment = TextAnchor.UpperRight;
        }

        var hiLabel = FindComponentInChildren<Text>(hud.transform, "HiScoreLabel");
        if (hiLabel == null)
        {
            hiLabel = UIText("HiScoreLabel", hud.transform, "HI-SCORE 00000", 28,
                             TextAnchor.UpperRight, HiScoreGrey, FontStyle.Bold);
            var rt = (RectTransform)hiLabel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-40f, -86f);
            rt.sizeDelta = new Vector2(700f, 44f);
        }

        var hudCtrl = GetOrAdd<HUDController>(hud);
        var hudSO = new SerializedObject(hudCtrl);
        hudSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        hudSO.FindProperty("scoreText").objectReferenceValue = scoreLabel;
        hudSO.FindProperty("hiScoreText").objectReferenceValue = hiLabel;
        hudSO.ApplyModifiedPropertiesWithoutUndo();

        // --- Game Over: add HI-SCORE line + NEW HIGH SCORE badge ---
        var goPanel = FindChildByName(canvas.transform, "GameOverPanel");
        var finalScore = FindComponentInChildren<Text>(goPanel.transform, "FinalScore");

        var goHi = FindComponentInChildren<Text>(goPanel.transform, "GameOverHiScore");
        if (goHi == null)
        {
            goHi = UIText("GameOverHiScore", goPanel.transform, "HI-SCORE 00000", 44,
                          TextAnchor.MiddleCenter, HiScoreGrey, FontStyle.Bold);
            var rt = (RectTransform)goHi.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.44f);
            rt.sizeDelta = new Vector2(900f, 80f);
        }

        var badge = FindComponentInChildren<Text>(goPanel.transform, "NewHighScoreBadge");
        if (badge == null)
        {
            badge = UIText("NewHighScoreBadge", goPanel.transform, "NEW HIGH SCORE!", 42,
                           TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.30f), FontStyle.Bold);
            var rt = (RectTransform)badge.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.585f);
            rt.sizeDelta = new Vector2(1000f, 80f);
        }
        badge.gameObject.SetActive(false);

        var panel = GetOrAdd<GameOverPanel>(goPanel);
        var pSO = new SerializedObject(panel);
        pSO.FindProperty("scoreManager").objectReferenceValue = scoreManager;
        pSO.FindProperty("scoreText").objectReferenceValue = finalScore;
        pSO.FindProperty("hiScoreText").objectReferenceValue = goHi;
        pSO.FindProperty("newHighScoreBadge").objectReferenceValue = badge.gameObject;
        pSO.ApplyModifiedPropertiesWithoutUndo();

        goPanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] HUD moved top-right + HI-SCORE; Game Over HI-SCORE + badge wired.");
    }

    // =========================================================================
    //  FOREST BACKGROUND SWAP  -  replace the meadow pack with the Illusion
    //  Forest pack (bg-assets/), keep the existing parallax + camera systems.
    // =========================================================================
    //
    //  Layer identification (from the actual art):
    //    forest_back.png   160x272  dim opaque forest silhouette ...... FAR
    //    forest_middle.png 384x272  detailed gnarled trees + mound .... MIDDLE
    //    forest_ground.png 176x96   grass-topped dirt/root ground tiles NEAR / floor
    //
    const string ForestDir = "Assets/Backgrounds/Forest";

    [MenuItem("Pawbound/Swap To Forest Background")]
    public static void SwapForestBackground()
    {
        try
        {
            ImportBackgroundSprite(ForestDir + "/forest_back.png", 18f);
            ImportBackgroundSprite(ForestDir + "/forest_middle.png", 18f);
            ImportBackgroundSprite(ForestDir + "/forest_ground.png", 18f);
            ImportBackgroundSprite(ForestDir + "/forest_preview.png", 18f);
            AssetDatabase.Refresh();

            var backSpr = FirstSprite(ForestDir + "/forest_back.png");
            var midSpr = FirstSprite(ForestDir + "/forest_middle.png");
            var groundSpr = FirstSprite(ForestDir + "/forest_ground.png");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var mainCamGO = GameObject.Find("Main Camera");

            //                 layer  sprite     sortLayer      order  parallax  posY   cam
            ConfigureForestLayer("1_0", backSpr,   "1SKY",        0,     0.82f,  3.5f,  mainCamGO);
            ConfigureForestLayer("2_0", midSpr,    "2clouds",     0,     0.62f,  3.0f,  mainCamGO);
            ConfigureForestLayer("4_0", groundSpr, "3Background", 100,   0.02f, -5.85f, mainCamGO);

            var l3 = GameObject.Find("3_0");
            if (l3 != null) l3.SetActive(false); // meadow pack no longer needed; kept for easy restore

            if (mainCamGO != null)
            {
                var camc = mainCamGO.GetComponent<Camera>();
                if (camc != null) camc.backgroundColor = new Color(0.05f, 0.09f, 0.06f, 1f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Menu background -> the forest composite (keeps menu / gameplay consistent).
            var menuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            var canvas = GameObject.Find("MenuCanvas");
            if (canvas != null)
            {
                var bg = FindChildByName(canvas.transform, "Background");
                var previewSpr = FirstSprite(ForestDir + "/forest_preview.png");
                if (bg != null && previewSpr != null)
                {
                    var img = bg.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = previewSpr;
                        img.color = new Color(0.85f, 0.85f, 0.9f, 1f);
                    }
                }
            }
            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Forest background swap COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Forest background swap FAILED: " + e);
            throw;
        }
    }

    static void ImportBackgroundSprite(string path, float ppu)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { Debug.LogWarning($"[Pawbound] no importer at {path}"); return; }

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.wrapMode = TextureWrapMode.Repeat;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spritePixelsPerUnit = ppu;
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        s.spriteExtrude = 0;
        ti.SetTextureSettings(s);

        ti.SaveAndReimport();
    }

    static void ConfigureForestLayer(string layerName, Sprite sprite, string sortLayer, int order,
                                     float parallax, float posY, GameObject mainCamGO)
    {
        var go = GameObject.Find(layerName);
        if (go == null) { Debug.LogWarning($"[Pawbound] parallax layer '{layerName}' not found"); return; }
        go.SetActive(true);

        var bc = go.GetComponent<BackgroundController>();
        if (bc != null)
        {
            var so = new SerializedObject(bc);
            so.FindProperty("parallaxEffect").floatValue = parallax;
            so.FindProperty("infiniteHorizontal").boolValue = true;
            if (mainCamGO != null) so.FindProperty("cam").objectReferenceValue = mainCamGO.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var t = go.transform;
        t.localScale = Vector3.one;
        var p = t.position; p.y = posY; t.position = p;

        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sprite = sprite;
            sr.sortingLayerName = sortLayer;
            sr.sortingOrder = order;
            sr.color = Color.white;
            sr.drawMode = SpriteDrawMode.Simple;
        }
        EditorUtility.SetDirty(go);
    }

    // =========================================================================
    //  GROUND FIX + FIREBALL SPEED
    //
    //  forest_ground.png is not a strip: it holds two floating ground "islands"
    //  with transparent margins and a transparent gap between them, so repeating
    //  the whole 176px sprite shows the (dark) camera colour through every gap.
    //  forest_ground_tile.png is a fully-opaque 96x67 crop of the solid interior
    //  of island A, mirrored horizontally so it is palindromic and repeats with
    //  no seam. Point-filtered, no scaling -> pixel-art preserved.
    // =========================================================================
    const string GroundTilePath = ForestDir + "/forest_ground_tile.png";
    const string FireballPrefabRel = PrefabDir + "/Fireball.prefab";
    const float FireballStartSpeed = 4.5f;

    [MenuItem("Pawbound/Fix Ground + Fireball Speed")]
    public static void FixGroundAndFireball()
    {
        try
        {
            ImportBackgroundSprite(GroundTilePath, 18f);
            AssetDatabase.Refresh();

            var tileSpr = FirstSprite(GroundTilePath);
            if (tileSpr == null) { Debug.LogError("[Pawbound] forest_ground_tile sprite missing"); return; }

            // --- gameplay scene: repoint the ground layer to the seamless tile ---
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var mainCamGO = GameObject.Find("Main Camera");

            var ground = GameObject.Find("4_0");
            if (ground != null)
            {
                foreach (var sr in ground.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    sr.sprite = tileSpr;
                    sr.sortingLayerName = "3Background";
                    sr.sortingOrder = 100;
                    sr.color = Color.white;
                    sr.drawMode = SpriteDrawMode.Simple;
                }
                var t = ground.transform;
                t.localScale = Vector3.one;
                var p = t.position; p.y = -5.58f; t.position = p;   // grass line ~ y -4 (collider top)

                var bc = ground.GetComponent<BackgroundController>();
                if (bc != null)
                {
                    var so = new SerializedObject(bc);
                    so.FindProperty("parallaxEffect").floatValue = 0.02f;   // ~ world-locked
                    so.FindProperty("infiniteHorizontal").boolValue = true;
                    if (mainCamGO != null) so.FindProperty("cam").objectReferenceValue = mainCamGO.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorUtility.SetDirty(ground);
            }

            // --- FireballSpawner initial speed 6 -> 4.5 ---
            var spawner = UnityEngine.Object.FindFirstObjectByType<FireballSpawner>();
            if (spawner != null)
            {
                var so = new SerializedObject(spawner);
                so.FindProperty("fireballSpeed").floatValue = FireballStartSpeed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // --- Fireball prefab speed 6 -> 4.5 (fallback / declared value) ---
            var root = PrefabUtility.LoadPrefabContents(FireballPrefabRel);
            var fb = root.GetComponent<Fireball>();
            if (fb != null)
            {
                var so = new SerializedObject(fb);
                so.FindProperty("speed").floatValue = FireballStartSpeed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, FireballPrefabRel);
            PrefabUtility.UnloadPrefabContents(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Ground tile + Fireball speed fix COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Ground/Fireball fix FAILED: " + e);
            throw;
        }
    }

    // =========================================================================
    //  DUCK + DIFFICULTY CURVE
    // =========================================================================
    [MenuItem("Pawbound/Build Duck + Difficulty")]
    public static void BuildDuckAndDifficulty()
    {
        try
        {
            BuildDuckClip();
            AddDuckToKnightController();
            ConfigureDuckAndDifficultyScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Duck + Difficulty build COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Duck + Difficulty build FAILED: " + e);
            throw;
        }
    }

    static void BuildDuckClip()
    {
        var roll = LoadFrames("Assets/noBKG_KnightRoll_strip.png");
        // Frame 6 is the roll-recovery crouch: torso up, low profile, feet planted
        // on the ground line - reads as a duck, not a rolling ball.
        MakeSpriteClip(KnightAnimDir + "/Knight_Duck.anim", Slice(roll, 6, 1), 6f, true);
        Debug.Log("[Pawbound] Knight_Duck.anim built (Roll frame 6, held crouch).");
    }

    static void AddDuckToKnightController()
    {
        var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(KnightControllerPath);
        if (ac == null) { Debug.LogError("[Pawbound] Knight.controller missing"); return; }

        bool hasParam = false;
        foreach (var p in ac.parameters) if (p.name == "Ducking") hasParam = true;
        if (!hasParam) ac.AddParameter("Ducking", AnimatorControllerParameterType.Bool);

        var sm = ac.layers[0].stateMachine;
        AnimatorState duck = null, run = null, idle = null;
        foreach (var cs in sm.states)
        {
            if (cs.state.name == "Duck") duck = cs.state;
            else if (cs.state.name == "Run") run = cs.state;
            else if (cs.state.name == "Idle") idle = cs.state;
        }

        var duckClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(KnightAnimDir + "/Knight_Duck.anim");

        if (duck == null)
        {
            duck = sm.AddState("Duck");
            duck.motion = duckClip;

            var anyDuck = sm.AddAnyStateTransition(duck);
            anyDuck.canTransitionToSelf = false;
            anyDuck.hasExitTime = false;
            anyDuck.duration = 0.03f;
            anyDuck.hasFixedDuration = true;
            anyDuck.AddCondition(AnimatorConditionMode.If, 0f, "Ducking");

            if (run != null)
            {
                var t = duck.AddTransition(run);
                t.hasExitTime = false; t.duration = 0.05f; t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Ducking");
                t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            }
            if (idle != null)
            {
                var t = duck.AddTransition(idle);
                t.hasExitTime = false; t.duration = 0.05f; t.hasFixedDuration = true;
                t.AddCondition(AnimatorConditionMode.IfNot, 0f, "Ducking");
                t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            }
        }
        else
        {
            duck.motion = duckClip;
        }

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pawbound] Knight.controller: Ducking param + Duck state ensured.");
    }

    static void ConfigureDuckAndDifficultyScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var player = GameObject.Find("Player");
        var knight = player != null ? player.GetComponent<KnightController>() : null;
        var box = player != null ? player.GetComponent<BoxCollider2D>() : null;
        var skeletonSpawner = UnityEngine.Object.FindFirstObjectByType<SkeletonSpawner>();
        var fireballSpawner = UnityEngine.Object.FindFirstObjectByType<FireballSpawner>();

        // --- KnightController: wire collider + standing/duck sizes -----------
        if (knight != null && box != null)
        {
            Vector2 standSize = box.size;
            Vector2 standOffset = box.offset;
            float feetLocalY = standOffset.y - standSize.y * 0.5f;
            float duckHeight = standSize.y * 0.5f;
            Vector2 duckSize = new Vector2(standSize.x, duckHeight);
            Vector2 duckOffset = new Vector2(standOffset.x, feetLocalY + duckHeight * 0.5f);

            var so = new SerializedObject(knight);
            so.FindProperty("bodyCollider").objectReferenceValue = box;
            SetVec2(so, "standingColliderSize", standSize);
            SetVec2(so, "standingColliderOffset", standOffset);
            SetVec2(so, "duckColliderSize", duckSize);
            SetVec2(so, "duckColliderOffset", duckOffset);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Pawbound] Knight collider  stand size {standSize} off {standOffset}  duck size {duckSize} off {duckOffset}");
        }

        // --- FireballSpawner: DUCK-able HIGH range, JUMP-able LOW range ------
        if (fireballSpawner != null)
        {
            var so = new SerializedObject(fireballSpawner);
            SetVec2(so, "lowHeightRange", new Vector2(0.4f, 0.9f));
            SetVec2(so, "highHeightRange", new Vector2(1.35f, 1.55f));
            var fs = so.FindProperty("fireballSpeed"); if (fs != null) fs.floatValue = 4.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // --- DifficultyManager object ------------------------------------
        var dmGO = GameObject.Find("DifficultyManager") ?? new GameObject("DifficultyManager");
        dmGO.transform.position = Vector3.zero;
        var dm = GetOrAdd<DifficultyManager>(dmGO);
        var dmSO = new SerializedObject(dm);
        dmSO.FindProperty("skeletonSpawner").objectReferenceValue = skeletonSpawner;
        dmSO.FindProperty("fireballSpawner").objectReferenceValue = fireballSpawner;
        dmSO.FindProperty("knight").objectReferenceValue = knight;
        dmSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Scene: Knight duck wired, fireball ranges set, DifficultyManager added.");
    }

    static void SetVec2(SerializedObject so, string prop, Vector2 v)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.vector2Value = v;
    }

    // =========================================================================
    //  FINAL POLISH PASS  -  readability / cohesion tuning only, no new systems
    // =========================================================================
    [MenuItem("Pawbound/Final Polish Pass")]
    public static void PolishPass()
    {
        try
        {
            // 1. Duck pose -> a planted crouch (Roll frame 6) instead of a rolling ball.
            BuildDuckClip();
            AddDuckToKnightController();

            PolishGameplayScene();
            PolishMainMenu();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Pawbound] Final polish pass COMPLETE.");
        }
        catch (Exception e)
        {
            Debug.LogError("[Pawbound] Final polish pass FAILED: " + e);
            throw;
        }
    }





    // =========================================================================
    //  BUG FIX  -  camera drifts behind the Knight over time (LazyFollow binding
    //  mode does not track a 2D target correctly). Switch to WorldSpace so the
    //  camera rigidly holds FollowOffset from the (fixed-Y, X-tracking) target.
    // =========================================================================
    [MenuItem("Pawbound/Fix Camera Binding Mode")]
    public static void FixCameraBindingMode()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var vcamGO = GameObject.Find("CinemachineCamera");
        if (vcamGO == null) { Debug.LogError("[Pawbound] CinemachineCamera not found"); return; }

        var follow = vcamGO.GetComponent<CinemachineFollow>();
        if (follow == null) { Debug.LogError("[Pawbound] CinemachineFollow not found"); return; }

        var ts = follow.TrackerSettings;
        var before = ts.BindingMode;
        ts.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
        follow.TrackerSettings = ts;

        // FollowOffset must be pure world -Z for a 2D side-scroller.
        follow.FollowOffset = new Vector3(0f, 0f, -10f);

        EditorUtility.SetDirty(follow);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Pawbound] CinemachineFollow.BindingMode {before} -> WorldSpace, FollowOffset (0,0,-10). Saved.");
    }

    static readonly Color OutlineDark = new Color(0f, 0f, 0f, 0.85f);

    static void AddOutline(Component uiComponent, Vector2 distance)
    {
        if (uiComponent == null) return;
        var o = uiComponent.GetComponent<Outline>();
        if (o == null) o = uiComponent.gameObject.AddComponent<Outline>();
        o.effectColor = OutlineDark;
        o.effectDistance = distance;
        o.useGraphicAlpha = true;
    }

    static void AnchorAt(Component c, float x, float y)
    {
        if (c == null) return;
        var rt = (RectTransform)c.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(x, y);
        rt.anchoredPosition = Vector2.zero;
    }

    static void PolishGameplayScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ---- camera: tighten damping so the Knight stops rubber-banding ----
        var vcamGO = GameObject.Find("CinemachineCamera");
        if (vcamGO != null)
        {
            var follow = vcamGO.GetComponent<CinemachineFollow>();
            if (follow != null)
            {
                var ts = follow.TrackerSettings;
                ts.PositionDamping = new Vector3(0.12f, 0f, 0f);
                follow.TrackerSettings = ts;
                EditorUtility.SetDirty(follow);
            }
        }

        // ---- forest: keep it subordinate to gameplay (subtle darkening of the
        //      two back layers only; the ground stays full white) ----
        TintLayer("1_0", new Color(0.78f, 0.80f, 0.78f));
        TintLayer("2_0", new Color(0.86f, 0.88f, 0.86f));
        TintLayer("4_0", Color.white);

        // ---- HUD ----
        var canvas = GameObject.Find("GameplayCanvas");
        if (canvas != null)
        {
            var hud = FindChildByName(canvas.transform, "HUD");
            if (hud != null)
            {
                var score = FindComponentInChildren<Text>(hud.transform, "ScoreLabel");
                if (score != null) { score.fontSize = 46; AddOutline(score, new Vector2(2f, -2f)); }
                var hi = FindComponentInChildren<Text>(hud.transform, "HiScoreLabel");
                if (hi != null) { hi.fontSize = 28; AddOutline(hi, new Vector2(2f, -2f)); }
            }

            // ---- Game Over: darker overlay, even spacing, matched buttons ----
            var go = FindChildByName(canvas.transform, "GameOverPanel");
            if (go != null)
            {
                var panelImg = go.GetComponent<Image>();
                if (panelImg != null) panelImg.color = new Color(0f, 0f, 0f, 0.82f);

                var title = FindComponentInChildren<Text>(go.transform, "OverTitle");
                var badge = FindComponentInChildren<Text>(go.transform, "NewHighScoreBadge");
                var fscore = FindComponentInChildren<Text>(go.transform, "FinalScore");
                var fhi = FindComponentInChildren<Text>(go.transform, "GameOverHiScore");
                var restartBtn = FindChildByName(go.transform, "RestartButton");
                var menuBtn = FindChildByName(go.transform, "MenuButton");

                AnchorAt(title, 0.5f, 0.72f);
                AnchorAt(badge, 0.5f, 0.60f);
                AnchorAt(fscore, 0.5f, 0.50f);
                AnchorAt(fhi, 0.5f, 0.42f);
                if (restartBtn != null) { AnchorAt(restartBtn.transform, 0.5f, 0.30f); ((RectTransform)restartBtn.transform).sizeDelta = new Vector2(440f, 96f); }
                if (menuBtn != null) { AnchorAt(menuBtn.transform, 0.5f, 0.18f); ((RectTransform)menuBtn.transform).sizeDelta = new Vector2(440f, 96f); }

                AddOutline(title, new Vector2(3f, -3f));
                AddOutline(badge, new Vector2(2f, -2f));
                AddOutline(fscore, new Vector2(2f, -2f));
                AddOutline(fhi, new Vector2(2f, -2f));
                AddOutline(FindComponentInChildren<Text>(go.transform, "RestartButtonLabel"), new Vector2(2f, -2f));
                AddOutline(FindComponentInChildren<Text>(go.transform, "MenuButtonLabel"), new Vector2(2f, -2f));

                if (fscore != null) fscore.text = "SCORE 00000";
                if (fhi != null) fhi.text = "HI-SCORE 00000";
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Gameplay scene polished (camera damping, forest tint, HUD, Game Over).");
    }

    static void TintLayer(string layerName, Color c)
    {
        var go = GameObject.Find(layerName);
        if (go == null) return;
        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            sr.color = c;
        EditorUtility.SetDirty(go);
    }

    static void PolishMainMenu()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        var canvas = GameObject.Find("MenuCanvas");
        if (canvas == null) { EditorSceneManager.SaveScene(scene); return; }

        var bg = FindChildByName(canvas.transform, "Background");
        if (bg != null)
        {
            var img = bg.GetComponent<Image>();
            if (img != null) img.color = new Color(0.72f, 0.72f, 0.80f, 1f);
        }
        var scrim = FindChildByName(canvas.transform, "Scrim");
        if (scrim != null)
        {
            var img = scrim.GetComponent<Image>();
            if (img != null) img.color = new Color(0f, 0f, 0f, 0.55f);
        }

        var title = FindComponentInChildren<Text>(canvas.transform, "Title");
        var subtitle = FindComponentInChildren<Text>(canvas.transform, "Subtitle");
        var hi = FindComponentInChildren<Text>(canvas.transform, "HiScoreText");
        var startBtn = FindChildByName(canvas.transform, "StartButton");
        var startLabel = FindComponentInChildren<Text>(canvas.transform, "StartButtonLabel");

        if (title != null) { title.fontSize = 130; AnchorAt(title, 0.5f, 0.70f); AddOutline(title, new Vector2(4f, -4f)); }
        if (subtitle != null) { subtitle.fontSize = 34; AnchorAt(subtitle, 0.5f, 0.605f); AddOutline(subtitle, new Vector2(2f, -2f)); }
        if (hi != null) { hi.fontSize = 32; AnchorAt(hi, 0.5f, 0.45f); AddOutline(hi, new Vector2(2f, -2f)); }

        if (startBtn != null)
        {
            AnchorAt(startBtn.transform, 0.5f, 0.30f);
            ((RectTransform)startBtn.transform).sizeDelta = new Vector2(480f, 104f);
            var bimg = startBtn.GetComponent<Image>();
            if (bimg != null)
            {
                bimg.color = new Color(0.10f, 0.10f, 0.13f, 0.94f);
                AddOutline(bimg, new Vector2(3f, -3f));
                var so = bimg.GetComponent<Outline>();
                if (so != null) so.effectColor = new Color(0.62f, 0.50f, 0.28f, 0.85f); // subtle fantasy-gold edge
            }
        }
        if (startLabel != null) { startLabel.fontSize = 42; AddOutline(startLabel, new Vector2(2f, -2f)); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Main Menu polished (scrim, title hierarchy, spacing, outlines, button).");
    }

    // ---- tags & layers ------------------------------------------------------
    static void EnsureTagsAndLayers()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var so = new SerializedObject(asset);

        var layersProp = so.FindProperty("layers");
        foreach (var name in new[] { "Ground", "Player", "Enemy", "Hazard" })
            EnsureLayer(layersProp, name);

        var tagsProp = so.FindProperty("tags");
        foreach (var tag in new[] { "Player", "Enemy", "Hazard", "Obstacle" })
            EnsureTag(tagsProp, tag);

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[Pawbound] Tags & layers ensured.");
    }

    static void EnsureLayer(SerializedProperty layersProp, string name)
    {
        for (int i = 0; i < layersProp.arraySize; i++)
            if (layersProp.GetArrayElementAtIndex(i).stringValue == name) return;

        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue)) { sp.stringValue = name; return; }
        }
        Debug.LogWarning($"[Pawbound] No free user layer slot for '{name}'.");
    }

    static void EnsureTag(SerializedProperty tagsProp, string tag)
    {
        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;
        tagsProp.arraySize++;
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
    }

    // ---- sprite import ----------------------------------------------------
    static void FixKnightSpriteImport()
    {
        foreach (var path in KnightSheets)
            SetSpriteSheetPivot(path, SpriteAlignment.BottomCenter, new Vector2(0.5f, 0f));
        Debug.Log("[Pawbound] Knight sprite pivots set to bottom-centre.");
    }

    /// <summary>Set a consistent pivot/alignment on every sub-sprite of a multiple-mode texture.</summary>
    static void SetSpriteSheetPivot(string path, SpriteAlignment alignment, Vector2 pivot)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[Pawbound] No TextureImporter at {path}"); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        var rects = provider.GetSpriteRects();
        foreach (var r in rects)
        {
            r.alignment = alignment;
            r.pivot = pivot;
        }
        provider.SetSpriteRects(rects);
        provider.Apply();

        importer.SaveAndReimport();
    }

    // ---- knight animator --------------------------------------------------
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        var leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void TrySetTag(GameObject go, string tag)
    {
        try { go.tag = tag; }
        catch (Exception) { Debug.LogWarning($"[Pawbound] Tag '{tag}' not available yet; left as '{go.tag}'."); }
    }

    static void BuildKnightAnimator()
    {
        EnsureFolder(KnightAnimDir);

        var idle = LoadFrames("Assets/noBKG_KnightIdle_strip.png");
        var run = LoadFrames("Assets/noBKG_KnightRun_strip.png");
        var jumpFall = LoadFrames("Assets/noBKG_KnightJumpAndFall_strip.png");
        var death = LoadFrames("Assets/noBKG_KnightDeath_strip.png");

        var idleClip = MakeSpriteClip(KnightAnimDir + "/Knight_Idle.anim", idle, 10f, true);
        var runClip = MakeSpriteClip(KnightAnimDir + "/Knight_Run.anim", run, 14f, true);
        var jumpClip = MakeSpriteClip(KnightAnimDir + "/Knight_Jump.anim", Slice(jumpFall, 0, 5), 16f, false);
        var fallClip = MakeSpriteClip(KnightAnimDir + "/Knight_Fall.anim", Slice(jumpFall, 5, 5), 10f, true);
        var deathClip = MakeSpriteClip(KnightAnimDir + "/Knight_Death.anim", Slice(death, 0, 11), 12f, false);

        if (File.Exists(KnightControllerPath)) AssetDatabase.DeleteAsset(KnightControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(KnightControllerPath);

        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Dead", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle"); sIdle.motion = idleClip;
        var sRun = sm.AddState("Run"); sRun.motion = runClip;
        var sJump = sm.AddState("Jump"); sJump.motion = jumpClip;
        var sFall = sm.AddState("Fall"); sFall.motion = fallClip;
        var sDeath = sm.AddState("Death"); sDeath.motion = deathClip;
        sm.defaultState = sIdle;

        AddTransition(sIdle, sRun, (c => c.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed")));
        AddTransition(sRun, sIdle, (c => c.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed")));

        var anyJump = sm.AddAnyStateTransition(sJump);
        anyJump.canTransitionToSelf = false;
        anyJump.hasExitTime = false;
        anyJump.duration = 0.02f;
        anyJump.hasFixedDuration = true;
        anyJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        AddTransition(sJump, sFall, (c => c.AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalSpeed")));
        var jumpFallFallback = sJump.AddTransition(sFall);
        jumpFallFallback.hasExitTime = true; jumpFallFallback.exitTime = 0.8f;
        jumpFallFallback.duration = 0.1f; jumpFallFallback.hasFixedDuration = true;

        AddTransition(sFall, sRun, (c =>
        {
            c.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            c.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        }));
        AddTransition(sFall, sIdle, (c =>
        {
            c.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            c.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }));

        var anyDeath = sm.AddAnyStateTransition(sDeath);
        anyDeath.canTransitionToSelf = false;
        anyDeath.hasExitTime = false;
        anyDeath.duration = 0.05f;
        anyDeath.hasFixedDuration = true;
        anyDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pawbound] Knight.controller built (Idle/Run/Jump/Fall/Death).");
    }

    static void AddTransition(AnimatorState from, AnimatorState to, Action<AnimatorStateTransition> configure)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.06f;
        t.hasFixedDuration = true;
        configure(t);
    }

    // ---- scene wiring ---------------------------------------------------
    static void ConfigurePhaseAScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int groundLayer = LayerMask.NameToLayer("Ground");
        int playerLayer = LayerMask.NameToLayer("Player");

        var idleFrames = LoadFrames("Assets/noBKG_KnightIdle_strip.png");
        var knightController = AssetDatabase.LoadAssetAtPath<AnimatorController>(KnightControllerPath);

        // --- player -------------------------------------------------------
        var player = GameObject.Find("player");
        if (player == null) { Debug.LogError("[Pawbound] No 'player' GameObject in scene."); return; }
        player.name = "Player";
        TrySetTag(player, "Player");
        if (playerLayer >= 0) player.layer = playerLayer;

        var legacyMove = player.GetComponent<PlayerMovement>();
        if (legacyMove != null) UnityEngine.Object.DestroyImmediate(legacyMove, true);

        player.transform.position = new Vector3(KnightStartX, GroundSurfaceY, 0f);
        player.transform.rotation = Quaternion.identity;
        player.transform.localScale = new Vector3(KnightScale, KnightScale, 1f);

        var sr = GetOrAdd<SpriteRenderer>(player);
        if (idleFrames.Length > 0) sr.sprite = idleFrames[0];
        sr.color = Color.white;
        sr.flipX = false;
        sr.sortingLayerName = "player";
        sr.sortingOrder = 0;

        var rb = GetOrAdd<Rigidbody2D>(player);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 3.5f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var box = GetOrAdd<BoxCollider2D>(player);
        box.size = new Vector2(0.20f, 0.30f);
        box.offset = new Vector2(0f, 0.16f);
        box.isTrigger = false;

        var groundCheck = FindOrCreateChild(player.transform, "GroundCheck");
        groundCheck.localPosition = new Vector3(0f, 0f, 0f);

        var animator = GetOrAdd<Animator>(player);
        animator.runtimeAnimatorController = knightController;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var kc = GetOrAdd<KnightController>(player);
        var kcSO = new SerializedObject(kc);
        kcSO.FindProperty("groundCheck").objectReferenceValue = groundCheck;
        kcSO.FindProperty("animator").objectReferenceValue = animator;
        if (groundLayer >= 0)
            kcSO.FindProperty("groundLayer").intValue = 1 << groundLayer;
        kcSO.ApplyModifiedPropertiesWithoutUndo();

        // --- ground ------------------------------------------------------
        var ground = GameObject.Find("Ground") ?? new GameObject("Ground");
        if (groundLayer >= 0) ground.layer = groundLayer;
        ground.transform.position = new Vector3(KnightStartX, GroundSurfaceY - 2f, 0f);
        var gCol = GetOrAdd<BoxCollider2D>(ground);
        gCol.size = new Vector2(80f, 4f);
        gCol.offset = Vector2.zero;
        var gFollow = GetOrAdd<FollowerX>(ground);
        var gFollowSO = new SerializedObject(gFollow);
        gFollowSO.FindProperty("target").objectReferenceValue = player.transform;
        gFollowSO.ApplyModifiedPropertiesWithoutUndo();

        // --- camera target (locks camera Y) ----------------------------
        var camTarget = GameObject.Find("CameraTarget") ?? new GameObject("CameraTarget");
        camTarget.transform.position = new Vector3(KnightStartX, 0f, 0f);
        var tracker = GetOrAdd<CameraTargetTracker>(camTarget);
        var trackerSO = new SerializedObject(tracker);
        trackerSO.FindProperty("target").objectReferenceValue = player.transform;
        trackerSO.ApplyModifiedPropertiesWithoutUndo();

        var vcamGO = GameObject.Find("CinemachineCamera");
        if (vcamGO != null)
        {
            var vcam = vcamGO.GetComponent<CinemachineCamera>();
            if (vcam != null)
            {
                var t = vcam.Target;
                t.TrackingTarget = camTarget.transform;
                vcam.Target = t;
                EditorUtility.SetDirty(vcam);
            }
        }

        // --- parallax --------------------------------------------------
        foreach (var bc in UnityEngine.Object.FindObjectsByType<BackgroundController>(FindObjectsSortMode.None))
        {
            var bcSO = new SerializedObject(bc);
            bcSO.FindProperty("cam").objectReferenceValue = camTarget.transform;
            bcSO.FindProperty("infiniteHorizontal").boolValue = true;
            bcSO.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Pawbound] Scene configured for Phase A and saved.");
    }

    // ---- helpers -------------------------------------------------------
    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static Transform FindOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Sprite[] LoadFrames(string texturePath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(texturePath);
        return all.OfType<Sprite>()
                  .OrderBy(s => TrailingIndex(s.name))
                  .ToArray();
    }

    /// <summary>First sprite of each texture, in the order given (for one-frame-per-file sheets like FB001..FB005).</summary>
    static Sprite[] LoadFramesMulti(params string[] texturePaths)
    {
        var list = new List<Sprite>();
        foreach (var p in texturePaths)
        {
            var s = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>()
                        .OrderBy(x => TrailingIndex(x.name)).FirstOrDefault();
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    static int TrailingIndex(string name)
    {
        int u = name.LastIndexOf('_');
        if (u >= 0 && int.TryParse(name.Substring(u + 1), out int n)) return n;
        return 0;
    }

    static Sprite[] Slice(Sprite[] src, int start, int count)
    {
        if (src.Length == 0) return src;
        start = Mathf.Clamp(start, 0, src.Length - 1);
        count = Mathf.Clamp(count, 1, src.Length - start);
        var outp = new Sprite[count];
        Array.Copy(src, start, outp, 0, count);
        return outp;
    }

    static AnimationClip MakeSpriteClip(string path, Sprite[] frames, float fps, bool loop)
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"[Pawbound] No frames for clip {path}");
            return null;
        }

        var clip = new AnimationClip { frameRate = fps };

        var binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }
}
