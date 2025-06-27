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
using Serilog;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Concrete implementation of ILoggingService using Serilog
    /// </summary>
    public class LoggingService : ILoggingService
    {
        private readonly ILogger _logger;

        public LoggingService()
        {
            _logger = Log.Logger;
        }

        public void LogDebug(string message, params object[] args)
        {
            _logger.Debug(message, args);
        }

        public void LogInformation(string message, params object[] args)
        {
            _logger.Information(message, args);
        }

        public void LogWarning(string message, params object[] args)
        {
            _logger.Warning(message, args);
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            _logger.Error(exception, message, args);
        }

        public void LogFatal(Exception exception, string message, params object[] args)
        {
            _logger.Fatal(exception, message, args);
        }

        public void LogUserAction(string action, object? context = null)
        {
            if (context != null)
            {
                _logger.Information("User Action: {Action} with context {@Context}", action, context);
            }
            else
            {
                _logger.Information("User Action: {Action}", action);
            }
        }

        public void LogPerformance(string operation, TimeSpan duration, object? context = null)
        {
            if (context != null)
            {
                _logger.Information("Performance: {Operation} took {Duration}ms with context {@Context}", 
                    operation, duration.TotalMilliseconds, context);
            }
            else
            {
                _logger.Information("Performance: {Operation} took {Duration}ms", 
                    operation, duration.TotalMilliseconds);
            }
        }
    }
}