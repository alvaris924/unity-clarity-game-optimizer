using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class BuildOutputRulesTests
    {
        private const string Staging = "E:/Project/Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main";

        [Test]
        public void Abi_comes_from_the_folder_name()
        {
            Assert.That(BuildOutputRules.AbiOf(Staging + "/jniLibs/arm64-v8a/libil2cpp.so"), Is.EqualTo("arm64-v8a"));
            Assert.That(BuildOutputRules.AbiOf(Staging + "\\jniLibs\\armeabi-v7a\\libunity.so"), Is.EqualTo("armeabi-v7a"));
            Assert.That(BuildOutputRules.AbiOf("Builds/lib/x86_64/libfoo.so"), Is.EqualTo("x86_64"));
            Assert.That(BuildOutputRules.AbiOf("Builds/Game_Data/level0"), Is.Null);
            Assert.That(BuildOutputRules.AbiOf("arm64-v8a"), Is.Null, "a file named like an ABI is not in an ABI folder");
            Assert.That(BuildOutputRules.AbiOf(null), Is.Null);
            Assert.That(BuildOutputRules.AbiRelativePath(Staging + "/jniLibs/arm64-v8a/libil2cpp.so"), Is.EqualTo("arm64-v8a/libil2cpp.so"));
            Assert.That(BuildOutputRules.AbiRelativePath("Builds/Game.exe"), Is.Null);
        }

        [Test]
        public void Files_are_classified_by_path_and_role()
        {
            Assert.That(BuildOutputRules.IsNativeLibrary(Staging + "/jniLibs/arm64-v8a/libil2cpp.so"), Is.True);
            Assert.That(BuildOutputRules.IsNativeLibrary("Builds/Game.app/Contents/Frameworks/libfoo.dylib"), Is.True);
            Assert.That(BuildOutputRules.IsNativeLibrary(Staging + "/jniLibs/armeabi-v7a/stray.dll"), Is.True, "anything under an ABI folder");
            Assert.That(BuildOutputRules.IsSharedLibrary(Staging + "/jniLibs/armeabi-v7a/stray.dll"), Is.False);
            Assert.That(BuildOutputRules.IsManagedAssembly("E:/Project/Library/Bee/Android/Prj/IL2CPP/Il2CppBackup/Managed/Assembly-CSharp.dll"), Is.True);
            Assert.That(BuildOutputRules.IsManagedAssembly("Builds/Game_Data/Managed/Assembly-CSharp.pdb"), Is.False);
            Assert.That(BuildOutputRules.IsManagedAssembly("Builds/UnityPlayer.dll"), Is.False);
            Assert.That(BuildOutputRules.IsDebugSymbols("Builds/Game_Data/Managed/Assembly-CSharp.pdb", "pdb"), Is.True);
            Assert.That(BuildOutputRules.IsDebugSymbols("C:/Builds/game-1.0-v1-IL2CPP.symbols.zip", "Symbols"), Is.True);
            Assert.That(BuildOutputRules.IsDebugSymbols("C:/Builds/game.symbols.zip", "zip"), Is.True);
            Assert.That(BuildOutputRules.IsDebugSymbols("E:/Project/Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/symbols/arm64-v8a/libil2cpp.so", "so"), Is.True, "the unstripped copy");
            Assert.That(BuildOutputRules.IsDebugSymbols("Builds/Game.exe", "exe"), Is.False);
            Assert.That(BuildOutputRules.IsAssetData(Staging + "/assets/bin/Data/data.unity3d", "unity3d"), Is.True);
            Assert.That(BuildOutputRules.IsAssetData("Builds/Game_Data/sharedassets0.assets.resS", "resS"), Is.True);
            Assert.That(BuildOutputRules.IsAssetData(Staging + "/assets/bin/Data/Resources/unity default resources", "unity default resources"), Is.True);
            Assert.That(BuildOutputRules.IsAssetData(Staging + "/assets/bin/Data/resources.resource", "resource"), Is.True);
            Assert.That(BuildOutputRules.IsAssetData(Staging + "/assets/bin/Data/boot.config", "config"), Is.False);
            Assert.That(BuildOutputRules.IsResourcesPayload(Staging + "/assets/bin/Data/resources.resource"), Is.True);
            Assert.That(BuildOutputRules.IsResourcesPayload("Builds/Game_Data/resources.assets.resS"), Is.True);
            Assert.That(BuildOutputRules.IsResourcesPayload("Builds/Game_Data/sharedassets0.assets"), Is.False);
            Assert.That(BuildOutputRules.IsStreamingAsset("Builds/Game_Data/StreamingAssets/video.mp4"), Is.True);
            Assert.That(BuildOutputRules.IsStreamingAsset(Staging + "/assets/aa/catalog.json"), Is.True, "Android puts StreamingAssets in the assets root");
            Assert.That(BuildOutputRules.IsStreamingAsset(Staging + "/assets/bin/Data/data.unity3d"), Is.False);
            Assert.That(BuildOutputRules.IsMarkedDoNotShip("Builds/Game_BackUpThisFolder_ButDontShipItWithYourGame/x.pdb"), Is.True);
            Assert.That(BuildOutputRules.IsMarkedDoNotShip("Builds/Game_BurstDebugInformation_DoNotShip/x.txt"), Is.True);
            Assert.That(BuildOutputRules.IsMarkedDoNotShip("Builds/Game_Data/level0"), Is.False);
        }

        [Test]
        public void What_ships_depends_on_the_kind_of_output()
        {
            const string aab = "C:/Users/me/Desktop/Builds/game.aab";
            Assert.That(BuildOutputRules.IsShipped(aab, aab), Is.True);
            Assert.That(BuildOutputRules.IsShipped("C:/Users/me/Desktop/Builds/game.symbols.zip", aab), Is.False, "beside a package, not in it");
            Assert.That(BuildOutputRules.IsShipped(Staging + "/jniLibs/arm64-v8a/libil2cpp.so", aab), Is.False, "inside the package already");
            Assert.That(BuildOutputRules.IsPackageOutput(aab), Is.True);
            Assert.That(BuildOutputRules.IsPackageOutput("Builds/game.apk"), Is.True);

            const string exe = "D:/Builds/Win/Game.exe";
            Assert.That(BuildOutputRules.IsShipped(exe, exe), Is.True);
            Assert.That(BuildOutputRules.IsShipped("D:/Builds/Win/Game_Data/level0", exe), Is.True);
            Assert.That(BuildOutputRules.IsShipped("D:/Builds/Win/UnityPlayer.dll", exe), Is.True);
            Assert.That(BuildOutputRules.IsShipped("D:/Builds/Win/Game_BackUpThisFolder_ButDontShipItWithYourGame/x.pdb", exe), Is.False);
            Assert.That(BuildOutputRules.IsShipped("D:/Builds/Other/Game.exe", exe), Is.False);
            Assert.That(BuildOutputRules.IsPackageOutput(exe), Is.False);

            const string folder = "D:/Builds/Linux/";
            Assert.That(BuildOutputRules.IsShipped("D:/Builds/Linux/Game_Data/level0", folder), Is.True);
            Assert.That(BuildOutputRules.IsShipped("", folder), Is.False);
            Assert.That(BuildOutputRules.IsShipped("D:/Builds/Linux/x", ""), Is.False);
        }

        [Test]
        public void Folder_of_an_asset_path_is_its_first_two_levels()
        {
            Assert.That(BuildSizeReport.FolderOf("Assets/Art/Buildings/x.fbx"), Is.EqualTo("Assets/Art"));
            Assert.That(BuildSizeReport.FolderOf("Assets/x.png"), Is.EqualTo("Assets"));
            Assert.That(BuildSizeReport.FolderOf("Packages/com.unity.x/Runtime/y.shader"), Is.EqualTo("Packages/com.unity.x"));
            Assert.That(BuildSizeReport.FolderOf("x"), Is.EqualTo("x"));
        }
    }
}
