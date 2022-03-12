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
      public string GameCard => "Create a new game.";
      public string StatisticsCard => "View player statistics";
      public string ProfileCard => "Edit player profile";
      public string VenueCard => "Add or edit venues";
   }

   public class Titles
   {
      public string GameCard => GetTitle();
      public string StatisticsCard => GetTitle();
      public string ProfileCard => GetTitle();
      public string VenueCard => GetTitle();

      public string GetTitle([CallerMemberName] string title = "title") => title;
   }

   public class NavigationViewItemViewModel
   {
      public IImage Icon
      {
         get;
         set;
      }
      public object Header
      {
         get;
         set;
      }

      public object Title
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
