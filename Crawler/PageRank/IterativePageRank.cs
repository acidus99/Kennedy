using System;
using System.Threading.Tasks;

namespace Kennedy.Crawler.PageRank
{
    public class IterativePageRank
    {
        protected int mPageCount;
        protected LinkMatrix linkMatrix;
        protected float[] mC;
        protected float mAlpha;
        protected bool mParallel = true;

        public bool Parallel
        {
            get { return mParallel; }
            set { mParallel = value; }
        }

        public IterativePageRank(int page_count, float damping_factor = 0.85f)
        {
            mPageCount = page_count;
            mAlpha = damping_factor;

            linkMatrix = new LinkMatrix();
            mC = new float[page_count];
        }

        public void AddLink(int from_page_index, int to_page_index)
            => linkMatrix.AddLink(from_page_index, to_page_index);

        private void SetPageOutLinkCount()
        {
            for (int i = 0; i < mPageCount; ++i)
            {
                int outLinkCount = linkMatrix.OutLinkCount(i);

                if (outLinkCount > 0)
                {
                    mC[i] = 1.0f / outLinkCount;
                }
            }
        }

        public float[] RankPages(double tolerance)
        {
            linkMatrix.Prepare();

            SetPageOutLinkCount();

            float[] P = new float[mPageCount];
            float[] P_prev = new float[mPageCount];

            double diff = 0;
			
			for(int i=0; i < mPageCount; ++i)
			{
				P[i] = 1.0f / mPageCount;
			}

            PrintRanks(0, P);

            Task[] tasks = null;
            if (mParallel)
            {
                tasks = new Task[mPageCount];
            }

            int iteration = 0;

            float P_val = 0;
            do
            {
                for (int i = 0; i < mPageCount; ++i)
                {
                    P_prev[i] = P[i];
                }

                for (int j = 0; j < mPageCount; ++j)
                {
                    if (mParallel)
                    {
                        var task_j = j;
                        tasks[j] = Task.Factory.StartNew(() =>
                        {
                            float val = 0;
                            for (int i = 0; i < mPageCount; ++i)
                            {
                                val += (1 - mAlpha) * (linkMatrix.HasLink(i, task_j) ? mC[i] : 0) * P[i] + mAlpha * P[i] / mPageCount;
                            }
							
                            P[task_j] = val;
                        });
                    }
                    else
                    {
                        P_val = 0;
                        for (int i = 0; i < mPageCount; ++i)
                        {
                            P_val += (1 - mAlpha) * (linkMatrix.HasLink(i, j) ? mC[i] : 0) * P[i] + mAlpha * P[i] / mPageCount;
                        }
                        P[j] = P_val;
                    }
                }

                if (mParallel)
                {
                    Task.WaitAll(tasks);
                }
                iteration++;
                PrintRanks(iteration, P);

                diff = Diff(P, P_prev);
                Console.WriteLine("{2}\tIteration: {0}, RMSE: {1}", iteration, diff, DateTime.Now);
            } while (diff > tolerance);

            return P;

        }

        private void PrintRanks(int iteration, float [] P)
        {
            Console.WriteLine("==================================");
            Console.WriteLine($"Interation {iteration}");
            float sum = 0;
            for (int i=0; i < mPageCount; i++)
            {
                sum += P[i];
                Console.WriteLine($"index: {i}\tRank: {P[i]}");
            }
            Console.WriteLine($"SUM: {sum}");
        }


        private double Diff(float[] p1, float[] p2)
        {
            float p_diff = 0;
            float sum = 0;
            for (int i = 0; i < mPageCount; ++i)
            {
                p_diff = p1[i] - p2[i];
                sum += p_diff * p_diff;
             }
            return System.Math.Sqrt(sum);
        }
    }
}
