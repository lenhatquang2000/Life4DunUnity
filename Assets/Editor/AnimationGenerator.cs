using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class AnimationGenerator : EditorWindow
{
    private string characterName = "Mira";
    private string animationsPath = "Assets/Character/Mira/animations";
    private float frameRate = 12f;

    [MenuItem("Tools/Animation Generator")]
    public static void ShowWindow()
    {
        GetWindow<AnimationGenerator>("Animation Generator");
    }

    void OnGUI()
    {
        GUILayout.Label("Character Animation Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        characterName = EditorGUILayout.TextField("Character Name", characterName);
        
        GUILayout.Label("Animations Folder Path", EditorStyles.label);
        animationsPath = EditorGUILayout.TextField(animationsPath);
        
        if (GUILayout.Button("Browse", GUILayout.Width(100)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Animations Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                animationsPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                // Auto-extract character name from path
                string[] pathParts = animationsPath.Split('/');
                if (pathParts.Length >= 3)
                {
                    characterName = pathParts[pathParts.Length - 2];
                }
            }
        }

        frameRate = EditorGUILayout.FloatField("Frame Rate (FPS)", frameRate);
        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Animations", GUILayout.Height(40)))
        {
            GenerateAnimations();
        }
    }

    private void GenerateAnimations()
    {
        if (!Directory.Exists(animationsPath))
        {
            Debug.LogError("Animations folder not found at " + animationsPath);
            return;
        }

        string characterRoot = Path.GetDirectoryName(animationsPath);
        string outputRoot = Path.Combine(characterRoot, "GeneratedAnimations").Replace("\\", "/");
        
        if (!Directory.Exists(outputRoot))
        {
            Directory.CreateDirectory(outputRoot);
        }

        string controllerName = $"{characterName}_AnimatorController";
        string controllerPath = Path.Combine(outputRoot, controllerName + ".controller").Replace("\\", "/");
        
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        string[] actionFolders = Directory.GetDirectories(animationsPath);
        foreach (string actionPath in actionFolders)
        {
            string actionName = Path.GetFileName(actionPath);
            string actionOutputDir = Path.Combine(outputRoot, actionName).Replace("\\", "/");
            
            if (!Directory.Exists(actionOutputDir))
            {
                Directory.CreateDirectory(actionOutputDir);
            }

            string[] directionFolders = Directory.GetDirectories(actionPath);
            foreach (string dirPath in directionFolders)
            {
                string dirName = Path.GetFileName(dirPath);
                AnimationClip clip = CreateAnimationClip(dirPath, actionName, dirName, actionOutputDir);
                
                if (clip != null)
                {
                    AddClipToController(controller, clip);
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Animations and Animator Controller generated successfully for {characterName} in {outputRoot}");
    }

    private static void AddClipToController(AnimatorController controller, AnimationClip clip)
    {
        var layer = controller.layers[0];
        var stateMachine = layer.stateMachine;
        
        // Check if state already exists
        if (stateMachine.states.Any(s => s.state.name == clip.name)) return;
        
        stateMachine.AddState(clip.name).motion = clip;
    }

    private void FixPivotForTexture(string assetPath)
    {
        string metaPath = assetPath + ".meta";
        if (File.Exists(metaPath))
        {
            try
            {
                string content = File.ReadAllText(metaPath);
                string newContent = content;

                newContent = System.Text.RegularExpressions.Regex.Replace(newContent, @"alignment:\s*\d+", "alignment: 7");
                newContent = System.Text.RegularExpressions.Regex.Replace(newContent, @"spritePivot:\s*\{x:\s*[0-9\.]+, y:\s*[0-9\.]+\}", "spritePivot: {x: 0.5, y: 0}");
                newContent = System.Text.RegularExpressions.Regex.Replace(newContent, @"pivot:\s*\{x:\s*[0-9\.]+, y:\s*[0-9\.]+\}", "pivot: {x: 0.5, y: 0}");

                if (newContent != content)
                {
                    File.WriteAllText(metaPath, newContent);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error fixing pivot for {assetPath}: {e.Message}");
            }
        }
    }

    private AnimationClip CreateAnimationClip(string folderPath, string actionName, string dirName, string outputDir)
    {
        string clipName = $"{actionName}_{dirName}";
        string clipPath = Path.Combine(outputDir, clipName + ".anim").Replace("\\", "/");

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, clipPath);
        }

        // Set clip settings
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Get all sprites in folder
        string[] fileEntries = Directory.GetFiles(folderPath, "*.png");
        List<Sprite> sprites = new List<Sprite>();

        foreach (string filePath in fileEntries.OrderBy(f => f))
        {
            string assetPath = filePath.Replace("\\", "/");
            FixPivotForTexture(assetPath);
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(sprite);
                }
            }
        }

        if (sprites.Count == 0) return null;

        // Create keyframes
        EditorCurveBinding spriteBinding = new EditorCurveBinding();
        spriteBinding.type = typeof(SpriteRenderer);
        spriteBinding.path = "";
        spriteBinding.propertyName = "m_Sprite";

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Count + 1];
        float frameTime = 1f / frameRate;

        for (int i = 0; i < sprites.Count; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        // Last keyframe to hold the final sprite for a bit
        keyframes[sprites.Count] = new ObjectReferenceKeyframe
        {
            time = sprites.Count * frameTime,
            value = sprites[sprites.Count - 1]
        };

        clip.frameRate = frameRate;
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);
        EditorUtility.SetDirty(clip);
        return clip;
    }
}
