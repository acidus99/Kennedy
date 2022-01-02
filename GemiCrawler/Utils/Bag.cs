using System;
using System.Collections.Generic;
using System.Linq;
namespace GemiCrawler.Utils
{
    /// <summary>
    /// Generic Object Bag - Holds objects and keeps a count of how many times the same object has been added
    /// to the bag. Very handy to throw in a bunch of items and get out a list of unique objects
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Bag<T>
    {
        object locker;

        public int TotalAdds { get; private set; }

        Dictionary<T, int> bag;

        public Bag()
        {
            bag = new Dictionary<T, int>();
            locker = new object();
        }

        public void Clear()
            => bag.Clear();

        public int Add(T t)
        {
            int count = 0;

            lock (locker)
            {
                TotalAdds += 1;
                if (!bag.ContainsKey(t))
                {
                    count = 1;
                    bag[t] = 1;
                }
                else
                {
                    count = bag[t] + 1;
                    bag[t] = count;
                }
            }
            return count;
        }

        public void AddRange(IEnumerable<T> enumberable)
        {
            foreach(T t in enumberable)
            {
                Add(t);
            }
        }

        public bool Contains(T t)
        {
            lock(locker)
            {
                return bag.ContainsKey(t);
            }
        }

        public IEnumerable<T> GetValues()
            => bag.Keys;

        public int UniqueItems
            =>bag.Keys.Count;
    }
}
