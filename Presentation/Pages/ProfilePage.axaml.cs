using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NinetyNine.Presentation.Pages
{
   public partial class ProfilePage : UserControl
   {
      public ProfilePage()
      {
         InitializeComponent();
      }

      private void InitializeComponent()
      {
         AvaloniaXamlLoader.Load(this);
      }
   }
}
