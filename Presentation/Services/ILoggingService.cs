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

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service interface for structured logging throughout the application
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Log debug information
        /// </summary>
        void LogDebug(string message, params object[] args);

        /// <summary>
        /// Log general information
        /// </summary>
        void LogInformation(string message, params object[] args);

        /// <summary>
        /// Log warnings
        /// </summary>
        void LogWarning(string message, params object[] args);

        /// <summary>
        /// Log errors
        /// </summary>
        void LogError(Exception exception, string message, params object[] args);

        /// <summary>
        /// Log fatal errors
        /// </summary>
        void LogFatal(Exception exception, string message, params object[] args);

        /// <summary>
        /// Log user actions for analytics and debugging
        /// </summary>
        void LogUserAction(string action, object? context = null);

        /// <summary>
        /// Log performance metrics
        /// </summary>
        void LogPerformance(string operation, TimeSpan duration, object? context = null);
    }
}