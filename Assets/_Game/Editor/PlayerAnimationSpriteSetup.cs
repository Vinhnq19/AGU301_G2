#if UNITY_EDITOR
using System.Collections.Generic;
using DungeonBuilder.Player;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes the IDLE, RUN and DEATH directional sprite arrays on DB_Player's PlayerAnimation.
/// Hand-writing nested Sprite[] references in prefab YAML gets scrambled by Unity on
/// import, so this sets them through the SerializedObject API, matching by name.
///
/// Foraging sprites are managed manually in the Inspector (the chosen sheet is a
/// content decision), so this script does NOT touch _foraging — it is safe to run
/// without clobbering your foraging setup.
///
/// Run via menu: Tools > Player Animation > Fix Idle/Run Sprites
/// </summary>
public static class PlayerAnimationSpriteSetup
{
    private const string PrefabPath = "Assets/_Game/Generated/Prefabs/Player/DB_Player.prefab";
    private const string IdleTexture = "Assets/Sprite/Bunny/IDLE/Bunny_Idle.png";
    private const string RunTexture = "Assets/Sprite/Bunny/RUN/Bunny_Run.png";
    private const string DeathTexture = "Assets/Sprite/Bunny/DEATH/Bunny_Death.png";

    [MenuItem("Tools/Player Animation/Fix Idle/Run Sprites")]
    public static void Setup()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[PlayerAnimationSpriteSetup] Prefab not found: {PrefabPath}");
            return;
        }

        PlayerAnimation anim = prefab.GetComponent<PlayerAnimation>();
        if (anim == null)
        {
            Debug.LogError("[PlayerAnimationSpriteSetup] PlayerAnimation component not found on DB_Player.");
            return;
        }

        Dictionary<string, Sprite> idle = LoadSprites(IdleTexture);
        Dictionary<string, Sprite> run = LoadSprites(RunTexture);
        Dictionary<string, Sprite> death = LoadSprites(DeathTexture);

        SerializedObject so = new SerializedObject(anim);

        // Thứ tự hàng của các sheet Bunny (theo số sprite): hàng 0 = UP, hàng 1 = SIDE-right,
        // hàng 2 = side-left (không dùng, side trái flipX từ side phải), hàng 3 = DOWN.
        // Mapping này lấy từ prefab đã được chỉnh tay đúng (commit trước 1032d82) — ĐỪNG đổi
        // nếu chưa kiểm tra lại bằng cách chạy game.

        // Idle: 5 frames/row.
        Assign(so, "_idle.up", Range(idle, "Bunny_Idle_", 0, 4));
        Assign(so, "_idle.side", Range(idle, "Bunny_Idle_", 5, 9));
        Assign(so, "_idle.down", Range(idle, "Bunny_Idle_", 15, 19));

        // Run: 8 frames/row.
        Assign(so, "_run.up", Range(run, "Bunny_Run_", 0, 7));
        Assign(so, "_run.side", Range(run, "Bunny_Run_", 8, 15));
        Assign(so, "_run.down", Range(run, "Bunny_Run_", 24, 31));

        // Death: 12 frames/row. Mỗi hướng chỉ lấy đúng 1 hàng —
        // gán cả 48 frame sẽ làm animation chết chạy 4 lượt liên tiếp (mỗi hướng 1 lượt).
        Assign(so, "_death.up", Range(death, "Bunny_Death_", 0, 11));
        Assign(so, "_death.side", Range(death, "Bunny_Death_", 12, 23));
        Assign(so, "_death.down", Range(death, "Bunny_Death_", 36, 47));

        // Ensure _renderer points at the Visual child's SpriteRenderer (only if unset).
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

        Debug.Log("[PlayerAnimationSpriteSetup] Done. IDLE/RUN/DEATH directional sprite arrays fixed (foraging left untouched).");
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
