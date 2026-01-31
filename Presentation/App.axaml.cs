using NinetyNine.Presentation.ViewModels;
using NinetyNine.Presentation.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
using Serilog;

namespace NinetyNine.Presentation
{
   public class App : Application
   {
      public override void Initialize()
      {
         Log.Information("Initializing NinetyNine application");
         var settings_prov = new JsonSettingsProvider();
         Settings = settings_prov.Load<AppSettings>();

         AvaloniaXamlLoader.Load(this);

         // Apply theme after XAML is loaded using Avalonia 11 pattern
         Log.Information("Applying {Theme} theme", Settings.Theme);
         ApplyTheme(Settings.Theme);
      }

      public override void OnFrameworkInitializationCompleted()
      {
         Log.Information("Framework initialization completed");
         if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
         {
            Log.Information("Starting desktop application");
            desktop.MainWindow = new MainWindow
            {
               DataContext = new MainWindowViewModel()
            };

            desktop.Exit += (s, e) =>
            {
               Log.Information("Application is shutting down, saving settings");
               try
               {
                  new JsonSettingsProvider().Save(Settings);
                  Log.Information("Settings saved successfully");
               }
               catch (Exception ex)
               {
                  Log.Error(ex, "Failed to save settings during application shutdown");
               }
            };
         }
         else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
         {
            Log.Information("Starting single view application");
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
         Log.Information("Switching theme from {CurrentTheme} to {NewTheme}", Settings.Theme, theme);
         try
         {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
               ApplyTheme(theme);
            }, (DispatcherPriority)1);

            Settings.Theme = theme;
            Log.Information("Theme successfully changed to {Theme}", theme);
         }
         catch (Exception ex)
         {
            Log.Error(ex, "Error occurred while switching theme to {Theme}", theme);
         }
      }

      private void ApplyTheme(Theme theme)
      {
         // In Avalonia 11, use RequestedThemeVariant to switch between light/dark
         RequestedThemeVariant = theme switch
         {
            Theme.Light => ThemeVariant.Light,
            Theme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Dark
         };
      }
   }

   public class AppSettings
   {
      public Theme Theme { get; set; } = Theme.Dark; // Default to dark for our neon theme
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
            Log.Debug("Settings file {FileName} not found, using default settings", fileName);
            return new T();
         }

         try
         {
            var json = File.ReadAllText(filePath);
            var result = JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
            Log.Debug("Successfully loaded settings from {FileName}", fileName);
            return result;
         }
         catch (Exception ex)
         {
            Log.Error(ex, "Failed to load settings for type {SettingsType}", typeof(T).Name);
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
            Log.Debug("Successfully saved settings to {FileName}", fileName);
         }
         catch (Exception ex)
         {
            Log.Error(ex, "Failed to save settings for type {SettingsType}", typeof(T).Name);
         }
      }
   }
}
