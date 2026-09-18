using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns the last build, the textures and the models into the Build Size report: what the build ships
    /// and of what, the packed assets and folders ranked, the textures Unity stored raw although compression
    /// was asked for, the large meshes with compression off, and the output audit of native libraries per
    /// ABI, debug symbols, the scripting backend and the stripping level. Every byte is download size.
    /// Pure: the Editor side only supplies the descriptions.
    /// </summary>
    internal static class BuildSizeReport
    {
        public const string LastBuildScope = "Last build";
        public const string ProjectScope = "Project";
        public const string ReportPath = "Library/LastBuild.buildreport";

        public const string ReportMissingCheck = "build.report-missing";
        public const string NotDetailedCheck = "build.assets-not-detailed";
        public const string StrippingLowCheck = "build.stripping-low";
        public const string MonoBackendCheck = "build.mono-backend";
        public const string NativeOutlierCheck = "build.native-library-outlier";
        public const string UnexpectedFileCheck = "build.unexpected-file";
        public const string DebugSymbolsCheck = "build.debug-symbols";
        public const string ResourcesHeavyCheck = "build.resources-heavy";
        public const string TopAssetCheck = "build.top-asset";
        public const string TopFolderCheck = "build.top-folder";
        public const string TextureUncompressedCheck = "texture.stored-uncompressed";
        public const string MeshUncompressedCheck = "mesh.uncompressed";

        private const long MinimumResourcesBytes = 1024 * 1024;

        private static readonly string[] MobilePlatforms = { "Android", "iOS" };
        private static readonly string[] LowStrippingLevels = { "Disabled", "Minimal", "Low" };

        public static IReadOnlyList<string> Scopes
        {
            get { return new[] { LastBuildScope, ProjectScope }; }
        }

        /// <param name="scope"><see cref="LastBuildScope"/> or <see cref="ProjectScope"/>.</param>
        /// <param name="build">The last build, or null when the machine has none; read only in the last-build scope.</param>
        /// <param name="textures">The textures in scope, as imported for the active target.</param>
        /// <param name="models">The models in scope.</param>
        /// <param name="activeTarget">The Editor's active build target, which the imports were made for.</param>
        /// <param name="rules">Thresholds; null for the defaults.</param>
        public static Report Build(string scope, BuildDescription build, IReadOnlyList<TextureDescription> textures, IReadOnlyList<ModelDescription> models, string activeTarget, BuildSizeRules rules = null)
        {
            if (scope != LastBuildScope && scope != ProjectScope)
            {
                throw new ArgumentException("Unknown scope '" + scope + "'.", nameof(scope));
            }

            textures = textures ?? Array.Empty<TextureDescription>();
            models = models ?? Array.Empty<ModelDescription>();
            rules = rules ?? BuildSizeRules.Default;
            activeTarget = activeTarget ?? "";

            var report = new Report(Area.BuildSize, scope);
            bool lastBuild = scope == LastBuildScope;

            if (!lastBuild)
            {
                build = null;
            }

            if (build != null)
            {
                DescribeBuild(report, build, rules);
            }
            else if (lastBuild)
            {
                report.Findings.Add(ReportMissingFinding());
            }

            DescribeTextures(report, textures, rules);
            DescribeModels(report, models, rules);

            if (build != null)
            {
                report.Notes.Add("Sizes come from the " + build.Platform + " build of " + build.BuiltAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " at " + build.OutputPath + "; the build's own total counts intermediates such as IL2CPP sources, only the shipped size is download size.");
                if (!build.HasPackedAssets)
                {
                    report.Notes.Add("The build recorded no per-asset sizes, so the texture and mesh rows cover the whole project rather than what shipped.");
                }
            }

            if (!lastBuild)
            {
                report.Notes.Add("Project scope reads every texture and model under Assets, shipped or not; a build ships only what it references.");
            }

            if (activeTarget.Length > 0)
            {
                report.Notes.Add("Stored texture formats and mesh settings are read as imported for the active build target, " + activeTarget + (build != null && build.Platform.Length > 0 && !string.Equals(build.Platform, activeTarget, StringComparison.Ordinal) ? ", which is not the platform of the last build" : "") + ".");
            }

            report.Notes.Add("Projected sizes are arithmetic: 8 or 4 bits per pixel for block-compressed textures, and the mesh compression ratios measured on one modular pack.");
            return report;
        }

        private static void DescribeBuild(Report report, BuildDescription build, BuildSizeRules rules)
        {
            long shipped = 0;
            long nativeBytes = 0;
            int nativeCount = 0;
            long managedBytes = 0;
            int managedCount = 0;
            long assetData = 0;
            long resourcesPayload = 0;
            long streaming = 0;
            var abis = new SortedDictionary<string, long>(StringComparer.Ordinal);
            var librariesByName = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var unexpected = new List<BuildFileDescription>();
            var symbols = new List<BuildFileDescription>();

            foreach (BuildFileDescription file in build.Files)
            {
                bool shipping = BuildOutputRules.IsShipped(file.Path, build.OutputPath);
                if (shipping)
                {
                    shipped += file.Bytes;
                }

                if (BuildOutputRules.IsMarkedDoNotShip(file.Path))
                {
                    continue;
                }

                if (BuildOutputRules.IsDebugSymbols(file.Path, file.Role))
                {
                    if (shipping)
                    {
                        symbols.Add(file);
                    }

                    continue;
                }

                string abi = BuildOutputRules.AbiOf(file.Path);
                if (abi != null)
                {
                    long bytes;
                    abis.TryGetValue(abi, out bytes);
                    abis[abi] = bytes + file.Bytes;
                    if (BuildOutputRules.IsSharedLibrary(file.Path))
                    {
                        List<string> holders;
                        if (!librariesByName.TryGetValue(file.FileName, out holders))
                        {
                            holders = new List<string>();
                            librariesByName.Add(file.FileName, holders);
                        }

                        holders.Add(abi);
                    }
                    else
                    {
                        unexpected.Add(file);
                    }
                }

                if (BuildOutputRules.IsNativeLibrary(file.Path))
                {
                    nativeBytes += file.Bytes;
                    nativeCount++;
                }
                else if (BuildOutputRules.IsManagedAssembly(file.Path))
                {
                    managedBytes += file.Bytes;
                    managedCount++;
                }
                else if (BuildOutputRules.IsAssetData(file.Path, file.Role))
                {
                    assetData += file.Bytes;
                    if (BuildOutputRules.IsResourcesPayload(file.Path))
                    {
                        resourcesPayload += file.Bytes;
                    }
                }
                else if (BuildOutputRules.IsStreamingAsset(file.Path))
                {
                    streaming += file.Bytes;
                }
            }

            long packedBytes = 0;
            long packedResources = 0;
            var byAsset = new Dictionary<string, PackedTotal>(StringComparer.Ordinal);
            var byFolder = new Dictionary<string, PackedTotal>(StringComparer.Ordinal);
            foreach (PackedAssetDescription packed in build.PackedAssets)
            {
                packedBytes += packed.Bytes;
                if (packed.SourcePath.Length == 0)
                {
                    continue;
                }

                if (packed.SourcePath.IndexOf("/Resources/", StringComparison.Ordinal) >= 0)
                {
                    packedResources += packed.Bytes;
                }

                Accumulate(byAsset, packed.SourcePath, packed.TypeName, packed.Bytes);
            }

            foreach (KeyValuePair<string, PackedTotal> asset in byAsset)
            {
                Accumulate(byFolder, FolderOf(asset.Key), "", asset.Value.Bytes);
            }

            report.Metrics.Add(new Metric("build.shipped-bytes", Measurement.Bytes(shipped), Budget.Download, "Shipped build"));
            report.Metrics.Add(new Metric("build.total-bytes", Measurement.Bytes(build.TotalBytes), Budget.None, "Everything the build touched"));
            report.Metrics.Add(new Metric("build.files", Measurement.Count(build.Files.Count), Budget.None, "Files in the build report"));
            report.Metrics.Add(new Metric("build.native-library-bytes", Measurement.Bytes(nativeBytes), Budget.Download, "Native libraries, all ABIs"));
            report.Metrics.Add(new Metric("build.native-libraries", Measurement.Count(nativeCount), Budget.None, "Native library files"));
            report.Metrics.Add(new Metric("build.abis", Measurement.Count(abis.Count), Budget.None, "ABIs built"));
            report.Metrics.Add(new Metric("build.managed-bytes", Measurement.Bytes(managedBytes), Budget.Download, "Managed assemblies" + (string.Equals(build.ScriptingBackend, "IL2CPP", StringComparison.OrdinalIgnoreCase) ? " (IL2CPP input)" : "")));
            report.Metrics.Add(new Metric("build.managed-assemblies", Measurement.Count(managedCount), Budget.None, "Managed assembly files"));
            report.Metrics.Add(new Metric("build.asset-data-bytes", Measurement.Bytes(assetData), Budget.Download, "Serialized asset data"));
            report.Metrics.Add(new Metric("resources.bytes", Measurement.Bytes(build.HasPackedAssets ? packedResources : resourcesPayload), Budget.Download, "Under Resources folders"));
            report.Metrics.Add(new Metric("streaming-assets.bytes", Measurement.Bytes(streaming), Budget.Download, "StreamingAssets"));
            report.Metrics.Add(new Metric("assets.packed", Measurement.Count(byAsset.Count), Budget.None, "Assets with a recorded size"));
            report.Metrics.Add(new Metric("assets.packed-bytes", Measurement.Bytes(packedBytes), Budget.Download, "Packed asset data"));

            if (!build.HasPackedAssets)
            {
                report.Findings.Add(NotDetailedFinding(build, shipped));
            }

            if (string.Equals(build.ScriptingBackend, "IL2CPP", StringComparison.OrdinalIgnoreCase) && Array.IndexOf(LowStrippingLevels, build.StrippingLevel) >= 0)
            {
                report.Findings.Add(StrippingLowFinding(build, nativeBytes));
            }

            if (string.Equals(build.ScriptingBackend, "Mono", StringComparison.OrdinalIgnoreCase) && Array.IndexOf(MobilePlatforms, build.Platform) >= 0)
            {
                report.Findings.Add(MonoBackendFinding(build, managedBytes));
            }

            if (abis.Count > 1)
            {
                foreach (KeyValuePair<string, List<string>> entry in librariesByName)
                {
                    if (entry.Value.Count < abis.Count)
                    {
                        report.Findings.Add(NativeOutlierFinding(entry.Key, entry.Value, abis.Keys));
                    }
                }
            }

            foreach (BuildFileDescription file in unexpected)
            {
                report.Findings.Add(UnexpectedFileFinding(file));
            }

            foreach (BuildFileDescription file in symbols)
            {
                report.Findings.Add(DebugSymbolsFinding(file));
            }

            long payload = build.HasPackedAssets ? packedBytes : assetData;
            long resources = build.HasPackedAssets ? packedResources : resourcesPayload;
            if (payload > 0 && resources >= MinimumResourcesBytes && (double)resources / payload >= rules.ResourcesShareWarning)
            {
                report.Findings.Add(ResourcesHeavyFinding(resources, payload));
            }

            AddRanking(report, byAsset, rules.TopAssets, TopAssetCheck, "packed assets", "asset", "object", "objects");
            AddRanking(report, byFolder, rules.TopFolders, TopFolderCheck, "folders", "folder", "asset", "assets");

            if (abis.Count > 0)
            {
                var parts = new List<string>();
                foreach (KeyValuePair<string, long> abi in abis)
                {
                    parts.Add(abi.Key + " " + Units.FormatBytes(abi.Value));
                }

                report.Notes.Add("Native code per ABI: " + string.Join(", ", parts) + ". A store serves each device one ABI, so the download carries one of them.");
            }
        }

        private static void DescribeTextures(Report report, IReadOnlyList<TextureDescription> textures, BuildSizeRules rules)
        {
            int raw = 0;
            long rawBytes = 0;
            long recoverable = 0;
            var findings = new List<Finding>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TextureDescription texture in textures)
            {
                if (!seen.Add(texture.Path))
                {
                    continue;
                }

                TextureDiagnosis diagnosis = TextureRules.Diagnose(texture);
                if (diagnosis == null)
                {
                    continue;
                }

                raw++;
                long measured = texture.ShippedBytes >= 0 ? texture.ShippedBytes : diagnosis.CurrentBytes;
                long projected = diagnosis.Strategy == UncompressedStrategy.ManualReexport || diagnosis.CurrentBytes == 0
                    ? measured
                    : (long)(measured * ((double)diagnosis.ProjectedBytes / diagnosis.CurrentBytes));
                rawBytes += measured;
                recoverable += Math.Max(0, measured - projected);
                findings.Add(TextureFinding(texture, diagnosis, measured, projected, rules));
            }

            report.Metrics.Add(new Metric("textures.count", Measurement.Count(seen.Count), Budget.None, "Textures read"));
            report.Metrics.Add(new Metric("textures.stored-uncompressed", Measurement.Count(raw), Budget.None, "Stored raw although compression was requested"));
            report.Metrics.Add(new Metric("textures.uncompressed-bytes", Measurement.Bytes(rawBytes), Budget.Download, "Raw textures"));
            report.Metrics.Add(new Metric("textures.recoverable-bytes", Measurement.Bytes(recoverable), Budget.Download, "Recoverable by compressing them"));

            findings.Sort((a, b) => Saving(b).CompareTo(Saving(a)));
            report.Findings.AddRange(findings);
        }

        private static void DescribeModels(Report report, IReadOnlyList<ModelDescription> models, BuildSizeRules rules)
        {
            int uncompressed = 0;
            long recoverable = 0;
            var findings = new List<Finding>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (ModelDescription model in models)
            {
                if (!seen.Add(model.Path))
                {
                    continue;
                }

                if (model.Compression != MeshCompressionLevel.Off || model.IsSkinned || model.Bytes < rules.MeshMinimumBytes || model.MeshCount == 0)
                {
                    continue;
                }

                uncompressed++;
                long projected = MeshRules.ProjectedBytes(model.Bytes, MeshCompressionLevel.Medium);
                recoverable += model.Bytes - projected;
                findings.Add(MeshFinding(model, projected));
            }

            report.Metrics.Add(new Metric("models.count", Measurement.Count(seen.Count), Budget.None, "Models read"));
            report.Metrics.Add(new Metric("models.uncompressed", Measurement.Count(uncompressed), Budget.None, "Large models with mesh compression off"));
            report.Metrics.Add(new Metric("models.recoverable-bytes", Measurement.Bytes(recoverable), Budget.Download, "Recoverable with Medium mesh compression"));

            findings.Sort((a, b) => Saving(b).CompareTo(Saving(a)));
            report.Findings.AddRange(findings);
        }

        private static void AddRanking(Report report, Dictionary<string, PackedTotal> totals, int count, string check, string what, string noun, string one, string many)
        {
            var rows = new List<KeyValuePair<string, PackedTotal>>(totals);
            rows.Sort((a, b) =>
            {
                int byBytes = b.Value.Bytes.CompareTo(a.Value.Bytes);
                return byBytes != 0 ? byBytes : string.CompareOrdinal(a.Key, b.Key);
            });

            for (int i = 0; i < rows.Count && i < count; i++)
            {
                PackedTotal total = rows[i].Value;
                var finding = new Finding(check, rows[i].Key)
                {
                    Title = "#" + (i + 1).ToString(CultureInfo.InvariantCulture) + " of " + rows.Count.ToString("N0", CultureInfo.InvariantCulture) + " " + what + " · " + (total.TypeName.Length > 0 ? total.TypeName : Plural(total.Entries, one, many)),
                    Severity = Severity.Info,
                    Budget = Budget.Download,
                    Measured = Measurement.Bytes(total.Bytes),
                };
                finding.Evidence.Add(new Evidence(rows[i].Key, 0, 0, noun));
                report.Findings.Add(finding);
            }
        }

        private static Finding ReportMissingFinding()
        {
            var finding = new Finding(ReportMissingCheck, ReportPath)
            {
                Title = "No build report on this machine; the last build was made elsewhere or never",
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Verdict = "Build the player once from this machine with BuildOptions.DetailedBuildReport, so the report records the size of every asset, then scan again",
            };
            finding.Evidence.Add(new Evidence(ReportPath, 0, 0, "report"));
            return finding;
        }

        /// <summary>Measures the shipped size so the row sorts above the per-asset rows: it is the one thing the build cannot break down.</summary>
        private static Finding NotDetailedFinding(BuildDescription build, long shipped)
        {
            var finding = new Finding(NotDetailedCheck, ReportPath)
            {
                Title = "The " + build.Platform + " build of " + build.BuiltAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " recorded no per-asset sizes" + (build.DetailedRequested ? "" : "; it ran without the detailed report option"),
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Measured = shipped > 0 ? Measurement.Bytes(shipped) : (Measurement?)null,
                Verdict = "Rebuild with BuildOptions.DetailedBuildReport to rank every asset and folder by what it costs in the download",
            };
            finding.Evidence.Add(new Evidence(ReportPath, 0, 0, "report"));
            return finding;
        }

        private static Finding StrippingLowFinding(BuildDescription build, long nativeBytes)
        {
            var finding = new Finding(StrippingLowCheck, build.Platform)
            {
                Title = "Managed stripping is " + build.StrippingLevel + " with IL2CPP, so unused code from every assembly is compiled into the native library",
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(nativeBytes),
                Verdict = "Set Managed Stripping Level to Medium, or High with a link.xml for the plugins that rely on reflection, and rebuild",
            };
            finding.Evidence.Add(new Evidence("ProjectSettings/ProjectSettings.asset", 0, 0, "player settings"));
            return finding;
        }

        private static Finding MonoBackendFinding(BuildDescription build, long managedBytes)
        {
            var finding = new Finding(MonoBackendCheck, build.Platform)
            {
                Title = "The scripting backend is Mono, so every managed assembly ships as is and nothing is stripped at the native level",
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(managedBytes),
                Verdict = "Switch the scripting backend to IL2CPP, which the stores expect on mobile anyway",
            };
            finding.Evidence.Add(new Evidence("ProjectSettings/ProjectSettings.asset", 0, 0, "player settings"));
            return finding;
        }

        private static Finding NativeOutlierFinding(string library, List<string> holders, IEnumerable<string> abis)
        {
            var missing = new List<string>();
            foreach (string abi in abis)
            {
                if (!holders.Contains(abi))
                {
                    missing.Add(abi);
                }
            }

            var finding = new Finding(NativeOutlierCheck, library)
            {
                Title = "Ships for " + string.Join(", ", holders) + " but not for " + string.Join(", ", missing) + "; a plugin missing an ABI crashes on those devices, and a library that belongs to none of them is dead weight",
                Severity = Severity.Warning,
                Budget = Budget.Download,
                Verdict = "Check the plugin's platform settings: either provide the library for every ABI the build targets, or exclude it from the ABIs it does not belong to",
            };
            foreach (string abi in holders)
            {
                finding.Evidence.Add(new Evidence(abi + "/" + library, 0, 0, abi));
            }

            return finding;
        }

        private static Finding UnexpectedFileFinding(BuildFileDescription file)
        {
            string relative = BuildOutputRules.AbiRelativePath(file.Path) ?? file.FileName;
            var finding = new Finding(UnexpectedFileCheck, relative)
            {
                Title = "Sits in a native library folder but is not a shared library",
                Severity = Severity.Warning,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(file.Bytes),
                Verdict = "Find the plugin folder it came from and fix its platform settings so it does not ship as native code",
            };
            finding.Evidence.Add(new Evidence(file.Path, 0, 0, "file"));
            return finding;
        }

        private static Finding DebugSymbolsFinding(BuildFileDescription file)
        {
            var finding = new Finding(DebugSymbolsCheck, file.FileName)
            {
                Title = "Debug symbols ship with the build",
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(file.Bytes),
                Verdict = "Keep the symbols beside the build for crash reports, not inside it",
            };
            finding.Evidence.Add(new Evidence(file.Path, 0, 0, "file"));
            return finding;
        }

        private static Finding ResourcesHeavyFinding(long resources, long payload)
        {
            var finding = new Finding(ResourcesHeavyCheck, "Resources")
            {
                Title = "Resources folders hold " + ((double)resources / payload * 100).ToString("0", CultureInfo.InvariantCulture) + "% of the asset payload; everything under Resources ships whether anything references it or not",
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(resources),
                Verdict = "Move what is not loaded by name out of Resources, or into Addressables, so the build carries only what it references",
            };
            finding.Evidence.Add(new Evidence("Assets", 0, 0, "folder"));
            return finding;
        }

        private static Finding TextureFinding(TextureDescription texture, TextureDiagnosis diagnosis, long measured, long projected, BuildSizeRules rules)
        {
            string dimensions = texture.Width.ToString(CultureInfo.InvariantCulture) + "×" + texture.Height.ToString(CultureInfo.InvariantCulture);
            string target = diagnosis.TargetWidth.ToString(CultureInfo.InvariantCulture) + "×" + diagnosis.TargetHeight.ToString(CultureInfo.InvariantCulture);
            string reason = diagnosis.Reason == UncompressedReason.NotMultipleOfFour ? "is not a multiple of 4" : "is not a power of two";
            string verdict;
            switch (diagnosis.Strategy)
            {
                case UncompressedStrategy.FormatOnly:
                    verdict = "Override the platform format with one that takes any multiple of 4: ETC2 or ASTC on mobile, DXT or BC on desktop; no pixel changes";
                    break;
                case UncompressedStrategy.PadSource:
                    verdict = "Pad the source to " + target + " with up to 3 pixels of transparency" + (texture.IsPng ? "" : ", by re-exporting it since only a PNG pads in place");
                    break;
                case UncompressedStrategy.LiftClamp:
                    verdict = "Raise Max Size from " + texture.MaxSize.ToString(CultureInfo.InvariantCulture) + " to " + diagnosis.LiftMaxSizeTo.ToString(CultureInfo.InvariantCulture) + " so the source, padded to " + target + ", imports at its own size; the clamp's downscale does not round to a multiple of 4";
                    break;
                default:
                    verdict = "Re-export the source at a multiple of 4: Max Size downscales it to an illegal size, and lifting the clamp would ship more than the raw fallback";
                    break;
            }

            long saving = Math.Max(0, measured - projected);
            var finding = new Finding(TextureUncompressedCheck, texture.Path)
            {
                Title = "Stored as " + texture.StoredFormat + " although compression was requested: " + dimensions + " " + reason + (texture.IsClamped ? ", after Max Size " + texture.MaxSize.ToString(CultureInfo.InvariantCulture) + " clamped " + texture.SourceWidth.ToString(CultureInfo.InvariantCulture) + "×" + texture.SourceHeight.ToString(CultureInfo.InvariantCulture) : ""),
                Severity = saving >= rules.UncompressedWarningBytes && rules.UncompressedWarningBytes > 0 ? Severity.Warning : Severity.Advice,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(measured),
                Projected = diagnosis.Strategy == UncompressedStrategy.ManualReexport ? (Measurement?)null : Measurement.Bytes(projected),
                Verdict = verdict,
            };
            finding.Evidence.Add(new Evidence(texture.Path, 0, 0, "texture"));
            return finding;
        }

        private static Finding MeshFinding(ModelDescription model, long projected)
        {
            float error = MeshRules.ErrorMillimetres(MeshCompressionLevel.Medium, model.MaxDimension);
            var finding = new Finding(MeshUncompressedCheck, model.Path)
            {
                Title = "Mesh compression is off: " + Plural(model.MeshCount, "mesh", "meshes") + ", " + model.VertexCount.ToString("N0", CultureInfo.InvariantCulture) + " vertices, largest extent " + model.MaxDimension.ToString("0.#", CultureInfo.InvariantCulture) + " m",
                Severity = Severity.Advice,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(model.Bytes),
                Projected = Measurement.Bytes(projected),
                Verdict = "Set Mesh Compression to Medium: about a third of the size, with a worst-case position error near " + error.ToString("0.#", CultureInfo.InvariantCulture) + " mm at " + model.MaxDimension.ToString("0.#", CultureInfo.InvariantCulture) + " m; High cuts more but breaks the snapping of large modular pieces",
            };
            finding.Evidence.Add(new Evidence(model.Path, 0, 0, "model"));
            return finding;
        }

        private static long Saving(Finding finding)
        {
            if (!finding.Measured.HasValue)
            {
                return 0;
            }

            double projected = finding.Projected.HasValue ? finding.Projected.Value.Value : finding.Measured.Value.Value;
            return (long)Math.Max(0, finding.Measured.Value.Value - projected);
        }

        private static void Accumulate(Dictionary<string, PackedTotal> totals, string key, string typeName, long bytes)
        {
            PackedTotal total;
            if (!totals.TryGetValue(key, out total))
            {
                total = new PackedTotal { TypeName = typeName ?? "" };
                totals.Add(key, total);
            }

            total.Bytes += bytes;
            total.Entries++;
            if (total.TypeName.Length > 0 && !string.Equals(total.TypeName, typeName, StringComparison.Ordinal))
            {
                total.TypeName = "";
            }
        }

        /// <summary>The first two folder levels of an asset path: Assets/Art, Packages/com.unity.x.</summary>
        public static string FolderOf(string assetPath)
        {
            string[] parts = assetPath.Split('/');
            if (parts.Length <= 2)
            {
                return parts.Length == 2 ? parts[0] : assetPath;
            }

            return parts[0] + "/" + parts[1];
        }

        private static string Plural(int count, string one, string many)
        {
            return count.ToString("N0", CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many);
        }

        private sealed class PackedTotal
        {
            public long Bytes;
            public int Entries;
            public string TypeName = "";
        }
    }
}
