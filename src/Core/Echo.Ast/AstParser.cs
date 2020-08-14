using System;
using System.Collections.Generic;
using System.Linq;
using Echo.Ast.Factories;
using Echo.Ast.Helpers;
using Echo.ControlFlow;
using Echo.ControlFlow.Blocks;
using Echo.ControlFlow.Regions;
using Echo.ControlFlow.Serialization.Blocks;
using Echo.Core.Code;
using Echo.DataFlow;

namespace Echo.Ast
{
    /// <summary>
    /// Transforms a <see cref="ControlFlowGraph{TInstruction}"/> and a <see cref="DataFlowGraph{TContents}"/> into an Ast
    /// </summary>
    public sealed class AstParser<TInstruction>
    {
        private readonly ControlFlowGraph<TInstruction> _controlFlowGraph;
        private readonly DataFlowGraph<TInstruction> _dataFlowGraph;
        private readonly IInstructionSetArchitecture<StatementBase<TInstruction>> _astArchitecture;
        private readonly Dictionary<IVariable, int> _variableVersions = new Dictionary<IVariable, int>();
        private readonly Dictionary<(IVariable, int), AstVariable> _versionedAstVariables = new Dictionary<(IVariable, int), AstVariable>();
        private readonly Dictionary<long, Dictionary<IVariable, int>> _instructionToVersionedVariable = new Dictionary<long, Dictionary<IVariable, int>>();
        private readonly Dictionary<AstVariableCollection, AstVariable> _phiSlots = new Dictionary<AstVariableCollection, AstVariable>();
        private readonly Dictionary<long, AstVariable[]> _stackSlots = new Dictionary<long, AstVariable[]>();
        private readonly Dictionary<BasicControlFlowRegion<TInstruction>, BasicControlFlowRegion<StatementBase<TInstruction>>>
            _regionsMapping = new Dictionary<BasicControlFlowRegion<TInstruction>, BasicControlFlowRegion<StatementBase<TInstruction>>>();

        private long _id = -1;
        private long _varCount;
        private long _phiVarCount;
        
        /// <summary>
        /// Creates a new Ast parser with the given <see cref="ControlFlowGraph{TInstruction}"/>
        /// </summary>
        /// <param name="controlFlowGraph">The <see cref="ControlFlowGraph{TInstruction}"/> to parse</param>
        /// <param name="dataFlowGraph">The <see cref="DataFlowGraph{TContents}"/> to parse</param>
        public AstParser(ControlFlowGraph<TInstruction> controlFlowGraph, DataFlowGraph<TInstruction> dataFlowGraph)
        {
            _controlFlowGraph = controlFlowGraph;
            _dataFlowGraph = dataFlowGraph;
            _astArchitecture = new AstInstructionSetArchitectureDecorator<TInstruction>(_controlFlowGraph.Architecture);
        }

        private IInstructionSetArchitecture<TInstruction> Architecture => _controlFlowGraph.Architecture; 
        
        /// <summary>
        /// Parses the given <see cref="ControlFlowGraph{TInstruction}"/>
        /// </summary>
        /// <returns>A <see cref="ControlFlowGraph{TInstruction}"/> representing the Ast</returns>
        public ControlFlowGraph<StatementBase<TInstruction>> Parse()
        {
            var newGraph = new ControlFlowGraph<StatementBase<TInstruction>>(_astArchitecture);
            var blockBuilder = new BlockBuilder<TInstruction>();
            var rootScope = blockBuilder.ConstructBlocks(_controlFlowGraph);

            // Transform and add regions.
            foreach (var originalRegion in _controlFlowGraph.Regions)
            {
                var newRegion = TransformRegion(originalRegion);
                newGraph.Regions.Add(newRegion);
            }

            // Transform and add nodes.
            foreach (var originalBlock in rootScope.GetAllBlocks())
            {
                var originalNode = _controlFlowGraph.Nodes[originalBlock.Offset];
                var transformedBlock = TransformBlock(originalBlock);
                var newNode = new ControlFlowNode<StatementBase<TInstruction>>(originalBlock.Offset, transformedBlock);
                newGraph.Nodes.Add(newNode);
                
                // Move node to newly created region.
                if (originalNode.ParentRegion is BasicControlFlowRegion<TInstruction> basicRegion)
                    newNode.MoveToRegion(_regionsMapping[basicRegion]);
            }

            // Clone edges.
            foreach (var originalEdge in _controlFlowGraph.GetEdges())
            {
                var newOrigin = newGraph.Nodes[originalEdge.Origin.Offset];
                var newTarget = newGraph.Nodes[originalEdge.Target.Offset];
                newOrigin.ConnectWith(newTarget, originalEdge.Type);
            }
            
            // Fix entry point.
            newGraph.Entrypoint = newGraph.Nodes[_controlFlowGraph.Entrypoint.Offset];

            return newGraph;
        }

