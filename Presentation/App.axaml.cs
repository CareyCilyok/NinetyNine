using NinetyNine.Presentation.ViewModels;
using NinetyNine.Presentation.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using System.Threading;
using FluentAvalonia.UI.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using System.Text;
using Avalonia.Media;
using System.Text.Json;
using System.IO;

namespace NinetyNine.Presentation
{
   public class App : Application
   {
      public override void Initialize()
      {
         var settings_prov = new JsonSettingsProvider();
         Settings = settings_prov.Load<AppSettings>();

         switch (Settings.Theme)
         {
            case Theme.Light:
               Styles.Insert(0, App.FluentLight);
               break;

            case Theme.Dark:
               Styles.Insert(0, App.FluentDark);
               break;
         }

         AvaloniaXamlLoader.Load(this);
      }

      public override void OnFrameworkInitializationCompleted()
      {
         // NavigationViewStatic();
         if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
         {

            desktop.MainWindow = new MainWindow
            {
               DataContext = new MainWindowViewModel()
            };

            desktop.Exit += (s, e) =>
            {
               new JsonSettingsProvider().Save(Settings);
            };
         }
         else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
         {
            single.MainView = new MainView()
            {
               DataContext = new MainWindowViewModel()
            };
         }

         base.OnFrameworkInitializationCompleted();
      }
      private AppSettings Settings { get; set; } = new();
      public Theme GetTheme() => Settings.Theme;

      public async Task SetTheme(Theme theme)
      {
         try
         {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
               if (Application.Current != null)
               {
                  switch (theme)
                  {
                     case Theme.Light:
                        Application.Current.Styles[0] = App.FluentLight;
                        break;
                     case Theme.Dark:
                        Application.Current.Styles[0] = App.FluentDark;
                        break;
                  }
               }
            }, (DispatcherPriority)1);

            Settings.Theme = theme;
         }
         catch (Exception)
         {
            Console.WriteLine($"some error has executed");
         }
      }

      public readonly static Styles FluentDark = new Styles
        {
            new StyleInclude(new Uri("avares://NinetyNine.Presentation/Styles"))
            {
                Source = new Uri("avares://Avalonia.Themes.Fluent/FluentDark.xaml")
            }
        };

      public readonly static Styles FluentLight = new Styles
        {
            new StyleInclude(new Uri("avares://NinetyNine.Presentation/Styles"))
            {
                Source = new Uri("avares://Avalonia.Themes.Fluent/FluentLight.xaml")
            }
        };
   }

   public class AppSettings
   {
      public Theme Theme { get; set; } = Theme.Light;
   }

   public enum Theme
   {
      Light,
      Dark
   }

   /// <summary>
   /// JSON-based settings provider to replace deprecated BinaryFormatter
   /// </summary>
   public class JsonSettingsProvider
   {
      private readonly string _settingsDirectory;
      private readonly JsonSerializerOptions _jsonOptions;

      public JsonSettingsProvider()
      {
         _settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NinetyNine");
         _jsonOptions = new JsonSerializerOptions
         {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
         };

         Directory.CreateDirectory(_settingsDirectory);
      }

      public T Load<T>() where T : new()
      {
         var fileName = $"{typeof(T).Name}.json";
         var filePath = Path.Combine(_settingsDirectory, fileName);

         if (!File.Exists(filePath))
         {
            return new T();
         }

         try
         {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            return new T();
         }
      }

      public void Save<T>(T settings)
      {
         var fileName = $"{typeof(T).Name}.json";
         var filePath = Path.Combine(_settingsDirectory, fileName);

         try
         {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(filePath, json);
         }
         catch (Exception ex)
         {
            Console.WriteLine($"Error saving settings: {ex.Message}");
         }
      }
   }
}
