using UnityEngine;
using System.IO;

public class Screenshot : MonoBehaviour
{
    [SerializeField] private KeyCode screenshotKey = KeyCode.F12;
    [SerializeField] private string folderName = "Screenshots";
    
    private void Update()
    {
        if (Input.GetKeyDown(screenshotKey))
        {
            CaptureScreenshot();
        }
    }
    
    private void CaptureScreenshot()
    {
        // Create directory if it doesn't exist
        string directory = Path.Combine(Application.dataPath, "/Users/zhumozhao/Desktop/Preview/Torso", folderName);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Generate filename with timestamp
        string fileName = $"Screenshot_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
        string filePath = Path.Combine(directory, fileName);
        
        // Capture the screenshot
        ScreenCapture.CaptureScreenshot(filePath);
        
        Debug.Log($"Screenshot saved to: {filePath}");
    }
}