using Microsoft.Owin.FileSystems;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suffuz.Handlers
{
    public class FileSystem : IFileSystem
    {
        public FileSystem(string root)
        {
            this.Root = root;
        }

        public string Root { get; private set; }

        public bool TryGetDirectoryContents(string subpath, out IEnumerable<IFileInfo> contents)
        {
            subpath = RootPath(subpath);
            if (Path.HasExtension(subpath))
            {
                contents = null;
                return false;
            }
            else
            {
                var entries = new List<IFileInfo>();
                foreach (var file in Directory.EnumerateFileSystemEntries(subpath))
                {
                    IFileInfo fi;
                    if (TryGetFileInfo(file, out fi))
                    {
                        entries.Add(fi);
                    }
                }
                contents = entries;
                return true;
            }
        }

        private string RootPath(string path)
        {
            path = path.Replace("/", "\\");
            if (path.EndsWith("\\"))
            {
                path = path.Substring(0, path.Length - 1);
            }
            if (path.StartsWith("\\"))
            {
                path = path.Substring(1, path.Length - 1);
            }
            if (string.IsNullOrEmpty(path))
            {
                path = "index.html";
            }
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Root, path);
            }
            return path;
        }

        public bool TryGetFileInfo(string subpath, out IFileInfo fileInfo)
        {
            subpath = RootPath(subpath);
            if (Path.HasExtension(subpath))
            {
                if (File.Exists(subpath))
                {
                    var fi = new FileInfo(subpath);
                    fileInfo = new OwinFileInfo
                    {
                        IsDirectory = false,
                        LastModified = fi.LastWriteTime,
                        Length = fi.Length,
                        Name = fi.Name,
                        PhysicalPath = subpath
                    };
                    return true;
                }
                else
                {
                    fileInfo = null;
                    return false;
                }
            }
            else
            {
                if (Directory.Exists(subpath))
                {
                    var di = new DirectoryInfo(subpath);
                    fileInfo = new OwinFileInfo
                    {
                        IsDirectory = true,
                        LastModified = di.LastWriteTime,
                        Length = 0,
                        Name = di.Name,
                        PhysicalPath = subpath
                    };
                    return true;
                }
                else
                {
                    fileInfo = null;
                    return false;
                }
            }
        }
    }

    public class OwinFileInfo : IFileInfo
    {
        public bool IsDirectory
        {
            get;
            set;
        }

        public DateTime LastModified
        {
            get;
            set;
        }

        public long Length
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public string PhysicalPath
        {
            get;
            set;
        }

        public Stream CreateReadStream()
        {
            return new FileStream(PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
    }
}
