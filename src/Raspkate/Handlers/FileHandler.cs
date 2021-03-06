﻿using System.Linq;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using System;
using Raspkate.Config;

namespace Raspkate.Handlers
{
    public sealed class FileHandler : RaspkateHandler
    {
        internal const string Pattern =
            @"^(?<fileName>/?(\w(\w|\-|\s)*/)*(\w|\-)+(\.(\w|\-)+)*\.\w+)(\?(?<queryString>\w+=(\w|\-)+(&\w+=(\w|\-)+)*))?$";

        private readonly Regex regularExpression = new Regex(Pattern);
        private readonly ThreadLocal<string> requestedFileName = new ThreadLocal<string>(() => string.Empty);
        private readonly string configuredBasePath;
        private readonly string configuredDefaultPages;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public FileHandler(string name, string defaultPages, string basePath)
            : base(name)
        {
            this.configuredBasePath = basePath;
            this.configuredDefaultPages = defaultPages;
        }


        public override bool ShouldHandle(HttpListenerRequest request)
        {
            if (request.RawUrl == "/")
            {
                if (!string.IsNullOrEmpty(configuredDefaultPages))
                {
                    var defaultPageList = configuredDefaultPages.Split(';').Select(p => p.Trim());
                    foreach (var defaultPage in defaultPageList)
                    {
                        var fullName = Path.Combine(this.configuredBasePath, defaultPage);
                        if (File.Exists(fullName))
                        {
                            this.requestedFileName.Value = defaultPage;
                            return true;
                        }
                    }
                }
                return false;
            }

            var match = this.regularExpression.Match(request.RawUrl.Trim('\\', '/', '?'));
            if (match.Success)
            {
                this.requestedFileName.Value = match.Groups["fileName"].Value;
                return true;
            }
            return false;
        }

        public override HandlerProcessResult Process(HttpListenerRequest request)
        {
            try
            {
                var fileNameRequested = Path.Combine(this.configuredBasePath, this.requestedFileName.Value.Replace("/", Path.DirectorySeparatorChar.ToString()));
                log.DebugFormat("File requested: {0}", fileNameRequested);
                if (!File.Exists(fileNameRequested))
                {
                    return HandlerProcessResult.Text(HttpStatusCode.NotFound, string.Format("Requested file \"{0}\" doesn't exist.", fileNameRequested));
                }
                else
                {
                    var fileBytes = File.ReadAllBytes(fileNameRequested);
                    var contentType = Utils.GetMimeType(Path.GetExtension(fileNameRequested));
                    return HandlerProcessResult.File(HttpStatusCode.OK, contentType, fileBytes);
                }
            }
            catch(Exception ex)
            {
                log.Error("Error occurred when processing the request.", ex);
                return HandlerProcessResult.Exception(HttpStatusCode.InternalServerError, ex);
            }
        }
    }
}
