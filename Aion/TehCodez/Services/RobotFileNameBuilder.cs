using System;
using System.IO;

namespace Aion.Services
{
    internal class RobotFileNameBuilder
    {
        private string _robotDirectoryName;
        private string _version;
        private string _fileName;

        public RobotFileNameBuilder RobotDirectoryName(string robotDirectoryName)
        {
            _robotDirectoryName = robotDirectoryName ?? throw new ArgumentNullException(nameof(robotDirectoryName));
            return this;
        }

        public RobotFileNameBuilder Version(string version)
        {
            _version = version ?? throw new ArgumentNullException(nameof(version));
            return this;
        }

        public RobotFileNameBuilder FileName(string fileName)
        {
            _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            return this;
        }

        public string Build()
        {
            if (_robotDirectoryName == null) throw new InvalidOperationException();
            if (_version == null) throw new InvalidOperationException();
            if (_fileName == null) throw new InvalidOperationException();

            return Path.Combine(_robotDirectoryName, Path.GetFileNameWithoutExtension(_fileName), $"v{_version}", _fileName);
        }
    }
}