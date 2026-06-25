using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Character Importer Tool
/// Mở từ menu: Tools > Character Importer
///
/// Cách dùng:
///   1. Chọn thư mục character (ví dụ: Assets/Character/CharacterList/ZoeKo)
///   2. Nhập tên character (tự động điền từ tên folder)
///   3. Chỉnh FPS nếu cần
///   4. Nhấn "Import Character and Generate Prefab"
///
/// Tool sẽ tự động:
///   - Đọc mọi animation trong thư mục animations/ (mỗi sub-folder là 1 action)
///   - Mỗi action có các sub-folder hướng (east, north, south, west, ...)
///   - Tạo AnimationClip cho từng action_direction
///   - Tạo AnimatorController chứa tất cả states
///   - Tạo Prefab vào Assets/Character/Prefabs/{CharacterName}.prefab
/// </summary>
public class CharacterImporterTool : EditorWindow
{
    private string characterFolderPath = "Assets/Character/CharacterList/ZoeKo";
    private string characterName       = "ZoeKo";
    private float  frameRate           = 12f;
    private bool   loopAnimations      = true;

    private const string PrefabOutputRoot = "Assets/Character/Prefabs";

    private Vector2 scrollPos;
    private List<string> logMessages = new List<string>();
    private bool showLog = false;

    [MenuItem("Tools/Character Importer")]
    public static void ShowWindow()
    {
        var window = GetWindow<CharacterImporterTool>("Character Importer");
        window.minSize = new Vector2(480, 380);
    }

