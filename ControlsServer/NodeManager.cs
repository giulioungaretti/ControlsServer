using Opc.Ua;
using Opc.Ua.Server;

namespace ControlsServer;

/// <summary>
/// A node manager for a server that exposes several variables.
/// There is a lot of "boiler" plate that comes from the docs/template
/// </summary>
public class RobotNodeManager : CustomNodeManager2
{
    private Timer simulationTimer;
    private DateTime timestamp = DateTime.MinValue;

    // this is the root folder that we will add our nodes to
    private FolderState internalState;
    private const string internalStateName = "Controls";
    # region variables
    // these are the nodes that we will update
    private BaseDataVariableState temperatureState;
    private const string temperatureStateName = "Temperature";
    private BaseDataVariableState pressureState;
    private const string pressureStateName = "Pressure";
    private BaseDataVariableState modeState;
    private const string modeStateName = "Mode";
    #endregion

    /// <summary>
    /// Initializes the node manager.
    /// </summary>
    public RobotNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration, "example/namespace")
    {
        SystemContext.NodeIdFactory = this;
    }

    /// <summary>
    /// Creates the NodeId for the specified node.
    /// </summary>
    public override NodeId New(ISystemContext context, NodeState node)
    {
        if (node is BaseInstanceState instance && instance.Parent != null)
        {
            if (instance.Parent.NodeId.Identifier is string id)
            {
                return new NodeId(id + "_" + instance.SymbolicName, instance.Parent.NodeId.NamespaceIndex);
            }
        }

        return node.NodeId;
    }

    /// <summary>
    /// generates the path for a node with the given name according to what is **maybe** the convention
    /// </summary>
    private static string GenPath(string name)
    {
        return $"{internalStateName}_{name}";
    }
    /// <summary>
    /// Does any initialization required before the address space can be used.
    /// </summary>
    /// <remarks>
    /// The externalReferences is an out parameter that allows the node manager to link to nodes
    /// in other node managers. For example, the 'Objects' node is managed by the CoreNodeManager and
    /// should have a reference to the root folder node(s) exposed by this node manager.  
    /// </remarks>
    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            // create the root folder for the nodes.
            internalState = CreateFolder(null, internalStateName, internalStateName);
            internalState.AddReference(ReferenceTypes.Organizes, true, ObjectIds.ObjectsFolder);
            internalState.EventNotifier = EventNotifiers.SubscribeToEvents;
            AddRootNotifier(internalState);

            // add internal folder to the reference list and stick it in the objects folder
            List<IReference> references =
            [
                new NodeStateReference(ReferenceTypes.Organizes, false, internalState.NodeId)
            ];
            externalReferences[ObjectIds.ObjectsFolder] = !externalReferences.ContainsKey(ObjectIds.ObjectsFolder)
                ? (IList<IReference>)references
                : throw new Exception("Objects folder not found in external references");
            try
            {
                // example nesting nodes, useful for organizing
                FolderState sensorFolder = CreateFolder(internalState, "Sensors", "Sensors");
                pressureState = CreateVariable(sensorFolder, GenPath(pressureStateName), pressureStateName, DataTypeIds.Float, ValueRanks.Scalar, 0f);
                temperatureState = CreateVariable(internalState, GenPath(pressureStateName), temperatureStateName, DataTypeIds.Float, ValueRanks.Scalar, 0f);
                modeState = CreateVariable(internalState, GenPath(modeStateName), modeStateName, DataTypeIds.Int16, ValueRanks.Scalar, (short)2);

                MethodState stopMethod = CreateMethod(internalState, GenPath(nameof(Stop)), nameof(Stop));
                stopMethod.OnCallMethod = new GenericMethodCalledEventHandler(Stop);

                MethodState startMethod = CreateMethod(internalState, GenPath(nameof(Start)), nameof(Start));
                stopMethod.OnCallMethod = new GenericMethodCalledEventHandler(Start);


                #region copy pasted example with arugments
                //MethodState multiplyMethod = CreateMethod(internalState, "Robot1_Multiply", "Multiply");
                //// set input arguments
                //multiplyMethod.InputArguments = new PropertyState<Argument[]>(multiplyMethod)
                //{
                //    NodeId = new NodeId(multiplyMethod.BrowseName.Name + "InArgs", NamespaceIndex),
                //    BrowseName = BrowseNames.InputArguments
                //};
                //multiplyMethod.InputArguments.DisplayName = multiplyMethod.InputArguments.BrowseName.Name;
                //multiplyMethod.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                //multiplyMethod.InputArguments.ReferenceTypeId = ReferenceTypeIds.HasProperty;
                //multiplyMethod.InputArguments.DataType = DataTypeIds.Argument;
                //multiplyMethod.InputArguments.ValueRank = ValueRanks.OneDimension;

                //multiplyMethod.InputArguments.Value = new Argument[]
                //{
                //    new() { Name = "a", Description = "A",  DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar },
                //    new() { Name = "b", Description = "B",  DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar }
                //};

                //// set output arguments
                //multiplyMethod.OutputArguments = new PropertyState<Argument[]>(multiplyMethod)
                //{
                //    NodeId = new NodeId(multiplyMethod.BrowseName.Name + "OutArgs", NamespaceIndex),
                //    BrowseName = BrowseNames.OutputArguments
                //};
                //multiplyMethod.OutputArguments.DisplayName = multiplyMethod.OutputArguments.BrowseName.Name;
                //multiplyMethod.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
                //multiplyMethod.OutputArguments.ReferenceTypeId = ReferenceTypeIds.HasProperty;
                //multiplyMethod.OutputArguments.DataType = DataTypeIds.Argument;
                //multiplyMethod.OutputArguments.ValueRank = ValueRanks.OneDimension;

                //multiplyMethod.OutputArguments.Value = new Argument[]
                //{
                //    new() { Name = "result", Description = "Result",  DataType = DataTypeIds.Double, ValueRank = ValueRanks.Scalar }
                //};

                //multiplyMethod.OnCallMethod = new GenericMethodCalledEventHandler(OnMultiplyCall);
                #endregion 
            }
            catch (Exception e)
            {
                Utils.Trace(e, "Error populating  the address space.");
            }
            // add predefined nodes
            AddPredefinedNode(SystemContext, internalState);

            // we add our simulation timer here after our address space is ready to store values
            // in a real system this would be a timer that reads from the hardware or similar setup
            // or a opc-ua client itself that writes to the server
            simulationTimer = new Timer(DoSimulation, null, 250, 250);
        }
    }

    /// <summary>
    /// Frees any resources allocated for the address space and stops the
    /// simulation.
    /// </summary>
    public override void DeleteAddressSpace()
    {
        lock (Lock)
        {
            simulationTimer.Dispose();
        }
    }


    // this is a collections of helpers, some used some not. Once the controllers are more complex it might make sense to have them
    // else remove 
    #region HELPERS
    /// <summary>
    /// Creates a new folder.
    /// </summary>
    private FolderState CreateFolder(NodeState? parent, string path, string name)
    {
        FolderState folder = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = ObjectTypeIds.FolderType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(path, NamespaceIndex),
            DisplayName = new LocalizedText("en", name),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            EventNotifier = EventNotifiers.None
        };

        parent?.AddChild(folder);

        return folder;
    }

    /// <summary>
    /// Creates a new object.
    /// </summary>
    private BaseObjectState CreateObject(NodeState parent, string path, string name)
    {
        BaseObjectState folder = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = ObjectTypeIds.BaseObjectType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex)
        };
        folder.DisplayName = folder.BrowseName.Name;
        folder.WriteMask = AttributeWriteMask.None;
        folder.UserWriteMask = AttributeWriteMask.None;
        folder.EventNotifier = EventNotifiers.None;

        parent?.AddChild(folder);

        return folder;
    }

    /// <summary>
    /// Creates a new object type.
    /// </summary>
    private BaseObjectTypeState CreateObjectType(NodeState parent, IDictionary<NodeId, IList<IReference>> externalReferences, string path, string name)
    {
        BaseObjectTypeState type = new()
        {
            SymbolicName = name,
            SuperTypeId = ObjectTypeIds.BaseObjectType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex)
        };
        type.DisplayName = type.BrowseName.Name;
        type.WriteMask = AttributeWriteMask.None;
        type.UserWriteMask = AttributeWriteMask.None;
        type.IsAbstract = false;


        if (!externalReferences.TryGetValue(ObjectTypeIds.BaseObjectType, out IList<IReference> references))
        {
            externalReferences[ObjectTypeIds.BaseObjectType] = references = new List<IReference>();
        }

        references.Add(new NodeStateReference(ReferenceTypes.HasSubtype, false, type.NodeId));

        if (parent != null)
        {
            parent.AddReference(ReferenceTypes.Organizes, false, type.NodeId);
            type.AddReference(ReferenceTypes.Organizes, true, parent.NodeId);
        }

        AddPredefinedNode(SystemContext, type);
        return type;
    }

    /// <summary>
    /// Creates a new variable.
    /// </summary>
    private BaseDataVariableState CreateVariable(NodeState parent, string path, string name, BuiltInType dataType, int valueRank, object? initialValue = null)
    {
        return CreateVariable(parent, path, name, (uint)dataType, valueRank, initialValue);
    }

    /// <summary>
    /// Creates a new variable.
    /// </summary>
    private BaseDataVariableState CreateVariable(NodeState parent, string path, string name, NodeId dataType, int valueRank, object? initialValue = null)
    {
        BaseDataVariableState variable = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypes.Organizes,
            TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(path, NamespaceIndex),
            DisplayName = new LocalizedText("en", name),
            WriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            UserWriteMask = AttributeWriteMask.DisplayName | AttributeWriteMask.Description,
            DataType = dataType,
            ValueRank = valueRank,
            AccessLevel = AccessLevels.CurrentReadOrWrite,
            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
            Historizing = false,
            Value = initialValue,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow
        };

        parent?.AddChild(variable);

        return variable;
    }

    /// <summary>
    /// Creates a new variable type.
    /// </summary>
    private BaseVariableTypeState CreateVariableType(NodeState parent, IDictionary<NodeId, IList<IReference>> externalReferences, string path, string name, BuiltInType dataType, int valueRank)
    {
        BaseDataVariableTypeState type = new()
        {
            SymbolicName = name,
            SuperTypeId = VariableTypeIds.BaseDataVariableType,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex)
        };
        type.DisplayName = type.BrowseName.Name;
        type.WriteMask = AttributeWriteMask.None;
        type.UserWriteMask = AttributeWriteMask.None;
        type.IsAbstract = false;
        type.DataType = (uint)dataType;
        type.ValueRank = valueRank;
        type.Value = null;


        if (!externalReferences.TryGetValue(VariableTypeIds.BaseDataVariableType, out IList<IReference> references))
        {
            externalReferences[VariableTypeIds.BaseDataVariableType] = references = new List<IReference>();
        }

        references.Add(new NodeStateReference(ReferenceTypes.HasSubtype, false, type.NodeId));

        if (parent != null)
        {
            parent.AddReference(ReferenceTypes.Organizes, false, type.NodeId);
            type.AddReference(ReferenceTypes.Organizes, true, parent.NodeId);
        }

        AddPredefinedNode(SystemContext, type);
        return type;
    }

    /// <summary>
    /// Creates a new data type.
    /// </summary>
    private DataTypeState CreateDataType(NodeState parent, IDictionary<NodeId, IList<IReference>> externalReferences, string path, string name)
    {
        DataTypeState type = new()
        {
            SymbolicName = name,
            SuperTypeId = DataTypeIds.Structure,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex)
        };
        type.DisplayName = type.BrowseName.Name;
        type.WriteMask = AttributeWriteMask.None;
        type.UserWriteMask = AttributeWriteMask.None;
        type.IsAbstract = false;


        if (!externalReferences.TryGetValue(DataTypeIds.Structure, out IList<IReference> references))
        {
            externalReferences[DataTypeIds.Structure] = references = new List<IReference>();
        }

        references.Add(new NodeStateReference(ReferenceTypeIds.HasSubtype, false, type.NodeId));

        if (parent != null)
        {
            parent.AddReference(ReferenceTypes.Organizes, false, type.NodeId);
            type.AddReference(ReferenceTypes.Organizes, true, parent.NodeId);
        }

        AddPredefinedNode(SystemContext, type);
        return type;
    }

    /// <summary>
    /// Creates a new reference type.
    /// </summary>
    private ReferenceTypeState CreateReferenceType(NodeState parent, IDictionary<NodeId, IList<IReference>> externalReferences, string path, string name)
    {
        ReferenceTypeState type = new()
        {
            SymbolicName = name,
            SuperTypeId = ReferenceTypeIds.NonHierarchicalReferences,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex)
        };
        type.DisplayName = type.BrowseName.Name;
        type.WriteMask = AttributeWriteMask.None;
        type.UserWriteMask = AttributeWriteMask.None;
        type.IsAbstract = false;
        type.Symmetric = true;
        type.InverseName = name;


        if (!externalReferences.TryGetValue(ReferenceTypeIds.NonHierarchicalReferences, out IList<IReference> references))
        {
            externalReferences[ReferenceTypeIds.NonHierarchicalReferences] = references = new List<IReference>();
        }

        references.Add(new NodeStateReference(ReferenceTypeIds.HasSubtype, false, type.NodeId));

        if (parent != null)
        {
            parent.AddReference(ReferenceTypes.Organizes, false, type.NodeId);
            type.AddReference(ReferenceTypes.Organizes, true, parent.NodeId);
        }

        AddPredefinedNode(SystemContext, type);
        return type;
    }

    /// <summary>
    /// Creates a new view.
    /// </summary>
    private ViewState CreateView(NodeState parent, IDictionary<NodeId, IList<IReference>> externalReferences, string path, string name)
    {
        ViewState type = new()
        {
            SymbolicName = name,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex)
        };
        type.DisplayName = type.BrowseName.Name;
        type.WriteMask = AttributeWriteMask.None;
        type.UserWriteMask = AttributeWriteMask.None;
        type.ContainsNoLoops = true;


        if (!externalReferences.TryGetValue(ObjectIds.ViewsFolder, out IList<IReference> references))
        {
            externalReferences[ObjectIds.ViewsFolder] = references = new List<IReference>();
        }

        type.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ViewsFolder);
        references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, type.NodeId));

        if (parent != null)
        {
            parent.AddReference(ReferenceTypes.Organizes, false, type.NodeId);
            type.AddReference(ReferenceTypes.Organizes, true, parent.NodeId);
        }

        AddPredefinedNode(SystemContext, type);
        return type;
    }

    /// <summary>
    /// Creates a new method.
    /// </summary>
    private MethodState CreateMethod(NodeState parent, string path, string name)
    {
        MethodState method = new(parent)
        {
            SymbolicName = name,
            ReferenceTypeId = ReferenceTypeIds.HasComponent,
            NodeId = new NodeId(path, NamespaceIndex),
            BrowseName = new QualifiedName(path, NamespaceIndex),
            DisplayName = new LocalizedText("en", name),
            WriteMask = AttributeWriteMask.None,
            UserWriteMask = AttributeWriteMask.None,
            Executable = true,
            UserExecutable = true
        };

        parent?.AddChild(method);

        return method;
    }
    #endregion

    #region Services implmentations
    private ServiceResult Stop(
        ISystemContext context,
        MethodState method,
        IList<object> inputArguments,
        IList<object> outputArguments)
    {
        modeState.Value = (short)0;
        modeState.Timestamp = DateTime.Now;
        modeState.ClearChangeMasks(SystemContext, false);
        return ServiceResult.Good;
    }

    private ServiceResult Start(
        ISystemContext context,
        MethodState method,
        IList<object> inputArguments,
        IList<object> outputArguments)
    {

        // all arguments must be provided.
        if (inputArguments.Count < 2)
        {
            return StatusCodes.BadArgumentsMissing;
        }

        try
        {
            double a = (double)inputArguments[0];
            double b = (double)inputArguments[1];

            // set output parameter
            outputArguments[0] = a * b;
            return ServiceResult.Good;
        }
        catch
        {
            return new ServiceResult(StatusCodes.BadInvalidArgument);
        }
    }


    private void Update<T>(BaseDataVariableState variable, T newValue)
    {
        variable.Value = newValue;
        variable.Timestamp = DateTime.Now;
        variable.ClearChangeMasks(SystemContext, false);
    }
    private void DoSimulation(object state)
    {
        try
        {
            lock (Lock)
            {
                DateTime now = DateTime.Now;
                double dt = (now - timestamp).TotalMilliseconds;
                timestamp = now;

                // simulate random values for pressure and temperature
                double pressure = pressureState.Value switch
                {
                    float f when f > 0 => f + ((Random.Shared.NextDouble() - 0.5) * 0.1),
                    _ => Random.Shared.NextDouble()
                };

                Update(pressureState, pressure);

                double temperature = temperatureState.Value switch
                {
                    float f when f > 0 => f + ((Random.Shared.NextDouble() - 0.5) * 0.1),
                    _ => Random.Shared.NextDouble()
                };
                Update(temperatureState, temperature);
            }
        }
        catch (Exception e)
        {
            Utils.Trace(e, "Unexpected error doing simulation.");
        }
    }

    #endregion
}
