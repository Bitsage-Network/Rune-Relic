using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RuneRelic.Art
{
    /// <summary>
    /// Helper for importing and configuring art assets.
    /// Provides templates for player forms, runes, shrines, and effects.
    /// </summary>
    public static class ArtAssetImporter
    {
        // =====================================================================
        // Asset Paths
        // =====================================================================

        public const string PREFABS_PATH = "Assets/Prefabs/";
        public const string MATERIALS_PATH = "Assets/Materials/";
        public const string MODELS_PATH = "Assets/Models/";
        public const string TEXTURES_PATH = "Assets/Textures/";
        public const string VFX_PATH = "Assets/VFX/";

        // =====================================================================
        // Player Form Templates
        // =====================================================================

        /// <summary>
        /// Expected player form prefab structure.
        /// </summary>
        public static readonly PlayerFormTemplate[] FormTemplates = {
            new PlayerFormTemplate {
                Name = "Spark",
                BaseScale = 1f,
                ExpectedPolygons = 200,
                RecommendedColors = new[] { "#E5F0FF", "#99C2FF", "#3388FF" },
                Description = "Small wisp of energy. Simple geometric shape with glow effect."
            },
            new PlayerFormTemplate {
                Name = "Glyph",
                BaseScale = 1.4f,
                ExpectedPolygons = 400,
                RecommendedColors = new[] { "#99CCFF", "#6699FF", "#3366CC" },
                Description = "Crystalline rune shape. More defined geometry with arcane symbols."
            },
            new PlayerFormTemplate {
                Name = "Ward",
                BaseScale = 2f,
                ExpectedPolygons = 600,
                RecommendedColors = new[] { "#66E599", "#33CC66", "#009944" },
                Description = "Protective sentinel form. Armored appearance with shield elements."
            },
            new PlayerFormTemplate {
                Name = "Arcane",
                BaseScale = 2.8f,
                ExpectedPolygons = 800,
                RecommendedColors = new[] { "#B366FF", "#9933FF", "#6600CC" },
                Description = "Powerful mage form. Flowing energy tendrils and mystical orbs."
            },
            new PlayerFormTemplate {
                Name = "Ancient",
                BaseScale = 4f,
                ExpectedPolygons = 1000,
                RecommendedColors = new[] { "#FFCC33", "#FF9900", "#CC6600" },
                Description = "Ultimate form. Majestic presence with crown/halo elements."
            }
        };

        /// <summary>
        /// Expected rune prefab structure.
        /// </summary>
        public static readonly RuneTemplate[] RuneTemplates = {
            new RuneTemplate {
                Name = "Wisdom",
                Color = "#4D80FF",
                GlowColor = "#99BBFF",
                Shape = "Book/scroll shape with floating pages",
                PointValue = 10
            },
            new RuneTemplate {
                Name = "Power",
                Color = "#FF4D4D",
                GlowColor = "#FF9999",
                Shape = "Sword/fist shape with energy crackling",
                PointValue = 15
            },
            new RuneTemplate {
                Name = "Speed",
                Color = "#FFFF4D",
                GlowColor = "#FFFFCC",
                Shape = "Lightning bolt/wing shape with motion blur",
                PointValue = 12
            },
            new RuneTemplate {
                Name = "Shield",
                Color = "#4DFF80",
                GlowColor = "#99FFBB",
                Shape = "Shield/barrier shape with protective aura",
                PointValue = 8
            },
            new RuneTemplate {
                Name = "Arcane",
                Color = "#B34DFF",
                GlowColor = "#D699FF",
                Shape = "Mysterious orb with swirling patterns",
                PointValue = 25
            },
            new RuneTemplate {
                Name = "Chaos",
                Color = "#FFFFFF",
                GlowColor = "Rainbow cycle",
                Shape = "Unstable geometric shape, constantly morphing",
                PointValue = 50
            }
        };

        /// <summary>
        /// Expected shrine prefab structure.
        /// </summary>
        public static readonly ShrineTemplate[] ShrineTemplates = {
            new ShrineTemplate {
                Name = "Wisdom",
                Color = "#4D80FF",
                Style = "Ancient pedestal with floating books/scrolls",
                CaptureRadius = 3f
            },
            new ShrineTemplate {
                Name = "Power",
                Color = "#FF4D4D",
                Style = "War altar with weapon motifs",
                CaptureRadius = 3f
            },
            new ShrineTemplate {
                Name = "Speed",
                Color = "#FFFF4D",
                Style = "Wind shrine with swirling vortex",
                CaptureRadius = 3f
            },
            new ShrineTemplate {
                Name = "Shield",
                Color = "#4DFF80",
                Style = "Guardian statue with protective dome",
                CaptureRadius = 3f
            }
        };

#if UNITY_EDITOR
        /// <summary>
        /// Create folder structure for art assets.
        /// </summary>
        [MenuItem("RuneRelic/Setup Art Folders")]
        public static void SetupArtFolders()
        {
            string[] folders = {
                "Assets/Prefabs",
                "Assets/Prefabs/Players",
                "Assets/Prefabs/Players/Forms",
                "Assets/Prefabs/Runes",
                "Assets/Prefabs/Shrines",
                "Assets/Prefabs/VFX",
                "Assets/Materials",
                "Assets/Materials/Players",
                "Assets/Materials/Runes",
                "Assets/Materials/Shrines",
                "Assets/Materials/Environment",
                "Assets/Models",
                "Assets/Models/Players",
                "Assets/Models/Runes",
                "Assets/Models/Shrines",
                "Assets/Models/Environment",
                "Assets/Textures",
                "Assets/Textures/Players",
                "Assets/Textures/Runes",
                "Assets/Textures/UI",
                "Assets/VFX",
                "Assets/VFX/Particles",
                "Assets/VFX/Trails",
                "Assets/Audio",
                "Assets/Audio/Music",
                "Assets/Audio/SFX"
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = System.IO.Path.GetDirectoryName(folder);
                    string newFolder = System.IO.Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, newFolder);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[ArtAssetImporter] Art folder structure created!");
        }

        /// <summary>
        /// Create placeholder materials for all forms.
        /// </summary>
        [MenuItem("RuneRelic/Create Placeholder Materials")]
        public static void CreatePlaceholderMaterials()
        {
            // Find the PlayerForm shader
            Shader playerShader = Shader.Find("RuneRelic/PlayerForm");
            if (playerShader == null)
            {
                playerShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            // Create form materials
            for (int i = 0; i < FormTemplates.Length; i++)
            {
                var template = FormTemplates[i];
                string path = $"Assets/Materials/Players/Mat_{template.Name}.mat";

                if (!System.IO.File.Exists(path))
                {
                    Material mat = new Material(playerShader);
                    mat.name = $"Mat_{template.Name}";

                    if (ColorUtility.TryParseHtmlString(template.RecommendedColors[0], out Color color))
                    {
                        mat.SetColor("_Color", color);
                    }

                    mat.SetFloat("_FormLevel", i);

                    AssetDatabase.CreateAsset(mat, path);
                }
            }

            // Create rune materials
            Shader runeShader = Shader.Find("RuneRelic/RuneGlow");
            if (runeShader == null)
            {
                runeShader = Shader.Find("Universal Render Pipeline/Lit");
            }

            foreach (var template in RuneTemplates)
            {
                string path = $"Assets/Materials/Runes/Mat_{template.Name}Rune.mat";

                if (!System.IO.File.Exists(path))
                {
                    Material mat = new Material(runeShader);
                    mat.name = $"Mat_{template.Name}Rune";

                    if (ColorUtility.TryParseHtmlString(template.Color, out Color color))
                    {
                        mat.SetColor("_Color", color);
                        mat.SetColor("_GlowColor", color);
                    }

                    if (template.Name == "Chaos")
                    {
                        mat.SetFloat("_RainbowMode", 1);
                    }

                    AssetDatabase.CreateAsset(mat, path);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[ArtAssetImporter] Placeholder materials created!");
        }

        /// <summary>
        /// Generate asset specification document.
        /// </summary>
        [MenuItem("RuneRelic/Export Art Spec Document")]
        public static void ExportArtSpecDocument()
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("# Rune Relic Art Asset Specifications");
            sb.AppendLine();
            sb.AppendLine("## Style Guide");
            sb.AppendLine("- **Style**: Low-poly with subtle glow effects");
            sb.AppendLine("- **Polygon Budget**: 200-1000 tris per form");
            sb.AppendLine("- **Texture Size**: 256x256 or 512x512 max");
            sb.AppendLine("- **Color Palette**: Neon on dark backgrounds");
            sb.AppendLine();

            sb.AppendLine("## Player Forms");
            sb.AppendLine();
            foreach (var form in FormTemplates)
            {
                sb.AppendLine($"### {form.Name}");
                sb.AppendLine($"- **Scale**: {form.BaseScale}x");
                sb.AppendLine($"- **Polygons**: ~{form.ExpectedPolygons}");
                sb.AppendLine($"- **Colors**: {string.Join(", ", form.RecommendedColors)}");
                sb.AppendLine($"- **Description**: {form.Description}");
                sb.AppendLine();
            }

            sb.AppendLine("## Runes");
            sb.AppendLine();
            foreach (var rune in RuneTemplates)
            {
                sb.AppendLine($"### {rune.Name} Rune");
                sb.AppendLine($"- **Color**: {rune.Color}");
                sb.AppendLine($"- **Glow**: {rune.GlowColor}");
                sb.AppendLine($"- **Shape**: {rune.Shape}");
                sb.AppendLine($"- **Points**: {rune.PointValue}");
                sb.AppendLine();
            }

            sb.AppendLine("## Shrines");
            sb.AppendLine();
            foreach (var shrine in ShrineTemplates)
            {
                sb.AppendLine($"### {shrine.Name} Shrine");
                sb.AppendLine($"- **Color**: {shrine.Color}");
                sb.AppendLine($"- **Style**: {shrine.Style}");
                sb.AppendLine($"- **Radius**: {shrine.CaptureRadius}m");
                sb.AppendLine();
            }

            string path = "Assets/ART_SPEC.md";
            System.IO.File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();

            Debug.Log($"[ArtAssetImporter] Art spec exported to {path}");
        }
#endif
    }

    [System.Serializable]
    public struct PlayerFormTemplate
    {
        public string Name;
        public float BaseScale;
        public int ExpectedPolygons;
        public string[] RecommendedColors;
        public string Description;
    }

    [System.Serializable]
    public struct RuneTemplate
    {
        public string Name;
        public string Color;
        public string GlowColor;
        public string Shape;
        public int PointValue;
    }

    [System.Serializable]
    public struct ShrineTemplate
    {
        public string Name;
        public string Color;
        public string Style;
        public float CaptureRadius;
    }
}
