using GroupDocs.Metadata.MVC.Products.Metadata.Config;

namespace GroupDocs.Metadata.MVC.Products.Common.Config
{
    /// <summary>
    /// Global configuration
    /// </summary>
    public class GlobalConfiguration
    {
        public ServerConfiguration Server;
        public ApplicationConfiguration Application;
        public CommonConfiguration Common;
        public MetadataConfiguration Metadata;

        /// <summary>
        /// Get all configurations
        /// </summary>
        public GlobalConfiguration()
        {            
            Server = new ServerConfiguration();
            Application = new ApplicationConfiguration();
            Metadata = new MetadataConfiguration();
            Common = new CommonConfiguration();
        }
    }
}