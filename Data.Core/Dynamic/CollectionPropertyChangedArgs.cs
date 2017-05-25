using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Data.Core.Dynamic
{
    public class CollectionPropertyChangedArgs
    {
        static readonly string pattern = @"(?<property>[\d\S]+)\.(?<operation>(Remove|Add|Replace){1})\((?<newIndex>-?\d+),(?<newCount>-?\d+),(?<oldIndex>-?\d+),(?<oldCount>-?\d+)\)";

        public CollectionPropertyChangedArgs(string propertyNameExpression)
        {
            var match = Regex.Match(propertyNameExpression, pattern);
            IsValid = match.Success;
            if (IsValid)
            {
                this.PropertyName = match.Groups["property"].Value;
                this.Operation = match.Groups["operation"].Value;
                this.OldIndex = int.Parse(match.Groups["oldIndex"].Value);
                this.OldCount = int.Parse(match.Groups["oldCount"].Value);
                this.NewIndex = int.Parse(match.Groups["newIndex"].Value);
                this.NewCount = int.Parse(match.Groups["newCount"].Value);
            }
        }

        public string PropertyName { get; set; }
        public string Operation { get; set; }
        public int OldIndex { get; set; }
        public int OldCount { get; set; }
        public int NewIndex { get; set; }
        public int NewCount { get; set; }

        public bool IsValid { get; private set; }

    }
}
