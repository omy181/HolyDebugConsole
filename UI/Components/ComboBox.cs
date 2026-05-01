using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ComboBox
{
    private readonly TextField _input;
    private readonly VisualElement _root;
    private TemplateContainer _popup;
    private Func<List<string>> _options;
    private int _selectedIndex = -1;

    [SerializeField] private VisualTreeAsset _popupTemplate;

    public string Value => _input.value;

    public ComboBox(TemplateContainer template, Func<List<string>> options, VisualTreeAsset popupTemplate)
    {
        _root = template.Q<VisualElement>("Parameter");
        _input = template.Q<TextField>("CommandBlockParameter");
        _options = options;
        _popupTemplate = popupTemplate;
        
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.character != '\t') return;

        var options = _options();
        if (options == null || options.Count == 0) return;

        evt.StopPropagation();
        evt.PreventDefault();

        if (_popup == null)
            TogglePopup();

        if (evt.shiftKey)
            _selectedIndex = (_selectedIndex - 1 + options.Count) % options.Count;
        else
            _selectedIndex = (_selectedIndex + 1) % options.Count;

        _input.value = options[_selectedIndex];
        HighlightOption(_selectedIndex);
    }

    private void HighlightOption(int index)
    {
        var listView = _popup?.Q<ListView>("popup-list");
        if (listView == null) return;

        listView.selectedIndex = index;
        listView.ScrollToItem(index);
    }

    public void OpenPopup()
    {
        if (_popup != null) return;
        TogglePopup();
    }

    private void TogglePopup()
    {
        if (_popup != null)
        {
            ClosePopup();
            return;
        }

        _selectedIndex = -1;

        _popup = _popupTemplate.Instantiate();
        _popup.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);

        var listView = _popup.Q<ListView>("popup-list");
        listView.selectionType = SelectionType.Single;
        listView.itemsSource = _options();
        listView.fixedItemHeight = 24;
        listView.makeItem = () =>
        {
            var label = new Label();
            label.AddToClassList("combobox-option");
            return label;
        };
        listView.bindItem = (el, i) =>
        {
            var label = (Label)el;
            label.text = _options()[i];
            label.userData = _options()[i];

            label.UnregisterCallback<ClickEvent>(OnItemClicked);
            label.RegisterCallback<ClickEvent>(OnItemClicked);
        };

        _popup.style.position = Position.Absolute;
        _popup.style.maxHeight = 300;

        var panelRoot = _root.panel.visualTree;
        panelRoot.Add(_popup);

        _popup.RegisterCallbackOnce<GeometryChangedEvent>(_ =>
        {
            var arrowWorldBound = _input.worldBound;
            var localPos = panelRoot.WorldToLocal(new Vector2(arrowWorldBound.xMax, arrowWorldBound.yMin));
            _popup.style.left = localPos.x;
            _popup.style.top = localPos.y;
            _popup.style.width = _root.worldBound.width;
        });

        _root.schedule.Execute(() =>
        {
            panelRoot.RegisterCallback<MouseDownEvent>(OnClickOutside, TrickleDown.TrickleDown);
        }).ExecuteLater(1);
    }

    private void OnItemClicked(ClickEvent evt)
    {
        var label = evt.target as Label;
        if (label == null) return;

        _input.value = label.userData as string;
        evt.StopPropagation();

        return;
        ClosePopup();
    }

    private void OnClickOutside(MouseDownEvent evt)
    {
        return;
        if (_popup != null && !_popup.Contains(evt.target as VisualElement))
            ClosePopup();
    }

    public void ClosePopup()
    {
        _root.panel.visualTree.UnregisterCallback<MouseDownEvent>(OnClickOutside, TrickleDown.TrickleDown);
        _popup?.RemoveFromHierarchy();
        _popup = null;
    }
}