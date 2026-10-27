using UnityEditor;
using UnityEngine;

namespace InkRunner.BlackInkPrototype.Editor
{
    /// <summary>Importer settings only; the supplied image files are copied byte-for-byte.</summary>
    public static class EnvironmentAssetImporter
    {
        public const string TextureRoot = "Assets/Features/BlackInkPrototype/Textures/Environment";

        public static void ConfigureAll()
        {
            for (int i = 1; i <= 6; i++) Configure(TextureRoot + "/platform_" + i + ".png");
            for (int i = 1; i <= 2; i++) Configure(TextureRoot + "/obstacle_" + i + ".png");
        }

        public static Sprite[] LoadPlatformSprites() => LoadSprites("platform_", 6);
        public static Sprite[] LoadObstacleSprites() => LoadSprites("obstacle_", 2);

        private static Sprite[] LoadSprites(string prefix, int count)
        {
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(TextureRoot + "/" + prefix + (i + 1) + ".png");
            return result;
        }

        private static void Configure(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new System.IO.FileNotFoundException("Missing supplied environment image", path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                settings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.mipmapEnabled || !importer.alphaIsTransparency || importer.maxTextureSize != 2048 ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
