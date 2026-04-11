using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Holylib.DebugConsole {

    [RequireComponent(typeof(UIDocument))]
    public class HolyDebugConsole : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        private bool _subscribeToConsole = true;
        
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
        public static void OutputToConsole (string message, LogType type = LogType.Log)
        {
            var key = (message, type);

            if (!_logCounts.TryAdd(key, 1))
                _logCounts[key]++;


            if (type is LogType.Error or LogType.Exception or LogType.Warning)
            {
                if (instance._subscribeToConsole) Application.logMessageReceivedThreaded -= instance._handleLog;
                Debug.LogError(message);
                if (instance._subscribeToConsole) Application.logMessageReceivedThreaded += instance._handleLog;
            }
            
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
            if (_subscribeToConsole)
            {
                Application.logMessageReceivedThreaded += _handleLog;
            }
        }

        void OnDisable() {
            if (_subscribeToConsole)
            {
                Application.logMessageReceived -= _handleLog;
            }
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

            _searchField.RegisterCallback<KeyUpEvent>(evt => {
                _selectedBlockIndex = -1;
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
        
        private bool _isInParameterEditMode;
        private double _lasTimeUpDownPressed;
        private void _InputHandling() {
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
            } else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame) {
                _leftOrRightPressed();
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
            if (!_isInParameterEditMode) {
                _selectedBlockIndex = -1;
                _updateCommandBlocksList();
            }
        }
        private void _tabPressed() {
            if (!_isInParameterEditMode && _selectedBlockParameterCount > 0) {
                _selectedParameterIndex = 0;
                _isInParameterEditMode = true;
                _updateCommandBlocksList();
            } else if (_isInParameterEditMode) {
                _selectedParameterIndex = (_selectedParameterIndex + 1 + _selectedBlockParameterCount) % _selectedBlockParameterCount;
                _updateCommandBlocksList();
            }
        }
        private void _backToSearchPressed() {
            _selectedParameterIndex = -1;
            _isInParameterEditMode = false;
            _selectedBlockIndex = -1;

            _updateCommandBlocksList();
        }
        private void _enterPressed() {
            if (_selectedBlockIndex != -1) {

                using var evt = MouseUpEvent.GetPooled();
                evt.target = _selectedBlock;
                _selectedBlock.SendEvent(evt);
            }

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
        private void _leftOrRightPressed() {
            if (_selectedBlockParameterCount > 0 && !_isInParameterEditMode) {
                _isInParameterEditMode = true;
                _selectedParameterIndex = 0;
                _updateCommandBlocksList();
            }
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
            _selectedBlockIndex = -1;

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


            bool success;
            
            try {
                success = DebugCommandRegistry.TryInvoke(input);
            }
            catch (Exception e) {
                OutputToConsole($"Command not found: {input}, Exception: {e}", LogType.Warning);
                success = false;
            }
            
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
                _logQueue.Enqueue(($"{logString}\n{stackTrace}", type));
            }
        }

        private readonly Queue<(string, LogType)> _logQueue = new Queue<(string, LogType)>();
        

  #endregion
        
        #region Command Blocks
        
        private int _selectedBlockIndex = -1;
        private int _blockCount;
        private int _selectedParameterIndex;
        private int _selectedBlockParameterCount;
        private VisualElement _selectedBlockParameterParent;
        private VisualElement _selectedBlock;
        
        #region Visuals
                private void _instantiateCommandBlocks() {

            _selectedBlockIndex = -1;
            _selectedParameterIndex = -1;
            _isInParameterEditMode = false;
            _selectedBlockParameterCount = -1;
            _selectedBlock = null;

            _blocksUI.Clear();

            Dictionary<DebugGroupStyle, List<VisualElement>> groups = new();

        #region Pinned Group

            var pinnedGroup = _commandBlockGroup.Instantiate();
            pinnedGroup.name = _pinnedGroupName;
            pinnedGroup.Q<Label>().text = $"⭐ {_pinnedGroupName}";
            _blocksUI.Add(pinnedGroup);

  #endregion


            foreach (var command in DebugCommandRegistry.Commands) {
                var parameters = command.Value.method.GetParameters();

                VisualElement block;

                if (parameters.Length == 0) {
                    block = _instantiateRegularCommandBlock(command.Value.name);
                } else {
                    block = _instantiateParameterCommandBlock(command.Value.name, parameters);
                }

                block.name = command.Value.group.Name;

                if (_pinnedBlocks.Contains(command.Value.method.Name)) {
                    block.Q<Button>("Pin").text = "Unpin";
                    block.name += "_" + _pinnedGroupName;
                } else {
                    block.Q<Button>("Pin").text = "Pin";

                }


                block.Children().First().style.borderLeftColor = command.Value.group.Color;

                if (!groups.ContainsKey(command.Value.group)) {
                    groups[command.Value.group] = new();
                }



                if (_pinnedBlocks.Contains(command.Value.method.Name)) {
                    _blocksUI.Q<TemplateContainer>(_pinnedGroupName).Q<VisualElement>("Blocks").Add(block);
                } else {
                    groups[command.Value.group].Add(block);
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


            _blockCount = DebugCommandRegistry.Commands.Count;
        }
        private void _updateCommandBlocksList() {
            string input = _searchField.value;

            int visibleBlockCount = 0;
            foreach (var group in _blocksUI.Children()) {

                int visibleBlockCountInGroup = 0;
                foreach (var block in group.Q<VisualElement>("Blocks").Children()) {
                    string commandName = block.Q<Label>("CommandBlockLabel").text.ToLower();
                    if (commandName.Contains(input.ToLower()) || block.name.ToLower().Contains(input.ToLower())) {
                        visibleBlockCount++;
                        visibleBlockCountInGroup++;
                        block.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);

                        var childBlock = block.Children().First();

                        if (_selectedBlockIndex == visibleBlockCount - 1) {

                            _selectedBlockParameterParent = childBlock.Q<VisualElement>("Parameters");

                            _selectedBlockParameterCount = _selectedBlockParameterParent?.childCount ?? 0;
                            _selectedBlock = childBlock;
                            _blocksUI.ScrollTo(block);

                            childBlock.AddToClassList("command-block-pseudo-hover");

                            if (_selectedBlockParameterParent != null) {
                                if (_selectedParameterIndex >= 0) {
                                    int a = 0;
                                    foreach (var childParameter in _selectedBlockParameterParent.Children()) {

                                        if (a == _selectedParameterIndex) {
                                            childParameter.Q<TextField>().Focus();
                                            break;
                                        }
                                        a++;
                                    }
                                }
                            }

                        } else {
                            childBlock.RemoveFromClassList("command-block-pseudo-hover");
                        }

                    } else {
                        block.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    }
                }

                if (visibleBlockCountInGroup > 0) {
                    group.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                } else {
                    group.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
            }

            if (_selectedBlockIndex == -1) {
                _selectedBlock = null;
                _selectedBlockParameterCount = 0;
                _selectedParameterIndex = -1;

                _searchField.Focus();
            } else if (!_isInParameterEditMode) {
                _selectedBlock.Focus();
            }

            _blockCount = visibleBlockCount;
        }
        
        private VisualElement _instantiateRegularCommandBlock (string command) {
            var block = _regularCommandBlock.Instantiate();
            block.Q<Label>("CommandBlockLabel").text = command;
            block.RegisterCallback<MouseUpEvent>((a) => {

                _playExecuteAnimation(block.Children().First(), _executeCommand(command));
            });
            block.Q<Button>("Pin").clicked += () => {
                _pinBlock(command);
            };
            return block;
        }

        private VisualElement _instantiateParameterCommandBlock (string command, ParameterInfo[] parameters) {
            var block = _parameterCommandBlock.Instantiate();
            block.Q<Label>("CommandBlockLabel").text = command;
            _blocksUI.Add(block);

            List<(TextField field, Type type)> _parameterFields = new();

            foreach (var parameter in parameters) {

                var parameterField = _commandBlockParameter.Instantiate();
                var field = parameterField.Q<TextField>("CommandBlockParameter");
                field.label = parameter.Name;
                _parameterFields.Add(new(field, parameter.ParameterType));

                block.Q<VisualElement>("Parameters").Add(parameterField);
            }

            block.Q<Button>("Pin").clicked += () => {
                _pinBlock(command);
            };

            block.RegisterCallback<MouseUpEvent>((a) => {
                string fullCommand = command;

                foreach (var field in _parameterFields) {

                    if (field.type == typeof(string)) {
                        fullCommand += $" \"{field.field.value}\"";
                    } else {
                        fullCommand += $" {field.field.value}";
                    }

                }

                _playExecuteAnimation(block.Children().First(), _executeCommand(fullCommand));
            });

            return block;
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

        private List<string> _pinnedBlocks = new();
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

            PlayerPrefs.SetString("PinnedCommands", condensedPins);
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
            if (_blockCount == 0) return;

            if (_isInParameterEditMode && _selectedBlockParameterCount > 0) {

                _selectedParameterIndex--;

                if (_selectedParameterIndex < 0) {
                    _isInParameterEditMode = false;

                    _selectedBlockIndex--;
                    if (_selectedBlockIndex < 0) {
                        _selectedBlockIndex = -1;
                    }

                    _selectedParameterIndex = -1;

                    _updateCommandBlocksList();

                    if (_selectedBlockParameterCount > 0) {
                        _isInParameterEditMode = true;
                        _selectedParameterIndex = _selectedBlockParameterCount - 1;
                        _updateCommandBlocksList();
                    }
                } else {

                    if (_selectedBlockIndex == -1) {
                        _selectedBlockIndex = _blockCount - 1;
                        _isInParameterEditMode = false;
                    }

                    _updateCommandBlocksList();
                }


            } else {
                if (_selectedBlockIndex == -1) {
                    _selectedBlockIndex = _blockCount - 1;

                } else {
                    _selectedBlockIndex--;
                    if (_selectedBlockIndex < 0) {
                        _selectedBlockIndex = -1;
                        _isInParameterEditMode = false;
                    }
                    _selectedParameterIndex = -1;
                }


                _updateCommandBlocksList();

                if (_selectedBlockParameterCount > 0) {
                    _isInParameterEditMode = true;
                    _selectedParameterIndex = _selectedBlockParameterCount - 1;
                    _updateCommandBlocksList();
                }
            }

        }

        private void _downInList() {
            if (_blockCount == 0) return;

            if (_isInParameterEditMode) {
                if (_selectedBlockParameterCount > 0) {
                    _selectedParameterIndex++;

                    if (_selectedParameterIndex >= _selectedBlockParameterCount) {
                        _isInParameterEditMode = false;

                        _selectedBlockIndex++;
                        if (_selectedBlockIndex >= _blockCount) {
                            _selectedBlockIndex = -1;
                        }
                        _selectedParameterIndex = -1;

                        _updateCommandBlocksList();

                        if (_selectedBlockParameterCount > 0) {
                            _isInParameterEditMode = true;
                            _selectedParameterIndex = 0;
                            _updateCommandBlocksList();
                        }
                    } else {
                        _updateCommandBlocksList();
                    }
                }

            } else {

                _selectedBlockIndex++;
                if (_selectedBlockIndex >= _blockCount) {
                    _selectedBlockIndex = -1;
                    _isInParameterEditMode = false;
                }

                _selectedParameterIndex = -1;

                _updateCommandBlocksList();

                if (_selectedBlockParameterCount > 0) {
                    _isInParameterEditMode = true;
                    _selectedParameterIndex = 0;
                    _updateCommandBlocksList();
                }
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
            public readonly string name;
            public readonly MethodInfo method;
            public DebugGroupStyle group;
            public MethodGroup (MethodInfo method, DebugGroupStyle group, string name) {
                this.method = method;
                this.group = group;
                this.name = name;
            }
        }

        public static Dictionary<string, DebugGroupStyle> NameToGroup = new Dictionary<string, DebugGroupStyle>();
        public static Dictionary<string, MethodGroup> Commands = new Dictionary<string, MethodGroup>();
        public static Dictionary<string, Dictionary<string, ScriptableObject>> ScriptableObjects = new Dictionary<string, Dictionary<string, ScriptableObject>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterCommands() {
            _registerStyles();
            _registerCommands();
        }

        private static void _registerCommands() {
            Stopwatch stopwatch = Stopwatch.StartNew();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (assembly.GetName().Name != "Core")
                {
                    continue;
                }
                foreach (Type type in assembly.GetTypes()) {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Default | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                        try {
                            var attribute = method.GetCustomAttribute<DebugCommandAttribute>();
                            if (attribute == null) continue;
                            
                            if (method.DeclaringType.IsSubclassOf(typeof(ScriptableObject)))
                            {
#if UNITY_EDITOR
                                string search = $"t:{method.DeclaringType.Name}";
                                string[] guids = AssetDatabase.FindAssets(search, new[] { "ScriptableObjects" });
    
                                Dictionary<string, ScriptableObject> values = new Dictionary<string, ScriptableObject>();
                                if (!NameToGroup.TryGetValue(attribute.Group, out DebugGroupStyle group))
                                {
                                    group = new DebugGroupStyle(attribute.Group, Color.white);
                                    NameToGroup[attribute.Group] = group;
                                }
                                
                                foreach (string guid in guids)
                                {
                                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                                    ScriptableObject data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                                    if (data != null)
                                    {
                                        string keyString = $"{method.Name}_{data.name}";
                                        keyString = keyString.Replace(' ', '_');
                                        
                                        values.Add(keyString, data);
                                        Commands[keyString] = new MethodGroup(method, group, keyString);
                                    }
                                }
                                
                                ScriptableObjects[method.Name] = values;
#endif   
                            }
                            else
                            {
                                if (NameToGroup.TryGetValue(attribute.Group, out DebugGroupStyle group)) {
                                    Commands[method.Name] = new MethodGroup(method, group, method.Name);
                                } else {
                                    NameToGroup[attribute.Group] = new DebugGroupStyle(attribute.Group, Color.white);
                                    Commands[method.Name] = new MethodGroup(method, NameToGroup[attribute.Group], method.Name);
                                }
                            }
                        }
                        catch (Exception e) { 
                            Debug.LogException(e);
                        }
                    }
                }
            }
            stopwatch.Stop();
            Debug.Log($"Time to register debug commands: {stopwatch.Elapsed.TotalMilliseconds}ms");
        }

        private static void _registerStyles()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (Type type in assembly.GetTypes())
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                try
                {
                    var attribute = field.GetCustomAttribute<DebugCommandGroupAttribute>();
                    if (attribute != null)
                    {
                        NameToGroup[attribute.GroupName] = (DebugGroupStyle)field.GetValue(null);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
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

            string command = tokens[0];
            if (!Commands.TryGetValue(command, out MethodGroup methodGroup))
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

            if (methodGroup.method.IsStatic)
            { 
                methodGroup.method.Invoke(null, parsedArgs);
            }
            else
            {
                Type ownerClass = methodGroup.method.DeclaringType;
                if (ownerClass != null && ownerClass.IsSubclassOf(typeof(MonoBehaviour)))
                {
                    Object[] objs = Object.FindObjectsByType(methodGroup.method.DeclaringType);
                    foreach (Object obj in objs)
                    {
                        methodGroup.method.Invoke(obj, parsedArgs);
                    }
                }
                else if (ownerClass != null && ownerClass.IsSubclassOf(typeof(ScriptableObject)))
                {
                    if (ScriptableObjects.TryGetValue(methodGroup.method.Name, out Dictionary<string, ScriptableObject> value)
                        && value.TryGetValue(methodGroup.name, out ScriptableObject obj))
                    {
                        methodGroup.method.Invoke(obj, parsedArgs);
                    }
                }
            }
            return true;
        }
    }
    #endregion

}