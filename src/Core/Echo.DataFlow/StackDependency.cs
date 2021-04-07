using System.Linq;

namespace Echo.DataFlow
{
    public class StackDependency<TContents> : DataDependency<StackDataSource<TContents>, TContents>
    {
        /// <inheritdoc />
        public override DataDependencyType DependencyType => DataDependencyType.Stack;

        public StackDataSource<TContents> Add(DataFlowNode<TContents> node)
        {
            var source = new StackDataSource<TContents>(node);
            if (Add(source))
                return source;
            return this.First(x => x.Equals(source));
        }
    }
}