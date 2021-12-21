using System;
using System.Net;

namespace ekyc_api.Utils
{
    public class HttpStatusException : Exception
    {
        public HttpStatusException(HttpStatusCode status, string msg) : base(msg)
        {
            Status = status;
        }

        public HttpStatusCode Status { get; }
    }
}