    void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(8);
        DrawSettings();
        EditorGUILayout.Space(12);
        DrawActionButton();
        EditorGUILayout.Space(8);
        DrawLog();
    }

    void DrawHeader()
    {
        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Character Importer Tool", style, GUILayout.Height(30));
        EditorGUILayout.LabelField("Tu dong tao AnimationClips, AnimatorController va Prefab cho Character",
            EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.Space(4);
        DrawHorizontalLine();
    }

    void DrawSettings()
    {
        EditorGUILayout.LabelField("Cai dat", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Character Folder", GUILayout.Width(130));
        characterFolderPath = EditorGUILayout.TextField(characterFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string selected = EditorUtility.OpenFolderPanel("Chon thu muc Character", "Assets", "");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath))
                    characterFolderPath = "Assets" + selected.Substring(Application.dataPath.Length).Replace("\\", "/");
                else
                    characterFolderPath = selected.Replace("\\", "/");

                characterName = Path.GetFileName(characterFolderPath.TrimEnd('/'));
            }
        }
        EditorGUILayout.EndHorizontal();

        characterName  = EditorGUILayout.TextField("Character Name", characterName);
        frameRate      = EditorGUILayout.FloatField("Frame Rate (FPS)", frameRate);
        if (frameRate <= 0) frameRate = 12f;
        loopAnimations = EditorGUILayout.Toggle("Loop Animations", loopAnimations);

        EditorGUILayout.Space(6);

        using (new EditorGUI.DisabledGroupScope(true))
        {
            string animPath  = characterFolderPath.TrimEnd('/') + "/animations";
            string genPath   = characterFolderPath.TrimEnd('/') + "/GeneratedAnimations";
            string prefabOut = PrefabOutputRoot + "/" + characterName + ".prefab";

            EditorGUILayout.TextField("Animations Input",  animPath);
            EditorGUILayout.TextField("Clips + Controller Output", genPath);
            EditorGUILayout.TextField("Prefab Output", prefabOut);
        }
    }

    void DrawActionButton()
    {
        bool valid = ValidateInputs(out string error);

        if (!valid)
            EditorGUILayout.HelpBox(error, MessageType.Warning);

        using (new EditorGUI.DisabledGroupScope(!valid))
        {
            var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 13 };
            if (GUILayout.Button("Import Character & Generate Prefab", btnStyle, GUILayout.Height(44)))
            {
                logMessages.Clear();
                showLog = true;
                RunImport();
            }
        }
    }

    void DrawLog()
    {
        if (!showLog || logMessages.Count == 0) return;

        DrawHorizontalLine();
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(160));
        foreach (var msg in logMessages)
        {
            Color prev = GUI.color;
            if (msg.StartsWith("[ERROR]"))  GUI.color = new Color(1f, 0.4f, 0.4f);
            else if (msg.StartsWith("[OK]")) GUI.color = new Color(0.4f, 1f, 0.5f);
            EditorGUILayout.LabelField(msg, EditorStyles.miniLabel);
            GUI.color = prev;
        }
        EditorGUILayout.EndScrollView();
    }

    bool ValidateInputs(out string error)
    {
        if (string.IsNullOrWhiteSpace(characterFolderPath))
        { error = "Chua chon thu muc Character."; return false; }
        if (string.IsNullOrWhiteSpace(characterName))
        { error = "Chua nhap ten Character."; return false; }
        string animDir = characterFolderPath.TrimEnd('/') + "/animations";
        if (!Directory.Exists(animDir))
        { error = "Khong tim thay thu muc animations tai:\n" + animDir; return false; }
        error = "";
        return true;
    }

    void RunImport()
    {
        string charRoot = characterFolderPath.TrimEnd('/');
        string animRoot = charRoot + "/animations";
        string genRoot  = charRoot + "/GeneratedAnimations";

        Log("Bat dau import character: " + characterName);
        Log("Animations folder: " + animRoot);

        EnsureDirectory(genRoot);
        EnsureDirectory(PrefabOutputRoot);

        string controllerPath = genRoot + "/" + characterName + "_AnimatorController.controller";
        AnimatorController controller = GetOrCreateController(controllerPath);
        if (controller == null) { Log("[ERROR] Khong the tao AnimatorController!"); return; }
        Log("[OK] AnimatorController: " + controllerPath);

        string[] actionFolders = Directory.GetDirectories(animRoot).OrderBy(d => d).ToArray();
        if (actionFolders.Length == 0) { Log("[ERROR] Khong tim thay animation nao!"); return; }

        int totalClips = 0;
        foreach (string actionPath in actionFolders)
        {
            string actionName   = Path.GetFileName(actionPath);
            string actionOutDir = genRoot + "/" + actionName;
            EnsureDirectory(actionOutDir);
            Log("Action: " + actionName);

            string[] dirFolders = Directory.GetDirectories(actionPath).OrderBy(d => d).ToArray();
            foreach (string dirPath in dirFolders)
            {
                string dirName = Path.GetFileName(dirPath);
                AnimationClip clip = CreateOrUpdateClip(dirPath, actionName, dirName, actionOutDir);
                if (clip != null)
                {
                    AddStateToController(controller, clip);
                    Log("[OK] Clip: " + actionName + "_" + dirName + "  (" + CountPngs(dirPath) + " frames)");
                    totalClips++;
                }
                else
                {
                    Log("[ERROR] Khong co PNG trong: " + actionName + "/" + dirName);
                }
            }
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Log("[OK] Da tao " + totalClips + " AnimationClips.");

        bool prefabOk = CreatePrefab(controller);
        if (prefabOk)
            Log("[OK] Prefab da luu vao: " + PrefabOutputRoot + "/" + characterName + ".prefab");
        else
            Log("[ERROR] Tao Prefab that bai!");

        AssetDatabase.Refresh();
        Log("[OK] Hoan tat!");
        EditorUtility.DisplayDialog("Character Importer",
            "Import thanh cong!\n\n" + totalClips + " AnimationClips\n1 AnimatorController\n1 Prefab (" + characterName + ")\n\nKiem tra: " + PrefabOutputRoot + "/" + characterName + ".prefab",
            "OK");
    }

    AnimatorController GetOrCreateController(string controllerPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (existing != null)
        {
            var sm = existing.layers[0].stateMachine;
            foreach (var s in sm.states.ToArray()) sm.RemoveState(s.state);
            return existing;
        }
        return AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
    }

    void AddStateToController(AnimatorController controller, AnimationClip clip)
    {
        var sm = controller.layers[0].stateMachine;
        if (sm.states.Any(s => s.state.name == clip.name)) return;
        sm.AddState(clip.name).motion = clip;
    }

    AnimationClip CreateOrUpdateClip(string folderPath, string actionName, string dirName, string outputDir)
    {
        string[] pngs = Directory.GetFiles(folderPath, "*.png").OrderBy(f => f).ToArray();
        if (pngs.Length == 0) return null;

        List<Sprite> sprites = new List<Sprite>();
        foreach (string png in pngs)
        {
            string assetPath = ToAssetPath(png);
            FixSpritePivot(assetPath);
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                if (obj is Sprite sp) sprites.Add(sp);
        }
        if (sprites.Count == 0) return null;

        string clipName = actionName + "_" + dirName;
        string clipPath = outputDir + "/" + clipName + ".anim";

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loopAnimations;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer), path = "", propertyName = "m_Sprite"
        };

        float frameDuration = 1f / frameRate;
        ObjectReferenceKeyframe[] kf = new ObjectReferenceKeyframe[sprites.Count + 1];
        for (int i = 0; i < sprites.Count; i++)
            kf[i] = new ObjectReferenceKeyframe { time = i * frameDuration, value = sprites[i] };
        kf[sprites.Count] = new ObjectReferenceKeyframe { time = sprites.Count * frameDuration, value = sprites[sprites.Count - 1] };

        clip.frameRate = frameRate;
        AnimationUtility.SetObjectReferenceCurve(clip, binding, kf);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    void FixSpritePivot(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        bool dirty = false;

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }

        // spriteAlignment and spritePivot must be set via TextureImporterSettings
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        if (settings.spriteAlignment != (int)SpriteAlignment.BottomCenter ||
            settings.spritePivot != new Vector2(0.5f, 0f))
        {
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot     = new Vector2(0.5f, 0f);
            importer.SetTextureSettings(settings);
            dirty = true;
        }

        if (dirty)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    bool CreatePrefab(AnimatorController controller)
    {
        string prefabPath = PrefabOutputRoot + "/" + characterName + ".prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject go;

        if (existingPrefab != null)
        {
            go = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        else
        {
            go = new GameObject(characterName);
        }

        if (go.GetComponent<SpriteRenderer>() == null) go.AddComponent<SpriteRenderer>();

        Animator animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale  = 0f;
        rb.freezeRotation = true;
        rb.constraints   = RigidbodyConstraints2D.FreezeRotation;

        if (go.GetComponent<BoxCollider2D>() == null) go.AddComponent<BoxCollider2D>();

        if (existingPrefab != null)
        {
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            PrefabUtility.UnloadPrefabContents(go);
            return true;
        }
        else
        {
            bool success;
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out success);
            Object.DestroyImmediate(go);
            return success;
        }
    }

    void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            AssetDatabase.ImportAsset(path);
        }
    }

    string ToAssetPath(string path)
    {
        string n = path.Replace("\\", "/");
        if (n.StartsWith(Application.dataPath)) return "Assets" + n.Substring(Application.dataPath.Length);
        return n;
    }

    int CountPngs(string folder) => Directory.GetFiles(folder, "*.png").Length;

    void Log(string msg)
    {
        logMessages.Add(msg);
        if (msg.StartsWith("[ERROR]")) Debug.LogError("[CharacterImporter] " + msg);
        else Debug.Log("[CharacterImporter] " + msg);
        Repaint();
    }

    void DrawHorizontalLine()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f));
        EditorGUILayout.Space(2);
    }
}
