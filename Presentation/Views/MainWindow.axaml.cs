using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
         DragBorder.PointerPressed += (s, e) =>
         {
            BeginMoveDrag(e);
         };
      }
   }
}
