# Holy Debug Console
Easy-to-use, attribute-based runtime visual debug console for Unity

<img width="1913" height="1077" alt="image" src="https://github.com/user-attachments/assets/e597e0ce-0d42-4585-8eaa-1d8ff433b201" />

## Features
- Attribute-based command creation
- Commands with parameters
- Command grouping
- Command and group searching
- Command pinning
- Custom keybinds for commands
- Property and Field value debugging
- Keyboard and mouse navigation

## How to setup
1. Copy the Git URL and paste it into the Package Manager.

2. Assign the `HolyDebugConsole.cs` script to a GameObject.

3. On the automatically added UI Document component, fill the respective fields like this:

<img width="673" height="235" alt="image" src="https://github.com/user-attachments/assets/076b5552-1f29-4cbc-8624-1c3359facaa1" />


4. You are ready to go!
   You can call the `ToggleConsole()` function to open the console:
   ```C#
   HolyDebugConsole.instance.ToggleConsole();
   ```
   Or for a quick start:
   1. Drag the `HolyDebugConsoleExample.cs` script onto a GameObject.
   2. Run the game.
   3. Press F1 to toggle the console.

## How to add a new command

For any static function with primitive parameters, you can add the `[DebugCommand]` attribute on top, and that's it!
```C#
    [DebugCommand]
    private static void Print(string text) {
        Debug.Log(text);
    } 
```

Your function will be added to the available commands list.

<img width="468" height="131" alt="image" src="https://github.com/user-attachments/assets/97b62433-ce21-47bb-8267-5f52100e8684" />


For debugging properties or fields add the `[DebugVariable]` attribute on top.
```C#
    private static int TestInt;
    
    [DebugVariable]
    private static int TestIntProperty 
    { 
        get => TestInt;
        set => TestInt = value; 
    }
```

Your get value will be visible next to the property name, and typing a value will trigger the setter 

<img width="379" height="58" alt="image" src="https://github.com/user-attachments/assets/5332f608-c818-4eab-a9aa-810536192727" />

You can also make it readOnly

```C#
    [DebugVariable(isReadOnly:true)]
    private static int TestIntProperty 
    { 
        get => TestInt;
    }
```

<img width="381" height="34" alt="image" src="https://github.com/user-attachments/assets/895a9e99-1628-4906-960b-873ca99f8875" />


## How to customize the command category

First, you need to create a new group style:
```C#
public static partial class GroupStyles {
    
    public const string SaveLoad = "SaveLoad";
    [DebugCommandGroup(SaveLoad)] public static readonly DebugGroupStyle SaveLoadStyle 
        = new DebugGroupStyle("💾 Save Utilities", Color.purple);
}
```
Give a visible name and category color.
Then add the group style parameter to the commands you want to group together:

```C#
    [DebugCommand(GroupStyles.SaveLoad)]
    private static void Save(string saveName) {
        // Save Logic
    }

    [DebugCommand(GroupStyles.SaveLoad)]
    private static void Load(string saveName) {
        // Load Logic
    } 
```

And this is how it shows up in the game:

<img width="471" height="207" alt="image" src="https://github.com/user-attachments/assets/15713d7e-8ab7-49b5-9791-e71de45536ca" />

## Some Functions you might want to use

Toggle Console:
```C#
HolyDebugConsole.instance.ToggleConsole();
```

Called when the console is toggled:
```C#
HolyDebugConsole.instance.OnConsoleToggled += /*Action*/;
```

Check if the console is currently open:
```C#
bool isOpen = HolyDebugConsole.IsConsoleOpen;
```

## Shortcuts
- Up/Down arrow keys to switch between commands
- Enter to execute selected command
- Esc/Ctrl+F to go back to searching
- Tab to switch between parameters of selected command
- Hold Debug Command Key (Defult is Alt Gr) and press any keybind key

## About
This project is a console I made to use in my games, but feel free to use it in your projects as well.

- made in Unity using Reflection, UI Toolkit
