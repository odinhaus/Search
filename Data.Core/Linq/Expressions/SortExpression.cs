using Newtonsoft.Json;
using Common.Serialization.Binary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public enum SortDirection
    {
        Asc,
        Desc
    }


    public class SortExpression : BinarySerializableExpression
    {
        public SortExpression() { }

        public SortExpression(Expression body, SortDirection direction)
        {
            this.MemberName = GetMemberName(body);
            this.SortDirection = direction;
        }

        public SortExpression(string memberName, SortDirection direction)
        {
            this.MemberName = memberName;
            this.SortDirection = direction;
        }

        private string GetMemberName(Expression body)
        {
            var b = ((LambdaExpression)body).Body;
            if (b is UnaryExpression)
            {
                b = ((UnaryExpression)b).Operand;
            }
            var member = b as MemberExpression;
            return member.Member.Name;
        }

        public string MemberName { get; protected set; }

        [JsonProperty("NodeType")]
        public string TypeName
        {
            get
            {
                return ((QueryExpressionType)NodeType).ToString();
            }
        }

        [JsonIgnore]
        public override ExpressionType NodeType
        {
            get
            {
                return (ExpressionType)QueryExpressionType.Sort;
            }
        }

        [JsonIgnore]
        public override Type Type
        {
            get
            {
                return typeof(void);
            }
        }

        [JsonIgnore]
        public override bool CanReduce
        {
            get
            {
                return false;
            }
        }

        public SortDirection SortDirection { get; protected set; }

       
        public override string ToString()
        {
            return string.Format("{0} {1}", MemberName, SortDirection);
        }

        protected override void OnToBytes(BinaryWriter bw)
        {
            bw.Write(MemberName);
            bw.Write((int)SortDirection);
        }

        protected override void OnFromBytes(BinaryReader br)
        {
            MemberName = br.ReadString();
            SortDirection = (SortDirection)br.ReadInt32();
        }
    }
}
