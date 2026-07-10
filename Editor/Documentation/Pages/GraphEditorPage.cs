using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class GraphEditorPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Graph Editor", "Редактор графа");
        public string Category => HonamiDocLocalization.Get("03. Core Concepts", "03. Основні концепти");
        public string SearchKeywords => "editor window graph nodes navigation inspector shortcuts редактор вікно вузли навігація";
        public int Order => 200;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddPageIntro(root,
                new[]
                {
                    HonamiDocLocalization.Get("Canvas navigation and keyboard shortcuts", "Навігація по полотну та гарячі клавіші"),
                    HonamiDocLocalization.Get("Where node and transition properties live", "Де живуть властивості вузлів і транзішнів"),
                    HonamiDocLocalization.Get("Fast node creation via drag & drop", "Швидке створення вузлів через drag & drop")
                },
                new[]
                {
                    HonamiDocLocalization.Get("'First Steps': a controller assigned to a character", "«First Steps»: контролер, призначений персонажу")
                });

            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Graph Editor is where you build the animation logic for your characters. It's a node-based interface that allows for visual state management.",
                "Редактор графа — це місце, де ви створюєте логіку анімації для ваших персонажів. Це вузловий інтерфейс, що дозволяє візуально керувати станами."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Navigation & Shortcuts", "Навігація та гарячі клавіші"), HonamiEditorIcons.TimelineWhite);
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Action", "Дія"), 150),
                (HonamiDocLocalization.Get("Shortcut", "Клавіші"), 150),
                (HonamiDocLocalization.Get("Description", "Опис"), 0),
                ("Pan", "Middle Click / Alt+Left", HonamiDocLocalization.Get("Move around the canvas.", "Переміщення по полотну.")),
                ("Zoom", "Scroll Wheel", HonamiDocLocalization.Get("Zoom in and out.", "Масштабування.")),
                ("Add Node", "Right Click", HonamiDocLocalization.Get("Open the node creation menu.", "Відкрити меню створення вузлів.")),
                ("Delete", "Delete / Backspace", HonamiDocLocalization.Get("Remove selected nodes/transitions.", "Видалити вибрані вузли/переходи.")),
                ("Duplicate", "Ctrl+D", HonamiDocLocalization.Get("Duplicate selected nodes.", "Дублювати вибрані вузли."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("The Inspector", "Інспектор"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "When a node or transition is selected, the Inspector on the right shows its properties. This is where you assign animation clips, configure transition times, and add sub-nodes.",
                "Коли вибрано вузол або перехід, Інспектор праворуч показує його властивості. Тут ви призначаєте анімаційні кліпи, налаштовуєте час переходів та додаєте підвузли."
            ));

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "You can drag and drop AnimationClips directly from the Project window into the Graph to create new Animation Nodes instantly.",
                "Ви можете перетягувати AnimationClips безпосередньо з вікна Project у граф, щоб миттєво створювати нові анімаційні вузли."
            ));
        }
    }
}
