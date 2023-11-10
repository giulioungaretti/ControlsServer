using Opc.Ua;
using Opc.Ua.Server;

namespace ControlsServer;

internal class ControlsServer : StandardServer
{
    /// <summary>
    /// Creates the node managers for the server.
    /// </summary>
    /// <remarks>
    /// This method allows the sub-class create any additional node managers which it uses. The SDK
    /// always creates a CoreNodeManager which handles the built-in nodes defined by the specification.
    /// Any additional NodeManagers are expected to handle application specific nodes.
    /// </remarks>
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        Utils.Trace("Creating the Node Managers.");

        List<INodeManager> nodeManagers =
            [
                // create the custom node managers.
                new RobotNodeManager(server, configuration)
            ];

        // create master node manager.
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }
}