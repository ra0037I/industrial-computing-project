#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Fügt beim Android-Build die Google-ML-Kit-Texterkennung als Gradle-Abhängigkeit hinzu.
/// Ablage: Assets/Editor/MlKitGradleDependency.cs
/// </summary>
public class MlKitGradleDependency : IPostGenerateGradleAndroidProject
{
    const string Artifact = "com.google.mlkit:text-recognition:16.0.1";

    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // path zeigt auf das Modul "unityLibrary"
        string groovy = Path.Combine(path, "build.gradle");
        string kotlin = Path.Combine(path, "build.gradle.kts");

        string file;
        string line;
        if (File.Exists(groovy))
        {
            file = groovy;
            line = "    implementation '" + Artifact + "'";
        }
        else if (File.Exists(kotlin))
        {
            file = kotlin;
            line = "    implementation(\"" + Artifact + "\")";
        }
        else
        {
            Debug.LogError("MlKitGradleDependency: build.gradle nicht gefunden in " + path);
            return;
        }

        string text = File.ReadAllText(file);
        if (text.Contains("com.google.mlkit:text-recognition"))
        {
            Debug.Log("MlKitGradleDependency: ML Kit ist bereits eingetragen.");
            return;
        }

        // Ein zusätzlicher dependencies-Block am Dateiende ist in Gradle erlaubt.
        text += "\n\n// Hinzugefügt von MlKitGradleDependency.cs\ndependencies {\n" + line + "\n}\n";
        File.WriteAllText(file, text);
        Debug.Log("MlKitGradleDependency: ML Kit zu " + file + " hinzugefügt.");
    }
}
#endif