        private ControlFlowRegion<StatementBase<TInstruction>> TransformRegion(IControlFlowRegion<TInstruction> region)
        {
            switch (region)
            {
                case BasicControlFlowRegion<TInstruction> basicRegion:
                    // Create new basic region.
                    var newBasicRegion = new BasicControlFlowRegion<StatementBase<TInstruction>>();
                    TransformSubRegions(basicRegion, newBasicRegion);

                    // Register basic region pair.
                    _regionsMapping[basicRegion] = newBasicRegion;

                    return newBasicRegion;

                case ExceptionHandlerRegion<TInstruction> ehRegion:
                    var newEhRegion = new ExceptionHandlerRegion<StatementBase<TInstruction>>();

                    // ProtectedRegion is read-only, so instead we just transform all sub regions and add it to the
                    // existing protected region.
                    TransformSubRegions(ehRegion.ProtectedRegion, newEhRegion.ProtectedRegion);
                    _regionsMapping[ehRegion.ProtectedRegion] = newEhRegion.ProtectedRegion;

                    // Add handler regions.
                    foreach (var subRegion in ehRegion.HandlerRegions)
                        newEhRegion.HandlerRegions.Add(TransformRegion(subRegion));

                    return newEhRegion;

                default:
                    throw new ArgumentOutOfRangeException(nameof(region));
            }

            void TransformSubRegions(
                BasicControlFlowRegion<TInstruction> originalRegion, 
                BasicControlFlowRegion<StatementBase<TInstruction>> newRegion)
            {
                foreach (var subRegion in originalRegion.Regions)
                    newRegion.Regions.Add(TransformRegion(subRegion));
            }
        }

