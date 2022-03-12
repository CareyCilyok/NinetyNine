using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NinetyNine.Presentation.Pages
{
   public partial class VenuePage : UserControl
   {
      public VenuePage()
      {
         InitializeComponent();
      }

      private void InitializeComponent()
      {
         AvaloniaXamlLoader.Load(this);
      }
   }
}
