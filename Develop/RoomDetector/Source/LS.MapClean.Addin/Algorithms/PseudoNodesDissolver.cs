using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace LS.MapClean.Addin.Algorithms
{
    /// <summary>
    /// http://docs.autodesk.com/MAP/2014/CHS/index.html?url=filesMAPLRN/GUID-1C93E885-3623-4969-8374-5E6FD572BB07.htm,topicNumber=MAPLRNd30e135,hash=GUID-EEE3FBB2-8710-45DC-BF98-01AEA2D016AC
    /// http://knowledge.autodesk.com/support/autocad-map-3d/learn-explore/caas/documentation/MAP/2014/ENU/filesMAPUSE/GUID-BC8D97B9-1F07-4889-9239-6A257470D5F9-htm.html
    /// A pseudo-node is an unnecessary node in a geometric link that is shared by only two objects. 
    /// For example, a long link might be divided unnecessarily into many, smaller links by pseudo-nodes.
    /// 
    /// Using the Dissolve Pseudo-Nodes cleanup action, you can locate any pseudo-nodes, dissolve the node, 
    /// and join the two objects.  This option removes nodes that are at the intersection of two linear objects, 
    /// but leaves the vertex in place.
    /// </summary>
    class PseudoNodesDissolver : AlgorithmBase
    {
        public override void Check(IEnumerable<ObjectId> selectedObjectIds)
        {
            // TODO: need to be implemented in future.
            throw new System.NotImplementedException();
        }

        public void Fix()
        {
            // TODO: need to...
            throw new System.NotImplementedException();
        }
    }
}
