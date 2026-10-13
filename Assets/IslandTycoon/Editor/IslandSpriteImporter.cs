using UnityEditor;
using UnityEngine;

namespace IslandTycoon.EditorTools
{
    /// <summary>
    /// Imports every texture under Resources/IslandTycoon/Sprites as a ready-to-use sprite.
    /// Lives in its own assembly so it works even while game scripts have compile errors.
    /// </summary>
    public class IslandSpriteImporter : AssetPostprocessor
    {
        public const string Root = "Assets/IslandTycoon/Resources/IslandTycoon/Sprites/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.maxTextureSize = 2048;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spritePixelsPerUnit = 100;
            bool grounded = assetPath.Contains("/Buildings/") || assetPath.Contains("/Characters/");
            s.spriteAlignment = (int)(grounded ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(s);

            // 9-slice borders (left, bottom, right, top) for stretchable frames
            if (assetPath.EndsWith("UI/Kit/panel_big.png")) ti.spriteBorder = new Vector4(290, 300, 290, 300);
            else if (assetPath.EndsWith("UI/Kit/bar_empty.png")) ti.spriteBorder = new Vector4(195, 48, 70, 62);
            else if (assetPath.EndsWith("UI/Kit/panel_info.png")) ti.spriteBorder = new Vector4(140, 120, 140, 120);
            else ti.spriteBorder = Vector4.zero;
        }
    }
}
