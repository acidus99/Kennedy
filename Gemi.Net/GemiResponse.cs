using System;
using System.Text;

namespace Gemi.Net
{
    public class GemiResponse
    {

        public GemiUrl RequestUrl { get; private set; }

        public string ResponseLine { get; private set; }


        public int StatusCode { get; private set; } = 0;

        public ConnectStatus ConnectStatus { get; internal set; } = ConnectStatus.Unknown;

        public byte[] ResponseBytes { get; private set; }

        public string ResponseText { get; private set; }

        /// <summary>
        /// The complete MIME Type, sent by the server for 2x responses
        /// </summary>
        public string MimeType { get; private set; } = "";
        
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


        public bool IsTextResponse => MimeType.StartsWith("text/");

        public bool IsInput => InStatusRange(10);
        public bool IsSuccess => InStatusRange(20);
        public bool IsRedirect => InStatusRange(30);
        public bool IsTempFail => InStatusRange(40);
        public bool IsPermFail => InStatusRange(50);
        public bool IsAuth => InStatusRange(60);

        private bool InStatusRange(int low)
            => (StatusCode >= low && StatusCode<= low + 9);

        public string SizeInfo()
            
        {
            if (IsSuccess)
            {
                if (IsTextResponse)
                {
                    return "Text Length: " + ResponseText.Length; 
                }
                else
                {
                    return $"Binary data ({ResponseBytes.Length} bytes)";
                }
            } else
            {
                return "No Body";
            }
            
        }

        internal GemiResponse()
        {
            ConnectStatus = ConnectStatus.Error;
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
                ResponseBytes = body;

                if (IsTextResponse)
                {
                    //TODO add charset parsing here
                    ResponseText = Encoding.UTF8.GetString(ResponseBytes);
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
                    s += "\n" + ResponseText;
                } else
                {
                    s += $"\nBinary data ({ResponseBytes.Length} bytes)";
                }
            }
            return s;
        }

    }
    

    /// <summary>
    /// Status of the network connection made for a Gemini request.
    /// Used to show errors at the network level vs protocol level
    /// </summary>
    public enum ConnectStatus
    {
        Unknown,
        Success,
        Error
    }


}
