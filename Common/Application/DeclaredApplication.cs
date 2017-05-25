using Altus.Suffūz.Serialization.Binary;
using Common.Configuration;
using Common.Licensing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Common.Application
{
    [Serializable]
    [XmlRoot("App")]
    public class DeclaredApp
    {
        public DeclaredApp()
        {
            Files = new List<AppFile>();
            Licenses = new ILicense[0];
            WasUpdated = false;
        }
        [XmlAttribute()]
        [BinarySerializable(1)]
        public string Name { get; set; }
        [XmlAttribute()]
        [BinarySerializable(2)]
        public string Product { get; set; }
        [XmlElement()]
        [BinarySerializable(3)]
        public string Version { get; set; }
        [XmlElement()]
        [BinarySerializable(4)]
        public bool IsLocal { get; set; }
        [XmlElement()]
        [BinarySerializable(7)]
        public string SourceUri { get; set; }
        [XmlElement()]
        [BinarySerializable(8)]
        public bool IncludeSourceBytes { get; set; }
        [XmlArray(ElementName = "Files")]
        [BinarySerializable(9, typeof(IList<AppFile>))]
        public List<AppFile> Files { get; set; }
        [XmlElement()]
        [BinarySerializable(10)]
        public bool DeletePrevious { get; set; }
        [XmlIgnore]
        [BinarySerializable(11)]
        public string CodeBase { get; set; }
        [XmlIgnore]
        [BinarySerializable(12)]
        public bool IsCore { get; set; }

        private string _location = null;
        [XmlIgnore]
        public string Location
        {
            get
            {
                if (_location == null)
                {
                    if (Manifest == null)
                        return CodeBase;
                    else
                    {
                        DiscoveryTarget target = Manifest.Targets
                            .Where(t => t.Product.Equals(AppContext.GetEnvironmentVariable<string>("ProductName", ""), StringComparison.InvariantCultureIgnoreCase))
                            .FirstOrDefault();
                        if (target != null)
                        {
                            DiscoveryFileElement file = target.Files.Where(f => f.Reflect && f.LoadedAssembly != null).FirstOrDefault();
                            if (file != null)
                            {
                                _location = Path.GetDirectoryName(file.LoadedAssembly.Location);
                            }
                            else
                            {
                                return CodeBase;
                            }
                        }
                        else
                        {
                            return CodeBase;
                        }
                    }
                }
                return _location;
            }

        }
        [XmlIgnore]
        public DiscoveryManifest Manifest { get; set; }
        [XmlIgnore]
        public bool WasUpdated { get; internal set; }

        private DiscoveryFileElement _primary = null;
        [XmlIgnore]
        public DiscoveryFileElement PrimaryFile
        {
            get
            {
                if (_primary == null)
                {
                    if (Manifest != null)
                    {
                        DiscoveryTarget target = Manifest.Targets
                            .Where(t => t.Product.Equals(AppContext.GetEnvironmentVariable<string>("ProductName", ""), StringComparison.InvariantCultureIgnoreCase))
                            .FirstOrDefault();
                        if (target != null)
                        {
                            DiscoveryFileElement file = target.Files.Where(f => f.IsPrimary).FirstOrDefault();
                            if (file != null)
                            {
                                _primary = file;
                            }
                        }
                    }
                }
                return _primary;
            }
        }
        [XmlIgnore]
        public Assembly PrimaryAssembly
        {
            get
            {
                if (PrimaryFile == null) return null;
                return PrimaryFile.LoadedAssembly;
            }
        }

        //AppDataContextAttribute _dataAttrib = null;
        //[XmlIgnore]
        //public Type DeclaredDataContext
        //{
        //    get
        //    {
        //        AppDataContextAttribute attrib;
        //        if (TryGetDataAttribute(out attrib))
        //        {
        //            return attrib.ContextType;
        //        }
        //        else return null;
        //    }
        //}

        //[XmlIgnore]
        //public Type DeclaredDataConnection
        //{
        //    get
        //    {
        //        AppDataContextAttribute attrib;
        //        if (TryGetDataAttribute(out attrib))
        //        {
        //            return attrib.ConnectionType;
        //        }
        //        else return null;
        //    }
        //}

        //[XmlIgnore]
        //public Type DeclaredDataConnectionManager
        //{
        //    get
        //    {
        //        AppDataContextAttribute attrib;
        //        if (TryGetDataAttribute(out attrib))
        //        {
        //            return attrib.ConnectionManagerType;
        //        }
        //        else return null;
        //    }
        //}

        //private bool TryGetDataAttribute(out AppDataContextAttribute attrib)
        //{
        //    bool ret = _dataAttrib != null;
        //    attrib = _dataAttrib;
        //    if (attrib == null)
        //    {
        //        if (this.PrimaryAssembly != null)
        //        {
        //            _dataAttrib = this.PrimaryAssembly.GetCustomAttribute<AppDataContextAttribute>();
        //            attrib = _dataAttrib;
        //            ret = attrib != null;
        //        }
        //    }
        //    return ret;
        //}

        [XmlIgnore]
        public ILicense[] Licenses
        {
            get;
            internal set;
        }

        public override string ToString()
        {
            return string.Format("{0}:{1}{2}",
                this.Product,
                this.Name,
                this.PrimaryAssembly == null ? "" : "[" + this.PrimaryAssembly.GetName().Name + "]");
        }
    }
}
