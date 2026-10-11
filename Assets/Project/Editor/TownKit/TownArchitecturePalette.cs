using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RetailEmpireTycoon.Editor.TownKit
{
    /// <summary>
    /// Owns only the new architecture kit's atlas and material. The original world palette is read, never rewritten.
    /// Width stays 32 because the native world shader classifies surfaces by their horizontal palette slot.
    /// </summary>
    internal static class TownArchitecturePalette
    {
        public const string Root = "Assets/Art/TownArchitecture";
        public const string MaterialPath = Root + "/Materials/TownArchitectureSurface.mat";
        public const string TexturePath = Root + "/Materials/TownArchitecturePalette.asset";
        public const string PngPath = Root + "/Materials/TownArchitecturePalette.png";
        public const string LegacyTexturePath = "Assets/Prefabs/TownWorld/TownPalette.asset";
        private const string LegacyMaterialPath = "Assets/Prefabs/TownWorld/TownPalette.mat";
        public const int AtlasWidth = 32;
        public const int AtlasHeight = 3;

        // Avoid the native shader's asphalt/grass/soil columns for new architectural paint and roofing.
        private static readonly int[] ExtensionColumns = { 0, 1, 2, 3, 4, 5, 6, 9 };
        private static readonly Color32[] ExtensionColors =
        {
            new Color32(214, 151, 124, 255), new Color32(138, 77, 61, 255),
            new Color32(236, 216, 180, 255), new Color32(185, 190, 183, 255),
            new Color32(235, 235, 224, 255), new Color32(62, 73, 83, 255),
            new Color32(142, 74, 51, 255), new Color32(135, 147, 149, 255)
        };

        public static Vector2 UV(TownColor color)
        {
            int index = (int)color;
            if (index >= 0 && index < AtlasWidth) return TownPalette.UV(color);
            int extension = index - AtlasWidth;
            if (extension < 0 || extension >= ExtensionColors.Length)
                throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown architectural palette colour.");
            return new Vector2((ExtensionColumns[extension] + .5f) / AtlasWidth, .5f / AtlasHeight);
        }

        /// <summary>Reads the actual displayed world colours, rather than the older source-kit defaults.</summary>
        public static Color32[] ReadLegacyColors()
        {
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyTexturePath);
            if (palette == null || !palette.isReadable || palette.width != AtlasWidth || palette.height != 1
                || palette.mipmapCount != 1 || palette.filterMode != FilterMode.Point)
                throw new InvalidOperationException("The existing readable 32 x 1 native world palette is required; it must not be regenerated.");
            return palette.GetPixels32();
        }

        /// <summary>Creates assets only below Root and only in the disposable isolated editor.</summary>
        public static Material CreateMaterial()
        {
            TownWorldSafety.RequireIsolatedEditor();
            if (TownPalette.Colors.Length != AtlasWidth || (int)TownColor.Charcoal != AtlasWidth - 1)
                throw new InvalidOperationException("Legacy town palette size or enum numbering changed.");
            var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(LegacyMaterialPath);
            var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyTexturePath);
            if (sourceMaterial == null || sourceMaterial.shader == null
                || sourceMaterial.shader.name != "Universal Render Pipeline/Town Surface"
                || sourceMaterial.GetTexture("_BaseMap") != sourceTexture)
                throw new InvalidOperationException("The existing native town material and its unchanged shader are required.");
            var legacy = ReadLegacyColors();
            var pixels = new Color32[AtlasWidth * AtlasHeight];
            for (int row = 0; row < AtlasHeight; row++)
                Array.Copy(legacy, 0, pixels, row * AtlasWidth, AtlasWidth);
            for (int index = 0; index < ExtensionColors.Length; index++)
                pixels[ExtensionColumns[index]] = ExtensionColors[index];

            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            // A native Texture2D asset keeps the exact non-power-of-two height; the PNG is an export copy.
            var draft = new Texture2D(AtlasWidth, AtlasHeight, TextureFormat.RGBA32, false, false)
            {
                name = "TownArchitecturePalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            Texture2D texture;
            try
            {
                draft.SetPixels32(pixels);
                draft.Apply(false, false);
                File.WriteAllBytes(PngPath, draft.EncodeToPNG());
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                if (texture == null) { AssetDatabase.CreateAsset(draft, TexturePath); texture = draft; }
                else { EditorUtility.CopySerialized(draft, texture); EditorUtility.SetDirty(texture); }
            }
            finally
            {
                if (!AssetDatabase.Contains(draft)) Object.DestroyImmediate(draft);
            }
            AssetDatabase.ImportAsset(PngPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(PngPath);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.isReadable = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
            var exported = AssetDatabase.LoadAssetAtPath<Texture2D>(PngPath);
            if (exported == null || exported.width != AtlasWidth || exported.height != AtlasHeight
                || !exported.GetPixels32().SequenceEqual(pixels))
                throw new InvalidOperationException("The exported architecture atlas changed size or colours during import.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(sourceMaterial); AssetDatabase.CreateAsset(material, MaterialPath); }
            else { material.shader = sourceMaterial.shader; material.CopyPropertiesFromMaterial(sourceMaterial); }
            material.name = "TownArchitectureSurface";
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_PaletteIndex", -1);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            if (!ReadLegacyColors().SequenceEqual(legacy))
                throw new InvalidOperationException("Architecture generation changed the original world palette.");
            return material;
        }
    }
}
