using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public class ContainerMappings : IContainerMappings
    {
        List<ContainerMapping> _inner = new List<ContainerMapping>();
        public IContainerMapping Add()
        {
            var mapping = new ContainerMapping();
            _inner.Add(mapping);
            return mapping;
        }

        public IEnumerator<IContainerMapping> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
