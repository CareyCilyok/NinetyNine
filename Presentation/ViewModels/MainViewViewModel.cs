using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace NinetyNine.Presentation.ViewModels
{
   public class MainViewViewModel : ViewModelBase
   {
      public MainViewViewModel()
      {
         Descriptions = new();
         Titles = new();
      }

      private void AddNavItems()
      {

      }

      public Titles Titles { get; }
      public Descriptions Descriptions { get; }
      public IReadOnlyList<NavigationViewItemViewModel> NavItems
      {
         get;
         set;
      }
   }

   public class Descriptions
   {
      public string Game => "Create a new game single or multi player of 99.";
      public string Statistics => "View player statistics. See your win/loss history, average score, handicap and more.";
      public string Profile => "Edit your profile";
      public string Venues => "Add or edit venues where you play or hangout";
   }

   public class Titles
   {
      public string Game => "Game";
      public string Statistics => "Statistics";
      public string Profile => "Profile";
      public string Venues => "Venues";
   }

   public class NavigationViewItemViewModel
   {
      public IImage Icon
      {
         get;
         set;
      }
      public string Header
      {
         get;
         set;
      }

      public string Title
      {
         get;
         set;
      }

      public object Content
      {
         get;
         set;
      }

      public IReadOnlyList<NavigationViewItemViewModel> NavItems
      {
         get;
         set;
      }
   }
}
