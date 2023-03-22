using System;

using Gemini.Net;
using Kennedy.Data;


namespace Kennedy.Crawler
{
	public class HostHealthTracker
	{
		const int DefaultWindowSize = 10;

		Dictionary<string, HostHealthContainer> Hosts;

		int WindowSize;

		public HostHealthTracker(int windowSize = DefaultWindowSize)
		{
			Hosts = new Dictionary<string, HostHealthContainer>();
			WindowSize = windowSize;
		}

		public void AddResponse(GeminiResponse response)
		{
			string key = GetKey(response.RequestUrl);
			if(!Hosts.ContainsKey(key))
			{
				Hosts[key] = new HostHealthContainer(WindowSize);
            }
			Hosts[key].AddResponse(response);
        }

		public bool ShouldSendRequest(GeminiUrl url)
		{
			var key = GetKey(url);

			if (!Hosts.ContainsKey(key))
			{
				return true;
			}
			return Hosts[key].ShouldSendRequest(url);
		}

		private string GetKey(GeminiUrl url)
			=> url.Authority;

		private class HostHealthContainer
		{
            Queue<GeminiResponse> RecentResponses;
			float totalRequests;
            float errorRequests;

            int WindowSize;

			public HostHealthContainer(int windowSize)
			{
				RecentResponses = new Queue<GeminiResponse>(); 
				WindowSize = windowSize;
				totalRequests = 0;
				errorRequests = 0;
			}

            public void AddResponse(GeminiResponse response)
            {
				totalRequests++;
				if(IsError(response))
				{
					errorRequests++;
				}

                if (RecentResponses.Count == WindowSize)
                {
                    RecentResponses.Dequeue();
                }
                RecentResponses.Enqueue(response);
            }

            public bool ShouldSendRequest(GeminiUrl url)
            {
				if(totalRequests > WindowSize)
				{
					if (errorRequests / totalRequests > 0.8)
					{
						return false;
					}
				}

                var errors = RecentResponses
                    .Where(x => IsError(x))
					.Count();

                return errors != WindowSize;
            }

			private bool IsError(GeminiResponse resp)
				=> resp.ConnectStatus == ConnectStatus.Error;
        }
    }
}

