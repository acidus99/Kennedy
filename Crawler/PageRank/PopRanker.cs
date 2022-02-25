using System;
using System.Threading.Tasks;

namespace Kennedy.Crawler.PageRank
{
    public class PopRanker
    {
        protected LinkMatrix linkMatrix;


        //tracks the number of vote totals for a specific page
        double[] voteTotals;

        //tracks the value of a vote for a specific page
        double[] voteValues;

        public int PageCount => linkMatrix.PageCount;

        public PopRanker()
        {
            linkMatrix = new LinkMatrix();
        }

        public void AddLink(int from_page_index, int to_page_index)
            => linkMatrix.AddLink(from_page_index, to_page_index);


        public double[] RankPages()
        {
            linkMatrix.Prepare();

            int totalPages = linkMatrix.PageCount;

            voteTotals = new double[totalPages];
            voteValues = new double[totalPages];

            double totalVotesValues = 0;


            double votePerLink = 1.0f / totalPages;
            double seedVoteValue = votePerLink;

            //populate
            for (int i=0; i < totalPages; ++i)
			{
                //Seed our vote values with our teleport seed value
                voteTotals[i] = seedVoteValue;
                //also add to our total
                totalVotesValues += seedVoteValue;

                //set our value for a vote from a specific page
                var outlinkCount = linkMatrix.OutLinkCount(i);
                voteValues[i] = (outlinkCount > 0) ? (votePerLink / outlinkCount) : 0f;
			}
            

            //add up the votes!
            for(int currPage=0; currPage < totalPages; currPage++)
            {
                int[] linksTo = linkMatrix.GetPageLinks(currPage);
                if(linksTo == null)
                {
                    continue;
                }
                double voteValue = voteValues[currPage];

                foreach(int toPage in linksTo)
                {
                    totalVotesValues += voteValue;
                    voteTotals[toPage] += voteValue;
                }
            }

            //create percentages
            double[] percentages = new double[totalPages];
            for(int i=0; i < totalPages; i++)
            {
                percentages[i] = voteTotals[i] / totalVotesValues;
            }

            return percentages;
        }
    }
}
