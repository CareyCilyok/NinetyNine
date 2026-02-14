using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
#if DEBUG
using Avalonia.Diagnostics;
#endif

namespace NinetyNine.Presentation.Views
{
   public partial class MainWindow : Window
   {
      public MainWindow()
      {
         InitializeComponent();

#if DEBUG
         this.AttachDevTools();
#endif
      }
   }
}
