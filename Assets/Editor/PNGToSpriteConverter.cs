using UnityEngine;
using UnityEditor;
using System.IO;

public class PNGToSpriteConverter : EditorWindow
{
    private string folderPath = "Assets/Resources/Previews/"; // Your folder path in the Resources directory

    [MenuItem("Tools/Convert PNGs to Sprites")]
    public static void ShowWindow()
    {
        EditorWindow.GetWindow(typeof(PNGToSpriteConverter));
    }

    void OnGUI()
    {
        GUILayout.Label("PNG to Sprite Converter", EditorStyles.boldLabel);
        folderPath = EditorGUILayout.TextField("Folder Path", folderPath);

        if (GUILayout.Button("Convert PNGs to Sprites"))
        {
            ConvertPNGsToSprites(folderPath);
        }
    }

    private void ConvertPNGsToSprites(string folderPath)
    {
        // Ensure the folder exists
        if (!Directory.Exists(folderPath))
        {
            Debug.LogError("Folder path doesn't exist!");
            return;
        }

        // Get all PNG files in the directory (recursively)
        string[] files = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            // Get the relative path to the Resources folder
            string relativePath = file.Replace(Application.dataPath, "Assets");
            string resourcePath = Path.GetDirectoryName(relativePath).Replace("\\", "/"); // For cross-platform compatibility
            
            // Load the PNG file
            byte[] fileData = File.ReadAllBytes(file);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData)) // Load the PNG into a texture
            {
                // Create a sprite from the texture
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

                // Save the sprite as an asset in the Resources folder
                string spriteName = Path.GetFileNameWithoutExtension(file);
                string spritePath = Path.Combine(resourcePath, spriteName) + ".asset";

                // Create and save the asset
                AssetDatabase.CreateAsset(sprite, spritePath);
                AssetDatabase.SaveAssets();

                Debug.Log($"Successfully created sprite: {spriteName}");
            }
            else
            {
                Debug.LogError($"Failed to load texture from {file}");
            }
        }

        AssetDatabase.Refresh(); // Refresh to show new assets
    }
}
