using GroupDocs.Metadata.MVC.Products.Common.Entity.Web;
using GroupDocs.Metadata.MVC.Products.Common.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using GroupDocs.Metadata.MVC.Products.Metadata.Config;
using GroupDocs.Metadata.MVC.Products.Common.Util.Comparator;
using GroupDocs.Metadata.Common;
using GroupDocs.Metadata.Options;
using GroupDocs.Metadata.Tagging;
using GroupDocs.Metadata.Formats.Document;
using GroupDocs.Metadata.MVC.Products.Metadata.Entity;

namespace GroupDocs.Metadata.MVC.Products.Metadata.Controllers
{
    /// <summary>
    /// MetadataApiController
    /// </summary>
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class MetadataApiController : ApiController
    {
        private readonly Common.Config.GlobalConfiguration globalConfiguration;

        /// <summary>
        /// Constructor
        /// </summary>
        public MetadataApiController()
        {
            // Check if filesDirectory is relative or absolute path           
            globalConfiguration = new Common.Config.GlobalConfiguration();

            // TODO: investigate the System.IO.IOException
            License license = new License();
            if (!string.IsNullOrEmpty(globalConfiguration.Application.LicensePath))
            {
                license.SetLicense(globalConfiguration.Application.LicensePath);
            }
        }

        /// <summary>
        /// Load Metadata configuration
        /// </summary>       
        /// <returns>Metadata configuration</returns>
        [HttpGet]
        [Route("loadConfig")]
        public MetadataConfiguration LoadConfig()
        {            
            return globalConfiguration.Metadata;
        }

        /// <summary>
        /// Get all files and directories from storage
        /// </summary>
        /// <param name="postedData">Post data</param>
        /// <returns>List of files and directories</returns>
        [HttpPost]
        [Route("loadFileTree")]
        public HttpResponseMessage loadFileTree(PostedDataEntity postedData)
        {
            try
            {
                List<FileDescriptionEntity> filesList = new List<FileDescriptionEntity>();
                if (!string.IsNullOrEmpty(globalConfiguration.Metadata.GetFilesDirectory()))
                {
                    filesList = LoadFiles(globalConfiguration);
                }
                return Request.CreateResponse(HttpStatusCode.OK, filesList);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Resources.GenerateException(ex));
            }
        }

