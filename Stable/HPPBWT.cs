using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Range = RaPIDv2_Stable_1._0.Utl.Range;

namespace RaPIDv2_Stable_1._0
{
    public class HPPBWT
    {


        static PDA newPDA;
        static PDA oldPDA;
        static PDA temPDA;
        static int[] psHolder;
        static Int16[] minDZ;
        static int[] offsetsHolder;


        static int blockSize = -1;
        static int msD_Wait = 10;
        static short blockHasOneSignal = -10;

        static int nBucket;

        static ConcurrentBag<(int a, int b, byte rID, Range rg)>[] Global_Holder;
        static List<(int a, int b, byte rID, Range rg)>[] Sorted_Holder;


        public class PDA
        {
            public int[] pArr;
            public int zeroCnt = 0;
            public Int16[] mLens;

            public PDA(int nHapTotal)
            {
                pArr = new int[nHapTotal];
                mLens = new Int16[nHapTotal];
            }

        }

        public HPPBWT()
        {
            Console.WriteLine(DateTime.Now + " Initializing HP-PBWT " + Program.panel.nHap + " Samples (#haplotypes) ...");

            newPDA = new PDA(Program.panel.nHap);
            oldPDA = new PDA(Program.panel.nHap);

            psHolder = new int[Program.panel.nHap];
            offsetsHolder = new int[Program.nThread_PD];
            minDZ = new short[Program.nThread_PD];

            blockSize = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Program.panel.nHap) / Convert.ToDouble(Program.nThread_PD)));


            nBucket = Program.nLM_Bucket;

            Global_Holder = new ConcurrentBag<(int a, int b, byte rID, Range rg)>[nBucket];
            Sorted_Holder = new List<(int a, int b, byte rID, Range rg)>[nBucket];
            for (int b = 0; b < nBucket; b++)
            {
                Global_Holder[b] = new ConcurrentBag<(int a, int b, byte rID, Range rg)>();
                Sorted_Holder[b] = new List<(int a, int b, byte rID, Range rg)>();
            }

            Console.WriteLine(DateTime.Now + " HP-PBWT Initialized.");

        }


        void coreP_Arr(int RDP_ID, int wndIndex)
        {
            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_PD;

            int siteIndex = Program.rdp.Get_SelectedSites_ByThread(RDP_ID)[wndIndex];

            BitArray oneSite = Program.panel.panel_BitArr[siteIndex];

            #region step 1 local sum
            Parallel.For(0, Program.nThread_PD, op, (i) =>
            {

                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }
                int e = i * blockSize + blockSize;
                if (e > Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                if (oneSite[oldPDA.pArr[s]] == false)
                {
                    psHolder[s] = 0;
                }
                else
                {
                    psHolder[s] = 1;
                }

                for (int k = s + 1; k < e; k++)
                {
                    //psHolder[k] = psHolder[k - 1] + site[oldPDA.pArr[k]];
                    if (oneSite[oldPDA.pArr[k]] == false)
                    {
                        psHolder[k] = psHolder[k - 1];

                    }
                    else
                    {
                        psHolder[k] = psHolder[k - 1] + 1;
                    }
                }

                offsetsHolder[i] = psHolder[e - 1];

            });
            #endregion

            #region step 2 seq offset handling
            for (int i = 1; i < Program.nThread_PD; i++)
            {
                offsetsHolder[i] = offsetsHolder[i - 1] + offsetsHolder[i];
            }

            #endregion

            #region step 3 apply offsets
            Parallel.For(1, Program.nThread_PD, op, (i) =>
            {
                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }
                int e = i * blockSize + blockSize;
                if (e > Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                for (int k = s; k < e; k++)
                {
                    psHolder[k] = psHolder[k] + offsetsHolder[i - 1];
                }
            });
            #endregion

            #region step 4 pps -> index settle to new p arr 
            newPDA.zeroCnt = Program.panel.nHap - psHolder[Program.panel.nHap - 1];
            int oneOff = newPDA.zeroCnt - 1;
            Parallel.For(0, Program.panel.nHap, op, (i) =>
            {
                if (oneSite[oldPDA.pArr[i]] == false)
                {
                    newPDA.pArr[i - psHolder[i]] = oldPDA.pArr[i];
                }
                else
                {
                    newPDA.pArr[psHolder[i] + oneOff] = oldPDA.pArr[i];
                }
            });
            #endregion

        }

        void RunMinRangeDZ(int RDP_ID, int wndIndex)
        {
            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_PD;

            Parallel.For(0, Program.nThread_PD, op, (i) =>
            {
                minDZ[i] = short.MaxValue;
                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }

                int e = i * blockSize + blockSize;

                if (e >= Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                #region first block
                if (s == 0)
                {
                    if (psHolder[e - 1] != 0)
                    {
                        minDZ[i] = blockHasOneSignal;
                    }

                }
                #endregion

                #region other blocks
                else
                {

                    if (psHolder[e - 1] != psHolder[s - 1])
                    {
                        minDZ[i] = blockHasOneSignal;

                    }
                    else
                    {

                        for (int j = s; j < e; j++)
                        {
                            minDZ[i] = Math.Min(minDZ[i], oldPDA.mLens[oldPDA.pArr[j]]);

                        }

                    }


                }
                #endregion

                //}
            });
        }

        void coreD_Arr(int RDP_ID, int wndIndex)
        {
            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_PD;

            int siteIndex = Program.rdp.Get_SelectedSites_ByThread(RDP_ID)[wndIndex];


            RunMinRangeDZ(RDP_ID, wndIndex);

            BitArray oneSite = Program.panel.panel_BitArr[siteIndex];

            //for (int i = 0; i < nThread; i++)
            Parallel.For(0, Program.nThread_PD, op, (i) =>
            {
                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }

                int e = i * blockSize + blockSize;

                if (e >= Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                short prvLowM_Zero = -1;
                short prvLowM_One = -1;
                int hID = oldPDA.pArr[s];



                #region first block
                if (s == 0)
                {

                    prvLowM_Zero = -1;
                    prvLowM_One = -1;
                    hID = oldPDA.pArr[s];

                    for (int k = s; k < e; k++)
                    {

                        hID = oldPDA.pArr[k];

                        //incoming is 0

                        if (oneSite[hID] == false)
                        {
                            newPDA.mLens[hID] = Math.Min(oldPDA.mLens[hID], prvLowM_One);
                            newPDA.mLens[hID]++;
                            prvLowM_One = short.MaxValue;
                            prvLowM_Zero = Math.Min(prvLowM_Zero, oldPDA.mLens[hID]);


                        }
                        else//incoming is 1
                        {
                            newPDA.mLens[hID] = Math.Min(oldPDA.mLens[hID], prvLowM_Zero);
                            newPDA.mLens[hID]++;
                            prvLowM_Zero = short.MaxValue;
                            prvLowM_One = Math.Min(prvLowM_One, oldPDA.mLens[hID]);


                        }
                    }


                }
                #endregion

                #region other blocks
                else
                {


                    //look up to set prvLowM_One and prvLowM_Zero
                    prvLowM_Zero = -1;
                    prvLowM_One = -1;

                    bool minZeroSearch = true;
                    bool minOneSearch = true;

                    //special cases:
                    //A there is no 0 in upper blocks
                    //B there is no 1 in upper blocks
                    if (psHolder[s - 1] == s)//A there is no 0 in upper blocks
                    {
                        prvLowM_Zero = -1;
                        minZeroSearch = false;
                    }

                    if (psHolder[s - 1] == 0) //B there is no 1 in upper blocks
                    {
                        prvLowM_One = -1;
                        minOneSearch = false;
                    }

                    //minZero and minOne should only happen once

                    if (oneSite[oldPDA.pArr[s - 1]] == false)
                    {
                        //prvLowM_One = int.MaxValue;
                        minOneSearch = false;
                    }
                    else
                    {
                        //prvLowM_Zero = int.MaxValue;
                        minZeroSearch = false;
                    }

                    // search

                    if (minZeroSearch)
                    {

                        int seekIndex = s - 1;
                        // Benny, do work here!

                        //skip method to compute prvLowM_Zero

                        int blk_ID = i - 1;
                        //int bs = blk_ID * blockSize;

                        //int be = blk_ID * blockSize + blockSize;

                        while (blk_ID > 0)
                        {
                            if ((psHolder[blk_ID * blockSize - 1] != psHolder[(blk_ID + 1) * blockSize - 1]))
                            {
                                break;
                            }

                            blk_ID--;
                        }
                        seekIndex = (blk_ID + 1) * blockSize - 1;
                        prvLowM_Zero = oldPDA.mLens[oldPDA.pArr[seekIndex]];

                        while (seekIndex >= 0 && oneSite[oldPDA.pArr[seekIndex]] == false)
                        {
                            prvLowM_Zero = Math.Min(oldPDA.mLens[oldPDA.pArr[seekIndex]], prvLowM_Zero);
                            seekIndex--;
                        }


                        if (seekIndex == -1)
                        {
                            prvLowM_Zero = -1;
                        }


                        for (int b = blk_ID + 1; b < i; b++)
                        {

                            prvLowM_Zero = Math.Min(minDZ[b], prvLowM_Zero);
                        }




                    }


                    if (minOneSearch)
                    {
                        int seekIndex = s - 1;
                        //locate first upper one

                        if (seekIndex >= 0)
                        {
                            prvLowM_One = oldPDA.mLens[oldPDA.pArr[seekIndex]];
                            while (seekIndex >= 0 && oneSite[oldPDA.pArr[seekIndex]] != false)
                            {
                                prvLowM_One = Math.Min(oldPDA.mLens[oldPDA.pArr[seekIndex]], prvLowM_One);
                                seekIndex--;
                            }

                            if (seekIndex == -1)
                            {
                                prvLowM_One = -1;
                            }
                        }
                    }

                    if (oneSite[oldPDA.pArr[s - 1]] == false)
                    {
                        prvLowM_One = short.MaxValue;
                    }
                    else
                    {
                        prvLowM_Zero = short.MaxValue;
                    }


                    for (int k = s; k < e; k++)
                    {

                        hID = oldPDA.pArr[k];

                        //incoming is 0
                        if (oneSite[hID] == false)
                        {
                            newPDA.mLens[hID] = Math.Min(oldPDA.mLens[hID], prvLowM_One);
                            newPDA.mLens[hID]++;
                            prvLowM_One = short.MaxValue;
                            prvLowM_Zero = Math.Min(prvLowM_Zero, oldPDA.mLens[hID]);


                        }
                        else//incoming is 1
                        {
                            newPDA.mLens[hID] = Math.Min(oldPDA.mLens[hID], prvLowM_Zero);
                            newPDA.mLens[hID]++;
                            prvLowM_Zero = short.MaxValue;
                            prvLowM_One = Math.Min(prvLowM_One, oldPDA.mLens[hID]);
                        }
                    }
                }
                #endregion

                //}
            });


        }

        void InitialSort(int RDP_ID)
        {

            int[] selectedSites = Program.rdp.Get_SelectedSites_ByThread(RDP_ID);

            BitArray oneSite = Program.panel.panel_BitArr[selectedSites[0]];

            #region step 1 local sum
            Parallel.For(0, Program.nThread_PD, (i) =>
            {
                //range by blocks
                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }

                int e = i * blockSize + blockSize;
                if (e > Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }


                if (oneSite[s] == false)
                {
                    psHolder[s] = 0;
                }
                else
                {
                    psHolder[s] = 1;
                }



                for (int k = s + 1; k < e; k++)
                {
                    if (oneSite[k] == false)
                    {
                        psHolder[k] = psHolder[k - 1];
                    }
                    else
                    {
                        psHolder[k] = psHolder[k - 1] + 1;
                    }
                }

                offsetsHolder[i] = psHolder[e - 1];
            });
            #endregion

            #region step 2 seq offset handling
            for (int i = 1; i < Program.nThread_PD; i++)
            {
                offsetsHolder[i] = offsetsHolder[i - 1] + offsetsHolder[i];
            }

            #endregion

            #region step 3 apply offsets
            Parallel.For(1, Program.nThread_PD, (i) =>
            {
                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }
                int e = i * blockSize + blockSize;
                if (e > Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                for (int k = s; k < e; k++)
                {
                    psHolder[k] = psHolder[k] + offsetsHolder[i - 1];
                }
            });
            #endregion

            #region step 4 pps -> index settle to new p arr 
            oldPDA.zeroCnt = Program.panel.nHap - psHolder[Program.panel.nHap - 1];
            int oneOff = oldPDA.zeroCnt - 1;

            Parallel.For(0, Program.panel.nHap, (i) =>
            //for (int i = 0; i < Program.panel.nHap; i++)
            {
                if (oneSite[i] == false)
                {
                    oldPDA.pArr[i - psHolder[i]] = i;
                    oldPDA.mLens[i - psHolder[i]] = 1;
                }
                else
                {
                    oldPDA.pArr[psHolder[i] + oneOff] = i;
                    oldPDA.mLens[psHolder[i] + oneOff] = 1;

                }
            });
            //D Arr adjust

            oldPDA.mLens[oldPDA.pArr.First()] = 0;
            if (oldPDA.zeroCnt != Program.panel.nHap)
            {
                oldPDA.mLens[oldPDA.pArr[oneOff + 1]] = 0;
            }
            #endregion



        }

        void Run_PDL(byte RDP_ID)
        {
            int[] selectedSites = Program.rdp.Get_SelectedSites_ByThread(RDP_ID);

            InitialSort(RDP_ID);

            for (int w = 1; w < Program.rdp.LM_StartWndID; w++)
            {
                coreP_Arr(RDP_ID, w);
                coreD_Arr(RDP_ID, w);

                temPDA = oldPDA;
                oldPDA = newPDA;
                newPDA = temPDA;

            }


            for (int w = Program.rdp.LM_StartWndID; w < selectedSites.Length - 1; w++)
            {
                //Console.WriteLine(DateTime.Now + ": " + w);
                coreP_Arr(RDP_ID, w);
                coreD_Arr(RDP_ID, w);

                temPDA = oldPDA;
                oldPDA = newPDA;
                newPDA = temPDA;

                CoreL(RDP_ID, w);
            }


            coreP_Arr(RDP_ID, selectedSites.Length - 1);
            coreD_Arr(RDP_ID, selectedSites.Length - 1);

            temPDA = oldPDA;
            oldPDA = newPDA;
            newPDA = temPDA;

            CoreL_Tail(RDP_ID, selectedSites.Length - 1);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool AcceptsHaplotype(int A_key)
        {
            return A_key % Program.nPartition == Program.currentPartition;
        }



        public void ResetHolders()
        {
            //Console.WriteLine(DateTime.Now + " Rest Holders...");
            Parallel.For(0, nBucket, b =>
            {
                Global_Holder[b].Clear();
                Sorted_Holder[b].Clear();
            });
        }


        public void Run()
        {
            ParallelOptions pop = new ParallelOptions();
            pop.MaxDegreeOfParallelism = Program.nInstanceHP_PBWT;
            for (Program.currentPartition = Program.firstPartition; Program.currentPartition <= Program.lastPartition; Program.currentPartition++)
            {
                Console.WriteLine(DateTime.Now + " P:" + Program.currentPartition + "/" + Program.nPartition + " ...");

                for (byte rID = 0; rID < Program.nRDP; rID++)
                {//gather from LM by each RDP

                    Run_PDL(rID);
                }
                SortHolder();

                Run_VO();

                if (Program.currentPartition != Program.lastPartition)
                {
                    ResetHolders();
                }
                Utl.TryGC_Collect();

            }

            Program.BW.DoneAdding();

        }

        void CoreL(byte RDP_ID, int Wnd_ID)
        {


            #region step 1 ROI scan


            List<int> ROI_Indexes = new List<int>();
            int D_THD = 0;
            while (Program.gMap.GetGenDistWND_EndLen(Wnd_ID, D_THD) < Program.LLM_GLen)
            {
                D_THD++;
            }

            List<List<int>> indexArr = new List<List<int>>();
            for (int i = 0; i < Program.nThread_LM; i++)
            {
                indexArr.Add(new List<int>());
            }
            //Console.WriteLine(RDP_ID+"Checkpoint A");
            #region step 1a break points

            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_LM;

            int blockSize = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Program.panel.nHap) / Program.nThread_LM));

            Parallel.For(0, Program.nThread_LM, op, (i) =>
            //for (int i = 0;i < Program.nThread_LM;i++) 
            {

                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }
                int e = i * blockSize + blockSize;
                if (e > Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                int k = s;
                while (k < e)
                {
                    if (oldPDA.mLens[oldPDA.pArr[k]] >= D_THD)
                    {//high range
                        indexArr[i].Add(k);

                        while (k < e && oldPDA.mLens[oldPDA.pArr[k]] >= D_THD)
                        {
                            k++;
                        }
                        indexArr[i].Add(k - 1);

                    }
                    else
                    {
                        k++;
                    }

                }
            }
            );
            #endregion
            //Console.WriteLine(RDP_ID + "Checkpoint B");
            #region step 1b merge
            List<int> mergedRanges = new List<int>();
            mergedRanges.AddRange(indexArr.First());
            for (int i = 1; i < Program.nThread_LM; i++)
            {
                if (indexArr[i].Count == 0)
                { continue; }
                if (mergedRanges.Count != 0 && mergedRanges[^1] == indexArr[i].First() - 1)
                {//try connect
                    mergedRanges[^1] = indexArr[i][1];

                    for (int j = 2; j < indexArr[i].Count; j++)
                    {
                        mergedRanges.Add(indexArr[i][j]);
                    }
                }
                else
                {
                    mergedRanges.AddRange(indexArr[i]);
                }
            }





            List<(int S, int E)> ROIs = new List<(int, int)>();
            for (int i = 0; i < mergedRanges.Count; i += 2)
            {
                ROIs.Add((mergedRanges[i], mergedRanges[i + 1]));
            }

            #endregion
            //Console.WriteLine(RDP_ID + "Checkpoint C");
            #endregion

            int siteIndex = Program.rdp.Get_SelectedSites_ByThread(RDP_ID)[Wnd_ID + 1];

            BitArray oneSite = Program.panel.panel_BitArr[siteIndex];

            short wID_short = (short)Wnd_ID;


            #region LM & Collet

            Parallel.ForEach(ROIs, op, (oneROI) =>
            {
                short minVal = short.MaxValue;
                int A_Key = -1, B_Key = -1;

                for (int tgtLoop = oneROI.S; tgtLoop <= oneROI.E; tgtLoop++)
                {
                    //up groups
                    minVal = Math.Min(minVal, oldPDA.mLens[oldPDA.pArr[tgtLoop]]);
                    for (int srcLoop = oneROI.S - 1; srcLoop < tgtLoop; srcLoop++)
                    {

                        if (AcceptsHaplotype(oldPDA.pArr[srcLoop]) == false && AcceptsHaplotype(oldPDA.pArr[tgtLoop]) == false)
                        { continue; }

                        //report
                        if (oneSite[oldPDA.pArr[srcLoop]] != oneSite[oldPDA.pArr[tgtLoop]])
                        {

                            if (oldPDA.pArr[srcLoop] < oldPDA.pArr[tgtLoop])
                            {
                                A_Key = oldPDA.pArr[srcLoop];
                                B_Key = oldPDA.pArr[tgtLoop];
                            }
                            else
                            {
                                A_Key = oldPDA.pArr[tgtLoop];
                                B_Key = oldPDA.pArr[srcLoop];
                            }

                            Global_Holder[A_Key % nBucket].Add(new(A_Key, B_Key, RDP_ID, new Range(wID_short, minVal, true)));

                        }
                    }

                }

            });
            #endregion



        }

        void CoreL_Tail(byte RDP_ID, int Wnd_ID)
        {

            #region step 1 ROI scan


            List<int> ROI_Indexes = new List<int>();
            int D_THD = 0;
            while (Program.gMap.GetGenDistWND_EndLen(Wnd_ID, D_THD) < Program.LLM_GLen)
            {
                D_THD++;
            }

            List<List<int>> indexArr = new List<List<int>>();
            for (int i = 0; i < Program.nThread_LM; i++)
            {
                indexArr.Add(new List<int>());
            }
            //Console.WriteLine(RDP_ID+"Checkpoint A");
            #region step 1a break points

            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_LM;

            int blockSize = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Program.panel.nHap) / Program.nThread_LM));

            Parallel.For(0, Program.nThread_LM, op, (i) =>
            //for (int i = 0;i < Program.nThread_LM;i++) 
            {

                int s = i * blockSize;
                if (s >= Program.panel.nHap)
                { return; }
                int e = i * blockSize + blockSize;
                if (e > Program.panel.nHap)
                {
                    e = Program.panel.nHap;
                }

                int k = s;
                while (k < e)
                {
                    if (oldPDA.mLens[oldPDA.pArr[k]] >= D_THD)
                    {//high range
                        indexArr[i].Add(k);

                        while (k < e && oldPDA.mLens[oldPDA.pArr[k]] >= D_THD)
                        {
                            k++;
                        }
                        indexArr[i].Add(k - 1);

                    }
                    else
                    {
                        k++;
                    }

                }
            }
            );
            #endregion
            //Console.WriteLine(RDP_ID + "Checkpoint B");
            #region step 1b merge
            List<int> mergedRanges = new List<int>();
            mergedRanges.AddRange(indexArr.First());
            for (int i = 1; i < Program.nThread_LM; i++)
            {
                if (indexArr[i].Count == 0)
                { continue; }
                if (mergedRanges.Count != 0 && mergedRanges[^1] == indexArr[i].First() - 1)
                {//try connect
                    mergedRanges[^1] = indexArr[i][1];

                    for (int j = 2; j < indexArr[i].Count; j++)
                    {
                        mergedRanges.Add(indexArr[i][j]);
                    }
                }
                else
                {
                    mergedRanges.AddRange(indexArr[i]);
                }
            }





            List<(int S, int E)> ROIs = new List<(int, int)>();
            for (int i = 0; i < mergedRanges.Count; i += 2)
            {
                ROIs.Add((mergedRanges[i], mergedRanges[i + 1]));
            }

            #endregion
            //Console.WriteLine(RDP_ID + "Checkpoint C");
            #endregion

            short wID_short = (short)Wnd_ID;

            #region LM & Collet

            Parallel.ForEach(ROIs, op, (oneROI) =>
            //for (int ROI_ID = 0; ROI_ID < ROIs.Count; ROI_ID++)
            {


                short minVal = short.MaxValue;
                int A_Key = -1, B_Key = -1;

                for (int tgtLoop = oneROI.S; tgtLoop <= oneROI.E; tgtLoop++)
                {
                    //up groups
                    minVal = Math.Min(minVal, oldPDA.mLens[oldPDA.pArr[tgtLoop]]);
                    for (int srcLoop = oneROI.S - 1; srcLoop < tgtLoop; srcLoop++)
                    {
                        //report
                        if (AcceptsHaplotype(oldPDA.pArr[srcLoop]) == false && AcceptsHaplotype(oldPDA.pArr[tgtLoop]) == false)
                        { continue; }

                        if (oldPDA.pArr[srcLoop] < oldPDA.pArr[tgtLoop])
                        {
                            A_Key = oldPDA.pArr[srcLoop];
                            B_Key = oldPDA.pArr[tgtLoop];
                        }
                        else
                        {
                            A_Key = oldPDA.pArr[tgtLoop];
                            B_Key = oldPDA.pArr[srcLoop];
                        }

                        Global_Holder[A_Key % nBucket].Add(new(A_Key, B_Key, RDP_ID, new Range(wID_short, minVal, true)));

                    }


                }

            }
            );
            #endregion

        }

        void SortHolder()
        {
            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_Sort;
            //Console.WriteLine(DateTime.Now + " Sorting Holder...");
            Parallel.For(0, nBucket, op, (b) =>
            {
                Sorted_Holder[b] = Global_Holder[b].ToList();
                Sorted_Holder[b].Sort((x, y) =>
                {
                    int cmp = x.a.CompareTo(y.a);
                    if (cmp != 0) return cmp;

                    cmp = x.b.CompareTo(y.b);
                    if (cmp != 0) return cmp;

                    cmp = x.rID.CompareTo(y.rID);
                    if (cmp != 0) return cmp;

                    return x.rg.Start.CompareTo(y.rg.Start);
                });
            });
            //Console.WriteLine(DateTime.Now + " Sorting Complete.");
        }


        void Run_VO()
        {
            //Console.WriteLine(DateTime.Now + " O+V...");
            ParallelOptions op = new ParallelOptions();
            op.MaxDegreeOfParallelism = Program.nThread_VO;
            Parallel.ForEach(Sorted_Holder, op, (one) =>
            {
                OneGroup_Verify_Overlap(one);
            });

            //Console.WriteLine(DateTime.Now + " O+V Done.");
        }

        bool RangeExist(List<Range>[] local, int RDP_ID, Range range)
        {
            if (range == null)
            {
                return false;
            }

            int cnt = 0;
            for (int r = 0; r < Program.nRDP; r++)
            {
                if (RDP_ID == r)
                { continue; }

                foreach (Range oneRange in local[r])
                {
                    if (oneRange == null)
                    { continue; }
                    if (range.End < oneRange.Start)
                    { break; }

                    if (range.OverlapWith(oneRange))
                    {
                        cnt++;
                        if (cnt >= Program.nOverlapTHD)
                        {
                            return true;
                        }
                        break;
                    }
                }
            }

            return false;
        }

        void OneGroup_Verify_Overlap(List<(int a, int b, byte rID, Range rg)> ranges)
        {
            if (ranges.Count < 2)
            {
                return;
            }


            int writerTarget;
            StringBuilder sb = new StringBuilder(128);
            string startTag, endTag;
            double dist;
            List<Range>[] local = new List<Range>[Program.nRDP];
            List<Range>[] verified = new List<Range>[Program.nRDP];
            for (int i = 0; i < Program.nRDP; i++)
            {
                local[i] = new List<Range>();
                verified[i] = new List<Range>();
            }

            List<Range> result = new List<Range>();
            int[] ptr = new int[Program.nRDP];
            int minIndex = -1;
            Range next, tem;
            bool isMerged;


            int rangeIndex;
            for (rangeIndex = 0; rangeIndex < ranges.Count - 1; rangeIndex++)
            {


                if (ranges[rangeIndex].a == ranges[rangeIndex + 1].a
                    && ranges[rangeIndex].b == ranges[rangeIndex + 1].b)
                {
                    local[ranges[rangeIndex].rID].Add(ranges[rangeIndex].rg);
                    continue;
                }

                #region Verify


                for (int r = 0; r < Program.nRDP; r++)
                {
                    verified[r] = new List<Range>();

                    foreach (Range one in local[r])
                    {
                        if (RangeExist(local, r, one))
                        {
                            verified[r].Add(one);
                        }
                    }
                }
                #endregion

                #region Overlap merge
                result.Clear();

                ptr = new int[Program.nRDP];

                minIndex = -1;

                while (true)
                {
                    minIndex = -1;
                    next = null;


                    // Find the next range with the smallest start
                    for (int i = 0; i < Program.nRDP; i++)
                    {
                        if (ptr[i] < verified[i].Count)
                        {
                            if (minIndex == -1 || next == null || verified[i][ptr[i]].Start < next.Start)
                            {
                                minIndex = i;
                                next = verified[i][ptr[i]];
                            }
                        }
                    }

                    if (minIndex == -1)
                        break; // All pointers exhausted

                    // Begin a new merge from the selected minimum-start range
                    tem = new Range(next.Start, next.End);
                    ptr[minIndex]++;


                    do
                    {
                        isMerged = false;
                        for (int i = 0; i < Program.nRDP; i++)
                        {
                            while (ptr[i] < verified[i].Count && tem.OverlapWith(verified[i][ptr[i]]))
                            {
                                tem.Merge(verified[i][ptr[i]]);
                                ptr[i]++;
                                isMerged = true;
                            }
                        }
                    } while (isMerged);

                    result.Add(tem);

                }
                #endregion

                #region Write results
                writerTarget = ranges[rangeIndex].b % Program.nWriter;

                foreach (Range oneRange in result)
                {

                    endTag = Program.rdp.wndTags[oneRange.End];
                    if (oneRange.Start == 0)
                    {
                        startTag = Program.panel.firstPhyTag;
                    }
                    else
                    {
                        startTag = Program.rdp.wndTags[oneRange.Start - 1];
                    }

                    dist = Program.gMap.GetGenDistWND_StartEnd(oneRange.Start, oneRange.End);


                    #region sb
                    sb.Append(Program.panel.indvTags[ranges[rangeIndex].a / 2]);
                    sb.Append('\t');
                    sb.Append((ranges[rangeIndex].a % 2) == 1 ? '1' : '0');
                    sb.Append("\t");
                    sb.Append(Program.panel.indvTags[ranges[rangeIndex].b / 2]);
                    sb.Append("\t");
                    sb.Append((ranges[rangeIndex].b % 2) == 1 ? '1' : '0');
                    sb.Append("\t");
                    sb.Append(startTag);
                    sb.Append("\t");
                    sb.Append(endTag);
                    sb.Append("\t");
                    sb.Append(dist);
                    #endregion
                    Program.BW.Add(sb.ToString(), writerTarget);

                    sb.Clear();
                }


                for (int i = 0; i < Program.nRDP; i++)
                {
                    local[i].Clear();
                    verified[i].Clear();
                }

                #endregion
            }

            if (ranges[^2].a == ranges[^1].a
                && ranges[^2].b == ranges[^1].b)
            {
                local[ranges[^1].rID].Add(ranges[^1].rg);
                #region Verify


                for (int r = 0; r < Program.nRDP; r++)
                {
                    verified[r] = new List<Range>();

                    foreach (Range one in local[r])
                    {
                        if (RangeExist(local, r, one))
                        {
                            verified[r].Add(one);
                        }
                    }
                }
                #endregion

                #region Overlap merge
                result.Clear();

                ptr = new int[Program.nRDP];

                minIndex = -1;



                while (true)
                {
                    minIndex = -1;
                    next = null;


                    // Find the next range with the smallest start
                    for (int i = 0; i < Program.nRDP; i++)
                    {
                        if (ptr[i] < verified[i].Count)
                        {
                            if (minIndex == -1 || next == null || verified[i][ptr[i]].Start < next.Start)
                            {
                                minIndex = i;
                                next = verified[i][ptr[i]];
                            }
                        }
                    }

                    if (minIndex == -1)
                        break; // All pointers exhausted

                    // Begin a new merge from the selected minimum-start range
                    tem = new Range(next.Start, next.End);
                    ptr[minIndex]++;


                    do
                    {
                        isMerged = false;
                        for (int i = 0; i < Program.nRDP; i++)
                        {
                            while (ptr[i] < verified[i].Count && tem.OverlapWith(verified[i][ptr[i]]))
                            {
                                tem.Merge(verified[i][ptr[i]]);
                                ptr[i]++;
                                isMerged = true;
                            }
                        }
                    } while (isMerged);

                    result.Add(tem);

                }
                #endregion

                #region Write results
                writerTarget = ranges[rangeIndex].b % Program.nWriter;

                foreach (Range oneRange in result)
                {

                    endTag = Program.rdp.wndTags[oneRange.End];
                    if (oneRange.Start == 0)
                    {
                        startTag = Program.panel.firstPhyTag;
                    }
                    else
                    {
                        startTag = Program.rdp.wndTags[oneRange.Start - 1];
                    }

                    dist = Program.gMap.GetGenDistWND_StartEnd(oneRange.Start, oneRange.End);


                    #region sb
                    sb.Append(Program.panel.indvTags[ranges[rangeIndex].a / 2]);
                    sb.Append('\t');
                    sb.Append((ranges[rangeIndex].a % 2) == 1 ? '1' : '0');
                    sb.Append("\t");
                    sb.Append(Program.panel.indvTags[ranges[rangeIndex].b / 2]);
                    sb.Append("\t");
                    sb.Append((ranges[rangeIndex].b % 2) == 1 ? '1' : '0');
                    sb.Append("\t");
                    sb.Append(startTag);
                    sb.Append("\t");
                    sb.Append(endTag);
                    sb.Append("\t");
                    sb.Append(dist);
                    #endregion
                    Program.BW.Add(sb.ToString(), writerTarget);

                    sb.Clear();
                }


                for (int i = 0; i < Program.nRDP; i++)
                {
                    local[i].Clear();
                    verified[i].Clear();
                }

                #endregion
            }


        }

        
    }
}
