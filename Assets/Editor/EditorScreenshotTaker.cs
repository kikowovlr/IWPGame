using UnityEditor;
using UnityEngine;

public class EditorScreenshotTaker
{
    // This adds a top-menu shortcut: Tools > Take 1080p Screenshot
    [MenuItem("Tools/Take 1080p Screenshot")]
    public static void TakeScreenshot()
    {
        // Ensure the Game view tab is the active focal window 
        EditorApplication.ExecuteMenuItem("Window/General/Game");

        string fileName = "EditorScreenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        ScreenCapture.CaptureScreenshot(fileName);

        Debug.Log("Edit Mode screenshot saved to project root folder as: " + fileName);
    }
}
