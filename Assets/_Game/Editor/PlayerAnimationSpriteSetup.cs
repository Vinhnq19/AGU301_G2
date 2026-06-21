#if UNITY_EDITOR
using System.Collections.Generic;
using DungeonBuilder.Player;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns the Bunny sprite frames to the PlayerAnimation component on DB_Player.
/// Hand-writing nested Sprite[] references in prefab YAML is unreliable — Unity
/// scrambles/empties them on import — so this does it through the SerializedObject
/// API and matches sprites by name.
///
/// It runs automatically once after each domain reload (via InitializeOnLoad) when
/// the foraging array is still empty, and is also available as a menu item:
///   Tools > Player Animation > Setup Sprites
/// </summary>
[InitializeOnLoad]
public static class PlayerAnimationSpriteSetup
{
    private const string PrefabPath = "Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab";

    private const string IdleTexture = "Assets/Sprite/Bunny/IDLE/Bunny_Idle.png";
    private const string RunTexture = "Assets/Sprite/Bunny/RUN/Bunny_Run.png";
    private const string ForageTexture = "Assets/Sprite/Bunny/WATERING CAN/Bunny_WateringCan.png";

    static PlayerAnimationSpriteSetup()
    {
        // Defer until the AssetDatabase is ready after a domain reload.
        EditorApplication.delayCall += () => RunSetup(auto: true);
    }

    [MenuItem("Tools/Player Animation/Setup Sprites")]
    public static void SetupMenu()
    {
        RunSetup(auto: false);
    }

    private static void RunSetup(bool auto)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            return; // not imported yet; the menu can be used once it is.
        }

        PlayerAnimation anim = prefab.GetComponent<PlayerAnimation>();
        if (anim == null)
        {
            if (!auto)
            {
                Debug.LogError("[PlayerAnimationSpriteSetup] PlayerAnimation component not found on DB_Player.");
            }
            return;
        }

        SerializedObject so = new SerializedObject(anim);

        // Skip the auto-run if the arrays are already populated (idempotent).
        SerializedProperty existing = so.FindProperty("_foraging.up");
        if (auto && existing != null && existing.arraySize > 0)
        {
            return;
        }

        Dictionary<string, Sprite> idle = LoadSprites(IdleTexture);
        Dictionary<string, Sprite> run = LoadSprites(RunTexture);
        Dictionary<string, Sprite> forage = LoadSprites(ForageTexture);

        // Idle: 5 frames/row, row order top->bottom = side-right, side-left, down, up.
        Assign(so, "_idle.up", Range(idle, "Bunny_Idle_", 15, 19));
        Assign(so, "_idle.down", Range(idle, "Bunny_Idle_", 10, 14));
        Assign(so, "_idle.side", Range(idle, "Bunny_Idle_", 0, 4));

        // Run: 8 frames/row, same row order.
        Assign(so, "_run.up", Range(run, "Bunny_Run_", 24, 31));
        Assign(so, "_run.down", Range(run, "Bunny_Run_", 16, 23));
        Assign(so, "_run.side", Range(run, "Bunny_Run_", 0, 7));

        // Foraging (Watering Can) is single-direction: same frames for up/down/side.
        Sprite[] forageFrames = Range(forage, "Bunny_WateringCan_", 0, 35);
        Assign(so, "_foraging.up", forageFrames);
        Assign(so, "_foraging.down", forageFrames);
        Assign(so, "_foraging.side", forageFrames);

        // Ensure _renderer points at the Visual child's SpriteRenderer.
        SerializedProperty rendererProp = so.FindProperty("_renderer");
        if (rendererProp != null && rendererProp.objectReferenceValue == null)
        {
            Transform visual = prefab.transform.Find("Visual");
            if (visual != null)
            {
                rendererProp.objectReferenceValue = visual.GetComponent<SpriteRenderer>();
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        Debug.Log("[PlayerAnimationSpriteSetup] Done. PlayerAnimation sprite arrays assigned on DB_Player.");
    }

    private static Dictionary<string, Sprite> LoadSprites(string texturePath)
    {
        Dictionary<string, Sprite> map = new Dictionary<string, Sprite>();
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(texturePath))
        {
            if (obj is Sprite sprite && !string.IsNullOrEmpty(sprite.name))
            {
                map[sprite.name] = sprite;
            }
        }
        return map;
    }

    private static Sprite[] Range(Dictionary<string, Sprite> map, string prefix, int from, int to)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = from; i <= to; i++)
        {
            if (map.TryGetValue(prefix + i, out Sprite sprite))
            {
                list.Add(sprite);
            }
            else
            {
                Debug.LogWarning($"[PlayerAnimationSpriteSetup] Missing sprite: {prefix}{i}");
            }
        }
        return list.ToArray();
    }

    private static void Assign(SerializedObject so, string propertyPath, Sprite[] sprites)
    {
        SerializedProperty prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogError($"[PlayerAnimationSpriteSetup] Property not found: {propertyPath}");
            return;
        }

        prop.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }
    }
}
#endif
