using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaPIDv2_HPC_1._0
{
    public class RandomProjection
    {
        double RDP_window_cM = Program.RDP_window_cM;
        int[][] selectedSites;//[nWnd,nRDP]
        int[][] selectedSites_TRSP;//[nRDP,nWnd]
        public List<int> windows = new List<int>();
        public List<string> wndTags = new List<string>();

        public int LM_StartWndID = -1;

        public RandomProjection()
        {
            if (Program.fixWnd_RDP == false)
            {
                MakeWindows_Dynamic();
            }
            else
            {
                MakeWindows_Fix(Program.nSiteFixWnd);
            }

            SelectSites();


        }

        /// <summary>
        /// returns a list of selected sites ID for a particular thread
        /// </summary>
        /// <param name="t"></param>
        /// <returns>#item = # windows </returns>
        public int[] Get_SelectedSites_ByThread(int t)
        {
            return selectedSites_TRSP[t];
        }

        /// <summary>
        /// returns a list of selected site for a particular window
        /// </summary>
        /// <param name="t"></param>
        /// <returns>#item = # thread</returns>
        public int[] Get_SelectedSites_ByWindow(int w)
        {
            return selectedSites[w];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="MAF_startIndex">inclusively</param>
        /// <param name="MAF_endIndex">inclusively</param>
        /// <returns></returns>
        public List<int> SiteSelection_Weighted(int MAF_startIndex, int MAF_endIndex)
        {
            ConcurrentBag<int> res = new ConcurrentBag<int>();
            double sum = 0;

            for (int i = MAF_startIndex; i <= MAF_endIndex; i++)
            {
                sum += Program.panel.MAFs[i];
            }

            Parallel.For(0, Program.nRDP, (r) =>
            {
                Random rnd = new Random((int)DateTime.Now.Ticks + Thread.CurrentThread.ManagedThreadId);
                double val = rnd.NextDouble() * sum;
                double baseNum = 0;
                for (int i = MAF_startIndex; i <= MAF_endIndex; i++)
                {
                    baseNum += Program.panel.MAFs[i];
                    if (val <= baseNum)
                    {
                        res.Add(i);
                        break;
                    }
                }
            });

            return res.ToList();
        }

        void MakeWindows_Dynamic()
        {

            double tgt = RDP_window_cM;

            int i = 0;
            int siteCnt = 0;
            for (i = 0; i < Program.panel.nSite; i++)
            {
                siteCnt++;
                if (Program.gMap.preLoaded_Rates[i] >= tgt && siteCnt >= Program.minSitePerWnd)
                {
                    windows.Add(i);
                    wndTags.Add(Program.panel.phyTags[i]);
                    tgt = Program.gMap.preLoaded_Rates[i] + RDP_window_cM;
                    siteCnt = 0;
                }

            }

            windows[windows.Count - 1] = i - 1;
            wndTags[wndTags.Count - 1] = Program.panel.phyTags.Last();

            double sum = 0;
            for (i = 0; i < windows.Count; i++)
            {

                if (Program.gMap.preLoaded_Rates[windows[i]] >= Program.LLM_GLen)
                {
                    LM_StartWndID = i;
                    break;
                }
            }
         

        }


        void MakeWindows_Fix(int wndSize)
        {

            double tgt = RDP_window_cM;

            int i = 0;

            for (i = wndSize; i < Program.panel.nSite; i = i + wndSize)
            {
                windows.Add(i);
                wndTags.Add(Program.panel.phyTags[i]);
            }

            windows[windows.Count - 1] = Program.panel.nSite - 1;
            wndTags[wndTags.Count - 1] = Program.panel.phyTags.Last();


            double sum = 0;
            for (i = 0; i < windows.Count; i++)
            {

                if (Program.gMap.preLoaded_Rates[windows[i]] >= Program.LLM_GLen)
                {
                    LM_StartWndID = i;
                    break;
                }
            }
         

        }

        void SelectSites()
        {
            selectedSites = new int[windows.Count][];
            selectedSites_TRSP = new int[Program.nRDP][];

            for (int i = 0; i < windows.Count; i++)
            {
                selectedSites[i] = new int[Program.nRDP];
            }

            for (int i = 0; i < Program.nRDP; i++)
            {
                selectedSites_TRSP[i] = new int[windows.Count];
            }

            int s = 0;
            List<int> selected;
            for (int i = 0; i < windows.Count; i++)
            {
                selected = SiteSelection_Weighted(s, windows[i]);
                for (int j = 0; j < selected.Count; j++)
                {
                    selectedSites[i][j] = selected[j];
                    selectedSites_TRSP[j][i] = selected[j];
                }
                s = windows[i] + 1;
            }
        }
    }
}
