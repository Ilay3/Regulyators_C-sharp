namespace Regulyators.UI.ViewModels
{
    /// <summary>
    /// Представляет элемент меню навигации
    /// </summary>
    public class MenuItem
    {
        /// <summary>
        /// Заголовок пункта меню
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Представление, связанное с пунктом меню
        /// </summary>
        public object ViewModel { get; set; }

        /// <summary>
        /// Идентификатор иконки из MaterialDesign
        /// </summary>
        public string IconKind { get; set; } = "ViewDashboard";

    }
}