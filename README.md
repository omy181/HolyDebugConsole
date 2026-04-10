# Holy Debug Console
Easy-to-use, attribute-based runtime visual debug console for Unity

<img width="1364" height="766" alt="image" src="https://github.com/user-attachments/assets/42101a51-535c-458f-a271-a7305245bc42" />


## Features
- Attribute-based command creation
- Commands with parameters
- Command grouping
- Command and group searching
- Command pinning
- Keyboard and mouse navigation

## How to setup
1. Copy the Git URL and paste it into the Package Manager.

2. Assign the `HolyDebugConsole.cs` script to a GameObject.

3. On the automatically added UI Document component, fill the respective fields like this:

<img width="508" height="149" alt="image" src="https://github.com/user-attachments/assets/b1833732-901f-4025-bf29-e5dfbd4aa1b9" />

4. You are ready to go!
   You can call the `ToggleConsole()` function to open the console:
   ```C#
   HolyDebugConsole.instance.ToggleConsole();
   ```
   For a quick start:
   1. Drag the `HolyDebugConsoleExample.cs` script onto a GameObject.
   2. Run the game.
   3. Press Tab to toggle the console.

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

## About
This project is a console I made to use in my games, but feel free to use it in your projects as well.

- made in Unity using Reflection, UI Toolkit
