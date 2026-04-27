using UnityEngine;
using UnityEngine.InputSystem;
using Holylib.DebugConsole;
public class HolyDebugConsoleExample : MonoBehaviour
{
    void Update()
    {
        _handleInput();
    }

    private void _handleInput() {
        if (Mouse.current.backButton.wasPressedThisFrame) {
            HolyDebugConsole.instance.ToggleConsole();
        }
    }

    [DebugCommand(HolyDebugGroupStyles.Uncategorized)]
    private static void Print(string text) {
        Debug.Log(text);
    } 
}
