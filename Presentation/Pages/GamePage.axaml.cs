using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NinetyNine.Presentation.Pages
{
   public partial class GamePage : UserControl
   {
      public GamePage()
      {
         InitializeComponent();
      }

      private void InitializeComponent()
      {
         AvaloniaXamlLoader.Load(this);
      }
   }
}
