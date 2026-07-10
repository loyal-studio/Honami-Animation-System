using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class PortalNodesPage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Portal Nodes", "Portal Nodes");
        public string Category => HonamiDocLocalization.Get("04. Node Library", "04. Бібліотека вузлів");
        public string SearchKeywords => "portal entrance exit jump teleport graph organization source filter портали вузли організація";
        public int Order => 350;
        public int EstimatedReadTime => 4;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "Portal Nodes help you organize large graphs by 'teleporting' the logic flow from one part of the graph to another without drawing a long, messy line. A portal is a named pair: an Entrance you transition into, and an Exit that continues the flow elsewhere.",
                "Вузли-портали допомагають організовувати великі графи, «телепортуючи» потік логіки з однієї частини графа в іншу без довгих заплутаних ліній. Портал — це іменована пара: Entrance, у який ви переходите, та Exit, що продовжує потік в іншому місці."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("How the pair works", "Як працює пара"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Portal Entrance: the receiving end of your transition. Draw a transition into it just like into a normal state and give it a Portal Name.", "Portal Entrance: приймальний кінець вашого переходу. Намалюйте перехід у нього, як у звичайний стан, і задайте Portal Name."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Portal Exit: placed anywhere else in the graph with the same Portal Name. Its outgoing transitions decide where the flow actually lands; their conditions are evaluated the moment the portal is used.", "Portal Exit: розміщується будь-де в графі з тим самим Portal Name. Його вихідні переходи вирішують, куди потік справді потрапить; їхні умови оцінюються в момент використання порталу."));
            HonamiDocumentationBuilder.AddBulletPoint(root, HonamiDocLocalization.Get("Both nodes are virtual — no playable is created, no extra runtime cost. The original transition's duration and curve are used for the final blend.", "Обидва вузли віртуальні — playable не створюється, додаткових витрат у рантаймі немає. Для фінального блендінгу використовуються тривалість і крива початкового переходу."));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Source Filter", "Фільтр джерела"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "An exit transition can carry a Source Filter: it is then considered only when the flow entered the portal from a specific state. This lets one shared portal route different origins to different destinations.",
                "Вихідний перехід може мати Source Filter: тоді він розглядається лише коли потік зайшов у портал з конкретного стану. Це дозволяє одному спільному порталу вести різні джерела до різних цілей."
            ));

            HonamiDocumentationBuilder.AddCallout(root, HonamiDocLocalization.Get(
                "Portals can chain into other portals, up to 32 hops. A deeper chain logs an error — that almost always means you built a circular portal loop.",
                "Портали можуть вести в інші портали, до 32 стрибків. Глибший ланцюг видасть помилку — це майже завжди означає циклічну петлю порталів."
            ), HonamiDocumentationBuilder.CalloutType.Warning);

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Give your portals descriptive names like 'To_Combat' or 'From_Death' to make the graph easy to read at a glance.",
                "Давайте порталам зрозумілі назви, такі як «To_Combat» або «From_Death», щоб граф було легко читати."
            ));
        }
    }
}
