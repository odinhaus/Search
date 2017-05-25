using Altus.Suffūz.Serialization;
using Common.Application;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DI
{
    public class PluginDependency
    {
        public PluginAttribute PluginAttribute { get; set; }
        public Dictionary<string, PluginDependency> Dependencies { get; set; }
        public bool Created { get; set; }

        public static Dictionary<string, PluginDependency> BuildDependencyGraph(PluginAttribute[] attributes)
        {
            List<PluginAttribute> attribs = new List<PluginAttribute>(attributes);

            // bubble the installers to the top of the list
            attribs.Sort(new Comparison<PluginAttribute>((a1, a2) =>
            {
                bool a1Is = typeof(IInstaller).IsAssignableFrom(a1.PluginType);
                bool a2Is = typeof(IInstaller).IsAssignableFrom(a2.PluginType);
                bool a1Ss = typeof(ISerializer).IsAssignableFrom(a1.PluginType);
                bool a2Ss = typeof(ISerializer).IsAssignableFrom(a2.PluginType);
                int a1V = a1Is ? 1 << 16 : 0;
                int a2V = a2Is ? 1 << 16 : 0;
                int a1S = a1Ss ? 1 << 30 : 0;
                int a2S = a2Ss ? 1 << 30 : 0;
                a1V += a1S + (int)a1.Priority;
                a2V += a2S + (int)a2.Priority;

                return -a1V.CompareTo(a2V);
            }));

            Dictionary<string, PluginDependency> list = new Dictionary<string, PluginDependency>();

            // index all the attributes in first pass
            for (int i = 0; i < attribs.Count; i++)
            {
                PluginDependency dep = new PluginDependency();
                dep.PluginAttribute = attribs[i];
                dep.Dependencies = new Dictionary<string, PluginDependency>();
                list.Add(attribs[i].Name, dep);
            }

            // assign attributes to parents in second pass
            Dictionary<string, PluginDependency>.Enumerator listEnum = list.GetEnumerator();
            while (listEnum.MoveNext())
            {
                if (listEnum.Current.Value.PluginAttribute.Dependencies != null
                    && listEnum.Current.Value.PluginAttribute.Dependencies.Length > 0)
                {
                    for (int i = 0; i < listEnum.Current.Value.PluginAttribute.Dependencies.Length; i++)
                    {
                        if (list.ContainsKey(listEnum.Current.Value.PluginAttribute.
                            Dependencies[i])
                            && !list[listEnum.Current.Value.PluginAttribute.
                            Dependencies[i]].Dependencies.ContainsKey(listEnum.Current.Value.PluginAttribute.Name)
                            && !list[listEnum.Current.Value.PluginAttribute.Dependencies[i]].PluginAttribute.Name.Equals(listEnum.Current.Value.PluginAttribute.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            list[listEnum.Current.Value.PluginAttribute.Dependencies[i]].Dependencies.Add(
                                listEnum.Current.Value.PluginAttribute.Name,
                                listEnum.Current.Value);
                        }
                    }
                }
            }

            // remove parented dependencies from root graph in third pass
            List<string> keys = new List<string>();
            listEnum = list.GetEnumerator();
            while (listEnum.MoveNext())
            {
                if (listEnum.Current.Value.PluginAttribute.Dependencies != null
                    && listEnum.Current.Value.PluginAttribute.Dependencies.Length > 0
                    && !keys.Contains(listEnum.Current.Key))
                {
                    foreach (string dep in listEnum.Current.Value.PluginAttribute.Dependencies)
                    {
                        if (list.ContainsKey(dep))
                        {
                            keys.Add(listEnum.Current.Key);
                            break;
                        }
                    }
                }
            }

            foreach (string key in keys)
            {
                list.Remove(key);
            }

            return list;
        }
    }
}
