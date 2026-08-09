#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class AddButtonAudioTool
{
    [MenuItem("Tools/Audio/Add UIButtonAudio To All Buttons In Scene")]
    private static void AddToAllButtons()
    {
        Button[] buttons = Object.FindObjectsByType<Button>();
        int added = 0;

        foreach (Button b in buttons)
        {
            if (b.GetComponent<UIButtonAudio>() == null)
            {
                Undo.AddComponent<UIButtonAudio>(b.gameObject);   // undoable
                added++;
            }
        }

        Debug.Log($"[AudioTool] Added UIButtonAudio to {added} buttons ({buttons.Length} total in scene).");
    }
}
#endif