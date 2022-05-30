using System;
using System.Linq;
using DnsClient;


namespace Kennedy.Crawler.Utils
{
    public class DnsWrapper
    {

        LookupClient client;

        public DnsWrapper()
        {
            client = new LookupClient(NameServer.GooglePublicDns);
        }

        public string DoLookup(string hostname)
        {
            try
            {
                var result = client.Query(hostname, QueryType.A);
                var record = result.Answers.ARecords().FirstOrDefault();
                return record?.Address.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
