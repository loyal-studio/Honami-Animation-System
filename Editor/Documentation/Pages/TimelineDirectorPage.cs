using UnityEngine;
using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class TimelineDirectorPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Timeline & Director", "Timeline та Director");
        public string Category => HonamiDocLocalization.Get("07. Tools & Pro", "07. Інструменти та Pro");
        public string SearchKeywords => "timeline director cutscene sequence wip in development таймлайн директор катсцена в розробці";
        public int Order => 620;
        public int EstimatedReadTime => 1;

        public void BuildContent(VisualElement root)
        {
            var banner = new VisualElement();
            banner.style.marginTop = 40;
            banner.style.paddingTop = banner.style.paddingBottom = 48;
            banner.style.paddingLeft = banner.style.paddingRight = 32;
            banner.style.backgroundColor = new Color(1.0f, 0.7f, 0.1f, 0.08f);
            banner.style.borderLeftWidth = 6;
            banner.style.borderLeftColor = new Color(1.0f, 0.7f, 0.1f);
            banner.style.borderTopLeftRadius = banner.style.borderTopRightRadius =
            banner.style.borderBottomLeftRadius = banner.style.borderBottomRightRadius = 8;
            banner.style.alignItems = Align.Center;

            var accentLine = new VisualElement();
            accentLine.style.width = 40;
            accentLine.style.height = 3;
            accentLine.style.backgroundColor = new Color(1.0f, 0.7f, 0.1f);
            accentLine.style.marginBottom = 20;
            accentLine.style.borderTopLeftRadius = accentLine.style.borderTopRightRadius =
            accentLine.style.borderBottomLeftRadius = accentLine.style.borderBottomRightRadius = 1.5f;
            banner.Add(accentLine);

            var title = new Label(HonamiDocLocalization.Get("IN DEVELOPMENT", "У РОЗРОБЦІ"));
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(1.0f, 0.7f, 0.1f);
            title.style.letterSpacing = 3f;
            title.style.marginBottom = 12;
            banner.Add(title);

            var subtitle = new Label(HonamiDocLocalization.Get(
                "The Honami Timeline and Director are work-in-progress features. Their API and data format may change without notice — do not rely on them in production yet.",
                "Honami Timeline та Director — це work-in-progress фічі. Їхні API та формат даних можуть змінитися без попередження — поки що не покладайтеся на них у продакшені."
            ));
            subtitle.style.fontSize = 14;
            subtitle.style.color = new Color(0.9f, 0.9f, 0.9f);
            subtitle.style.whiteSpace = WhiteSpace.Normal;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.maxWidth = 520;
            banner.Add(subtitle);

            root.Add(banner);

            HonamiDocumentationBuilder.AddSpace(root, 24);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Documentation for this system will appear here once the feature stabilizes. For gameplay animation, keep using states and transitions in a Honami Controller.",
                "Документація цієї системи з'явиться тут, щойно фіча стабілізується. Для геймплейної анімації й надалі використовуйте стейти та транзішни в Honami Controller."
            ));
        }
    }
}
