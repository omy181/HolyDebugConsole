using UnityEngine;
using UnityEngine.InputSystem;
using Holylib.DebugConsole;
public class HolyDebugConsoleExample : MonoBehaviour
{
    void Update()
    {
        _handleInput();
        _incrementTimer();
    }

    private void _handleInput() {
        if (Mouse.current.backButton.wasPressedThisFrame) {
            HolyDebugConsole.instance.ToggleConsole();
        }
    }

    private static float _timer;

    private void _incrementTimer() {
        _timer += UnityEngine.Time.deltaTime;
    }

    [DebugCommand(DebugGroupStyles.SaveSytem)]
    private static void SavedSave(string saveName) {
        Debug.Log($"Saved as {saveName}");
    }
    
    [DebugCommand(DebugGroupStyles.SaveSytem)]
    private static void LoadSave(string saveName) {
        Debug.Log($"{saveName} Loaded");
    }
    
    [DebugVariable(DebugGroupStyles.SaveSytem,true)]
    private static int SaveCount;
    
    [DebugCommand(DebugGroupStyles.SaveSytem)]
    private static void CreateNewSave() {
        Debug.Log($"New Save Added");
        SaveCount++;
    }
    
    [DebugCommand]
    private static void MoveCameraBetweenPoints(int pointA,int pointB,float time) {
        Debug.Log($"Camera moved from {pointA} to {pointB} in {time}");
    }

    [DebugVariable(DebugGroupStyles.Time)]
    private static float Timer {
        get => _timer;
        set {
            _timer =  value;
        }
    }

    [DebugVariable(DebugGroupStyles.Time,isReadOnly:true)]
    private static float Time => UnityEngine.Time.time;
}

public static class DebugGroupStyles {
    
    public const string SaveSytem = "Save System 💾";
    [DebugCommandGroup(SaveSytem)] public static readonly DebugGroupStyle SaveSystemStyle 
        = new DebugGroupStyle("Save System", new Color(0.39f, 0.35f, 0.58f));
    
    public const string Time = "Time ⏰";
    [DebugCommandGroup(Time)] public static readonly DebugGroupStyle TimeStyle 
        = new DebugGroupStyle("Time", new Color(1f, 0.21f, 0.29f));
}