        /// <summary>
        /// Get file properties
        /// </summary>
        /// <param name="postedData"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("loadProperties")]
        public HttpResponseMessage loadProperties(PostedDataEntity postedData)
        {
            try
            {
                List<FilePropertyEntity> outputProperties = new List<FilePropertyEntity>();

                outputProperties.AddRange(GetFileProperties(postedData, FilePropertyCategory.BuildIn));
                outputProperties.AddRange(GetFileProperties(postedData, FilePropertyCategory.Default));

                return Request.CreateResponse(HttpStatusCode.OK, outputProperties);
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Resources.GenerateException(ex));
            }
        }

        private static IList<FilePropertyEntity> GetFileProperties(PostedDataEntity postedData, FilePropertyCategory filePropertyCategory) {

            List<FilePropertyEntity> outputProperties = new List<FilePropertyEntity>();

            string password = (String.IsNullOrEmpty(postedData.password)) ? null : postedData.password;
            // set password for protected document                
            var loadOptions = new LoadOptions
            {
                Password = password
            };

            using (GroupDocs.Metadata.Metadata metadata = new GroupDocs.Metadata.Metadata(postedData.guid, loadOptions))
            {
                if (filePropertyCategory == FilePropertyCategory.BuildIn)
                {
                    if (metadata.FileFormat != FileFormat.Unknown && !metadata.GetDocumentInfo().IsEncrypted)
                    {
                        // Fetch all properties having a specific type and value
                        IEnumerable<MetadataProperty> buildInProperties = metadata.FindProperties(p => p.Value.RawValue != null
                                                                                                    && !string.Empty.Equals(p.Value.RawValue)
                                                                                                    && (!p.Value.RawValue.GetType().IsValueType 
                                                                                                        || !p.Value.RawValue.Equals(Activator.CreateInstance(p.Value.RawValue.GetType())))
                                                                                                    && p.Tags.Contains(Tags.Document.BuiltIn)
                                                                                                    && (p.Value.Type == MetadataPropertyType.String
                                                                                                        || p.Value.Type == MetadataPropertyType.DateTime));

                        foreach (var buildInProperty in buildInProperties)
                        {
                            var outputProperty = new FilePropertyEntity
                            {
                                name = buildInProperty.Name,
                                value = GetPropertyValue(buildInProperty),
                                type = buildInProperty.Value.Type,
                                category = FilePropertyCategory.BuildIn,
                                original = true
                            };
                            outputProperties.Add(outputProperty);
                        }
                    }
                }

                else if (filePropertyCategory == FilePropertyCategory.Default)
                {
                    // Fetch all metadata properties that fall into a particular category
                    IEnumerable<MetadataProperty> defaultProperties = metadata.FindProperties(p => p.Value.RawValue != null
                                                                                                && !string.Empty.Equals(p.Value.RawValue)
                                                                                                && (!p.Value.RawValue.GetType().IsValueType
                                                                                                    || !p.Value.RawValue.Equals(Activator.CreateInstance(p.Value.RawValue.GetType())))
                                                                                                && p.Tags.Any(t => t.Category == Tags.Content));

                    foreach (var defaultProperty in defaultProperties)
                    {
                        var outputProperty = new FilePropertyEntity
                        {
                            name = defaultProperty.Name,
                            value = GetPropertyValue(defaultProperty),
                            type = defaultProperty.Value.Type,
                            category = FilePropertyCategory.Default,
                            original = true
                        };
                        outputProperties.Add(outputProperty);
                    }
                }
            }

            return outputProperties;
        }

        private static dynamic GetPropertyValue(MetadataProperty property) {
            switch (property.Value.Type) 
            {
                case MetadataPropertyType.String:
                    return property.Value.ToClass<string>();
                case MetadataPropertyType.DateTime:
                    return property.Value.ToStruct(DateTime.MinValue);
                case MetadataPropertyType.Integer:
                    if (property.Name.ToLower().Contains("fileformat")) {
                        return ((FileFormat)property.Value.RawValue).ToString();
                    }
                    else
                    {
                        return property.Value.ToStruct(int.MinValue);
                    }
                case MetadataPropertyType.StringArray:
                    return property.Value.ToClass<string[]>()[0];
                default:
                    return property.Value.ToClass<string>();
            }
        }

        /// <summary>
        /// Get file properties
        /// </summary>
        /// <param name="postedData"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("loadPropertiesNames")]
        public HttpResponseMessage loadPropertiesNames(PostedDataEntity postedData)
        {
            try
            {
                var buildInProperties = GetFileProperties(postedData, FilePropertyCategory.BuildIn);

                var filePropertiesNames = new List<FilePropertyName>();

                using (GroupDocs.Metadata.Metadata metadata = new GroupDocs.Metadata.Metadata(postedData.guid, GetLoadOptions(postedData)))
                {
                    foreach (var property in (metadata.GetRootPackage().FindProperties(p =>
                        p.Value.ToClass<DocumentPackage>() != null)))
                    {
                        foreach (var descriptor in property.Value.ToClass<DocumentPackage>().KnowPropertyDescriptors
                            .Where(d => !buildInProperties.Select(op => op.name.ToLower()).Contains(d.Name.ToLower())
                                     && d.Tags.Contains(Tags.Document.BuiltIn)
                                     && (d.Type == MetadataPropertyType.String || d.Type == MetadataPropertyType.DateTime)))
                        {
                            filePropertiesNames.Add(new FilePropertyName { name = descriptor.Name, type = descriptor.Type });
                        }
                    }
                }

                return Request.CreateResponse(HttpStatusCode.OK, filePropertiesNames.ToArray());
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Resources.GenerateException(ex));
            }
        }

        /// <summary>
        /// Save file properties
        /// </summary>
        /// <param name="postedData"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("saveProperty")]
        public HttpResponseMessage saveProperty(PostedDataEntity postedData)
        {
            try
            {
                using (GroupDocs.Metadata.Metadata metadata = new GroupDocs.Metadata.Metadata(postedData.guid, GetLoadOptions(postedData)))
                {
                    if (metadata.FileFormat != FileFormat.Unknown && !metadata.GetDocumentInfo().IsEncrypted)
                    {
                        foreach (var property in postedData.properties)
                        {
                            metadata.SetProperties(p => string.Equals(p.Name, property.name,
                                StringComparison.OrdinalIgnoreCase), new PropertyValue(property.value));
                        }

                        metadata.Save(GetTempPath(postedData));
                    }
                }

                if (File.Exists(postedData.guid))
                {
                    File.Delete(postedData.guid);
                }

                File.Move(GetTempPath(postedData), postedData.guid);

                // TODO: consider option to response with updated file
                return Request.CreateResponse(HttpStatusCode.OK, new object());
            }
            catch (Exception ex)
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Resources.GenerateException(ex));
            }
        }

        private static string GetTempPath(PostedDataEntity postedData)
        {
            string tempFilename = Path.GetFileNameWithoutExtension(postedData.guid) + "_tmp";
            return Path.Combine(Path.GetDirectoryName(postedData.guid), tempFilename + Path.GetExtension(postedData.guid));
        }

        private static LoadOptions GetLoadOptions(PostedDataEntity postedData)
        {
            string password = (String.IsNullOrEmpty(postedData.password)) ? null : postedData.password;
            // set password for protected document                
            var loadOptions = new LoadOptions
            {
                Password = password
            };
            return loadOptions;
        }

        /// <summary>
        /// Remove file properties
        /// </summary>
        /// <param name="postedData"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("removeProperty")]
        public HttpResponseMessage removeProperty(PostedDataEntity postedData)
        {
            try
            {
                using (GroupDocs.Metadata.Metadata metadata = new GroupDocs.Metadata.Metadata(postedData.guid, GetLoadOptions(postedData)))
                {
                    if (metadata.FileFormat != FileFormat.Unknown && !metadata.GetDocumentInfo().IsEncrypted)
                    {
                        metadata.RemoveProperties(p => string.Equals(p.Name, postedData.properties[0].name,
                                StringComparison.OrdinalIgnoreCase));

                        metadata.Save(GetTempPath(postedData));
                    }
                }

                if (File.Exists(postedData.guid))
                {
                    File.Delete(postedData.guid);
                }

                File.Move(GetTempPath(postedData), postedData.guid);

                return Request.CreateResponse(HttpStatusCode.OK, new object());
            }
            catch (Exception ex) 
            {
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Resources.GenerateException(ex));
            }
        }

        /// <summary>
        /// Load documents
        /// </summary>
        /// <returns>List[FileDescriptionEntity]</returns>
        public static List<FileDescriptionEntity> LoadFiles(Common.Config.GlobalConfiguration globalConfiguration)
        {
            var currentPath = globalConfiguration.Metadata.GetFilesDirectory();
            List<string> allFiles = new List<string>(Directory.GetFiles(currentPath));
            allFiles.AddRange(Directory.GetDirectories(currentPath));
            List<FileDescriptionEntity> fileList = new List<FileDescriptionEntity>();

            // TODO: get temp directory name
            string tempDirectoryName = "temp";

            allFiles.Sort(new FileNameComparator());
            allFiles.Sort(new FileDateComparator());

            foreach (string file in allFiles)
            {
                FileInfo fileInfo = new FileInfo(file);
                // check if current file/folder is hidden
                if (!(tempDirectoryName.Equals(Path.GetFileName(file)) ||
                    fileInfo.Attributes.HasFlag(FileAttributes.Hidden) ||
                    Path.GetFileName(file).Equals(Path.GetFileName(globalConfiguration.Metadata.GetFilesDirectory()))))
                {
                    FileDescriptionEntity fileDescription = new FileDescriptionEntity();
                    fileDescription.guid = Path.GetFullPath(file);
                    fileDescription.name = Path.GetFileName(file);
                    // set is directory true/false
                    fileDescription.isDirectory = fileInfo.Attributes.HasFlag(FileAttributes.Directory);
                    // set file size
                    if (!fileDescription.isDirectory)
                    {
                        fileDescription.size = fileInfo.Length;
                    }
                    // add object to array list
                    fileList.Add(fileDescription);
                }
            }

            return fileList;
        }

        /// <summary>
        /// Load document description
        /// </summary>
        /// <param name="postedData">Post data</param>
        /// <returns>Document info object</returns>
        [HttpPost]
        [Route("loadDocumentDescription")]
        public HttpResponseMessage LoadDocumentDescription(PostedDataEntity postedData)
        {
            string password = "";
            try
            {
                LoadDocumentEntity loadDocumentEntity = LoadDocument(postedData);
                // return document description
                return Request.CreateResponse(HttpStatusCode.OK, loadDocumentEntity);
            }
            catch (System.Exception ex)
            {
                // set exception message
                // TODO: return InternalServerError for common Exception and Forbidden for PasswordProtectedException
                return Request.CreateResponse(HttpStatusCode.Forbidden, Resources.GenerateException(ex, password));
            }
        }

        /// <summary>
        /// Download currently viewed document
        /// </summary>
        /// <param name="path">Path of the document to download</param>
        /// <returns>Document stream as attachment</returns>
        [HttpGet]
        [Route("downloadDocument")]
        public HttpResponseMessage DownloadDocument(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (File.Exists(path))
                {
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
                    var fileStream = new FileStream(path, FileMode.Open);
                    response.Content = new StreamContent(fileStream);
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
                    response.Content.Headers.ContentDisposition.FileName = Path.GetFileName(path);
                    return response;
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Upload document
        /// </summary>
        /// <returns>Uploaded document object</returns>
        [HttpPost]
        [Route("uploadDocument")]
        public HttpResponseMessage UploadDocument()
        {
            try
            {
                string url = HttpContext.Current.Request.Form["url"];
                // get documents storage path
                string documentStoragePath = globalConfiguration.Metadata.GetFilesDirectory();
                bool rewrite = bool.Parse(HttpContext.Current.Request.Form["rewrite"]);
                string fileSavePath = "";
                if (string.IsNullOrEmpty(url))
                {
                    if (HttpContext.Current.Request.Files.AllKeys != null)
                    {
                        // Get the uploaded document from the Files collection
                        var httpPostedFile = HttpContext.Current.Request.Files["file"];
                        if (httpPostedFile != null)
                        {
                            if (rewrite)
                            {
                                // Get the complete file path
                                fileSavePath = Path.Combine(documentStoragePath, httpPostedFile.FileName);
                            }
                            else
                            {
                                fileSavePath = Resources.GetFreeFileName(documentStoragePath, httpPostedFile.FileName);
                            }

                            // Save the uploaded file to "UploadedFiles" folder
                            httpPostedFile.SaveAs(fileSavePath);
                        }
                    }
                }
                else
                {
                    using (WebClient client = new WebClient())
                    {
                        // get file name from the URL
                        Uri uri = new Uri(url);
                        string fileName = Path.GetFileName(uri.LocalPath);
                        if (rewrite)
                        {
                            // Get the complete file path
                            fileSavePath = Path.Combine(documentStoragePath, fileName);
                        }
                        else
                        {
                            fileSavePath = Resources.GetFreeFileName(documentStoragePath, fileName);
                        }
                        // Download the Web resource and save it into the current filesystem folder.
                        client.DownloadFile(url, fileSavePath);
                    }
                }
                UploadedDocumentEntity uploadedDocument = new UploadedDocumentEntity();
                uploadedDocument.guid = fileSavePath;
                return Request.CreateResponse(HttpStatusCode.OK, uploadedDocument);
            }
            catch (Exception ex)
            {
                // set exception message
                return Request.CreateResponse(HttpStatusCode.InternalServerError, Resources.GenerateException(ex));
            }
        }

        private LoadDocumentEntity LoadDocument(PostedDataEntity postedData)
        {
            // get/set parameters
            string documentGuid = postedData.guid;
            string password = (String.IsNullOrEmpty(postedData.password)) ? null : postedData.password;
            LoadDocumentEntity loadDocumentEntity = new LoadDocumentEntity();
            
            // check if documentGuid contains path or only file name
            if (!Path.IsPathRooted(documentGuid))
            {
                documentGuid = globalConfiguration.Metadata.GetFilesDirectory() + "/" + documentGuid;
            }

            // set password for protected document                
            var loadOptions = new LoadOptions
            {
                Password = password
            };

            GroupDocs.Metadata.Common.IReadOnlyList<PageInfo> pages;
            // TODO: make it more DRY
            using (GroupDocs.Metadata.Metadata metadata = new GroupDocs.Metadata.Metadata(postedData.guid, loadOptions))
            {
                 pages = metadata.GetDocumentInfo().Pages;
            }

            var pagesContent = GetAllPagesContent(password, documentGuid, pages);

            foreach (PageInfo page in pages)
            {
                PageDescriptionEntity pageData = GetPageDescriptionEntities(page);
                if (pagesContent.Count > 0)
                {
                    pageData.SetData(pagesContent[page.PageNumber - 1]);
                }
                loadDocumentEntity.SetPages(pageData);
            }
            
            loadDocumentEntity.SetGuid(documentGuid);
            
            // return document description
            return loadDocumentEntity;
        }

        private static PageDescriptionEntity GetPageDescriptionEntities(PageInfo page)
        {
            PageDescriptionEntity pageDescriptionEntity = new PageDescriptionEntity();
            pageDescriptionEntity.number = page.PageNumber;
            pageDescriptionEntity.height = page.Height;
            pageDescriptionEntity.width = page.Width;
            return pageDescriptionEntity;
        }

        private static List<string> GetAllPagesContent(string password, string documentGuid, GroupDocs.Metadata.Common.IReadOnlyList<PageInfo> pages)
        {
            List<string> allPages = new List<string>();

            for (int i = 0; i < pages.Count; i++)
            {
                byte[] bytes;
                using (var memoryStream = RenderPageToMemoryStream(i+1, documentGuid, password ))
                {
                    bytes = memoryStream.ToArray();
                }

                string encodedImage = Convert.ToBase64String(bytes);
                allPages.Add(encodedImage);
            }

            return allPages;
        }

        static MemoryStream RenderPageToMemoryStream(int pageNumberToRender, string documentGuid, string password)
        {
            MemoryStream result = new MemoryStream();

            var loadOptions = new LoadOptions
            {
                Password = password
            };

            using (GroupDocs.Metadata.Metadata metadata = new GroupDocs.Metadata.Metadata(documentGuid, loadOptions))
            {
                PreviewOptions previewOptions = new PreviewOptions(pageNumber => result);

                // Do not close stream as we're about to read from it
                previewOptions.PreviewFormat = PreviewOptions.PreviewFormats.PNG;
                previewOptions.PageNumbers = new[] { pageNumberToRender };
                metadata.GeneratePreview(previewOptions);
            }

            return result;
        }
    }
}