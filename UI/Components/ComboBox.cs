using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ComboBox
{
    private readonly TextField _input;
    private readonly Button _arrow;
    private readonly VisualElement _root;
    private TemplateContainer _popup;
    private Func<List<string>> _options;

    [SerializeField] private VisualTreeAsset _popupTemplate;

    public string Value => _input.value;

    public ComboBox(TemplateContainer template, Func<List<string>> options, VisualTreeAsset popupTemplate)
    {
        _root = template.Q<VisualElement>("ComboBox");
        _input = template.Q<TextField>("CommandBlockParameter");
        _arrow = template.Q<Button>("combobox-arrow");
        _options = options;
        _popupTemplate = popupTemplate;

        _arrow.clicked += TogglePopup;
    }

    private void TogglePopup()
    {
        if (_popup != null)
        {
            ClosePopup();
            return;
        }

        _popup = _popupTemplate.Instantiate();
        _popup.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);

        var listView = _popup.Q<ListView>("popup-list");
        listView.selectionType = SelectionType.None;
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
            var arrowWorldBound = _arrow.worldBound;
            var localPos = panelRoot.WorldToLocal(new Vector2(arrowWorldBound.xMin, arrowWorldBound.yMin));
            _popup.style.left = localPos.x;
            _popup.style.top = localPos.y - _popup.layout.height;
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
        ClosePopup();
    }

    private void OnClickOutside(MouseDownEvent evt)
    {
        if (_popup != null && !_popup.Contains(evt.target as VisualElement))
            ClosePopup();
    }

    private void ClosePopup()
    {
        _root.panel.visualTree.UnregisterCallback<MouseDownEvent>(OnClickOutside, TrickleDown.TrickleDown);
        _popup?.RemoveFromHierarchy();
        _popup = null;
    }
}