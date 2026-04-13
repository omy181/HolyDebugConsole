using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Holylib.DebugConsole {

    [RequireComponent(typeof(UIDocument))]
    public class HolyDebugConsole : MonoBehaviour {
        
        #region References
        [Header("References")]
        [HideInInspector][SerializeField] private VisualTreeAsset _regularCommandBlock;
        [HideInInspector][SerializeField] private VisualTreeAsset _parameterCommandBlock;
        [HideInInspector][SerializeField] private VisualTreeAsset _commandBlockParameter;
        [HideInInspector][SerializeField] private VisualTreeAsset _commandBlockGroup;
  
        private VisualElement _root;
        private ScrollView _blocksUI;
        private TextField _searchField;
        private Label _commandList;
        private static Label _outputText;
        
        private UIDocument _uiDocument => GetComponent<UIDocument>();
#endregion

        public static bool IsConsoleOpen { get; private set; }
        public static HolyDebugConsole instance;
        public void ToggleConsole() => _toggleConsole();
        public Action<bool> OnConsoleToggled;
        public void ExecuteLastCommand() => _executeCommand(_searchField.value);
        public void ExecuteCommand (string input) => _executeCommand(input);
        public static void OutputToConsole (string message, LogType type = LogType.Log) {
            var key = (message, type);

            if (!_logCounts.TryAdd(key, 1))
                _logCounts[key]++;

            _refreshOutput();
        }

        #region Initialization
        protected void Awake() {
            if (instance != null) {
                Debug.LogWarning(this + " already has an istance.");
            } else {
                instance = this;
            }

            _uiDocument.enabled = true;
        }
        private void Start() {
            _initialize();
        }
#if UNITY_EDITOR
        void Reset()
        {
            _uiDocument.enabled = false;
        }
#endif
        private void Update() {
            _outputTheQueueUpdate();
            _InputHandling();
        }
        protected virtual void OnDestroy() {
            instance = null;
        }
        void OnEnable() {
            Application.logMessageReceivedThreaded += _handleLog;
        }

        void OnDisable() {
            Application.logMessageReceived -= _handleLog;
        }
        private void _initialize() {
            _root = _uiDocument.rootVisualElement;
            _root.style.display = DisplayStyle.None;

            _outputText = _root.Q<Label>("ConsoleText");

            var clearConsoleButton = _root.Q<Button>("ClearConsole");
            clearConsoleButton.clicked += _clearConsole;

            var copyConsoleButton = _root.Q<Button>("ClipboardConsole");
            copyConsoleButton.clicked += _copyConsoleToClipboard;

            var exitConsole = _root.Q<Button>("Exit");
            exitConsole.clicked += _toggleConsole;
            
            var sizeIncreaseButton = _root.Q<Button>("SizePlus");
            sizeIncreaseButton.clicked += _increaseFontSize;
            
            var sizeDecreaseButton = _root.Q<Button>("SizeMinus");
            sizeDecreaseButton.clicked += _decreaseFontSize;
            
            _blocksUI = _root.Q<ScrollView>("Blocks");

            _searchField = _root.Q<TextField>("SearchBar");
            
            _searchField.RegisterCallback<FocusEvent>(evt => {
                SetSelectedBlockIndex(-1);
            });

            _searchField.RegisterCallback<KeyUpEvent>(evt => {
                SetSelectedBlockIndex(-1);
                _updateCommandBlocksList();
            });
            
            _loadPins();
            _instantiateCommandBlocks();
            _updateCommandBlocksList();
            _preventDefaultTabBehaviour();
            _setFontSize();
        }
        
  #endregion

        #region Input Handling
        
        private double _lasTimeUpDownPressed;
        private void _InputHandling() {
            
            if(!IsConsoleOpen) return;
            
            if (Keyboard.current.backspaceKey.wasReleasedThisFrame) {
                _backspacePressed();
            } else if (Keyboard.current.tabKey.wasPressedThisFrame) {
                _tabPressed();
            } else if (Keyboard.current.escapeKey.wasPressedThisFrame || (Keyboard.current.ctrlKey.isPressed && Keyboard.current.fKey.wasPressedThisFrame)) {
                _backToSearchPressed();
            } else if (Keyboard.current.enterKey.wasPressedThisFrame) {
                _enterPressed();
            } else if (Keyboard.current.upArrowKey.wasPressedThisFrame) {
                _upArrowPressed();
            } else if (Keyboard.current.downArrowKey.wasPressedThisFrame) {
                _downArrowPressed();
            }

            if (Keyboard.current.upArrowKey.wasReleasedThisFrame || Keyboard.current.downArrowKey.wasReleasedThisFrame) {
                _upOrDownStartedPressing();
            }

            if (Keyboard.current.upArrowKey.isPressed && _lasTimeUpDownPressed != -1 && _lasTimeUpDownPressed + 0.5f < Time.time) {
                _upArrowHolding();
            } else if (Keyboard.current.downArrowKey.isPressed && _lasTimeUpDownPressed != -1 && _lasTimeUpDownPressed + 0.5f < Time.time) {
                _downArrowHolding();
            }

        }
        
        #region Input Actions
        private void _backspacePressed() {
            if (!_hasParametersInSelection && GetSelectedBlockIndex() != -1) {
                SetSelectedBlockIndex(-1);
                _updateCommandBlocksList();
            }
        }
        private void _tabPressed() {
            if (_hasParametersInSelection) {
                SelectedParameterIndex = (SelectedParameterIndex + 1 + _getSelectedCommandBlock.ParameterLength) % _getSelectedCommandBlock.ParameterLength;
                _updateCommandBlocksList();
            }
        }
        private void _backToSearchPressed() {
            SelectedParameterIndex = -1;
            SetSelectedBlockIndex(-1);

            _updateCommandBlocksList();
        }
        private void _enterPressed() {
            _getSelectedCommandBlock?.RunCommand();
            _updateCommandBlocksList();
        }
        private void _upArrowPressed() {
            _upInList();
            _lasTimeUpDownPressed = Time.time;
        }
        private void _downArrowPressed() {
            _downInList();
            _lasTimeUpDownPressed = Time.time;
        }
        private void _upArrowHolding() {
            _upInList();
            _lasTimeUpDownPressed = Time.time - 0.45f;
        }
        private void _downArrowHolding() {
            _downInList();
            _lasTimeUpDownPressed = Time.time - 0.45f;
        }

        private void _upOrDownStartedPressing() {
            _lasTimeUpDownPressed = -1;
        }
        

  #endregion
        
        private void _preventDefaultTabBehaviour() {
            _root.RegisterCallback<KeyDownEvent>(evt => {
                if (evt.keyCode == KeyCode.Tab) {
                    evt.StopImmediatePropagation(); // prevents default next-field focus
                    evt.StopPropagation(); // optional, blocks text tab char
                }
            }, TrickleDown.TrickleDown);
        }
        
  #endregion

        #region Console
        
        private string _commandListString;
        private static string _outputString;
        private void _outputTheQueueUpdate() {
            lock (_logQueue) {
                while (_logQueue.Count > 0) {
                    var (message, type) = _logQueue.Dequeue();
                    OutputToConsole(message, type);
                }
            }
        }
        private IEnumerator FocusNextFrame() {
            yield return null;
            _searchField.Focus();
        }
        private void _toggleConsole() {
            SetSelectedBlockIndex(-1);

            IsConsoleOpen = !IsConsoleOpen;

            OnConsoleToggled?.Invoke(IsConsoleOpen);

            _root.style.display =
                _root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;

            if (IsConsoleOpen) {
                StartCoroutine(FocusNextFrame());
            }


        }
        private bool _executeCommand (string input) {
            if (string.IsNullOrWhiteSpace(input)) {
                _toggleConsole();
                return true;
            }

            bool success = false;

            try {
                success = DebugCommandRegistry.TryInvoke(input);
            }
            catch {
                success = false;
            }

            if (!success)
                OutputToConsole($"Command not found: {input}", LogType.Warning);

            return success;
        }

        private static readonly Dictionary<(string message, LogType type), int> _logCounts
            = new Dictionary<(string, LogType), int>();
        
        private static void _refreshOutput() {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (var entry in _logCounts) {
                string colorStart = "";
                string colorEnd = "";

                if (entry.Key.type == LogType.Error || entry.Key.type == LogType.Assert || entry.Key.type == LogType.Exception) {
                    colorStart = "<color=\"red\">";
                    colorEnd = "</color>";
                } else if (entry.Key.type == LogType.Warning) {
                    colorStart = "<color=\"yellow\">";
                    colorEnd = "</color>";
                }

                if (entry.Value > 1)
                    sb.AppendLine($"> ({entry.Value}) {colorStart}{entry.Key.message}{colorEnd}");
                else
                    sb.AppendLine($"> {colorStart}{entry.Key.message}{colorEnd}");
            }

            _outputText.text = sb.ToString();
        }

        private void _clearConsole() {
            _logCounts.Clear();
            _outputString = "";
            _outputText.text = _outputString;
        }

        private void _copyConsoleToClipboard() {
            GUIUtility.systemCopyBuffer = _outputText.text;
            Debug.Log("Copied to clipboard");
        }

        private readonly int _defaultFontSize = 14;
        private const string Fontplayerpref = "DebugConsoleFontSize";

        private void _setFontSize() {
            int fontSize = PlayerPrefs.GetInt(Fontplayerpref, _defaultFontSize);
            _outputText.style.fontSize = fontSize;
        }
        private void _increaseFontSize() {
            int fontSize = PlayerPrefs.GetInt(Fontplayerpref, _defaultFontSize);
            fontSize = Mathf.Clamp(fontSize+1,5,60);
            _outputText.style.fontSize = fontSize;
            PlayerPrefs.SetInt(Fontplayerpref, fontSize);
        }
        
        private void _decreaseFontSize() {
            int fontSize = PlayerPrefs.GetInt(Fontplayerpref, _defaultFontSize);
            fontSize = Mathf.Clamp(fontSize-1,5,60);
            _outputText.style.fontSize = fontSize;
            PlayerPrefs.SetInt(Fontplayerpref, fontSize);
        }
        
        private void _handleLog (string logString, string stackTrace, LogType type) {
            lock (_logQueue) {
                _logQueue.Enqueue((logString, type));
            }
        }

        private readonly Queue<(string, LogType)> _logQueue = new Queue<(string, LogType)>();
        

  #endregion
        
        #region Command Blocks

        private List<CommandBlock> _commandBlocks = new();
        private List<CommandBlock> _visibleCommandBlocks = new();

        private bool _hasParametersInSelection =>  _getSelectedCommandBlock?.ParameterLength > 0;
        private int _visibleBlockCount;
        private int _selectedParameterIndex;
        private int SelectedParameterIndex {
            get => _selectedParameterIndex;
            set {
                _selectedParameterIndex = value;

                if (_getSelectedCommandBlock == null || _getSelectedCommandBlock.ParameterLength <= 0 || SelectedParameterIndex < 0) return;

                int a = 0;
                foreach (var parameterField in _getSelectedCommandBlock.ParameterFields) {
                    
                    if (a == SelectedParameterIndex) {
                        parameterField.Field.Q<TextField>().Focus();
                        break;
                    }
                    a++;
                }
            }
        }
        private int _selectedBlockIndex = -1;
        private int GetSelectedBlockIndex() => _selectedBlockIndex;
        private void SetSelectedBlockIndex (int value,int manualParameterIndex = -1) {
            if (value == _selectedBlockIndex) return;
            
            bool isManualParameterIndex = manualParameterIndex != -1;

            if (value == -1 || value == _visibleBlockCount) {
                _selectedBlockIndex = -1;
                SelectedParameterIndex = isManualParameterIndex ? manualParameterIndex : -1;

            } else if (value == -2) {
                _selectedBlockIndex = _visibleBlockCount - 1;
                SelectedParameterIndex = isManualParameterIndex ? manualParameterIndex : _getSelectedCommandBlock.ParameterLength - 1;

            } else if (value >= 0 && value < _visibleBlockCount) {
                int prevBlockIndex = _selectedBlockIndex;
                _selectedBlockIndex = value;

                if (isManualParameterIndex) {
                    SelectedParameterIndex = manualParameterIndex;
                } else {
                    if (value <= prevBlockIndex) {
                        SelectedParameterIndex = _getSelectedCommandBlock.ParameterLength - 1;
                    } else {
                        SelectedParameterIndex = _getSelectedCommandBlock.ParameterLength != 0 ? 0 : -1;
                    }
                }
                


            } else {
                Debug.LogError($"{value} is not a valid selected block index");
            }

            _updateCommandBlocksList();
        }

        private CommandBlock _getSelectedCommandBlock {
            get {
                return _selectedBlockIndex != -1 ? _visibleCommandBlocks[_selectedBlockIndex] : null;
            }
        }

        private class CommandBlock {
            public string MethodName { get; private set; }
            public DebugGroupStyle GroupStyle{ get; private set; }
            public bool IsPinned => _pinnedBlocks.Contains(MethodName);
            public ParameterInfo[] Parameters { get; private set; }
            public int ParameterLength => Parameters?.Length ?? 0;
            public List<ParameterField> ParameterFields;
            public VisualElement VisualBlock;
            public VisualElement ParameterParent;

            public void Initialize(DebugCommandRegistry.MethodGroup methodGroup) {
                MethodName = methodGroup.method.Name;
                GroupStyle = methodGroup.group;
                Parameters = methodGroup.method.GetParameters();
                _createVisualBlock();
            }
            
            public void RunCommand() {

                instance.SetSelectedBlockIndex(instance._visibleCommandBlocks.IndexOf(this));
                
                if (Parameters.Length == 0) {
                    instance._playExecuteAnimation(VisualBlock.Children().First(), instance._executeCommand(MethodName));
                } else {
                    string fullCommand = MethodName;

                    foreach (var field in ParameterFields) {

                        if (field.Type == typeof(string)) {
                            fullCommand += $" \"{field.Field.value}\"";
                        } else {
                            fullCommand += $" {field.Field.value}";
                        }

                    }

                    instance._playExecuteAnimation(VisualBlock.Children().First(), instance._executeCommand(fullCommand));
                }
            }

            private void _createVisualBlock() {
                if (Parameters == null || Parameters.Length <= 0) {
                    VisualBlock = instance._instantiateRegularCommandBlock(this);
                } else {
                    var parameterCommandBlockOutput = instance._instantiateParameterCommandBlock(this);
                    VisualBlock = parameterCommandBlockOutput.visualElement;
                    ParameterFields = parameterCommandBlockOutput.parameterFields;
                }

                VisualBlock.name = GroupStyle.Name;

                if (IsPinned) {
                    VisualBlock.Q<Button>("Pin").text = "Unpin";
                    VisualBlock.name += "_" + _pinnedGroupName;
                } else {
                    VisualBlock.Q<Button>("Pin").text = "Pin";
                }
                
                VisualBlock.Children().First().style.borderLeftColor = GroupStyle.Color;
            }

            public override string ToString() {
                return $"{MethodName}-{GroupStyle.Name}";
            }
        }
        
        
        #region Visuals
        private void _instantiateCommandBlocks() {

            SetSelectedBlockIndex(-1);

            _blocksUI.Clear();
            _commandBlocks.Clear();

            Dictionary<DebugGroupStyle, List<VisualElement>> groups = new();

    
            // Create Pinned Group
            var pinnedGroup = _commandBlockGroup.Instantiate();
            pinnedGroup.name = _pinnedGroupName;
            pinnedGroup.Q<Label>().text = $"⭐ {_pinnedGroupName}";
            _blocksUI.Add(pinnedGroup);


            foreach (var command in DebugCommandRegistry.Commands.OrderBy(c=>c.Value.group.Name)) {
                CommandBlock commandBlock = new();
                commandBlock.Initialize(command.Value);
                _commandBlocks.Add(commandBlock);
                
                if (!groups.ContainsKey(commandBlock.GroupStyle)) {
                    groups[commandBlock.GroupStyle] = new();
                }

                if (commandBlock.IsPinned) {
                    _blocksUI.Q<TemplateContainer>(_pinnedGroupName).Q<VisualElement>("Blocks").Add(commandBlock.VisualBlock);
                } else {
                    groups[command.Value.group].Add(commandBlock.VisualBlock);
                }
            }
            
            foreach (var groupList in groups) {

                var group = _commandBlockGroup.Instantiate();
                group.name = groupList.Key.Name;
                group.Q<Label>().text = $"{groupList.Key.Name}";

                foreach (var block in groupList.Value) {
                    group.Q<VisualElement>("Blocks").Add(block);
                }
                _blocksUI.Add(group);
            }

            _visibleBlockCount = DebugCommandRegistry.Commands.Count;
        }
        private void _updateCommandBlocksList() {
            string input = _searchField.value.ToLower();

            int blocksCount = 0;
            int visibleBlockCount = 0;

            Dictionary<string,bool> visibleGroups = new();
            _visibleCommandBlocks.Clear();
            List<CommandBlock> pinnedBlocks = new();
            
            foreach (var commandBlock in _commandBlocks) {
                blocksCount++;
                
                if (commandBlock.MethodName.ToLower().Contains(input) || commandBlock.GroupStyle.Name.ToLower().Contains(input) || (commandBlock.IsPinned && _pinnedGroupName.ToLower().Contains(input))) {
                    visibleBlockCount++;

                    if (commandBlock.IsPinned) {
                        visibleGroups.TryAdd(_pinnedGroupName, true);
                        pinnedBlocks.Add(commandBlock);
                    } else {
                        visibleGroups.TryAdd(commandBlock.GroupStyle.Name, true);
                        _visibleCommandBlocks.Add(commandBlock);
                    }
                    
                    commandBlock.VisualBlock.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    
                    commandBlock.VisualBlock.Children().First().RemoveFromClassList("command-block-pseudo-hover");
                    
                } else {
                    commandBlock.VisualBlock.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
            }

            // hide groups with no blocks
            foreach (var group in _blocksUI.Children()) {
                
                if (visibleGroups.ContainsKey(group.name)) {
                    group.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                } else {
                    group.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
            }
            
            // add pinned blocks to beginning
            _visibleCommandBlocks.InsertRange(0,pinnedBlocks);
            
            // focus
            if (GetSelectedBlockIndex() == -1) {
                _searchField.Focus();
            } else if (_getSelectedCommandBlock?.ParameterLength <= 0) {
                _getSelectedCommandBlock.VisualBlock.Focus();
            }

            if (GetSelectedBlockIndex() != -1) {
                _blocksUI.ScrollTo(_getSelectedCommandBlock.VisualBlock);
                _getSelectedCommandBlock.VisualBlock.Children().First().AddToClassList("command-block-pseudo-hover");
            }


            _visibleBlockCount = visibleBlockCount;
            
        }
        
        private VisualElement _instantiateRegularCommandBlock (CommandBlock commandBlock) {
            var block = _regularCommandBlock.Instantiate();
            block.Q<Label>("CommandBlockLabel").text = commandBlock.MethodName;
            block.RegisterCallback<MouseUpEvent>((a) => {
                commandBlock.RunCommand();
            });
            block.Q<Button>("Pin").clicked += () => {
                _pinBlock(commandBlock.MethodName);
            };
            return block;
        }

        private (VisualElement visualElement,List<ParameterField> parameterFields) _instantiateParameterCommandBlock (CommandBlock commandBlock) {
            var block = _parameterCommandBlock.Instantiate();
            block.Q<Label>("CommandBlockLabel").text = commandBlock.MethodName;
            _blocksUI.Add(block);

            List<ParameterField> parameterFields = new();

            int parameterIndex = 0;
            foreach (var parameter in commandBlock.Parameters) {

                var parameterField = _commandBlockParameter.Instantiate();
                var field = parameterField.Q<TextField>("CommandBlockParameter");
                field.label = parameter.Name;
                field.RegisterCallback<FocusEvent>(evt => {
                    SetSelectedBlockIndex(instance._visibleCommandBlocks.IndexOf(commandBlock),parameterIndex);
                });
                
                
                parameterFields.Add(new(field, parameter.ParameterType));

                block.Q<VisualElement>("Parameters").Add(parameterField);
                parameterIndex++;
            }

            block.Q<Button>("Pin").clicked += () => {
                _pinBlock(commandBlock.MethodName);
            };

            block.RegisterCallback<MouseUpEvent>((a) => {
                commandBlock.RunCommand();
            });

            return (block,parameterFields);
        }
        
        private struct ParameterField {
            public TextField Field;
            public Type Type;
            public ParameterField(TextField field, Type type) {
                Field = field;
                Type = type;
            }
        }
        
        private void _playExecuteAnimation (VisualElement block, bool isSuccessfull) {

            string classStyle = isSuccessfull ? "command-block-pseudo-execute" : "command-block-pseudo-cantexecute";

            block.AddToClassList(classStyle);

            block.schedule.Execute(() => {
                block.RemoveFromClassList(classStyle);
            }).StartingIn(200);
        }

#endregion
        
        #region Pins

        private static List<string> _pinnedBlocks = new();
        private const string _pinnedGroupName = "Pinned";
        private void _pinBlock (string methodName) {
            if (_pinnedBlocks.Contains(methodName)) {
                _pinnedBlocks.Remove(methodName);
            } else {
                _pinnedBlocks.Add(methodName);
            }

            _savePins();
            _instantiateCommandBlocks();
            _updateCommandBlocksList();
        }

        private void _savePins() {
            string condensedPins = "";
            foreach (var pins in _pinnedBlocks) {
                condensedPins += $"{pins}_";
            }

            if (condensedPins.Length != 0)
                condensedPins.Remove(condensedPins.Length - 1);

            PlayerPrefs.SetString($"PinnedCommands", condensedPins);
        }

        private void _loadPins() {
            _pinnedBlocks.Clear();
            string condensedPins = PlayerPrefs.GetString("PinnedCommands", "");

            if (condensedPins.Length == 0) return;

            foreach (var pins in condensedPins.Split('_')) {
                _pinnedBlocks.Add(pins);
            }
        }

  #endregion

        #region Navigation

        private void _upInList() {
            if (_visibleBlockCount == 0) return;

            if(SelectedParameterIndex > 0){
                SelectedParameterIndex--;
            } else {
                SetSelectedBlockIndex(GetSelectedBlockIndex() - 1);
            }
        }

        private void _downInList() {
            if (_visibleBlockCount == 0) return;
            
            if(SelectedParameterIndex < _getSelectedCommandBlock?.ParameterLength-1){
                SelectedParameterIndex++;
            } else {
                SetSelectedBlockIndex(GetSelectedBlockIndex() + 1);
            }
        }

  #endregion
        

    #endregion
        
    }
    
    #region Attributes

    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class DebugCommandGroupAttribute : System.Attribute {

        public string GroupName { get; }
        public DebugCommandGroupAttribute (string groupNameName) {
            GroupName = groupNameName;
        }
    }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class DebugCommandAttribute : System.Attribute {

        public string Group { get; }
        public DebugCommandAttribute (string group) {
            Group = group;
        }

        public DebugCommandAttribute() {
            Group = HolyDebugGroupStyles.Uncategorized;
        }
    }

    public static class DebugCommandRegistry {

        public struct MethodGroup {
            public readonly MethodInfo method;
            public DebugGroupStyle group;
            public MethodGroup (MethodInfo method, DebugGroupStyle group) {
                this.method = method;
                this.group = group;
            }
        }

        public static Dictionary<string, DebugGroupStyle> NameToGroup = new Dictionary<string, DebugGroupStyle>();
        public static Dictionary<string, MethodGroup> Commands = new Dictionary<string, MethodGroup>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterCommands() {
            _registerStyles();
            _registerCommands();
        }

        private static void _registerCommands() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (Type type in assembly.GetTypes()) {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                        try {
                            var attribute = method.GetCustomAttribute<DebugCommandAttribute>();
                            if (attribute != null) {
                                if (NameToGroup.TryGetValue(attribute.Group, out DebugGroupStyle group)) {
                                    Commands[method.Name.ToLower()] = new(method, group);
                                } else {
                                    NameToGroup[attribute.Group] = new DebugGroupStyle(attribute.Group, Color.white);
                                    Commands[method.Name.ToLower()] = new(method, NameToGroup[attribute.Group]);
                                }
                            }
                        }
                        catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }
                }
            }
        }

        private static void _registerStyles() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (Type type in assembly.GetTypes()) {
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                        try {
                            var attribute = field.GetCustomAttribute<DebugCommandGroupAttribute>();
                            if (attribute != null) {
                                NameToGroup[attribute.GroupName] = (DebugGroupStyle)field.GetValue(null);
                            }
                        }
                        catch (Exception e) {
                            Debug.LogException(e);
                        }
                    }
                }
            }
        }

        public static bool TryInvoke (string input) {
            if (input.Length == 0) return false;

            if (input.Count(c => c == '"') % 2 != 0) {
                HolyDebugConsole.OutputToConsole($"Your quotation count doesn't make sense", LogType.Warning);
                return false;
            }

            List<string> tokens = new();

            bool quotationOpen = false;
            string quotedText = "";
            string currentPhrase = "";
            for (int c = 0; c < input.Length; c++) {
                if (input[c] == '"') {
                    if (!quotationOpen && currentPhrase != "") {
                        tokens.Add(currentPhrase);
                        currentPhrase = "";
                    }

                    quotationOpen = !quotationOpen;

                    if (!quotationOpen) {
                        tokens.Add(quotedText);
                        quotedText = "";
                    }
                } else {
                    if (quotationOpen) {
                        quotedText += input[c];
                    } else {
                        if (input[c] == ' ') {
                            if (currentPhrase.Length > 0) {
                                tokens.Add(currentPhrase);
                                currentPhrase = "";
                            }
                        } else {
                            currentPhrase += input[c];
                        }
                    }
                }
            }

            if (currentPhrase != "") tokens.Add(currentPhrase);


            MethodGroup methodGroup;
            string command = tokens[0].ToLower();
            if (!Commands.TryGetValue(command, out methodGroup))
                return false;

            var parameters = methodGroup.method.GetParameters();
            if (tokens.Count - 1 != parameters.Length) {
                HolyDebugConsole.OutputToConsole($"Expected {parameters.Length} arguments but got {tokens.Count - 1}.", LogType.Warning);
                return false;
            }

            object[] parsedArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                try {
                    parsedArgs[i] = Convert.ChangeType(tokens[i + 1], parameters[i].ParameterType);
                }
                catch {
                    HolyDebugConsole.OutputToConsole($"Failed to parse argument '{tokens[i + 1]}' as {parameters[i].ParameterType.Name}.", LogType.Warning);
                    return false;
                }
            }

            methodGroup.method.Invoke(null, parsedArgs);
            return true;
        }

    }
    #endregion

}