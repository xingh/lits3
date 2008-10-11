﻿using System;
using System.Net;
using System.Xml;

namespace LitS3
{
    /// <summary>
    /// Provides a base class which encapsulates an Amazon REST WebResponse and sets up an
    /// appropriate XmlReader for the kinds of XML data Amazon sends back.
    /// </summary>
    public abstract class S3Response : IDisposable
    {
        XmlReader reader;

        public HttpWebResponse WebResponse { get; private set; }

        /// <summary>
        /// Gets an XmlReader for parsing Amazon XML responses, creating one if necessary and
        /// calling MoveToContent().
        /// </summary>
        protected XmlReader Reader
        {
            get { return reader ?? (reader = CreateXmlReader()); }
        }

        protected S3Response(HttpWebResponse response)
        {
            CheckResponse(response);
            this.WebResponse = response;
        }

        static void CheckResponse(HttpWebResponse response)
        {
            // see if the server told us to screw off
            if (response.StatusCode == HttpStatusCode.TemporaryRedirect &&
                response.Headers[HttpResponseHeader.Location] == "http://aws.amazon.com/s3")
                throw new Exception("A GetAllBuckets request was rejected by the S3 server. Did you forget to authorize the request?");
        }

        XmlReader CreateXmlReader()
        {
            var reader = new XmlTextReader(WebResponse.GetResponseStream())
            {
                WhitespaceHandling = WhitespaceHandling.Significant,
                Namespaces = false
            };
            reader.MoveToContent();
            return reader;
        }

        /// <summary>
        /// Just a shortcut to close our WebResponse.
        /// </summary>
        public void Close()
        {
            WebResponse.Close();
        }

        void IDisposable.Dispose()
        {
            Close();
        }
    }
}
