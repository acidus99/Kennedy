using System;
using System.Linq;
using System.Net;
using DnsClient;

namespace Kennedy.Blazer
{
    public class DnsWrapper
    {
        LookupClient client;

        public DnsWrapper()
        {
            client = new LookupClient(NameServer.GooglePublicDns);
        }

        public IPAddress DoLookup(string hostname)
        {
            try
            {
                var result = client.Query(hostname, QueryType.A);
                var record = result.Answers.ARecords().FirstOrDefault();
                return record?.Address;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
