using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// How to read the file list of a build report. Unity lists everything the build touched, the
    /// intermediates of IL2CPP included, and names each file's role after its extension, so what ships and
    /// what it is come from the paths: the artifact at the output path, native libraries under a per-ABI
    /// folder, managed assemblies under Managed, the asset payload by extension, StreamingAssets and the
    /// Resources payload by folder and file name.
    /// </summary>
    internal static class BuildOutputRules
    {
        /// <summary>Android ABI folder names, as they appear under jniLibs or lib.</summary>
        public static readonly string[] KnownAbis = { "armeabi-v7a", "arm64-v8a", "x86", "x86_64" };

        private static readonly string[] AssetDataExtensions = { ".assets", ".ress", ".resource", ".unity3d", ".sharedassets" };
        private static readonly string[] SymbolExtensions = { ".pdb", ".mdb", ".dsym" };
        private static readonly string[] DoNotShipMarkers = { "DoNotShip", "BackUpThisFolder" };

        /// <summary>The ABI folder the path sits in, or null.</summary>
        public static string AbiOf(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string[] segments = path.Replace('\\', '/').Split('/');
            for (int i = 0; i < segments.Length - 1; i++)
            {
                foreach (string abi in KnownAbis)
                {
                    if (string.Equals(segments[i], abi, StringComparison.OrdinalIgnoreCase))
                    {
                        return abi;
                    }
                }
            }

            return null;
        }

        /// <summary>The path below the ABI folder, such as arm64-v8a/libunity.so, or null when the path has no ABI folder.</summary>
        public static string AbiRelativePath(string path)
        {
            string abi = AbiOf(path);
            if (abi == null)
            {
                return null;
            }

            string normalized = path.Replace('\\', '/');
            int index = normalized.IndexOf("/" + abi + "/", StringComparison.OrdinalIgnoreCase);
            return index < 0 ? normalized : normalized.Substring(index + 1);
        }

        public static bool IsSharedLibrary(string path)
        {
            return HasExtension(path, ".so") || HasExtension(path, ".dylib");
        }

        /// <summary>A shared library, or anything under an ABI folder.</summary>
        public static bool IsNativeLibrary(string path)
        {
            return IsSharedLibrary(path) || AbiOf(path) != null;
        }

        /// <summary>A .dll under a Managed folder: shipped as is with Mono, the input of IL2CPP otherwise.</summary>
        public static bool IsManagedAssembly(string path)
        {
            return HasExtension(path, ".dll") && ContainsSegment(path, "Managed");
        }

        /// <summary>Symbol files by extension or role, and anything under a folder named symbols, where the Android build keeps the unstripped copies of its native libraries.</summary>
        public static bool IsDebugSymbols(string path, string role)
        {
            if (string.Equals(role, "Symbols", StringComparison.OrdinalIgnoreCase) || ContainsSegment(path, "symbols"))
            {
                return true;
            }

            foreach (string extension in SymbolExtensions)
            {
                if (HasExtension(path, extension))
                {
                    return true;
                }
            }

            return path != null && path.EndsWith(".symbols.zip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Serialized asset files, resource files and the compressed data archive.</summary>
        public static bool IsAssetData(string path, string role)
        {
            if (string.Equals(role, "unity default resources", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (string extension in AssetDataExtensions)
            {
                if (HasExtension(path, extension))
                {
                    return true;
                }
            }

            string name = FileName(path);
            return string.Equals(name, "unity default resources", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "unity_builtin_extra", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The files that hold everything under Resources folders: resources.assets, resources.resource and their companions.</summary>
        public static bool IsResourcesPayload(string path)
        {
            return FileName(path).StartsWith("resources.", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A file copied from StreamingAssets: under a StreamingAssets folder, or in the Android assets folder outside the engine's own bin/Data.</summary>
        public static bool IsStreamingAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            if (ContainsSegment(path, "StreamingAssets"))
            {
                return true;
            }

            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/src/main/assets/", StringComparison.OrdinalIgnoreCase) >= 0 && normalized.IndexOf("/assets/bin/", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>A folder Unity marks as not for shipping, such as the IL2CPP backup beside a desktop player.</summary>
        public static bool IsMarkedDoNotShip(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            foreach (string marker in DoNotShipMarkers)
            {
                if (path.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a listed file is part of what ships. A package build (APK, AAB, IPA) is the one file at the
        /// output path; a player build is everything under the output folder that is not marked do-not-ship.
        /// </summary>
        public static bool IsShipped(string path, string outputPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            string file = path.Replace('\\', '/');
            string output = outputPath.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(file, output, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IsPackageOutput(output))
            {
                return false;
            }

            string folder = HasExtension(output, ".exe") || HasExtension(output, ".app") || HasExtension(output, ".x86_64") ? ParentFolder(output) : output;
            return folder.Length > 0 && file.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) && !IsMarkedDoNotShip(file.Substring(folder.Length));
        }

        /// <summary>APK, AAB and IPA outputs are single files that hold the whole build.</summary>
        public static bool IsPackageOutput(string outputPath)
        {
            return HasExtension(outputPath, ".apk") || HasExtension(outputPath, ".aab") || HasExtension(outputPath, ".ipa");
        }

        public static string FileName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            string normalized = path.Replace('\\', '/');
            int slash = normalized.LastIndexOf('/');
            return slash < 0 ? normalized : normalized.Substring(slash + 1);
        }

        private static string ParentFolder(string path)
        {
            int slash = path.LastIndexOf('/');
            return slash < 0 ? "" : path.Substring(0, slash);
        }

        private static bool HasExtension(string path, string extension)
        {
            return path != null && path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsSegment(string path, string segment)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            foreach (string part in path.Replace('\\', '/').Split('/'))
            {
                if (string.Equals(part, segment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
