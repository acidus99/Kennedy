//using System;

//using Gemini.Net;

//namespace Kennedy.Crawler.Frontiers
//{
//	public class MercatorFrontier : IUrlFrontier
//	{

//        object locker;
//        int UrlsInFrontier;

//		UrlQueue FrontendQueue;


//        Dictionary<string, int> HostToBackendQueue;

//		Queue<GeminiUrl>[] BackendQueues;

//		PriorityQueue<HeapEntry, DateTime> NextRequestHeap;


//		public MercatorFrontier()
//		{

//			FrontendQueue = new UrlQueue();
//			HostToBackendQueue = new Dictionary<string, int>();
//			NextRequestHeap = new PriorityQueue<HeapEntry, DateTime>();
//            UrlsInFrontier = 0;
//            locker = new object();
//			int backendQueues = 10;
//			BackendQueues = new Queue<GeminiUrl>[backendQueues];
//			for(int i=0; i<backendQueues; i++)
//			{
//				BackendQueues[i] = new Queue<GeminiUrl>();
//			}

//		}

//        public int Count => throw new NotImplementedException();

//        public int Total => throw new NotImplementedException();

//        public string ModuleName => "Mercator Url Frontier;

//        public void AddUrl(GeminiUrl url)
//        {
//            FrontendQueue.AddUrl(url);

//        }

//        public string GetStatus()
//            => $"Queue Size:\t{Count}";


//        private void FillBackendQueue(int i)
//        {
//            var url = FrontendQueue.GetUrl();
//            if(url != null)
//            {

//            }




//        }
        


//        public GeminiUrl GetUrl(int crawlerID)
//        {

//            HeapEntry next = null;

//            lock (locker)
//            {
//                next = (NextRequestHeap.Count > 0) ?
//                        NextRequestHeap.Dequeue() :
//                        null;
//            }

//            if(next == null)
//            {
//                return null;
//            }

//            var queueID = next.FromQueue;

//            //block until its ready
//            while (next.RequestAfter < DateTime.Now)
//            {
//                Thread.Sleep(100);
//            }

//            //now dequeue it
//            var url = BackendQueues[queueID].Dequeue();

//            //is the queue empty?
//            if (BackendQueues[queueID].Count == 0)
//            {

//            }
            

//            throw new NotImplementedException();
//        }


//        private class HeapEntry
//        {
//            public DateTime RequestAfter;
//            public int FromQueue;
//        }

//    }
//}

