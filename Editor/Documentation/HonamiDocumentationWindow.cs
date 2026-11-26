using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation
{
    public sealed class HonamiDocumentationWindow : EditorWindow
    {
        private VisualElement _leftPanel;
        private VisualElement _rightPanel;
        private ScrollView _contentScroll;
        private ScrollView _navScroll;
        private ToolbarSearchField _searchField;
        private TwoPaneSplitView _splitView;

        private List<IHonamiDocumentationPage> _pages;
        private IHonamiDocumentationPage _currentPage;
        private string _searchQuery = "";

        [MenuItem("Window/Honami/Documentation")]
        public static void ShowWindow()
        {
            var w = GetWindow<HonamiDocumentationWindow>();
            w.titleContent = HonamiEditorIcons.IconContent("HonamiGraphWhite", "Honami Docs");
            w.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            InitializePages();
            ConstructLayout();
        }

       private void InitializePages()
        {
            _pages = new List<IHonamiDocumentationPage>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IHonamiDocumentationPage>())
            {
               if (type.IsAbstract || type.IsInterface) continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogError($"[Honami] Documentation page '{type.FullName}' needs a public parameterless constructor.");
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(type) is IHonamiDocumentationPage page) _pages.Add(page);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Honami] Documentation page '{type.FullName}' failed to construct: {exception.Message}");
                }
            }

            _pages = _pages.OrderBy(p => p.Order).ThenBy(p => p.Title).ToList();
        }

        private void ConstructLayout()
        {
            var root = rootVisualElement;
            HonamiEditorLayout.PrepareRoot(root);

            var header = HonamiEditorLayout.Header(HonamiEditorIcons.GraphWhite, "Honami Animation System", HonamiDocLocalization.Get("Documentation", "Документація"), out _);
            root.Add(header);

            var toolbar = new Toolbar();
            toolbar.style.height = 28;
            toolbar.style.backgroundColor = HonamiEditorLayout.HeaderBg;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = HonamiEditorLayout.HeaderBorder;

            toolbar.Add(HonamiGraphStyles.Spacer());

            _searchField = new ToolbarSearchField();
            _searchField.style.width = 250;
            _searchField.style.marginTop = 4;
            _searchField.style.marginBottom = 4;
            _searchField.style.marginRight = 10;
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchQuery = evt.newValue?.ToLowerInvariant() ?? "";
                BuildNavigation();
            });
            toolbar.Add(_searchField);

            var langSelector = new EnumField(HonamiDocLocalization.CurrentLanguage);
            langSelector.style.width = 100;
            langSelector.style.marginTop = 4;
            langSelector.style.marginBottom = 4;
            langSelector.style.marginRight = 10;
            langSelector.RegisterValueChangedCallback(evt =>
            {
                HonamiDocLocalization.CurrentLanguage = (HonamiDocLanguage)evt.newValue;
                InitializePages();
                BuildNavigation();
                if (_currentPage != null) SelectPage(_currentPage);
            });
            toolbar.Add(langSelector);

            root.Add(toolbar);

            _splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            _splitView.style.flexGrow = 1;

            _leftPanel = new VisualElement();
            HonamiEditorLayout.StyleSidePanel(_leftPanel, rightBorder: true);

            _navScroll = new ScrollView();
            _navScroll.style.flexGrow = 1;
            _leftPanel.Add(_navScroll);

            _splitView.Add(_leftPanel);

            _rightPanel = new VisualElement();
            _rightPanel.style.flexGrow = 1;
            _rightPanel.style.backgroundColor = HonamiGraphStyles.WindowBg;

            _contentScroll = new ScrollView();
            _contentScroll.style.flexGrow = 1;
            _contentScroll.style.paddingLeft = 20;
            _contentScroll.style.paddingRight = 20;
            _contentScroll.style.paddingTop = 20;
            _contentScroll.style.paddingBottom = 40;
            _rightPanel.Add(_contentScroll);

            _splitView.Add(_rightPanel);
            root.Add(_splitView);

            BuildNavigation();

            if (_pages.Count > 0)
            {
                SelectPage(_pages[0]);
            }
        }

        private void BuildNavigation()
        {
            _navScroll.Clear();

            var filteredPages = _pages.Where(p => string.IsNullOrEmpty(_searchQuery) ||
                                                  p.Title.ToLowerInvariant().Contains(_searchQuery) ||
                                                  p.Category.ToLowerInvariant().Contains(_searchQuery) ||
                                                  p.SearchKeywords.ToLowerInvariant().Contains(_searchQuery))
                                      .ToList();

            var categories = filteredPages.Select(p => p.Category).Distinct().ToList();

            foreach (var category in categories)
            {
                var catContainer = new VisualElement();
                catContainer.style.marginTop = 16;
                catContainer.style.marginBottom = 4;
                _navScroll.Add(catContainer);

                var catHeader = new VisualElement();
                catHeader.style.flexDirection = FlexDirection.Row;
                catHeader.style.alignItems = Align.Center;
                catHeader.style.paddingLeft = 16;
                catHeader.style.paddingBottom = 8;
                catContainer.Add(catHeader);

                var catLabel = new Label(category.ToUpperInvariant());
                catLabel.style.fontSize = 10.5f;
                catLabel.style.color = HonamiGraphStyles.Accent;
                catLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                catLabel.style.letterSpacing = 1.2f;
                catHeader.Add(catLabel);

                var catPages = filteredPages.Where(p => p.Category == category).ToList();
                foreach (var page in catPages)
                {
                    var btn = new Button(() => SelectPage(page));
                    btn.text = page.Title;

                    bool isSelected = _currentPage == page;
                    btn.style.backgroundColor = isSelected ? new Color(HonamiGraphStyles.Accent.r, HonamiGraphStyles.Accent.g, HonamiGraphStyles.Accent.b, 0.12f) : Color.clear;

                    btn.BorderWidth(0);
                    btn.style.marginTop = btn.style.marginBottom = 0;
                    btn.style.marginLeft = 4;
                    btn.style.marginRight = 4;
                    btn.Radius(6);

                    btn.style.height = 32;
                    btn.style.unityTextAlign = TextAnchor.MiddleLeft;
                    btn.style.paddingLeft = 24;
                    btn.style.color = isSelected ? Color.white : new Color(0.75f, 0.75f, 0.75f);
                    btn.style.unityFontStyleAndWeight = isSelected ? FontStyle.Bold : FontStyle.Normal;
                    btn.style.fontSize = 12;

                    if (isSelected)
                    {
                        var indicator = new VisualElement();
                        indicator.style.position = Position.Absolute;
                        indicator.style.left = 0;
                        indicator.style.width = 3;
                        indicator.style.height = Length.Percent(100);
                        indicator.style.backgroundColor = HonamiGraphStyles.Accent;
                        indicator.style.borderTopRightRadius = indicator.style.borderBottomRightRadius = 2;
                        btn.Add(indicator);
                    }

                    btn.RegisterCallback<MouseOverEvent>(evt =>
                    {
                        if (!isSelected)
                        {
                            btn.style.backgroundColor = new Color(1, 1, 1, 0.04f);
                            btn.style.color = Color.white;
                        }
                    });
                    btn.RegisterCallback<MouseOutEvent>(evt =>
                    {
                        if (!isSelected)
                        {
                            btn.style.backgroundColor = Color.clear;
                            btn.style.color = new Color(0.75f, 0.75f, 0.75f);
                        }
                    });

                    _navScroll.Add(btn);
                }
            }

            if (filteredPages.Count == 0)
            {
                var empty = HonamiGraphStyles.MiniLabel(HonamiDocLocalization.Get("No results found.", "Результатів не знайдено."), HonamiGraphStyles.GreyText);
                empty.style.marginLeft = 16;
                empty.style.marginTop = 16;
                _navScroll.Add(empty);
            }
        }

        private void SelectPage(IHonamiDocumentationPage page)
        {
            _currentPage = page;
            BuildNavigation();

            _contentScroll.Clear();

            var progressContainer = new VisualElement();
            progressContainer.style.height = 4;
            progressContainer.style.backgroundColor = new Color(1, 1, 1, 0.05f);
            progressContainer.style.position = Position.Absolute;
            progressContainer.style.top = 0;
            progressContainer.style.left = -20;
            progressContainer.style.right = -20;
            _contentScroll.Add(progressContainer);

            var progressBar = new VisualElement();
            float progress = (_pages.IndexOf(page) + 1) / (float)_pages.Count;
            progressBar.style.width = Length.Percent(progress * 100);
            progressBar.style.height = Length.Percent(100);
            progressBar.style.backgroundColor = HonamiGraphStyles.Accent;
            progressContainer.Add(progressBar);

            var headerSection = new VisualElement();
            headerSection.style.marginTop = 30;
            headerSection.style.marginBottom = 40;
            _contentScroll.Add(headerSection);

            var categoryRow = new VisualElement();
            categoryRow.style.flexDirection = FlexDirection.Row;
            categoryRow.style.alignItems = Align.Center;
            categoryRow.style.marginBottom = 8;
            headerSection.Add(categoryRow);

            var catIcon = new Image { image = HonamiEditorIcons.GraphWhite };
            catIcon.style.width = 14;
            catIcon.style.height = 14;
            catIcon.style.marginRight = 8;
            catIcon.tintColor = HonamiGraphStyles.Accent;
            categoryRow.Add(catIcon);

            var category = new Label(page.Category.ToUpperInvariant());
            category.style.fontSize = 11;
            category.style.color = HonamiGraphStyles.Accent;
            category.style.unityFontStyleAndWeight = FontStyle.Bold;
            category.style.letterSpacing = 2f;
            categoryRow.Add(category);

            var title = new Label(page.Title);
            title.style.fontSize = 42;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 16;
            title.style.letterSpacing = -0.5f;
            headerSection.Add(title);

            var metaRow = new VisualElement();
            metaRow.style.flexDirection = FlexDirection.Row;
            metaRow.style.alignItems = Align.Center;
            headerSection.Add(metaRow);

            var langBadge = new VisualElement();
            langBadge.style.backgroundColor = new Color(1, 1, 1, 0.08f);
            langBadge.Padding(2, 8);
            langBadge.Radius(4);
            langBadge.style.marginRight = 12;
            metaRow.Add(langBadge);

            var langNotice = HonamiDocLocalization.Get("English", "Українська");
            var langLabel = new Label(langNotice);
            langLabel.style.fontSize = 10;
            langLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            langLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            langBadge.Add(langLabel);

            var readTimeText = page.EstimatedReadTime == 1
                ? HonamiDocLocalization.Get("1 min read", "1 хв читання")
                : string.Format(HonamiDocLocalization.Get("{0} min read", "{0} хв читання"), page.EstimatedReadTime);

            var timeLabel = new Label(readTimeText);
            timeLabel.style.fontSize = 10;
            timeLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            metaRow.Add(timeLabel);

            var dot = new Label(" • ");
            dot.style.color = new Color(0.3f, 0.3f, 0.3f);
            metaRow.Add(dot);

            var orderLabel = new Label($"{_pages.IndexOf(page) + 1} of {_pages.Count}");
            orderLabel.style.fontSize = 10;
            orderLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            metaRow.Add(orderLabel);

            var line = new VisualElement();
            line.style.height = 1;
            line.style.backgroundColor = new Color(1, 1, 1, 0.08f);
            line.style.marginTop = 30;
            headerSection.Add(line);

            page.BuildContent(_contentScroll);

            AddFooterNavigation(page);

            _contentScroll.scrollOffset = Vector2.zero;
        }

        private void AddFooterNavigation(IHonamiDocumentationPage page)
        {
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.marginTop = 60;
            footer.style.paddingTop = 30;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(1, 1, 1, 0.05f);

            int index = _pages.IndexOf(page);

            if (index > 0)
            {
                var prev = _pages[index - 1];
                var labelText = HonamiDocLocalization.Get("← PREVIOUS", "← ПОПЕРЕДНЯ");
                var btn = CreateNavButton(labelText, prev.Title, () => SelectPage(prev));
                footer.Add(btn);
            }
            else
            {
                footer.Add(new VisualElement { style = { flexGrow = 1 } });
            }

            if (index < _pages.Count - 1)
            {
                var next = _pages[index + 1];
                var labelText = HonamiDocLocalization.Get("NEXT →", "НАСТУПНА →");
                var btn = CreateNavButton(labelText, next.Title, () => SelectPage(next), true);
                footer.Add(btn);
            }

            _contentScroll.Add(footer);
        }

        private VisualElement CreateNavButton(string label, string title, Action onClick, bool alignRight = false)
        {
            var btn = new Button(onClick);
            btn.style.flexDirection = alignRight ? FlexDirection.RowReverse : FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.Padding(16, 20);
            btn.style.backgroundColor = new Color(0.14f, 0.15f, 0.16f);
            btn.Border(1, new Color(1, 1, 1, 0.08f));
            btn.Radius(10);
            btn.style.flexGrow = 1;
            btn.style.flexBasis = Length.Percent(48);
            btn.style.transitionProperty = new List<StylePropertyName> { "background-color", "border-color" };
            btn.style.transitionDuration = new List<TimeValue> { new TimeValue(150, TimeUnit.Millisecond) };

            btn.RegisterCallback<MouseOverEvent>(evt =>
            {
                btn.style.backgroundColor = new Color(0.18f, 0.20f, 0.22f);
                btn.BorderColor(HonamiGraphStyles.Accent);
            });
            btn.RegisterCallback<MouseOutEvent>(evt =>
            {
                btn.style.backgroundColor = new Color(0.14f, 0.15f, 0.16f);
                btn.BorderColor(new Color(1, 1, 1, 0.08f));
            });

            var textContainer = new VisualElement();
            textContainer.style.flexDirection = FlexDirection.Column;
            textContainer.style.alignItems = alignRight ? Align.FlexEnd : Align.FlexStart;
            textContainer.style.flexGrow = 1;

            var l = new Label(label);
            l.style.fontSize = 10;
            l.style.color = new Color(0.5f, 0.5f, 0.5f);
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginBottom = 2;
            textContainer.Add(l);

            var t = new Label(title);
            t.style.fontSize = 15;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = Color.white;
            textContainer.Add(t);

            btn.Add(textContainer);

            return btn;
        }
    }
}
