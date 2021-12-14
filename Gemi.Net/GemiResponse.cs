using System;
using System.Text;

namespace Gemi.Net
{
    public class GemiResponse
    {

        public GemiUrl RequestUrl { get; private set; }

        public string ResponseLine { get; private set; }

        public int StatusCode { get; private set; }

        public ConnectStatus ConnectStatus { get; internal set; }

        public byte[] BodyBytes { get; private set; }

        public string BodyText { get; private set; }

        public bool HasBody => (BodyBytes?.Length > 0);

        /// <summary>
        /// The complete MIME Type, sent by the server for 2x responses
        /// </summary>
        public string MimeType { get; private set; }
        
        /// <summary>
        /// The prompt, sent by the server for 1x responses, if any
        /// </summary>
        public string Prompt { get; private set; }

        /// <summary>
        /// The redirect URL, sent by the server for 3x responses, if any
        /// </summary>
        public GemiUrl Redirect { get; private set; }


        /// <summary>
        /// The error message, sent by the server for 4x, 5x, or 6x responses, if any
        /// </summary>
        public string ErrorMessage { get; internal set; }

        /// <summary>
        /// Latency of the request/resp, in ms
        /// </summary>
        public int ConnectTime { get; internal set; }

        public int DownloadTime { get; internal set; }


        public bool IsTextResponse => HasBody && MimeType.StartsWith("text/");

        public bool IsInput => InStatusRange(10);
        public bool IsSuccess => InStatusRange(20);
        public bool IsRedirect => InStatusRange(30);
        public bool IsTempFail => InStatusRange(40);
        public bool IsPermFail => InStatusRange(50);
        public bool IsAuth => InStatusRange(60);

        private bool InStatusRange(int low)
            => (StatusCode >= low && StatusCode<= low + 9);

        public int BodySize => HasBody ? BodyBytes.Length : 0;

        internal GemiResponse()
        {
            ConnectStatus = ConnectStatus.Error;
            StatusCode = 0;
            MimeType = "";
            ConnectTime = 0;
            DownloadTime = 0;
        }


        public GemiResponse(GemiUrl url, string responseLine)
        {
            RequestUrl = url;

            ConnectStatus = ConnectStatus.Success;
            ResponseLine = responseLine;

            int x = responseLine.IndexOf(' ');
            if (x < 1)
            {
                throw new ApplicationException($"Response Line '{responseLine}' does not match Gemini format");
            }

            ParseStatusCode(responseLine.Substring(0, x));
            ParseMeta((x > 0 && x + 1 != responseLine.Length) ?
                responseLine.Substring(x + 1) : "");
        }

        private void ParseStatusCode(string status)
        {
            StatusCode = Convert.ToInt16(status);
            if (StatusCode < 10 || StatusCode > 69)
            {
                throw new ApplicationException($"Invalid Static Code '{StatusCode}'");
            }
        }

        private void ParseMeta(string extraData)
        {
            if(IsInput)
            {
                Prompt = extraData;
            } else if(IsSuccess)
            {
                MimeType = extraData;
            } else if(IsRedirect)
            {
                Redirect = GemiUrl.MakeUrl(RequestUrl, extraData);
            } else
            {
                ErrorMessage = extraData;
            }
        }

        public void ParseBody(byte[] body)
        {
            if (body.Length > 0)
            {
                BodyBytes = body;

                if (IsTextResponse)
                {
                    //TODO add charset parsing here
                    BodyText = Encoding.UTF8.GetString(BodyBytes);
                }
            }
        }

        public override string ToString()
        {
            var s = ResponseLine;
            if(IsSuccess)
            {
                if (IsTextResponse)
                {
                    s += "\n" + BodyText;
                } else
                {
                    s += $"\nBinary data ({BodyBytes.Length} bytes)";
                }
            }
            return s;
        }

    }
    

    /// <summary>
    /// Status of the network connection made for a Gemini request.
    /// Used to show errors at the network level vs protocol level
    /// </summary>
    public enum ConnectStatus : int
    {
        Unknown = 0,
        Success = 1,
        Error = 2,
    }


}
