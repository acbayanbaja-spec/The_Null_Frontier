using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NullFrontier.EditorTools
{
    /// <summary>
    /// Import rules for the licensed art under Assets/Art. Runs on every
    /// (re)import, so the settings survive a Library wipe without anyone having
    /// to touch the inspector.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtAssets.Root, System.StringComparison.Ordinal)) return;

            var importer = (ModelImporter)assetImporter;
            bool isCharacter = assetPath.StartsWith(ArtAssets.CharacterDir, System.StringComparison.Ordinal);

            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.removeConstantScaleCurves = true;

            importer.importVisibility = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.generateSecondaryUV = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialSearch = ModelImporterMaterialSearch.Local;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;

            // Quaternius characters carry their palette in the FBX vertex-colour channel
            // and ship no textures. Unity 6 always imports that channel, but URP/Lit
            // ignores it, so ArtAssets routes character materials to a shader that reads it.

            // Kit pieces are static scenery: no rig, no clips.
            importer.animationType = isCharacter
                ? ModelImporterAnimationType.Generic
                : ModelImporterAnimationType.None;
            importer.importAnimation = isCharacter;
            importer.avatarSetup = isCharacter
                ? ModelImporterAvatarSetup.CreateFromThisModel
                : ModelImporterAvatarSetup.NoAvatar;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtAssets.Root, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;

            // UiFactory writes these as Sprite assets. Without this branch the rules
            // below reimport them as plain textures and every UI Image loses its sprite.
            if (assetPath.StartsWith(UiFactory.SpriteDir, System.StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 256;
                return;
            }

            // The planet maps are authored 2:1 and wrap all the way round the sphere, so
            // the kit defaults below (point filter, clamp, 1K) would visibly facet and
            // seam them. The day sheet keeps its 2K albedo; the cloud sheet is greyscale
            // and only its luminance is used, so it can stay at 1K.
            if (assetPath.StartsWith(ArtAssets.SpaceDir, System.StringComparison.Ordinal))
            {
                bool cloudCover = assetPath.EndsWith("Earth_Clouds.jpg", System.StringComparison.Ordinal);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 0;

                if (cloudCover)
                {
                    // No alpha channel in the sheet: the importer turns luminance into
                    // coverage, which is what makes the gaps between fronts transparent.
                    // Read as linear so a mid-grey cloud is half coverage, not a fifth.
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize = 1024;
                }
                else
                {
                    importer.sRGBTexture = true;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.maxTextureSize = 2048;
                }
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            // Flat-shaded kit art reads better with hard texel edges than bilinear blur.
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
