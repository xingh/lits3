﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace LitS3
{
    public class S3Request
    {
        const string AmazonHeaderPrefix = "x-amz-";
        const string AmazonDateHeader = "x-amz-date";
        const string MetadataPrefix = "x-amz-meta-";
        const string BucketNameHeader = "x-bucket-name";

        public S3Service Service { get; private set; }
        public HttpWebRequest WebRequest { get; private set; }

        public S3Request(S3Service service, string method, string bucketName, string objectKey)
        {
            this.Service = service;
            this.WebRequest = CreateWebRequest(method, bucketName, objectKey);
        }

        HttpWebRequest CreateWebRequest(string method, string bucketName, string objectKey)
        {
            var uriString = new StringBuilder(Service.UseSsl ? "https://" : "http://");

            if (bucketName != null && Service.UseSubdomains)
                uriString.Append(bucketName).Append('.');

            uriString.Append(Service.Host);

            if (Service.CustomPort != 0)
                uriString.Append(':').Append(Service.CustomPort);

            uriString.Append('/');

            if (bucketName != null && !Service.UseSubdomains)
                uriString.Append(bucketName).Append('/');

            // could be null
            uriString.Append(objectKey);

            var uri = new Uri(uriString.ToString());

            HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Method = method;
            request.AllowWriteStreamBuffering = false;
            request.AllowAutoRedirect = false;
            if (bucketName != null)
                request.Headers[BucketNameHeader] = bucketName;
            return request;
        }

        public bool IsAuthorized
        {
            get { return WebRequest.Headers[HttpRequestHeader.Authorization] != null; }
        }

        void AuthorizeIfNecessary()
        {
            if (!IsAuthorized) Authorize();
        }

        /// <summary>
        /// Signs the given HttpWebRequest using the HTTP Authorization header with a value
        /// generated using the contents of the request plus our SecretAccessKey.
        /// </summary>
        /// <remarks>
        /// See http://docs.amazonwebservices.com/AmazonS3/2006-03-01/RESTAuthentication.html
        /// 
        /// Needs to be refactored into other classes for reuse in constructing public URLs for objects.
        /// </remarks>
        public void Authorize()
        {
            if (IsAuthorized)
                throw new InvalidOperationException("This request has already been authorized.");

            WebRequest.Headers[AmazonDateHeader] = DateTime.UtcNow.ToString("r");

            var stringToSign = new StringBuilder()
                .Append(WebRequest.Method).Append('\n')
                .Append(WebRequest.Headers[HttpRequestHeader.ContentMd5]).Append('\n')
                .Append(WebRequest.ContentType).Append('\n')
                .Append('\n'); // ignore the official Date header since WebRequest won't send it

            var amzHeaders = new SortedList<string, string[]>();

            foreach (string header in WebRequest.Headers)
                if (header.StartsWith(AmazonHeaderPrefix))
                    amzHeaders.Add(header.ToLower(), WebRequest.Headers.GetValues(header));

            // append the sorted headers in amazon's defined CanonicalizedAmzHeaders format
            foreach (var amzHeader in amzHeaders)
            {
                stringToSign.Append(amzHeader.Key).Append(':');

                // ensure that there's no space around the colon
                bool lastCharWasWhitespace = true;

                foreach (char c in string.Join(",", amzHeader.Value))
                {
                    bool isWhitespace = char.IsWhiteSpace(c);

                    if (isWhitespace && !lastCharWasWhitespace)
                        stringToSign.Append(' '); // amazon wants whitespace "folded" to a single space
                    else if (!isWhitespace)
                        stringToSign.Append(c);

                    lastCharWasWhitespace = isWhitespace;
                }

                stringToSign.Append('\n');
            }

            // append the resource WebRequested using amazon's CanonicalizedResource format

            // does this WebRequest address a bucket?
            string bucketName = WebRequest.Headers[BucketNameHeader];

            if (Service.UseSubdomains && bucketName != null)
            {
                stringToSign.Append('/').Append(bucketName);
                WebRequest.Headers.Remove(BucketNameHeader);
            }

            stringToSign.Append(WebRequest.RequestUri.AbsolutePath);

            // todo: add sub-resource, if present. "?acl", "?location", "?logging", or "?torrent"

            // encode
            var signer = new HMACSHA1(Encoding.UTF8.GetBytes(Service.SecretAccessKey));
            var signed = Convert.ToBase64String(signer.ComputeHash(Encoding.UTF8.GetBytes(stringToSign.ToString())));

            string authorization = string.Format("AWS {0}:{1}", Service.AccessKeyID, signed);

            WebRequest.Headers[HttpRequestHeader.Authorization] = authorization;
        }

        /// <summary>
        /// Gets the S3 REST response synchronously. This method is just a shortcut for the GetResponse()
        /// method of our WebRequest property. It also calls Authorize() if necessary.
        /// </summary>
        public HttpWebResponse GetResponse()
        {
            AuthorizeIfNecessary();
            return (HttpWebResponse)WebRequest.GetResponse();
        }
    }
}
