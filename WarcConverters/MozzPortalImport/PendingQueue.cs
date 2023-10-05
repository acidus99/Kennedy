using System;
namespace Kennedy.WarcConverters.MozzPortalImport
{
	/// <summary>
	/// Ensures we don't re-add URLs we have already seen/processed
	/// </summary>
	public class PendingQueue
	{
		Queue<WaybackUrl> Queue = new Queue<WaybackUrl>();

		Dictionary<WaybackUrl, bool> Seen = new Dictionary<WaybackUrl, bool>();

		public int Count
			=> Queue.Count;

		public void Enqueue(WaybackUrl url)
		{
			if(Seen.ContainsKey(url))
			{
				return;
			}
			Seen[url] = true;
			Queue.Enqueue(url);
		}

		public void Enqueue(IEnumerable<WaybackUrl> urls)
		{
			foreach(var url in urls)
			{
				Enqueue(url);
			}
		}

        public WaybackUrl Dequeue()
			=> Queue.Dequeue();
	}
}

