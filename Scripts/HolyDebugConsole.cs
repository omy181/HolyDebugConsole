using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Holylib.DebugConsole {

    [RequireComponent(typeof(UIDocument))]
    public class HolyDebugConsole : MonoBehaviour {

        [Header("Settings")]
        [Tooltip("Debug commands in unity assemblies won't show up")]
        [field:SerializeField] public bool ExcludeUnityAssemblies { get; private set; } = true;
        [Tooltip("Debug commands outside of CSharp assembly won't show up")]
        [field:SerializeField] public bool OnlyIncludeCSharpAssembly { get;private set;} = true;


        #region References
        [Header("References")]
        [HideInInspector][SerializeField] private VisualTreeAsset _parameterCommandBlock;
        [HideInInspector][SerializeField] private VisualTreeAsset _commandBlockParameter;
        [HideInInspector][SerializeField] private VisualTreeAsset _commandBlockGroup;
        [HideInInspector][SerializeField] private VisualTreeAsset _consoleLine;
        [HideInInspector][SerializeField] private VisualTreeAsset _commandBlockBoolParameter;
        
        private VisualElement _root;
        private ScrollView _blocksUI;
        private TextField _searchField;
        private Label _commandList;
        private ScrollView _consoleView;
        
        private UIDocument _uiDocument => GetComponent<UIDocument>();
#endregion

        public static bool IsConsoleOpen { get; private set; }
        public static HolyDebugConsole instance;
        public void ToggleConsole() => _toggleConsole();
        public Action<bool> OnConsoleToggled;
        public void ExecuteLastCommand() => _executeCommand(_searchField.value);
        public void ExecuteCommand (string input) => _executeCommand(input);
        public static void OutputToConsole (LogElement logElement) {
            _logs.Add(logElement);
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
            _updateVariableFields();
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

            _consoleView = instance._root.Q<ScrollView>("ConsoleView");
            
            var clearConsoleButton = _root.Q<Button>("ClearConsole");
            clearConsoleButton.clicked += _clearConsole;

            var exitConsole = _root.Q<Button>("Exit");
            exitConsole.clicked += _toggleConsole;
            
            var hideShowConsole = _root.Q<Button>("HideShow");
            hideShowConsole.clicked += _hideShowConsole;
            
            var sizeIncreaseButton = _root.Q<Button>("SizePlus");
            sizeIncreaseButton.clicked += _increaseFontSize;
            
            var sizeDecreaseButton = _root.Q<Button>("SizeMinus");
            sizeDecreaseButton.clicked += _decreaseFontSize;
            
            var collapseConsole = _root.Q<Button>("CollapseConsole");
            collapseConsole.clicked += _collapseToggleConsole;
            
            var hideLog = _root.Q<Button>("LogHide");
            hideLog.clicked += ()=>_toggleLogHide(HolyLogType.Log);
            
            var hideWarning = _root.Q<Button>("WarningHide");
            hideWarning.clicked += ()=>_toggleLogHide(HolyLogType.Warning);
            
            var hideError = _root.Q<Button>("ErrorHide");
            hideError.clicked += ()=>_toggleLogHide(HolyLogType.Error);
            
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
            _setConsoleVisibility();
            _setConsoleCollapse();
            _setLogButtons();
        }
        
  #endregion

        #region Input Handling
        
        private double _lasTimeUpDownPressed;
        private void _InputHandling() {
            
            _keybindingInputCheck();
            
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
        private void _outputTheQueueUpdate() {
            lock (_logQueue) {
                while (_logQueue.Count > 0) {
                    var logeElement = _logQueue.Dequeue();
                    OutputToConsole(logeElement);
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
        private bool _modifyVariableCommand(CommandBlock commandBlock) {
                try {

                    object newVal;
                    if (commandBlock.Field.GetType() == typeof(bool)) {
                        newVal = !(bool)commandBlock.Field.GetValue(); // if type is bool, there is no input field, it just inverts the value
                    } else {
                        newVal = Convert.ChangeType(commandBlock.ParameterFields[0].GetValue(), commandBlock.Field.GetType());
                    }
                    
                    commandBlock.Field.SetValue(newVal);
                    return true;
                }
                catch (Exception e) {
                    Debug.LogException(e);
                    return false;
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
                Debug.LogWarning($"Command not found: {input}, Exception: {e}");
                success = false;
            }
            
            return success;
        }

        private static readonly List<LogElement> _logs = new List<LogElement>();
        
        private static void _refreshOutput() {

            instance._consoleView.Clear();

            bool isCollapse = PlayerPrefs.GetInt(ConsoleCollapsePlayerPref,0) == 1 ? true : false;
            
            Dictionary<LogElement, int> logCounts = new();
            foreach (var entry in _logs) {

                if (isCollapse) {
                    if (!logCounts.TryAdd(entry,1)) {
                        logCounts[entry]++;
                    }
                } else {
                    if(!instance._isTypeHidden(entry.type))
                        instance._instantiateConsoleLine(entry);
                }
            }

            if (isCollapse) {
                foreach (var key in logCounts) {
                    if(!instance._isTypeHidden(key.Key.type))
                        instance._instantiateConsoleLine(key.Key,key.Value);
                }
            }
            
            
            instance._setFontSize();
        }

        private void _clearConsole() {
            _logs.Clear();
            _refreshOutput();
        }
        private const string ConsoleVisibilityPlayerPref = "IsConsoleVisible";

        private void _hideShowConsole() {
            bool isVisible = PlayerPrefs.GetInt(ConsoleVisibilityPlayerPref, 1) == 1  ? true : false;
            isVisible = !isVisible;
            PlayerPrefs.SetInt(ConsoleVisibilityPlayerPref, isVisible ? 1 : 0);

            _setConsoleVisibility();
        }

        private void _setConsoleVisibility() {
            bool isVisible = PlayerPrefs.GetInt(ConsoleVisibilityPlayerPref, 1) == 1  ? true : false;
            _root.Q<ScrollView>("ConsoleView").style.visibility = isVisible ? Visibility.Visible : Visibility.Hidden;
            
            var hideShowConsole = _root.Q<Button>("HideShow");
            hideShowConsole.text = isVisible ? "Hide Console" : "Show Console";
        }
        
        private const string ConsoleCollapsePlayerPref = "IsConsoleCollapsed";

        private void _collapseToggleConsole() {
            bool isCollapsed = PlayerPrefs.GetInt(ConsoleCollapsePlayerPref, 0) == 1  ? true : false;
            isCollapsed = !isCollapsed;
            PlayerPrefs.SetInt(ConsoleCollapsePlayerPref, isCollapsed ? 1 : 0);

            _setConsoleCollapse();
        }

        private void _setConsoleCollapse() {
            bool isCollapsed = PlayerPrefs.GetInt(ConsoleCollapsePlayerPref, 0) == 1  ? true : false;

            var collapseConsole = _root.Q<Button>("CollapseConsole");
            collapseConsole.text = isCollapsed ? "UnCollapse Console" : "Collapse Console";
            _refreshOutput();
        }
        
        private const string _logHidePlayerPref = "LogHideValue";
        
        private void _toggleLogHide(HolyLogType holyLogType) {
            int state = PlayerPrefs.GetInt(_logHidePlayerPref, 0);
            
            PlayerPrefs.SetInt(_logHidePlayerPref,state ^ (int)holyLogType );
            
            _setLogButtons();
            
            _refreshOutput();
        }

        private void _setLogButtons() {
            var hideLog = _root.Q<Button>("LogHide");
            if (_isTypeHidden(HolyLogType.Log)) {
                hideLog.AddToClassList("log-button-pseudo-selected");
            } else {
                hideLog.RemoveFromClassList("log-button-pseudo-selected");
            }

            var hideWarning = _root.Q<Button>("WarningHide");
            if (_isTypeHidden(HolyLogType.Warning)) {
                hideWarning.AddToClassList("log-button-pseudo-selected");
            } else {
                hideWarning.RemoveFromClassList("log-button-pseudo-selected");
            }
            
            var hideError = _root.Q<Button>("ErrorHide");
            if (_isTypeHidden(HolyLogType.Error)) {
                hideError.AddToClassList("log-button-pseudo-selected");
            } else {
                hideError.RemoveFromClassList("log-button-pseudo-selected");
            }
        }
        
        private bool _isTypeHidden(HolyLogType holyLogType) {
            int state = PlayerPrefs.GetInt(_logHidePlayerPref, 0);
           return (state & (int)holyLogType) != 0;
        }

        private readonly int _defaultFontSize = 14;
        private const string FontPlayerPref = "DebugConsoleFontSize";

        private List<(VisualElement element, float ratio)> _trackedFontSizeElements = new();
        
        private void _trackFontSizeElement(VisualElement element) {
            var textElements = element.Query(className: "unity-text-element").ToList();
    
            // If no text children found, track the element itself
            var targets = textElements.Count > 0 ? textElements : new List<VisualElement> { element };

            foreach (var target in targets) {
                float ratio = target.resolvedStyle.fontSize / _defaultFontSize;
                _trackedFontSizeElements.Add((target, ratio));
            }

            element.RegisterCallback<DetachFromPanelEvent>(_ =>
                    _trackedFontSizeElements.RemoveAll(entry => targets.Contains(entry.element))
                );
        }

        private void _applyFontSize() {
            int fontSize = PlayerPrefs.GetInt(FontPlayerPref, _defaultFontSize);
            foreach (var (element, ratio) in _trackedFontSizeElements) {
                element.style.fontSize = Mathf.RoundToInt(fontSize * ratio);
            }
        }

        private void _setFontSize() {
            _applyFontSize();
        }

        private void _increaseFontSize() {
            int fontSize = PlayerPrefs.GetInt(FontPlayerPref, _defaultFontSize);
            fontSize = Mathf.Clamp(fontSize + 1, 10, 60);
            PlayerPrefs.SetInt(FontPlayerPref, fontSize);
            _applyFontSize();
        }

        private void _decreaseFontSize() {
            int fontSize = PlayerPrefs.GetInt(FontPlayerPref, _defaultFontSize);
            fontSize = Mathf.Clamp(fontSize - 1, 10, 60);
            PlayerPrefs.SetInt(FontPlayerPref, fontSize);
            _applyFontSize();
        }
        
        private void _handleLog (string logString, string stackTrace, UnityEngine.LogType type) {
            lock (_logQueue) {
                
                var customType = type switch
                {
                    UnityEngine.LogType.Log         => HolyLogType.Log,
                    UnityEngine.LogType.Warning     => HolyLogType.Warning,
                    UnityEngine.LogType.Error       => HolyLogType.Error,
                    UnityEngine.LogType.Assert      => HolyLogType.Error,
                    UnityEngine.LogType.Exception   => HolyLogType.Error,
                    _                               => HolyLogType.None
                };
                
                
                _logQueue.Enqueue(new LogElement(logString,stackTrace, customType,DateTime.Now.ToShortTimeString()));
            }
        }

        private readonly Queue<LogElement> _logQueue = new Queue<LogElement>();
        
        public struct LogElement {
            public readonly string message;
            public readonly string stackTrace;
            public readonly HolyLogType type;
            public readonly string time;
            public LogElement(string message,string stackTrace, HolyLogType type,string time) {
                this.message = message;
                this.type = type;
                this.stackTrace = stackTrace;
                this.time = time;
            }
        }
        
        #region Console Visualisation

        private void _instantiateConsoleLine(LogElement logElement,int count = 1) {
            
            string colorStart = "";
            string colorEnd = "";
            Color logColor = Color.white;

            if (logElement.type == HolyLogType.Error || logElement.type == HolyLogType.Assert || logElement.type == HolyLogType.Exception) {
                colorStart = "<color=\"red\">";
                colorEnd = "</color>";
                logColor =  Color.red;
            } else if (logElement.type == HolyLogType.Warning) {
                colorStart = "<color=\"yellow\">";
                colorEnd = "</color>";
                logColor = Color.yellow;
            }
                
            string coloredMessage = ($"{colorStart}{logElement.message}{colorEnd}");

            var logLine = _consoleLine.Instantiate();
            
            logLine.Q<Label>("Time").text = logElement.time;

            logLine.Q<VisualElement>("Icon").style.unityBackgroundImageTintColor = new StyleColor(logColor);
            
            logLine.Q<Label>("MainDebugText").text = coloredMessage;
            var stackTraceField = logLine.Q<TextField>("DebugTextStackTrace");
            stackTraceField.value = logElement.stackTrace;
            
            _root.Q<VisualElement>("ConsoleView").Add(logLine);


            if (count > 1) {
                logLine.Q<Label>("Count").text = count.ToString();
            } else {
                logLine.Q<Label>("Count").text = "";
            }
            
            
            stackTraceField.style.display = DisplayStyle.None;
            
            logLine.RegisterCallback<ClickEvent>(evt => {
                if (!stackTraceField.Contains(evt.target as VisualElement)) {
                    if (stackTraceField.style.display == DisplayStyle.None) {
                        stackTraceField.style.display = DisplayStyle.Flex;
                        stackTraceField.Focus();
                    } else {
                        stackTraceField.style.display = DisplayStyle.None;
                    }
                }
            });
            
            stackTraceField.RegisterCallback<BlurEvent>(evt => {
                stackTraceField.style.display = DisplayStyle.None;
            });
            

            _trackFontSizeElement(logLine);
        }
        
        
            #endregion

  #endregion
        
        #region Command Blocks

        private List<CommandBlock> _commandBlocks = new();
        private List<CommandBlock> _visibleCommandBlocks = new();
        private Dictionary<string, CommandBlock> _methodNameToCommandBlock = new();

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
                        parameterField.Focus();
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
            public VariableField Field { get; private set; }
            public ParameterInfo[] Parameters { get; private set; }
            public int ParameterLength => Mathf.Max(ParameterFields?.Count ?? 0,Parameters?.Length ?? 0);
            public List<ParameterField> ParameterFields;
            public VisualElement VisualBlock;
            public VisualElement ParameterParent;
            public Label VariableFieldText;
            

            public void Initialize(DebugCommandRegistry.MethodGroup methodGroup) {
                bool isVariableField = methodGroup.method == null;
                
                MethodName = !isVariableField ? methodGroup.method.Name : methodGroup.field?.Name ?? methodGroup.property.Name;
                GroupStyle = methodGroup.group;
                Parameters = !isVariableField ? methodGroup.method.GetParameters() : Array.Empty<ParameterInfo>();
                Field = isVariableField ? new VariableField(methodGroup.field,methodGroup.property,methodGroup.isReadOnly) : null;
                _createVisualBlock();
                RefreshVariable();

                if (isVariableField) {
                    instance._commandBlocksToUpdate.Add(this);
                }
            }
            
            public void RunCommand() {

                instance.SetSelectedBlockIndex(instance._visibleCommandBlocks.IndexOf(this));

                if (Field != null) {
                    
                    if(Field.IsReadOnly) return;
                    
                    instance._playExecuteAnimation(VisualBlock.Children().First(), instance._modifyVariableCommand(this));
                    RefreshVariable();
                    return;
                }
                
                if (ParameterLength == 0) {
                    instance._playExecuteAnimation(VisualBlock.Children().First(), instance._executeCommand(MethodName));
                } else {
                    string fullCommand = MethodName;

                    foreach (var field in ParameterFields) {

                        if (field.Type == typeof(string)) {
                            fullCommand += $" \"{field.GetValue()}\"";
                        } else {
                            fullCommand += $" {field.GetValue()}";
                        }

                    }

                    instance._playExecuteAnimation(VisualBlock.Children().First(), instance._executeCommand(fullCommand));
                }
            }

            private void _createVisualBlock() {
                
                var parameterCommandBlockOutput = instance._instantiateParameterCommandBlock(this);
                VisualBlock = parameterCommandBlockOutput.visualElement;
                ParameterFields = parameterCommandBlockOutput.parameterFields;

                VisualBlock.name = GroupStyle.Name;

                if (IsPinned) {
                    VisualBlock.Q<Button>("Pin").text = "Unpin";
                    VisualBlock.name += "_" + _pinnedGroupName;
                } else {
                    VisualBlock.Q<Button>("Pin").text = "Pin";
                }
                
                VisualBlock.Children().First().style.borderLeftColor = GroupStyle.Color;
            }

            public void RefreshVariable() {
                if(VariableFieldText != null)
                    VariableFieldText.text = Field != null ? Field.GetValue()?.ToString() : "";
            }

            public override string ToString() {
                return $"{MethodName}-{GroupStyle.Name}";
            }
        }
        
        private class VariableField {
            private FieldInfo _field;
            private PropertyInfo _property;
            public readonly bool IsReadOnly;
            
            public VariableField(FieldInfo field, PropertyInfo property,bool isReadOnly) {
                _field = field;
                _property = property;
                IsReadOnly = isReadOnly;
            }

            public Type GetType() {
                return _field != null ? _field.FieldType : _property.PropertyType;
            }

            public object GetValue() {
                return _field != null ? _field.GetValue(null) : _property.GetValue(null);
            }

            public void SetValue (object value) {
                if (_field != null) {
                    _field.SetValue(null, value);
                } else {
                    _property.SetValue(null, value);
                }
            }
        }

        private List<CommandBlock> _commandBlocksToUpdate = new();
        private void _updateVariableFields() {
            
            if(!IsConsoleOpen) return;
            
            foreach (var commandBlock in _commandBlocksToUpdate) {
                commandBlock.RefreshVariable();
            }
        }
        
        #region Visuals
        private void _instantiateCommandBlocks() {
            
            _loadKeybinds();
            
            SetSelectedBlockIndex(-1);
            
            _blocksUI.Clear();
            _commandBlocks.Clear();
            _methodNameToCommandBlock.Clear();
            
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
                if (!_methodNameToCommandBlock.TryAdd(commandBlock.MethodName, commandBlock)) {
                    Debug.LogError($"There are multiple commands with the nane '{commandBlock.MethodName}'\nChange their names.");
                }
                
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
                
                
                parameterFields.Add(new(() => field.value, parameter.ParameterType,field.Focus));

                block.Q<VisualElement>("Parameters").Add(parameterField);
                parameterIndex++;
            }
            
            // Variable Field
            commandBlock.VariableFieldText = block.Q<Label>("VariableValue");

            if (commandBlock.Field != null) {
                
                if (commandBlock.Field.GetType() == typeof(bool)) {


                } else if(!commandBlock.Field.IsReadOnly) {
                    TemplateContainer parameterField = _commandBlockParameter.Instantiate();
                    
                    var field = parameterField.Q<TextField>("CommandBlockParameter");
                    field.label = " ";
                    field.RegisterCallback<FocusEvent>(evt => {
                        SetSelectedBlockIndex(instance._visibleCommandBlocks.IndexOf(commandBlock),parameterIndex);
                    });
                
                    parameterFields.Add(new(()=>field.value, commandBlock.Field.GetType(),field.Focus));

                    block.Q<VisualElement>("Parameters").Add(parameterField);
                }
                
            }
            
            
            // keybinds
            var keybindDropdown = block.Q<DropdownField>("KeybindDropdown");
            var keybindButton = block.Q<Button>("KeybindButton");
            
            keybindButton.RegisterCallback<ClickEvent>((evt) => {

                if (keybindDropdown.style.display == DisplayStyle.Flex) {
                    keybindDropdown.style.display =  new StyleEnum<DisplayStyle>(DisplayStyle.None);
                } else {
                    keybindDropdown.style.display =  new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                }
                
            });
            
            var keys = Enum.GetValues(typeof(Key))
                .Cast<Key>()
                .Where(k => k == Key.None || (k >= Key.A && k <= Key.Z) ||
                    k is Key.Digit0 or Key.Digit1 or Key.Digit2 or Key.Digit3 or Key.Digit4
                        or Key.Digit5 or Key.Digit6 or Key.Digit7 or Key.Digit8 or Key.Digit9)
                .Select(k => k.ToString())
                .ToList();

            keybindDropdown.choices = keys;
            if (_keybindings.TryGetValue(commandBlock.MethodName, out var keybinding)) {
                keybindDropdown.value = keybindDropdown.choices.Find(c=>string.Equals(c,keybinding.ToString()));
                
                setKeybind(keybinding.ToString());
                
            }

            keybindDropdown.RegisterValueChangedCallback((evt) => {
                
                setKeybind(evt.newValue);
                
                _saveKeybinds();
                
                keybindDropdown.style.display =  new StyleEnum<DisplayStyle>(DisplayStyle.None);
            });
            
            
            keybindDropdown.style.display =  new StyleEnum<DisplayStyle>(DisplayStyle.None);
            

            block.Q<Button>("Pin").clicked += () => {
                _pinBlock(commandBlock.MethodName);
            };

            block.RegisterCallback<MouseUpEvent>((a) => {
                commandBlock.RunCommand();
            });

            return (block,parameterFields);

            void setKeybind(string selected) {

                string keyName = char.IsDigit(selected[0]) ? "Digit" + selected : selected;
    
                var keycode = Enum.TryParse(keyName, out Key key) ? key : Key.None;

                if (_setKeybinding(commandBlock.MethodName, keycode)) {
                    keybindButton.text = keyName;
                } else {
                    keybindButton.text = "Keybind";
                }
            }
        }
        
        private struct ParameterField {

            private Action _focus;
            private Func<string> _getValue;
            public Type Type;
            public ParameterField(Func<string> getValueFunc, Type type,Action focus) {
                _getValue = getValueFunc;
                Type = type;
                _focus = focus;
            }

            public string GetValue() {
                return _getValue();
            }

            public void Focus() {
                _focus.Invoke();
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
                condensedPins += $"{pins}%";
            }

            if (condensedPins.Length != 0)
                condensedPins.Remove(condensedPins.Length - 1);

            PlayerPrefs.SetString($"PinnedCommands", condensedPins);
        }

        private void _loadPins() {
            _pinnedBlocks.Clear();
            string condensedPins = PlayerPrefs.GetString("PinnedCommands", "");

            if (condensedPins.Length == 0) return;

            foreach (var pins in condensedPins.Split('%')) {
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

    #region Keybinds

        private const string _keybindSavePlayerPref = "CommandKeybinds";
        private const string _keybindSaveSeperator = "%";
        private Dictionary<string,Key> _keybindings = new();

        private void _saveKeybinds() {
            string save = "";
            var keybindings = _keybindings.ToList();
            for (int i = 0;i< _keybindings.Count;i++) {

                if (i != 0) {
                    save += _keybindSaveSeperator;
                }
                save += $"{keybindings[i].Key}-{(int)keybindings[i].Value}";
            }
            
            PlayerPrefs.SetString(_keybindSavePlayerPref,save);
        }

        private void _loadKeybinds() {
            _keybindings.Clear();
            var save = PlayerPrefs.GetString(_keybindSavePlayerPref,"");
            if (save  == "") return;
            
            
            foreach (var keybinds in save.Split(_keybindSaveSeperator)) {
                var values = keybinds.Split('-');

                var methodName = values[0];
                Key keyCode = Enum.Parse<Key>(values[1]);
                
                if (!_keybindings.TryAdd(methodName, keyCode)) {
                    Debug.LogError($"Couldn't load the keybind '{keyCode.ToString()}' for {methodName}");
                }
            }
            
        }

        private bool _setKeybinding(string methodName,Key keycode) {

            if (keycode == Key.None) {
                _keybindings.Remove(methodName);
                return false;
            }
            
            _keybindings[methodName] = keycode;
            
            return true;
        }

        private void _keybindingInputCheck() {
            
            if(!Keyboard.current.hKey.isPressed) return;
            
            foreach (var keybinding in _keybindings) {
                if (Keyboard.current[keybinding.Value].wasReleasedThisFrame) {
                    _methodNameToCommandBlock[keybinding.Key].RunCommand();
                }
            }
        }

        [ContextMenu("Erase Keybinds")]
        private void _eraseKeybinds() {
            PlayerPrefs.SetString(_keybindSavePlayerPref,"");
            Debug.Log("Keybinds Erased");
        }

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

    public static partial class DebugCommandRegistry {
        
        public struct MethodGroup {
            public readonly PropertyInfo property;
            public readonly FieldInfo field;
            public readonly MethodInfo method;
            public DebugGroupStyle group;
            public readonly bool isReadOnly;
            public MethodGroup (MethodInfo method, DebugGroupStyle group, FieldInfo field, PropertyInfo property, bool isReadOnly) {
                this.method = method;
                this.group = group;
                this.field = field;
                this.property = property;
                this.isReadOnly = isReadOnly;
            }
        }

        private static string[] UnityAssemblies = new []{
            "Unity","UnityEngine","System","UnityEditor"
        };

        public static Dictionary<string, DebugGroupStyle> NameToGroup = new Dictionary<string, DebugGroupStyle>();
        public static Dictionary<string, MethodGroup> Commands = new Dictionary<string, MethodGroup>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterCommands() {

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                string assemblyName = assembly.FullName;

                if (!assemblyName.StartsWith("HolyDebugConsole")) {
                    if(HolyDebugConsole.instance.OnlyIncludeCSharpAssembly && !assemblyName.StartsWith("Assembly-CSharp")) continue;
                    if(HolyDebugConsole.instance.ExcludeUnityAssemblies && UnityAssemblies.Any(assemblyName.StartsWith)) continue;
                }

                foreach (Type type in assembly.GetTypes()) {
                    _registerStyles(type);
                    _registerCommands(type);
                    _registerVariables(type);
                }
            }
        }

        private static void _registerCommands (Type type) {

            foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)) {
                try {
                    var attribute = method.GetCustomAttribute<DebugCommandAttribute>();
                    if (attribute != null) {
                        if (NameToGroup.TryGetValue(attribute.Group, out DebugGroupStyle group)) {
                            Commands[method.Name.ToLower()] = new(method, group, null, null, false);
                        } else {
                            NameToGroup[attribute.Group] = new DebugGroupStyle(attribute.Group, Color.white);
                            Commands[method.Name.ToLower()] = new(method, NameToGroup[attribute.Group], null, null, false);
                        }
                    }
                }
                catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        private static void _registerStyles (Type type) {

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

        public static bool TryInvoke (string input) {
            if (input.Length == 0) return false;

            if (input.Count(c => c == '"') % 2 != 0) {
                Debug.LogWarning($"Your quotation count doesn't make sense");
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
                Debug.LogWarning($"Expected {parameters.Length} arguments but got {tokens.Count - 1}.");
                return false;
            }

            object[] parsedArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++) {
                try {
                    parsedArgs[i] = Convert.ChangeType(tokens[i + 1], parameters[i].ParameterType);
                }
                catch {
                    Debug.LogWarning($"Failed to parse argument '{tokens[i + 1]}' as {parameters[i].ParameterType.Name}.");
                    return false;
                }
            }

            methodGroup.method.Invoke(null, parsedArgs);
            return true;
        }
    }
    #endregion

}