namespace Data.Core.Linq
{
    public interface IPath<T> : IPath where T : IModel
    {
        new T Root { get; set; }
    }

    public interface IPath : IModel, IAny
    {
        ILink[] Edges { get; set; }
        IModel[] Nodes { get; set; }
        IModel Root { get; set; }
    }
}