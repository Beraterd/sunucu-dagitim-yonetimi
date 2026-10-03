using System;
using System.IO;

namespace DeploymentHelper
{
    public class Logger
    {
        private readonly string _filePath;
        private static readonly object _lock = new();

        public Logger(string filePath)
        {
            _filePath = filePath;
        }

        public void Log(string message)
        {
            lock (_lock)
            {
                File.AppendAllText(
                    _filePath,
                    $"{DateTime.Now:G} - {message}{Environment.NewLine}");
            }
        }
    }
}
