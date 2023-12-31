using System;
using System.IO;
using System.Text.Json;

using Kennedy.Server.Helpers;
using Gemini.Net;
using RocketForce;

namespace Kennedy.Server.Views.Certs
{

    /// <summary>
    /// Shows the details about a 
    /// </summary>
    internal class CertsCheckView :AbstractView
    {
        public CertsCheckView(GeminiRequest request, Response response, GeminiServer app)
            : base(request, response, app) { }

        public override void Render()
        {

            GeminiUrl? url = ParseInput();
            if(url == null)
            {
                Response.Error("Invaid Gemini URL.");
                return;
            }

            CertChecker certChecker = new CertChecker();

            if (!certChecker.Request(url))
            {
                Response.Error("Could not get certificate.");
                return;
            }

            Response.Success();
            Response.WriteLine($"# 🔏 Certificate and Key Validator");
            Response.WriteLine();
            Response.WriteLine($"Capsule Checked: {url.Hostname}:{url.Port}");
            Response.WriteLine($"Checked on: {DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")} GMT"); 
            Response.WriteLine();

            Response.WriteLine("## Generic fingerprints");
            Response.WriteLine("* This is the SHA-256 hash of the entire certificate:");
            Response.WriteLine("```");
            Response.WriteLine(certChecker.GenericCertificateSha256());
            Response.WriteLine("```");
            Response.WriteLine("* This is the SHA-256 hash of the just the public key:");
            Response.WriteLine("```");
            Response.WriteLine(certChecker.GenericPublicKeySha256());
            Response.WriteLine("```");

            Response.WriteLine("## Client-specific fingerprints");

            Response.WriteLine($"### Elaho");
            Response.WriteLine("* Elaho shows a 'Server Certificate' field if you click the lock icon in the URL bar");
            Response.WriteLine("*  This value is the formatted MD5 hash of the entire certificate:");
            Response.WriteLine("```");
            Response.WriteLine(certChecker.ClientElaho());
            Response.WriteLine("```");

            Response.WriteLine($"### Kristall");
            Response.WriteLine("* Kristall shows a 'Fingerprint' field  on the 'Mistrusted Host' screen.");
            Response.WriteLine("*  This value is the formatted SHA-256 hash of the entire certificate:");
            Response.WriteLine("```");
            Response.WriteLine(certChecker.ClientKristall());
            Response.WriteLine("```");

            Response.WriteLine($"### Lagrange");
            Response.WriteLine("* Lagrange allows you to get a 'fingerprint' via the a 'Copy Fingerprint' button of the Page Information model.");
            Response.WriteLine("* This value is the unformatted SHA-256 hash of the just public key:");
            Response.WriteLine("```");
            Response.WriteLine(certChecker.ClientLagrange());
            Response.WriteLine("```");

          

            return;
        }

        private GeminiUrl? ParseInput()
        {
            var input = Request.Url.Query;

            if(input.StartsWith("gemini://"))
            {
                return GeminiUrl.MakeUrl(input);
            }
            return GeminiUrl.MakeUrl($"gemini://{input}/");
        }
    }
}
