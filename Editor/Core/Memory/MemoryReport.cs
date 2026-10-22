using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns what the scope references into the Memory report: the runtime bytes of its textures, meshes, audio
    /// and font atlases, the levers that free RAM without touching the look (Read/Write copies, mipmaps on sprites,
    /// whole clips decoded on load, one typeface baked twice) and the largest of each. Every byte is runtime RAM.
    /// Meshes are counted one by one, the ones the scope references, never a whole model file: a model can carry
    /// dozens of meshes nothing uses. Pure: the Editor side only supplies the descriptions.
    /// </summary>
    internal static class MemoryReport
    {
        public const string ShippedScope = "Build scenes and Resources";

        public const string TextureReadableCheck = "texture.read-write";
        public const string SpriteMipmapsCheck = "texture.sprite-mipmaps";
        public const string MipmapsBlockCompressionCheck = "texture.npot-mipmaps-raw";
        public const string TopTextureCheck = "texture.top-runtime";
        public const string MeshReadableCheck = "mesh.read-write";
        public const string TopMeshCheck = "mesh.top-vertices";
        public const string DecompressOnLoadCheck = "audio.decompress-on-load";
        public const string DuplicateFontCheck = "font.duplicate-bake";
        public const string ReadableStaticAtlasCheck = "font.readable-static-atlas";

        public static IReadOnlyList<string> Scopes
        {
            get { return new[] { ShippedScope }; }
        }

        public static Report Build(string scope, IReadOnlyList<TextureMemoryDescription> textures, IReadOnlyList<MeshMemoryDescription> meshes, IReadOnlyList<AudioMemoryDescription> audio, IReadOnlyList<FontMemoryDescription> fonts, string activeTarget, MemoryRules rules = null)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new ArgumentException("A Memory report needs a scope.", nameof(scope));
            }

            textures = textures ?? Array.Empty<TextureMemoryDescription>();
            meshes = meshes ?? Array.Empty<MeshMemoryDescription>();
            audio = audio ?? Array.Empty<AudioMemoryDescription>();
            fonts = fonts ?? Array.Empty<FontMemoryDescription>();
            rules = rules ?? MemoryRules.Default;
            activeTarget = activeTarget ?? "";

            var report = new Report(Area.Memory, scope);
            long textureBytes = textures.Sum(texture => texture.RuntimeBytes);
            long meshBytes = meshes.Sum(mesh => mesh.RuntimeBytes);
            long audioBytes = audio.Sum(clip => clip.RuntimeBytes);
            long fontBytes = fonts.Sum(font => font.AtlasBytes);

            report.Metrics.Add(new Metric("memory.total-bytes", Measurement.Bytes(textureBytes + meshBytes + audioBytes + fontBytes), Budget.RuntimeRam, "Textures, meshes, audio and font atlases"));
            report.Metrics.Add(new Metric("textures.count", Measurement.Count(textures.Count), Budget.None, "Textures"));
            report.Metrics.Add(new Metric("textures.runtime-bytes", Measurement.Bytes(textureBytes), Budget.RuntimeRam, "Texture memory"));
            report.Metrics.Add(new Metric("meshes.count", Measurement.Count(meshes.Count), Budget.None, "Meshes referenced"));
            report.Metrics.Add(new Metric("meshes.vertices", Measurement.Count(meshes.Sum(mesh => (long)mesh.Vertices)), Budget.None, "Vertices in those meshes"));
            report.Metrics.Add(new Metric("meshes.runtime-bytes", Measurement.Bytes(meshBytes), Budget.RuntimeRam, "Mesh memory"));
            report.Metrics.Add(new Metric("audio.count", Measurement.Count(audio.Count), Budget.None, "Audio clips"));
            report.Metrics.Add(new Metric("audio.runtime-bytes", Measurement.Bytes(audioBytes), Budget.RuntimeRam, "Audio memory"));
            report.Metrics.Add(new Metric("fonts.count", Measurement.Count(fonts.Count), Budget.None, "TextMesh Pro font assets"));
            report.Metrics.Add(new Metric("fonts.atlas-bytes", Measurement.Bytes(fontBytes), Budget.RuntimeRam, "Font atlas memory"));

            DescribeTextures(report, textures, rules);
            DescribeMeshes(report, meshes, rules);
            DescribeAudio(report, audio, rules);
            DescribeFonts(report, fonts);

            report.Notes.Add("Runtime bytes are the Editor Profiler's figures for objects imported for " + (activeTarget.Length > 0 ? "the active build target, " + activeTarget : "the active build target") + ": the ranking holds, the absolute values are not a device measurement (the Editor keeps extra copies of some textures and of Read/Write data).");
            if (scope == ShippedScope)
            {
                report.Notes.Add("The scope is what the enabled build scenes and every Resources folder reference. Addressables and asset bundles are not followed, and assets loaded by path at run time only count when they sit under Resources.");
            }

            return report;
        }

        private static void DescribeTextures(Report report, IReadOnlyList<TextureMemoryDescription> textures, MemoryRules rules)
        {
            foreach (TextureMemoryDescription texture in textures)
            {
                if (texture.IsReadable && texture.RuntimeBytes > 0)
                {
                    var finding = new Finding(TextureReadableCheck, texture.Path)
                    {
                        Title = "Read/Write is on: a CPU copy of the pixels stays in memory next to the GPU one",
                        Severity = texture.RuntimeBytes >= rules.ReadableTextureWarningBytes ? Severity.Warning : Severity.Advice,
                        Budget = Budget.RuntimeRam,
                        Measured = Measurement.Bytes(texture.RuntimeBytes),
                        Projected = Measurement.Bytes(texture.RuntimeBytes / 2),
                        Verdict = "Turn Read/Write off unless a script reads or writes the pixels (GetPixels, SetPixels, a runtime atlas)",
                        FixId = "texture.read-write-off",
                    };
                    finding.Evidence.Add(new Evidence(texture.Path, 0, 0, "texture"));
                    report.Findings.Add(finding);
                }

                if (!texture.HasMipmaps || texture.Width <= 0 || texture.Height <= 0)
                {
                    continue;
                }

                bool npot = !TextureRules.IsPowerOfTwo(texture.Width) || !TextureRules.IsPowerOfTwo(texture.Height);
                bool raw = TextureRules.IsRawFormat(texture.StoredFormat);
                if (npot && raw)
                {
                    long projected = texture.RuntimeBytes > 0
                        ? (long)(texture.RuntimeBytes * (double)TextureRules.BlockBytes(texture.Width, texture.Height, true, false) / Math.Max(1, TextureRules.RawBytes(texture.Width, texture.Height, true, true)))
                        : 0;
                    var finding = new Finding(MipmapsBlockCompressionCheck, texture.Path)
                    {
                        Title = "Stored as " + texture.StoredFormat + ": " + Dimensions(texture) + " is not a power of two and has mipmaps, which keeps it uncompressed whatever the format override",
                        Severity = Severity.Warning,
                        Budget = Budget.RuntimeRam,
                        Measured = Measurement.Bytes(texture.RuntimeBytes),
                        Projected = projected > 0 ? Measurement.Bytes(projected) : (Measurement?)null,
                        Verdict = texture.IsSprite
                            ? "Turn mipmaps off: a sprite draws near its own size, and without mipmaps it block-compresses"
                            : "Turn mipmaps off, or resize it to a power of two so it block-compresses with them",
                        FixId = texture.IsSprite ? "texture.mipmaps-off" : null,
                    };
                    finding.Evidence.Add(new Evidence(texture.Path, 0, 0, "texture"));
                    report.Findings.Add(finding);
                }
                else if (texture.IsSprite)
                {
                    var finding = new Finding(SpriteMipmapsCheck, texture.Path)
                    {
                        Title = "A sprite with mipmaps: the chain adds a third to the texture, and UI and 2D sprites draw near their own size",
                        Severity = Severity.Advice,
                        Budget = Budget.RuntimeRam,
                        Measured = Measurement.Bytes(texture.RuntimeBytes),
                        Projected = Measurement.Bytes((long)(texture.RuntimeBytes * 0.75)),
                        Verdict = "Turn mipmaps off unless the sprite is drawn much smaller than its size, such as in a world-space minimap",
                        FixId = "texture.mipmaps-off",
                    };
                    finding.Evidence.Add(new Evidence(texture.Path, 0, 0, "texture"));
                    report.Findings.Add(finding);
                }
            }

            int rank = 0;
            foreach (TextureMemoryDescription texture in textures.Where(t => t.RuntimeBytes > 0).OrderByDescending(t => t.RuntimeBytes).ThenBy(t => t.Path, StringComparer.Ordinal).Take(Math.Max(0, rules.TopTextures)))
            {
                rank++;
                var finding = new Finding(TopTextureCheck, texture.Path)
                {
                    Title = "#" + rank.ToString(CultureInfo.InvariantCulture) + " of " + textures.Count.ToString("N0", CultureInfo.InvariantCulture) + " textures: " + Dimensions(texture) + " " + texture.StoredFormat
                            + (texture.HasMipmaps ? ", mipmaps" : "") + ", Max Size " + texture.MaxSize.ToString(CultureInfo.InvariantCulture) + (texture.HasPlatformOverride ? "" : " (no platform override)"),
                    Severity = Severity.Info,
                    Budget = Budget.RuntimeRam,
                    Measured = Measurement.Bytes(texture.RuntimeBytes),
                };
                finding.Evidence.Add(new Evidence(texture.Path, 0, 0, "texture"));
                report.Findings.Add(finding);
            }
        }

        private static void DescribeMeshes(Report report, IReadOnlyList<MeshMemoryDescription> meshes, MemoryRules rules)
        {
            foreach (MeshMemoryDescription mesh in meshes)
            {
                if (!mesh.IsReadable || mesh.Vertices < rules.ReadableMeshMinimumVertices)
                {
                    continue;
                }

                var finding = new Finding(MeshReadableCheck, mesh.Subject)
                {
                    Title = "Read/Write is on: " + mesh.Vertices.ToString("N0", CultureInfo.InvariantCulture) + " vertices are kept on the CPU as well as the GPU",
                    Severity = Severity.Advice,
                    Budget = Budget.RuntimeRam,
                    Measured = Measurement.Bytes(mesh.RuntimeBytes),
                    Projected = Measurement.Bytes(mesh.RuntimeBytes / 2),
                    Verdict = "Turn Read/Write off on the model unless a script reads the vertices or a MeshCollider is rebuilt at run time",
                    FixId = "model.read-write-off",
                };
                finding.Evidence.Add(new Evidence(mesh.Path, 0, 0, "model"));
                report.Findings.Add(finding);
            }

            int rank = 0;
            foreach (MeshMemoryDescription mesh in meshes.Where(m => m.Vertices > 0).OrderByDescending(m => m.Vertices).ThenBy(m => m.Subject, StringComparer.Ordinal).Take(Math.Max(0, rules.TopMeshes)))
            {
                rank++;
                var finding = new Finding(TopMeshCheck, mesh.Subject)
                {
                    Title = "#" + rank.ToString(CultureInfo.InvariantCulture) + " of " + meshes.Count.ToString("N0", CultureInfo.InvariantCulture) + " meshes: " + mesh.Vertices.ToString("N0", CultureInfo.InvariantCulture) + " vertices" + (mesh.IsReadable ? ", Read/Write" : ""),
                    Severity = Severity.Info,
                    Budget = Budget.RuntimeRam,
                    Measured = Measurement.Bytes(mesh.RuntimeBytes),
                };
                finding.Evidence.Add(new Evidence(mesh.Path, 0, 0, "model"));
                report.Findings.Add(finding);
            }
        }

        private static void DescribeAudio(Report report, IReadOnlyList<AudioMemoryDescription> audio, MemoryRules rules)
        {
            foreach (AudioMemoryDescription clip in audio)
            {
                if (!string.Equals(clip.LoadType, "DecompressOnLoad", StringComparison.Ordinal) || clip.LengthSeconds < rules.DecompressOnLoadSeconds)
                {
                    continue;
                }

                long measured = Math.Max(clip.RuntimeBytes, clip.DecodedBytes);
                bool music = clip.LengthSeconds >= 30;
                var finding = new Finding(DecompressOnLoadCheck, clip.Path)
                {
                    Title = clip.LengthSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s clip decompressed on load: the whole clip sits in memory as PCM",
                    Severity = Severity.Warning,
                    Budget = Budget.RuntimeRam,
                    Measured = Measurement.Bytes(measured),
                    Projected = Measurement.Bytes(measured / 10),
                    Verdict = music
                        ? "Stream it: music plays once at a time and needs only a small buffer"
                        : "Load it Compressed In Memory: it is decoded as it plays, at about a tenth of the memory",
                    FixId = "audio.load-type",
                };
                finding.Evidence.Add(new Evidence(clip.Path, 0, 0, "audio"));
                report.Findings.Add(finding);
            }
        }

        private static void DescribeFonts(Report report, IReadOnlyList<FontMemoryDescription> fonts)
        {
            // Same bake and same population mode: a static asset with a dynamic one of the same font as its fallback is
            // the intended pair (the dynamic one only receives the glyphs the static atlas lacks), not a duplicate.
            foreach (IGrouping<string, FontMemoryDescription> group in fonts.Where(f => f.SourceFontId.Length > 0 && f.AtlasCount > 0).GroupBy(f => f.BakeKey + (f.IsDynamic ? "|dynamic" : "|static"), StringComparer.Ordinal))
            {
                List<FontMemoryDescription> bakes = group.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
                if (bakes.Count < 2)
                {
                    continue;
                }

                FontMemoryDescription keep = bakes.OrderByDescending(f => f.IsDynamic).ThenBy(f => f.Path, StringComparer.Ordinal).First();
                foreach (FontMemoryDescription extra in bakes.Where(f => !ReferenceEquals(f, keep)))
                {
                    var finding = new Finding(DuplicateFontCheck, extra.Path)
                    {
                        Title = "The same typeface baked again with the same point size, padding and atlas as " + FileName(keep.Path) + ": its atlas is a second copy of the glyphs",
                        Severity = Severity.Advice,
                        Budget = Budget.RuntimeRam,
                        Measured = Measurement.Bytes(extra.AtlasBytes),
                        Projected = Measurement.Bytes(0),
                        Verdict = "Point its labels at " + FileName(keep.Path) + " with a material preset that carries this asset's outline and colours (assign the font first, then the preset)",
                    };
                    finding.Evidence.Add(new Evidence(extra.Path, 0, 0, "font"));
                    finding.Evidence.Add(new Evidence(keep.Path, 0, 0, "kept"));
                    report.Findings.Add(finding);
                }
            }

            foreach (FontMemoryDescription font in fonts)
            {
                if (font.IsDynamic || !font.AtlasReadable || font.AtlasBytes <= 0)
                {
                    continue;
                }

                var finding = new Finding(ReadableStaticAtlasCheck, font.Path)
                {
                    Title = "A static font atlas with Read/Write on: it never adds glyphs, so the CPU copy is never used",
                    Severity = Severity.Advice,
                    Budget = Budget.RuntimeRam,
                    Measured = Measurement.Bytes(font.AtlasBytes),
                    Projected = Measurement.Bytes(font.AtlasBytes / 2),
                    Verdict = "Turn Read/Write off on the atlas texture",
                };
                finding.Evidence.Add(new Evidence(font.Path, 0, 0, "font"));
                report.Findings.Add(finding);
            }
        }

        private static string Dimensions(TextureMemoryDescription texture)
        {
            return texture.Width.ToString(CultureInfo.InvariantCulture) + "×" + texture.Height.ToString(CultureInfo.InvariantCulture);
        }

        private static string FileName(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }
    }
}
