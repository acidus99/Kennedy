using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Gemini.Net.Crawler.Utils
{
    public class ThreadedFileWriter
    {

        private object locker;

        private StreamWriter sw;
        private int counter;
        private int flushCounter = 0;

        public ThreadedFileWriter(string filename, int flushCounter = 20)
        {
            sw = new StreamWriter(filename);
            locker = new object();
            counter = 0;
            this.flushCounter = flushCounter;


        }

        public void Close()
        {
            this.sw.Close();
        }

        public void WriteLine(string line)
        {
            lock (locker)
            {
                sw.WriteLine(line);
                if (counter++ % flushCounter == 0)
                {
                    sw.Flush();
                }
            }
        }


    }
}