namespace MeshToTiff
{
    public class MeshToTiffPlugIn : Rhino.PlugIns.PlugIn
    {
        public MeshToTiffPlugIn()
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the only instance of the plug-in.
        /// </summary>
        public static MeshToTiffPlugIn Instance
        {
            get; private set;
        }
    }
}