using Common.Collections;
using Common.Serialization.Binary;
using Data.Core.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Data.Core.Auditing;

namespace Data.Core.Compilation
{
    interface ITestModel : IModel<long>, INotifyPropertyChanged, IBinarySerializable
    {
        [Altus.Suffūz.Serialization.Binary.BinarySerializable(10)]
        string Name { get; set; }
        [Altus.Suffūz.Serialization.Binary.BinarySerializable(11)]
        bool? IsActive { get; set; }
        IChild Child { get; set; }
        IChild[] Children { get; set; }
        Flock<IChild> MoreChildren { get; set; }
    }

    interface IChild : ISubModel
    {
        string Age { get; set; }
    }

    class BaseModel
    {
        public string Name { get; set; }
    }

    class EmittedModel : BaseModel, ITestModel
    {
        public EmittedModel(Dictionary<string, object> values)
        {
            object v1;
            if (values.TryGetValue("IsActive", out v1))
            {
                if (v1 is bool)
                {
                    IsActive = (bool)v1;
                }
                else if(v1 != null)
                {
                    var converter = TypeDescriptor.GetConverter(typeof(bool?));
                    ModelTypeConverter.TargetType = typeof(bool?);

                    if (converter.CanConvertFrom(v1.GetType()))
                    {
                        IsActive = (bool?)converter.ConvertFrom(v1);
                    }

                }
            }
        }

        public bool? IsActive
        {
            get;

            set;
        }

        public bool IsDeleted
        {
            get;

            set;
        }

        public bool IsNew
        {
            get;
        }

        public long Key
        {
            get;

            set;
        }

        public Type ModelType
        {
            get
            {
                return typeof(ITestModel);
            }
        }

        public byte[] ProtocolBuffer
        {
            get;
            set;
        }

        string ITestModel.Name
        {
            get
            {
                return base.Name;
            }

            set
            {
                base.Name = value;
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs("Name"));
                }
            }
        }

        public IChild Child
        {
            get;

            set;
        }

        public IChild[] Children
        {
            get;

            set;
        }

        public Flock<IChild> MoreChildren
        {
            get;

            set;
        }

        public DateTime Created
        {
            get;

            set;
        }

        public DateTime Modified
        {
            get;

            set;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event PropertyChangingEventHandler PropertyChanging;

        public string GetKey()
        {
            return Key.ToString();
        }

        public void SetKey(string value)
        {
            Key = long.Parse(value);
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    var nameIsNotNull = this.Name != null;
                    bw.Write(nameIsNotNull);
                    if (this.Name != null)
                        bw.Write(this.Name);
                    bw.Write(this.IsActive.HasValue);
                    if (this.IsActive.HasValue)
                        bw.Write(this.IsActive.Value);
                    bw.Write(this.IsDeleted);
                    bw.Write(this.Key);
                    var childIsNotNull = Child != null;
                    bw.Write(childIsNotNull);
                    if (childIsNotNull)
                    {
                        var childBytes = ((IBinarySerializable)Child).ToBytes();
                        bw.Write(childBytes.Length);
                        bw.Write(childBytes);
                    }
                    var childrenIsNotNull = Children != null;
                    bw.Write(childrenIsNotNull);
                    if (childrenIsNotNull)
                    {
                        bw.Write(Children.Length);
                        for(int i = 0; i < Children.Length; i++)
                        {
                            var bytes = ((IBinarySerializable)Children[i]).ToBytes();
                            bw.Write(bytes.Length);
                            bw.Write(bytes);
                        }
                    }

                    var moreChildrenIsNotNull = MoreChildren != null;
                    bw.Write(moreChildrenIsNotNull);
                    if (moreChildrenIsNotNull)
                    {
                        bw.Write(((ICollection)MoreChildren).Count);
                        for (int i = 0; i < ((IList)MoreChildren).Count; i++)
                        {
                            var bytes = ((IBinarySerializable)((IList)MoreChildren)[i]).ToBytes();
                            bw.Write(bytes.Length);
                            bw.Write(bytes);
                        }
                    }

                    bw.Write(this.ProtocolBuffer.Length);
                    bw.Write(this.ProtocolBuffer);
                }
                return ms.ToArray();
            }
        }

        public void FromBytes(byte[] source)
        {
            using (var ms = new MemoryStream(source))
            {
                using (var br = new BinaryReader(ms))
                {
                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //if (br.ReadBoolean())
                    //{
                    //    this.Name = br.ReadString();
                    //}

                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //if (br.ReadBoolean())
                    //{
                    //    this.IsActive = br.ReadBoolean();
                    //}

                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //this.IsDeleted = br.ReadBoolean();

                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //this.Key = br.ReadInt64();

                    if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    var childIsNotNull = br.ReadBoolean();
                    if (childIsNotNull)
                    {
                        var modelType = ModelTypeManager.GetModelType(br.ReadString());
                        var length = br.ReadInt32();
                        var bytes = br.ReadBytes(length);
                        this.Child = (IChild)RuntimeModelBuilder.CreateModelInstance(modelType);
                        ((IBinarySerializable)this.Child).FromBytes(bytes);
                    }

                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //var childrenIsNotNull = br.ReadBoolean();
                    //if (childrenIsNotNull)
                    //{
                    //    var count = br.ReadInt32();
                    //    this.Children = new IChild[count];
                    //    for (int i = 0; i < count; i++)
                    //    {
                    //        var length = br.ReadInt32();
                    //        var bytes = br.ReadBytes(length);
                    //        var child = RuntimeModelBuilder.CreateModelInstance<IChild>();
                    //        ((IBinarySerializable)child).FromBytes(bytes);
                    //        this.Children[i] = child;
                    //    }
                    //}

                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //var moreChildrenIsNotNull = br.ReadBoolean();
                    //if (moreChildrenIsNotNull)
                    //{
                    //    var count = br.ReadInt32();
                    //    this.MoreChildren = new Flock<IChild>();
                    //    for (int i = 0; i < count; i++)
                    //    {
                    //        var length = br.ReadInt32();
                    //        var bytes = br.ReadBytes(length);
                    //        var child = RuntimeModelBuilder.CreateModelInstance<IChild>();
                    //        ((IBinarySerializable)child).FromBytes(bytes);
                    //        ((IList)this.MoreChildren).Add(child);
                    //    }
                    //}

                    //if (br.BaseStream.Position >= br.BaseStream.Length) return;
                    //this.ProtocolBuffer = br.ReadBytes(br.ReadInt32());
                }
            }
        }

        public IEnumerable<AuditedChange> Compare(IModel model, string prefix)
        {
            throw new NotImplementedException();
        }
    }
}
