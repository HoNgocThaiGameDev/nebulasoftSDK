using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
using System.IO;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public class ExcemptFromEncryption : IPostprocessBuildWithReport // Will execute after XCode project is built
{
    public int callbackOrder { get { return 0; } }

    public void OnPostprocessBuild(BuildReport report)
    {
#if UNITY_IOS
        if (report.summary.platform == BuildTarget.iOS) // Check if the build is for iOS 
        {
            string plistPath = report.summary.outputPath + "/Info.plist";

            PlistDocument plist = new PlistDocument(); // Read Info.plist file into memory
            plist.ReadFromString(File.ReadAllText(plistPath));

            PlistElementDict rootDict = plist.root;
            rootDict.SetBoolean("ITSAppUsesNonExemptEncryption", false);

            File.WriteAllText(plistPath, plist.WriteToString()); // Override Info.plist

            string projectPath = PBXProject.GetPBXProjectPath(report.summary.outputPath);
            PBXProject pbxProject = new PBXProject();
            pbxProject.ReadFromFile(projectPath);

#if UNITY_2019_3_OR_NEWER
            string frameworkGuid = pbxProject.GetUnityFrameworkTargetGuid();
#else
            string frameworkGuid = pbxProject.TargetGuidByName("UnityFramework");
#endif

            pbxProject.SetBuildProperty(frameworkGuid, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "NO");

            pbxProject.WriteToFile(projectPath);
        }
#endif
    }
}