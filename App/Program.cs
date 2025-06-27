/// Copyright (c) 2020-2022
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using NinetyNine.Presentation;
using Serilog;

namespace NinetyNine.Application
{
   class Program
   {
      // Initialization code. Don't use any Avalonia, third-party APIs or any
      // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
      // yet and stuff might break.
      [STAThread]
      public static void Main(string[] args)
      {
         // Configure Serilog before anything else
         ConfigureLogger();
         
         try
         {
            Log.Information("Starting NinetyNine application...");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
         }
         catch (Exception ex)
         {
            Log.Fatal(ex, "Application terminated unexpectedly");
         }
         finally
         {
            Log.CloseAndFlush();
         }
      }

      private static void ConfigureLogger()
      {
         var logsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NinetyNine", "Logs");
         Directory.CreateDirectory(logsDirectory);

         Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
               Path.Combine(logsDirectory, "ninetynine-.log"),
               rollingInterval: RollingInterval.Day,
               retainedFileCountLimit: 7,
               outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
            .CreateLogger();
      }

      // Avalonia configuration, don't remove; also used by visual designer.
      public static AppBuilder BuildAvaloniaApp()
         => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
               OverlayPopups = true,
            })
            .With(new X11PlatformOptions
            {
               OverlayPopups = true
            })
            .With(new MacOSPlatformOptions
            {
               ShowInDock = true
            })
            .LogToTrace()
            .UseReactiveUI();

    }
}
