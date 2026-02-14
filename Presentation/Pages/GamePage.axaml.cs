using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NinetyNine.Presentation.ViewModels;

namespace NinetyNine.Presentation.Pages
{
   public partial class GamePage : UserControl
   {
      public GamePage()
      {
         InitializeComponent();
         DataContext = new GamePageViewModel();
      }

      private void InitializeComponent()
      {
         AvaloniaXamlLoader.Load(this);
      }
   }
}
