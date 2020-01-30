using GroupDocs.Metadata.MVC.Products.Common.Config;
using GroupDocs.Metadata.MVC.Products.Common.Util.Parser;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace GroupDocs.Metadata.MVC.Products.Metadata.Config
{
    /// <summary>
    /// MetadataConfiguration
    /// </summary>
    public class MetadataConfiguration : CommonConfiguration
    {
        [JsonProperty]
        private string filesDirectory = "DocumentSamples/Metadata";

        [JsonProperty]
        private string defaultDocument = "";

        [JsonProperty]
        private int preloadPageCount;

        [JsonProperty]
        private bool htmlMode = true;

        [JsonProperty]
        private bool cache = true;

        /// <summary>
        /// Constructor
        /// </summary>
        public MetadataConfiguration()
        {
            YamlParser parser = new YamlParser();
            dynamic configuration = parser.GetConfiguration("metadata");
            ConfigurationValuesGetter valuesGetter = new ConfigurationValuesGetter(configuration);

            // get Metadata configuration section from the web.config
            filesDirectory = valuesGetter.GetStringPropertyValue("filesDirectory", filesDirectory);
            if (!IsFullPath(filesDirectory))
            {
                filesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filesDirectory);
                if (!Directory.Exists(filesDirectory))
                {
                    Directory.CreateDirectory(filesDirectory);
                }
            }
            defaultDocument = valuesGetter.GetStringPropertyValue("defaultDocument", defaultDocument);
            preloadPageCount = valuesGetter.GetIntegerPropertyValue("preloadPageCount", preloadPageCount);
            htmlMode = valuesGetter.GetBooleanPropertyValue("htmlMode", htmlMode);
            cache = valuesGetter.GetBooleanPropertyValue("cache", cache);
        }

        private static bool IsFullPath(string path)
        {
            return !String.IsNullOrWhiteSpace(path)
                && path.IndexOfAny(System.IO.Path.GetInvalidPathChars().ToArray()) == -1
                && Path.IsPathRooted(path)
                && !Path.GetPathRoot(path).Equals(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        }

        public void SetFilesDirectory(string filesDirectory)
        {
            this.filesDirectory = filesDirectory;
        }

        public string GetFilesDirectory()
        {
            return filesDirectory;
        }

        public void SetDefaultDocument(string defaultDocument)
        {
            this.defaultDocument = defaultDocument;
        }

        public string GetDefaultDocument()
        {
            return defaultDocument;
        }

        public void SetPreloadPageCount(int preloadPageCount)
        {
            this.preloadPageCount = preloadPageCount;
        }

        public int GetPreloadPageCount()
        {
            return preloadPageCount;
        }

        public void SetIsHtmlMode(bool isHtmlMode)
        {
            this.htmlMode = isHtmlMode;
        }

        public bool GetIsHtmlMode()
        {
            return htmlMode;
        }

        public void SetCache(bool Cache)
        {
            this.cache = Cache;
        }

        public bool GetCache()
        {
            return cache;
        }
    }
}