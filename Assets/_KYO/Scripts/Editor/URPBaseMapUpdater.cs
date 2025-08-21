using UnityEditor;
using UnityEngine;

public class URPBaseMapUpdater
{
    [MenuItem("Assets/Update URP Materials to _BaseMap (with Diagnostics)")]
    public static void UpdateMaterialsWithDiagnostics()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int updatedCount = 0;

        Debug.Log($"[URP Updater] Found {guids.Length} materials. Starting check...");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null) continue;

            // URP 셰이더를 사용하는 머티리얼만 대상으로 합니다.
            if (!mat.shader.name.StartsWith("Universal Render Pipeline") && !mat.shader.name.StartsWith("URP/"))
            {
                continue;
            }

            Debug.Log($"[URP Updater] Processing '{mat.name}' with shader '{mat.shader.name}'");

            bool hasBaseMapProp = mat.HasProperty("_BaseMap");
            if (!hasBaseMapProp)
            {
                Debug.Log($"  -> Skipping: Does not have '_BaseMap' property.");
                continue;
            }

            Texture baseMap = mat.GetTexture("_BaseMap");
            if (baseMap != null)
            {
                Debug.Log($"  -> Skipping: '_BaseMap' is already assigned.");
                continue;
            }
            
            // _BaseMap이 비어있는 것을 확인했으니, 이제 _MainTex를 확인합니다.
            bool hasMainTexProp = mat.HasProperty("_MainTex");
            if (!hasMainTexProp)
            {
                Debug.Log($"  -> Skipping: Does not have '_MainTex' property.");
                continue;
            }

            Texture mainTex = mat.GetTexture("_MainTex");
            if (mainTex == null)
            {
                Debug.Log($"  -> Skipping: '_MainTex' property exists but is null.");
                continue;
            }

            // 모든 조건을 통과하면 텍스처를 복사합니다.
            Debug.Log($"  -> SUCCESS: Updating '{mat.name}'. Assigning '{mainTex.name}' to '_BaseMap'.");
            mat.SetTexture("_BaseMap", mainTex);
            EditorUtility.SetDirty(mat);
            updatedCount++;
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[URP Updater] Finished. Successfully updated {updatedCount} materials.");
        }
        else
        {
            Debug.Log($"[URP Updater] Finished. No materials required updating after checking all relevant materials.");
        }
    }
}