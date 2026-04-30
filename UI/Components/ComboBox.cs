using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class ComboBox
{
    private readonly TextField _input;
    private readonly Button _arrow;
    private readonly VisualElement _root;
    private TemplateContainer _popup;
    private List<string> _options;

    [SerializeField] private VisualTreeAsset _popupTemplate;

    public string Value => _input.value;

    public ComboBox(TemplateContainer template, List<string> options, VisualTreeAsset popupTemplate)
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
        _popup.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());

        var listView = _popup.Q<ListView>("popup-list");
        listView.itemsSource = _options;
        listView.fixedItemHeight = 24;
        listView.makeItem = () => new Label();
        listView.bindItem = (el, i) => ((Label)el).text = _options[i];

        listView.selectionChanged += (items) =>
        {
            _input.value = items.First() as string;
            ClosePopup();
        };

        _popup.style.position = Position.Absolute;
        _popup.style.maxHeight = 150;

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