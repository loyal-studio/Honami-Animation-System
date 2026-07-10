using UnityEngine.UIElements;

namespace HonamiAnimationSystem.Editor.Documentation.Pages
{
    public sealed class LogSubNodePage : IHonamiDocumentationPage
    {
        public string Title => HonamiDocLocalization.Get("Log Sub-Node", "Підвузол Log");
        public string Category => HonamiDocLocalization.Get("05. Sub-Nodes", "05. Підвузли");
        public string SearchKeywords => "log subnode debug console message format tokens логування дебаг консоль токени формат";
        public int Order => 430;
        public int EstimatedReadTime => 2;

        public void BuildContent(VisualElement root)
        {
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Log Sub-Node prints a formatted message to the Unity Console during a state's lifecycle. It is a lightweight way to trace when states enter, run or exit without touching gameplay code.",
                "Підвузол логування виводить відформатоване повідомлення в консоль Unity протягом життєвого циклу стану. Це легкий спосіб відстежувати, коли стани входять, працюють або виходять, не чіпаючи ігровий код."
            ));

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Configuration", "Налаштування"), HonamiEditorIcons.Controller);
            HonamiDocumentationBuilder.AddPropertyBox(root,
                (HonamiDocLocalization.Get("Log On Enter / Update / Exit", "Log On Enter / Update / Exit"), HonamiDocLocalization.Get("Three independent toggles choosing which lifecycle phases produce a log. Update logs every frame while the state is active.", "Три незалежні перемикачі, що обирають, які фази життєвого циклу створюють лог. Update логує щокадру, поки стан активний.")),
                (HonamiDocLocalization.Get("Message Format", "Message Format"), HonamiDocLocalization.Get("A multi-line template. Supports the tokens listed below.", "Багаторядковий шаблон. Підтримує токени, наведені нижче.")),
                (HonamiDocLocalization.Get("Log Type", "Log Type"), HonamiDocLocalization.Get("Log, Warning, Error, etc. (Unity LogType).", "Log, Warning, Error тощо (Unity LogType)."))
            );

            HonamiDocumentationBuilder.AddHeader(root, HonamiDocLocalization.Get("Message Tokens", "Токени повідомлення"), HonamiEditorIcons.GraphWhite);
            HonamiDocumentationBuilder.AddParagraph(root, HonamiDocLocalization.Get(
                "The Message Format string replaces these tokens at runtime:",
                "Рядок Message Format замінює ці токени в рантаймі:"
            ));
            HonamiDocumentationBuilder.AddTable(root,
                (HonamiDocLocalization.Get("Token", "Токен"), 170),
                (HonamiDocLocalization.Get("Resolves to", "Замінюється на"), 0),
                ("", 0),
                ("{state}", HonamiDocLocalization.Get("The current state's name.", "Назва поточного стану."), ""),
                ("{layer}", HonamiDocLocalization.Get("The layer index.", "Індекс шару."), ""),
                ("{controller}", HonamiDocLocalization.Get("The active controller's name.", "Назва активного контролера."), ""),
                ("{previousState}", HonamiDocLocalization.Get("The previously active state on this layer.", "Попередній активний стан на цьому шарі."), ""),
                ("{time}", HonamiDocLocalization.Get("Playback time in seconds (F2).", "Час відтворення в секундах (F2)."), ""),
                ("{normalizedTime}", HonamiDocLocalization.Get("State progress 0..1 (F2).", "Прогрес стану 0..1 (F2)."), ""),
                ("{param:Name}", HonamiDocLocalization.Get("The value of the parameter called Name (float/int/bool/trigger).", "Значення параметра з іменем Name (float/int/bool/trigger)."), "")
            );

            HonamiDocumentationBuilder.AddCodeBlock(root, "State: {state} | Layer: {layer} | Speed: {param:Speed} | t={normalizedTime}");

            HonamiDocumentationBuilder.AddTip(root, HonamiDocLocalization.Get(
                "Set Log Type to Warning so your trace stands out from ordinary logs, and disable Log On Update in shipped builds to avoid console spam.",
                "Встановіть Log Type у Warning, щоб трасування виділялося серед звичайних логів, і вимикайте Log On Update у релізних білдах, аби уникнути спаму в консолі."
            ));
        }
    }
}
