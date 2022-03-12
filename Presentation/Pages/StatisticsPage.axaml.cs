using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace NinetyNine.Presentation.Pages
{
   public partial class StatisticsPage : UserControl
   {
      public StatisticsPage()
      {
         InitializeComponent();
      }

      private void InitializeComponent()
      {
         AvaloniaXamlLoader.Load(this);
      }
   }
}
