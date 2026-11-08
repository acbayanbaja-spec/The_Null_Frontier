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
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                return;
            }

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                EnsureFolder(SettingsDir);
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
            }

            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipelineAsset == null)
            {
                pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            BuildQualityTiers(pipelineAsset);

            AssetDatabase.SaveAssets();
            Debug.Log("[NullFrontier] URP pipeline and quality tiers configured.");
        }

        private static void BuildQualityTiers(UniversalRenderPipelineAsset baseAsset)
        {
            // Unity already ships the quality levels, so configure each one instead of
            // creating them. Every level gets its own URP asset, because render scale and
            // shadows live on the pipeline asset rather than on QualitySettings.
            string[] levels = QualitySettings.names;
            for (int i = 0; i < levels.Length; i++)
            {
                float fraction = levels.Length > 1 ? (float)i / (levels.Length - 1) : 1f;
                UniversalRenderPipelineAsset tier = GetOrCreateTierAsset(baseAsset, i);

                tier.renderScale = Mathf.Lerp(0.6f, 1f, fraction);
                tier.shadowDistance = baseAsset.shadowDistance * Mathf.Lerp(0.4f, 1f, fraction);
                tier.msaaSampleCount = fraction >= 0.6f ? 4 : fraction >= 0.2f ? 2 : 1;

                // Shadow toggles are internal-set only, so write the serialized fields.
                var serialized = new SerializedObject(tier);
                serialized.FindProperty("m_MainLightShadowsSupported").boolValue = fraction >= 0.2f;
                serialized.FindProperty("m_SoftShadowsSupported").boolValue = fraction >= 0.6f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                QualitySettings.SetQualityLevel(i, true);
                QualitySettings.renderPipeline = tier;
                QualitySettings.vSyncCount = 0; // target frame rate is driven by Application.targetFrameRate
            }
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