        private BasicBlock<StatementBase<TInstruction>> TransformBlock(BasicBlock<TInstruction> block)
        {
            static IVariable[] CreateVariablesBuffer(int count) =>
                count == 0 ? Array.Empty<IVariable>() : new IVariable[count];
            
            int phiCount = 0;
            var result = new BasicBlock<StatementBase<TInstruction>>(block.Offset);

            foreach (var instruction in block.Instructions)
            {
                long offset = Architecture.GetOffset(instruction);
                var dataFlowNode = _dataFlowGraph.Nodes[offset];
                var stackDependencies = dataFlowNode.StackDependencies;
                var variableDependencies = dataFlowNode.VariableDependencies;
                var targetVariables = CreateVariablesBuffer(
                    stackDependencies.Count + variableDependencies.Count);
                
                for (int i = 0; i < stackDependencies.Count; i++)
                {
                    var sources = stackDependencies[i];
                    if (sources.Count == 1)
                    {
                        var source = sources.First();
                        if (source.Node.IsExternal)
                        {
                            targetVariables[i] =
                                new AstVariable(((ExternalDataSourceNode<TInstruction>) source.Node).Name);
                        }
                        else
                        {
                            var slot = _stackSlots[source.Node.Id][source.SlotIndex];
                            targetVariables[i] = slot;
                        }
                    }
                    else
                    {
                        var phiVar = CreatePhiSlot();
                        
                        var slots = sources
                            .Select(s => _stackSlots[s.Node.Id][s.SlotIndex]);
                        var variables = slots
                            .Select(s => new VariableExpression<TInstruction>(_id--, s));
                        
                        var phiStatement = new PhiStatement<TInstruction>(
                            _id--, variables.ToArray(), phiVar);
                        
                        result.Instructions.Insert(phiCount++, phiStatement);
                        targetVariables[i] = phiVar;
                    }
                }

                int index = stackDependencies.Count;
                foreach (var pair in variableDependencies)
                {
                    var variable = pair.Key;
                    var dependency = pair.Value;
                    if (dependency.Count <= 1)
                    {
                        if (!_variableVersions.ContainsKey(variable))
                            _variableVersions.Add(variable, 0);
                        
                        if (!_versionedAstVariables.ContainsKey((variable, _variableVersions[variable])))
                            _versionedAstVariables.Add((variable, _variableVersions[variable]), CreateVersionedVariable(variable));

                        targetVariables[index++] = _versionedAstVariables[(variable, _variableVersions[variable])];
                    }
                    else
                    {
                        var sources = new AstVariableCollection();
                        foreach (var source in dependency)
                        {
                            var node = source.Node;
                            long nodeOffset = Architecture.GetOffset(node.Contents);
                            if (_instructionToVersionedVariable.TryGetValue(nodeOffset, out var pair2))
                            {
                                sources.Add(_versionedAstVariables[(variable, pair2[variable])]);
                            }
                            else
                            {
                                if (!_variableVersions.ContainsKey(variable))
                                    _variableVersions.Add(variable, 0);
                                else _variableVersions[variable]++;
                                
                                var slot = new AstVariable($"{variable.Name}_v{_variableVersions[variable]}");
                                _instructionToVersionedVariable.Add(nodeOffset, new Dictionary<IVariable, int>
                                {
                                    [variable] = _variableVersions[variable]
                                });

                                _versionedAstVariables[(variable, _variableVersions[variable])] = slot;
                                sources.Add(slot);
                            }
                        }

                        if (_phiSlots.TryGetValue(sources, out var phiSlot))
                        {
                            targetVariables[index++] = phiSlot;
                        }
                        else
                        {
                            phiSlot = new AstVariable($"phi_{_phiVarCount++}");
                            var phi = new PhiStatement<TInstruction>(_id--,
                                sources.Select(s => new VariableExpression<TInstruction>(_id--, s)).ToArray(), phiSlot);
                            result.Instructions.Add(phi);
                            _phiSlots[sources] = phiSlot;
                            targetVariables[index++] = phiSlot;
                        }
                    }
                }

                var instructionExpression = new InstructionExpression<TInstruction>(
                    offset,
                    instruction,
                    targetVariables
                        .Select(t => new VariableExpression<TInstruction>(_id--, t))
                        .ToArray()
                );

                int writtenVariablesCount = Architecture.GetWrittenVariablesCount(instruction);
                var writtenVariables = CreateVariablesBuffer(writtenVariablesCount);
                
                if (writtenVariables.Length > 0)
                    Architecture.GetWrittenVariables(instruction, writtenVariables.AsSpan());

                foreach (var writtenVariable in writtenVariables)
                {
                    if (!_instructionToVersionedVariable.TryGetValue(offset, out var dict))
                    {
                        if (!_variableVersions.ContainsKey(writtenVariable))
                            _variableVersions.Add(writtenVariable, 0);
                        else _variableVersions[writtenVariable]++;
                        
                        dict = new Dictionary<IVariable, int>();
                        _instructionToVersionedVariable[offset] = dict;

                        _versionedAstVariables[(writtenVariable, _variableVersions[writtenVariable])] =
                            CreateVersionedVariable(writtenVariable);
                        dict.Add(writtenVariable, _variableVersions[writtenVariable]);
                    }
                    else
                    {
                        if (!dict.ContainsKey(writtenVariable))
                        {
                            if (!_variableVersions.ContainsKey(writtenVariable))
                                _variableVersions.Add(writtenVariable, 0);
                            
                            dict.Add(writtenVariable, _variableVersions[writtenVariable]++);
                        }

                        _versionedAstVariables[(writtenVariable, dict[writtenVariable])] =
                            CreateVersionedVariable(writtenVariable);
                    }
                }

                if (!dataFlowNode.GetDependants().Any() && writtenVariables.Length == 0)
                {
                    result.Instructions.Add(new ExpressionStatement<TInstruction>(_id--, instructionExpression));
                }
                else
                {
                    int stackPushCount = Architecture.GetStackPushCount(instruction);
                    var slots = stackPushCount == 0
                        ? Array.Empty<AstVariable>()
                        : Enumerable.Range(0, stackPushCount)
                            .Select(_ => CreateStackSlot())
                            .ToArray();

                    var combined = CreateVariablesBuffer(writtenVariables.Length + slots.Length);
                    slots.CopyTo(combined, 0);
                    int index2 = stackPushCount;
                    foreach (var writtenVariable in writtenVariables)
                    {
                        if (!_versionedAstVariables.ContainsKey((writtenVariable, _variableVersions[writtenVariable])))
                            _versionedAstVariables.Add((writtenVariable, _variableVersions[writtenVariable]), CreateVersionedVariable(writtenVariable));
                        
                        combined[index2++] =
                            _versionedAstVariables[(writtenVariable, _variableVersions[writtenVariable])];
                    }

                    _stackSlots[offset] = slots;
                    result.Instructions.Add(
                        new AssignmentStatement<TInstruction>(_id--, instructionExpression, combined));
                }
            }

            return result;
        }
        
        private AstVariable CreateStackSlot() => VariableFactory.CreateVariable($"stack_slot_{_varCount++}");

        private AstVariable CreatePhiSlot() => VariableFactory.CreateVariable($"phi_{_phiVarCount++}");

        private AstVariable CreateVersionedVariable(IVariable original) =>
            VariableFactory.CreateVariable(original.Name, _variableVersions[original]);
    }
}