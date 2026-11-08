using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// One-time project setup: creates URP assets, assigns the render pipeline,
    /// and builds the Low/Medium/High/Ultra quality tiers. Runs when the project
    /// is first opened in the Editor; safe to re-run (idempotent).
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        private const string SettingsDir = "Assets/Settings";
        private const string PipelineAssetPath = SettingsDir + "/NullFrontier_URP.asset";
        private const string RendererDataPath = SettingsDir + "/NullFrontier_Renderer.asset";

        static ProjectBootstrap()
        {
            EditorApplication.delayCall += EnsureConfigured;
        }

        private static void EnsureConfigured()
        {
            // Repair path: a renderer created by CreateInstance has no PostProcessData, and
            // URP silently skips post-processing when it is null. This must run even when a
            // pipeline is already assigned, so existing projects get repaired too.
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                EnsureFolder(SettingsDir);
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }
            AssignPostProcessData(rendererData);

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            BuildQualityTiers(pipelineAsset);

            AssetDatabase.SaveAssets();
            Debug.Log("[NullFrontier] URP pipeline, post resources and quality tiers configured.");
        }

        private static void AssignPostProcessData(UniversalRendererData rendererData)
        {
            const string path =
                "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";
            var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(path);
            if (data == null)
            {
                Debug.LogWarning("[NullFrontier] URP PostProcessData asset not found.");
                return;
            }

            var serialized = new SerializedObject(rendererData);
            SerializedProperty property = serialized.FindProperty("postProcessData");
            if (property == null || property.objectReferenceValue == data) return;

            property.objectReferenceValue = data;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        private static void BuildQualityTiers(UniversalRenderPipelineAsset baseAsset)
        {
            // Unity already ships the quality levels, so configure each one instead of
            // creating them. Every level gets its own URP asset, because render scale and
            // shadows live on the pipeline asset rather than on QualitySettings.
            string[] levels = QualitySettings.names;
            int activeLevel = QualitySettings.GetQualityLevel();
            for (int i = 0; i < levels.Length; i++)
            {
                float fraction = levels.Length > 1 ? (float)i / (levels.Length - 1) : 1f;
                UniversalRenderPipelineAsset tier = GetOrCreateTierAsset(baseAsset, i);
                if (tier == null) continue;

                tier.renderScale = Mathf.Lerp(0.6f, 1f, fraction);
                tier.shadowDistance = baseAsset.shadowDistance * Mathf.Lerp(0.4f, 1f, fraction);
                tier.msaaSampleCount = fraction >= 0.6f ? 4 : fraction >= 0.2f ? 2 : 1;

                // Shadow toggles are internal-set only, so write the serialized fields.
                var serialized = new SerializedObject(tier);
                serialized.FindProperty("m_MainLightShadowsSupported").boolValue = fraction >= 0.2f;
                serialized.FindProperty("m_SoftShadowsSupported").boolValue = fraction >= 0.6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(tier);

                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = tier;
                QualitySettings.vSyncCount = 0; // target frame rate is driven by Application.targetFrameRate
            }

            // Leave the user's tier selected; the loop above only needs each slot active
            // long enough for QualitySettings to record its pipeline.
            QualitySettings.SetQualityLevel(activeLevel, false);
        }

        private static UniversalRenderPipelineAsset GetOrCreateTierAsset(UniversalRenderPipelineAsset baseAsset, int index)
        {
            string path = $"{SettingsDir}/NullFrontier_URP_Tier{index}.asset";
            UniversalRenderPipelineAsset existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (existing != null) return existing;
            return AssetDatabase.CopyAsset(PipelineAssetPath, path)
                ? AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path)
                : baseAsset;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
                var leaf = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }
    }
}
