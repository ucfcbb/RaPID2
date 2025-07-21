using System;
using System.Collections.Concurrent;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaPIDv2_Stable_1._0
{
    public class Utl
    {
        public static void TryGC_Collect()
        {
            if (Interlocked.CompareExchange(ref Program.GC_Running, 1, 0) == 0)
            {
                Task.Run(() =>
                {
                    try
                    {
                        //Console.WriteLine(DateTime.Now + " [Async GC] Starting...");
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        //Console.WriteLine(DateTime.Now + " [Async GC] Completed.");
                    }
                    finally
                    {
                        Interlocked.Exchange(ref Program.GC_Running, 0);
                    }
                });
            }
            else
            {
                //Console.WriteLine(DateTime.Now + " [Async GC] Skipped: already running.");
            }
        }

        public class MAF
        {
            public MAF(int siteID)
            {
                double oneCnt = 0;
                foreach (bool one in Program.panel.panel_BitArr[siteID])
                {
                    if (one)
                    {
                        oneCnt++;
                    }
                }

                Program.panel.MAFs[siteID] = oneCnt / Program.panel.nHap;
                if (Program.panel.MAFs[siteID] > 0.5)
                {
                    Program.panel.MAFs[siteID] = 1 - Program.panel.MAFs[siteID];
                }

            }
        }

        public static bool charToBool(char c)
        {
            if (c == '0')
            {
                return false;
            }

            return true;
        }

        public class BufferWriterV3s
        {
            //public ConcurrentQueue<string> buffer = new ConcurrentQueue<string>();

            ConcurrentQueue<string>[] blkQueue = new ConcurrentQueue<string>[Program.nWriter];

            string outPath;
            bool AddComplete = false;
            int msWait = 10;

            public void Add(string s, int instanceID)
            {
                blkQueue[instanceID].Enqueue(s);
            }

            public bool GoodToAdd(int instanceID)
            {
                if (blkQueue[instanceID].Count > Program.BW_BufferSize)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }



            public void DoneAdding()
            {
                AddComplete = true;
            }

            public BufferWriterV3s(string outPath)
            {
                this.outPath = outPath;
                for (int i = 0; i < Program.nWriter; i++)
                {
                    blkQueue[i] = new ConcurrentQueue<string>();
                }
            }


            public void Run(bool append)
            {


                List<Task> tasks = new List<Task>();

                for (int i = 0; i < Program.nWriter; i++)
                {
                    int localID = i;
                    Task t = Task.Run(() => RunOneInstance(localID, append));
                    tasks.Add(t);

                }
                Console.WriteLine(DateTime.Now + " Buffer Writer On");
                Task.WaitAll(tasks.ToArray());
                Console.WriteLine(DateTime.Now + " Buffer Writer Off");

            }

            void RunOneInstance(int instanceID, bool append)
            {
                //try
                //{
                //Console.WriteLine(DateTime.Now + " Buffer Writer " + instanceID);
                StreamWriter sw = new StreamWriter(outPath + "." + instanceID + ".ibd", append);
                sw.NewLine = "\n";

                string line;
                bool dqRes;
                while (AddComplete == false || blkQueue[instanceID].Count > 0)
                {
                    if (blkQueue[instanceID].Count == 0)
                    {
                        //Console.WriteLine("Writer" + instanceID + " wait " + AddComplete + " ");
                        Thread.Sleep(msWait);
                        continue;
                    }

                    dqRes = blkQueue[instanceID].TryDequeue(out line);

                    if (dqRes == false)
                    {
                        continue;
                    }


                    sw.WriteLine(line);


                }

                sw.Close();

                Console.WriteLine(DateTime.Now + " Buffer Writer " + instanceID + " Complete.");
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine("Error In Writer");
                //    StringBuilder sb = new StringBuilder();
                //    while (ex != null)
                //    {
                //        sb.AppendLine($"Exception: {ex.GetType().Name}");
                //        sb.AppendLine($"Message  : {ex.Message}");
                //        sb.AppendLine($"StackTrace:\n{ex.StackTrace}");
                //        ex = ex.InnerException;
                //        if (ex != null) sb.AppendLine("Inner Exception:");
                //    }
                //    Console.WriteLine(sb.ToString());
                //    System.Diagnostics.Process.GetCurrentProcess().Kill();
                //}

            }


        }

        public class VCF_Mem_v10rtr
        {
            //todos:
            //reader complete logic
            //async with PBWT

            //call by:
            //step 1 initialize the reader
            //step 2 prob top
            //step 3 <L1> start block reader
            //step 4 <L2> run line splitter
            //step 5 <L3> bit array converter
            public double nHapDB;
            public int nHap, nIndv, nSite;
            public int nSite_Estimated;


            public class Block_Info
            {
                public List<int> lineIndexes = new List<int>();
                public int BlockID = -1;
                public Block_Info(List<int> charIndexes, int ID)
                {
                    lineIndexes = charIndexes.ToArray().ToList();
                    BlockID = ID;
                }

            }

            string VCF_Path;


            ConcurrentQueue<Block_Info> block_Infos = new ConcurrentQueue<Block_Info>();
            ConcurrentQueue<int> MAF_Queue = new ConcurrentQueue<int>();


            public int ReadLine_BlkIndex1;
            public int ReadLine_BlkIndex2;
            public int Blk1_rPTR;
            public int Blk2_rPTR;

            int L1_blockSize = Program.rtrBlockSize;
            int L1_nBlock = 20;
            //int L2_nBlock = 1000;
            int[] L1_nRead;
            int[] L1_Stat;//-1 ready to add, 0 ready to parse, -2 parser working on
            int[] L1_Seq;
            int L1_rPTR = 0;
            int L1_Add_Index = 0;//currently working on
            int L1_Taker_Index = 0;//currently working on
            public List<char[]> BufferArr = new List<char[]>();

            int msAddWait = 200;
            int msTakeWait = 10;
            bool L1_ReadComplete = false;
            bool L2_ReadComplete = false;
            bool L3_Complete = false;

            int dataLineLen;


            //each BitArray is a site
            public List<BitArray> panel_BitArr = new List<BitArray>();
            public string[] indvTags;
            public List<string> phyTags = new List<string>();
            public string firstPhyTag = "";
            public double[] MAFs;
            public double[] gMap;
            public void Set_L1_rPTR()
            {
                while (L1_Stat[0] != 0)
                {
                    Thread.Sleep(msTakeWait);
                    continue;
                }

                //skip top headers
                while (BufferArr[0][L1_rPTR] == '#' && BufferArr[0][L1_rPTR + 1] == '#')
                {
                    while (BufferArr[0][L1_rPTR] != '\n')
                    {
                        L1_rPTR++;
                    }
                    L1_rPTR++;
                }

                while (BufferArr[0][L1_rPTR] != '\n')
                {
                    //Console.Write(BufferArr[0][L1_rPTR]);
                    L1_rPTR++;
                }
                //Console.Write(BufferArr[0][L1_rPTR]);
                L1_rPTR++;
            }

            //sequential execution, run before async reading
            //set nIndv, nHap, dataLineLen
            //nSite_Estimated
            void ProbTop()
            {
                double dataByteCnt = 0;
                double topByteCnt = 0;
                double fileSize = 0;
                fileSize = Convert.ToUInt64(new FileInfo(VCF_Path).Length);

                StreamReader sr = new StreamReader(VCF_Path);

                string line;
                string[] parts;


                ulong lineCnt = 0;
                //skip top headers
                while ((line = sr.ReadLine()) != null && line.StartsWith("##"))
                {
                    topByteCnt += Convert.ToUInt64(System.Text.ASCIIEncoding.ASCII.GetByteCount(line));
                    lineCnt++;
                }
                parts = line.Split('\t');

                nIndv = parts.Length - 9;
                nHap = nIndv * 2;
                nHapDB = nHap;
                dataLineLen = nIndv * 4;

                indvTags = new string[nIndv];

                Parallel.For(9, parts.Length, (i) =>
                {
                    indvTags[i - 9] = parts[i];
                });



                //Console.WriteLine("nIndv. " + nIndv);
                topByteCnt += Convert.ToUInt64(System.Text.ASCIIEncoding.ASCII.GetByteCount(line));
                lineCnt++;
                Console.WriteLine("Data line start at " + lineCnt);


                lineCnt = 0;

                while (lineCnt < 100 && (line = sr.ReadLine()) != null)
                {
                    dataByteCnt += Convert.ToUInt64(System.Text.ASCIIEncoding.ASCII.GetByteCount(line));
                    lineCnt++;
                }


                double lineSize = dataByteCnt / lineCnt;
                nSite_Estimated = Convert.ToInt32(fileSize / lineSize * 1.01);
                Console.WriteLine("nSite_Estimated " + nSite_Estimated);

                MAFs = new double[nSite_Estimated];
                gMap = new double[nSite_Estimated];

                sr.Close();
            }

            public VCF_Mem_v10rtr(string path)
            {
                VCF_Path = path;
                ProbTop();

                Console.Write(DateTime.Now + " Allocating Space, " + nHap + " x " + nSite_Estimated + "...");


                for (int i = 0; i < nSite_Estimated; i++)
                {
                    panel_BitArr.Add(new BitArray(nHap));
                }
                Console.WriteLine("Done.");

                Console.Write(DateTime.Now + " Initializing RTR, " + L1_blockSize + " by " + L1_nBlock + "...");

                L1_nRead = new int[L1_nBlock];
                L1_Stat = new int[L1_nBlock];
                L1_Seq = new int[L1_nBlock];
                for (int i = 0; i < L1_nBlock; i++)
                {
                    L1_Stat[i] = -1;
                    L1_nRead[i] = -1;
                    L1_Seq[i] = -1;

                    BufferArr.Add(new char[L1_blockSize]);
                }
                Console.WriteLine("Done.");
            }



            /// <summary>
            /// current version for holding binary panel in memory
            /// </summary>
            public void LineSplitter()
            {
                //bool headerCross = false;

                int tabCnt = 0;
                int nextBlock, previousBlock;
                int i;

                //the first item is used for left over from the pervious

                List<int> lineIndexes = new List<int>();

                bool dataCross = false;


                StringBuilder sb = new StringBuilder();


                while (L1_ReadComplete == false || L1_Stat[L1_Taker_Index] == 0)
                {//loop through blocks

                    //DBG.takerIndex.Add(L1_Taker_Index);

                    if (L1_Stat[L1_Taker_Index] != 0)
                    {
                        Thread.Sleep(msTakeWait);
                        continue;
                    }
                    //flag this block is being used
                    L1_Stat[L1_Taker_Index] = -2;

                    nextBlock = (L1_Taker_Index + 1) % L1_nBlock;
                    previousBlock = (L1_Taker_Index - 1 + L1_nBlock) % L1_nBlock;

                    dataCross = false;

                    for (i = L1_rPTR; i < L1_nRead[L1_Taker_Index]; i++)
                    {//loop in a block


                        //DBG.Output_Buffer(BufferArr, L1_Taker_Index, i);

                        if (BufferArr[L1_Taker_Index][i] == '\t')
                        {
                            tabCnt++;
                            continue;
                        }

                        //collect phy tag
                        if (tabCnt == 1)
                        {
                            sb.Append(BufferArr[L1_Taker_Index][i]);
                        }

                        if (tabCnt == 9)
                        {
                            phyTags.Add(sb.ToString());

                            //if(sb.ToString().Contains("|"))
                            //{
                            //    //DBG.Output_Buffer(BufferArr, L1_Taker_Index, i);
                            //    //DBG.outputLast10(phyTags);

                            //    Console.WriteLine("Temp Log: " + DBG.temLog);
                            //    Console.WriteLine("Current Blk Seq" + DBG.blockSeq[L1_Taker_Index]);
                            //    int a = 0;
                            //}


                            sb.Clear();

                            tabCnt = 0;
                            //i++;

                            //cross block
                            if (i + dataLineLen > L1_nRead[L1_Taker_Index])
                            {

                                //remove after debug
                                int old_Ptr = L1_rPTR;


                                L1_rPTR = (i + dataLineLen) % L1_nRead[L1_Taker_Index];


                                while (L1_Stat[nextBlock] != 0)
                                {
                                    Thread.Sleep(msTakeWait);
                                    continue;
                                }
                                lineIndexes.Add(i);

                                dataCross = true;

                                //DBG.temLog = "Cross block b" + L1_Taker_Index + "->b" + nextBlock + " Seq" + DBG.blockSeq[L1_Taker_Index];
                                //DBG.temLog += "\nL1_rPTR = (i + dataLineLen) % L1_nRead[L1_Taker_Index]"
                                //    + i.ToString() + " " + dataLineLen.ToString() + " " + L1_nRead[L1_Taker_Index];
                                //DBG.temLog += "\nL1_rPTR=" + L1_rPTR;
                                //DBG.temLog += "\n Start Loc:\n";
                                //DBG.temLog += DBG.Store_Buffer(BufferArr, L1_Taker_Index, old_Ptr);

                                break;

                            }
                            else
                            {//within

                                lineIndexes.Add(i);

                                //jump
                                i += dataLineLen;

                                //DBG.temLog = "Jump within" + L1_Taker_Index+" Seq"+DBG.blockSeq[L1_Taker_Index];
                            }

                        }

                    }

                    //move to next block
                    while (L1_Stat[nextBlock] != 0)
                    {
                        if (L1_ReadComplete)
                        {

                            break;
                        }
                        Thread.Sleep(msTakeWait);
                        continue;
                    }


                    block_Infos.Enqueue(new Block_Info(lineIndexes, L1_Taker_Index));


                    L1_Taker_Index = nextBlock;


                    lineIndexes.Clear();
                    if (dataCross)
                    {
                        lineIndexes.Add(-1);
                    }



                }

                L2_ReadComplete = true;
                nSite = phyTags.Count;

                firstPhyTag = new string(phyTags.First().ToArray());

                Console.WriteLine();
                Console.WriteLine(DateTime.Now + " L2 Splitter Complete. " + nSite + " sites");

            }

            public void Read()
            {

                Console.WriteLine(DateTime.Now + " Block Reader Start.");

                StreamReader sr = new StreamReader(VCF_Path);
                int nBlockRead = 0;

                int blkSeq = 0;

                int len = -1;
                while (len != 0)
                {
                    while (L1_Stat[L1_Add_Index] != -1)
                    {
                        Thread.Sleep(msAddWait);
                        continue;
                    }
                    len = sr.ReadBlock(BufferArr[L1_Add_Index]);
                    L1_nRead[L1_Add_Index] = len;
                    L1_Stat[L1_Add_Index] = 0;

                    L1_Seq[L1_Add_Index] = nBlockRead;
                    nBlockRead++;

                    L1_Add_Index++;
                    L1_Add_Index = L1_Add_Index % L1_nBlock;

                    //DBG.blockSeq[L1_Add_Index] = blkSeq;

                    blkSeq++;


                }

                sr.Close();
                L1_ReadComplete = true;
                Console.WriteLine();
                Console.WriteLine(DateTime.Now + " Block Reader Complete.");
            }

            public string hapPair_To_IndvPair(int hapA, int hapB)
            {
                StringBuilder sb = new StringBuilder(64);

                sb.Append(Program.panel.indvTags[hapA / 2]);
                sb.Append('\t');
                sb.Append((hapA % 2).ToString());
                sb.Append("\t");
                sb.Append(Program.panel.indvTags[hapB / 2]);
                sb.Append("\t");
                sb.Append((hapB % 2).ToString());


                return sb.ToString();
            }
            public void BitConverter()
            {
                //in case of data cross block:
                //when done with on current block always only free previous block 
                Block_Info oneBlkInfo;
                ParallelOptions op = new ParallelOptions();
                op.MaxDegreeOfParallelism = Program.nThread_BitConv;
                bool dq;
                int baseSiteIndex = 0;

                int off_Shift;

                int hCntT = 0;
                int hCntT_Local;
                while (L2_ReadComplete == false || block_Infos.Count > 0)
                {
                    while (block_Infos.Count == 0)
                    {
                        Thread.Sleep(msTakeWait);
                        continue;
                    }
                    dq = block_Infos.TryDequeue(out oneBlkInfo);
                    if (dq == false)
                    {
                        continue;
                    }

                    if (oneBlkInfo.lineIndexes.Count == 0)
                    {
                        continue;
                    }


                    off_Shift = 0;
                    if (oneBlkInfo.lineIndexes[0] == -1)
                    {//first block is a cross block (head case)
                        off_Shift = 1;
                    }



                    hCntT_Local = hCntT;


                    Parallel.Invoke(
                        () =>
                        {

                            #region head case
                            if (off_Shift == 1)
                            {//first block is a cross block (head case)

                                int charIndex_Off = 0;
                                int hCntH = 0;
                                if (BufferArr[oneBlkInfo.BlockID][0] == '|' || BufferArr[oneBlkInfo.BlockID][0] == '\t' || BufferArr[oneBlkInfo.BlockID][0] == '\n')
                                {
                                    charIndex_Off = 1;
                                }

                                for (int h = hCntT_Local; h < nHap; h++)
                                {
                                    panel_BitArr[baseSiteIndex - off_Shift][h] = Utl.charToBool(BufferArr[oneBlkInfo.BlockID][hCntH * 2 + charIndex_Off]);
                                    hCntH++;
                                }
                                MAF_Queue.Enqueue(baseSiteIndex - off_Shift);

                                //L1_Stat[ (oneBlkInfo.BlockID-1 +L1_nBlock)%L1_nBlock] = -1;

                            }
                            else
                            {
                                for (int h = 0; h < nHap; h++)
                                {
                                    panel_BitArr[baseSiteIndex][h] = Utl.charToBool(BufferArr[oneBlkInfo.BlockID][oneBlkInfo.lineIndexes[0] + h * 2]);
                                }
                                MAF_Queue.Enqueue(baseSiteIndex);
                            }
                            #endregion
                        },

                        () =>
                        {
                            #region mid case
                            //, Program.pOP_nThread_HP
                            Parallel.For(1, oneBlkInfo.lineIndexes.Count - 1, op, (s) =>
                            //for (int s = 1; s < oneBlkInfo.lineIndexes.Count - 1; s++)
                            {
                                for (int h = 0; h < nHap; h++)
                                {
                                    panel_BitArr[baseSiteIndex + s - off_Shift][h] = Utl.charToBool(BufferArr[oneBlkInfo.BlockID][oneBlkInfo.lineIndexes[s] + h * 2]);
                                }
                                MAF_Queue.Enqueue(baseSiteIndex + s - off_Shift);
                            });
                            #endregion
                        },
                        () =>
                        {
                            #region tail case
                            //bug here, if there is only one and it is -1
                            if (oneBlkInfo.lineIndexes.Count == 1)
                            {
                                return;
                            }
                            int i = oneBlkInfo.lineIndexes.Last();

                            hCntT = 0;
                            while (i < L1_nRead[oneBlkInfo.BlockID])
                            {
                                panel_BitArr[baseSiteIndex + oneBlkInfo.lineIndexes.Count - 1 - off_Shift][hCntT] = Utl.charToBool(BufferArr[oneBlkInfo.BlockID][i]);
                                hCntT++;
                                i = i + 2;
                            }
                            if (hCntT == nHap)
                            {
                                MAF_Queue.Enqueue(baseSiteIndex + oneBlkInfo.lineIndexes.Count - 1 - off_Shift);
                                //if tail not cross

                                //L1_Stat[oneBlkInfo.BlockID] = -1;
                            }

                            #endregion
                        }
                        );
                    //plus base site index
                    baseSiteIndex = baseSiteIndex + oneBlkInfo.lineIndexes.Count - off_Shift;
                    L1_Stat[oneBlkInfo.BlockID] = -1;
                }

                L3_Complete = true;

                BufferArr.Clear();
                Console.WriteLine(DateTime.Now + " L3 Converter Complete. " + baseSiteIndex + " sites.");
            }

            public void MafCalculator()
            {
                ParallelOptions op = new ParallelOptions();
                op.MaxDegreeOfParallelism = Program.nThread_MAF;
                while (L3_Complete == false || MAF_Queue.Count != 0)
                {
                    if (MAF_Queue.Count == 0)
                    {
                        Thread.Sleep(msTakeWait);
                        continue;
                    }
                    //Program.pOP_nThread_HP,
                    Parallel.For(0, MAF_Queue.Count, op, (i) =>
                    {
                        int siteID;
                        bool dq = MAF_Queue.TryDequeue(out siteID);
                        if (dq)
                        {
                            MAF cal = new MAF(siteID);
                        }
                    });
                }
                Console.WriteLine(DateTime.Now + " MAF Complete.");
            }

        }

        public class G_Map
        {
            int indexCol = 3;
            int rateCol = 2;
            char delimiter = ' ';
            Dictionary<int, double> rateDic = new Dictionary<int, double>();
            List<int> sorted_Keys = new List<int>();
            public List<double> preLoaded_Rates = new List<double>();
            public List<double> preLoaded_Wnd_Rates = new List<double>();

            ////End, wlen, gLen
            //public Dictionary<int, List<double>> preloaded_Wnd_RateDictEL = new Dictionary<int, List<double>>();
            ////Start, End, gLen
            //double[][] preloaded_Wnd_RateDictSE;

            /// <summary>
            /// currently only use plink map
            /// format as:
            /// 20 . 0.001066 82603
            /// </summary>
            /// <param name="path"></param>
            public G_Map(string path, int phyCol = 3, int gRateCol = 2, char dataDelimiter = ' ')
            {
                indexCol = phyCol;
                rateCol = gRateCol;
                delimiter = dataDelimiter;

                Console.WriteLine("Loading G-Map: " + path + "...");
                StreamReader sr = new StreamReader(path);
                string line;
                string[] parts;
                int index;
                double rate = 0, prevRate = double.MinValue;
                while ((line = sr.ReadLine()) != null
                    && line.Contains("Position") == true)
                { }

                do
                {
                    parts = line.Split(delimiter);

                    index = Convert.ToInt32(parts[indexCol]);
                    try
                    {
                        rate = Double.Parse(parts[rateCol], System.Globalization.NumberStyles.Any);
                    }
                    catch
                    {
                        rate = 0;
                        Console.WriteLine("Bug Decimal Conversion: " + line);
                    }



                    if (rate <= prevRate)
                    {
                        continue;
                    }
                    prevRate = rate;

                    rateDic.Add(index, rate);


                } while ((line = sr.ReadLine()) != null);
                sr.Close();


                List<int> keys = rateDic.Keys.ToList();
                keys.Sort();

                rate = 0;
                prevRate = double.MinValue;
                Dictionary<int, double> rateDicTem = new Dictionary<int, double>();
                foreach (int one in keys)
                {
                    if (rateDic[one] <= prevRate)
                    {
                        continue;
                    }
                    prevRate = rateDic[one];
                    rateDicTem.Add(one, rateDic[one]);
                }

                rateDic.Clear();

                rateDic = new Dictionary<int, double>(rateDicTem);

                rateDicTem.Clear();

                sorted_Keys = rateDic.Keys.ToList();
                sorted_Keys.Sort();
            }

            double getGenLoc(int x)
            {
                if (rateDic.ContainsKey(x))
                {
                    return rateDic[x];
                }

                int prev = 0, next = 0;

                if (x < sorted_Keys.First())
                {
                    return 0;
                }
                else if (x > sorted_Keys.Last())
                {

                    return rateDic[sorted_Keys.Last()];
                }
                else
                {
                    int index = sorted_Keys.BinarySearch(x);

                    prev = sorted_Keys[~index - 1];
                    next = sorted_Keys[~index];
                }

                double result = rateDic[prev] + (x - prev) * (rateDic[next] - rateDic[prev]) / (next - prev);


                return result;


            }

            double getGenLoc(string phy)
            {
                return getGenLoc(Convert.ToInt32(phy));
            }

            void preLoad_One(int phy)
            {
                preLoaded_Rates.Add(getGenLoc(phy));
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="start">zero based index, not phy</param>
            /// <param name="end">zero based index, not phy</param>
            /// <returns></returns>
            public double GetGenDist_HighRes(int start, int end)
            {
                return preLoaded_Rates[end] - preLoaded_Rates[start];
            }

            /// <summary>
            /// 
            /// </summary>
            /// <param name="end">window index</param>
            /// <param name="len"># windows</param>
            /// <returns></returns>
            public double GetGenDistWND_EndLen(int end, int len)
            {
                if (len > end)
                {
                    return preLoaded_Wnd_Rates[end];
                }
                return preLoaded_Wnd_Rates[end] - preLoaded_Wnd_Rates[end - len];
            }
            public double GetGenDistWND_StartEnd(int start, int end)
            {
                if (start == 0)
                {
                    return preLoaded_Wnd_Rates[end] - preLoaded_Rates[0];
                }
                else
                {
                    return preLoaded_Wnd_Rates[end] - preLoaded_Wnd_Rates[start - 1];
                }
            }
            public void Preload_Phy(List<string> phys)
            {
                foreach (string one in phys)
                {
                    preLoaded_Rates.Add(getGenLoc(one));
                }

                Console.WriteLine(DateTime.Now + " G-Map Loaded.");
            }

            /// <summary>
            /// preload g-distance (rates) by window
            /// this must be called AFTER RDP windows had been created.
            /// </summary>
            public void Preload_Wnd()
            {
                for (int i = 0; i < Program.rdp.windows.Count; i++)
                {
                    preLoaded_Wnd_Rates.Add(preLoaded_Rates[Program.rdp.windows[i]]);
                }
            }



        }
        public class Range
        {

            public short End;
            public short Start;
            public Range(short end, short length, bool EL = true)
            {
                this.Start = (short)(end - length + 1);
                this.End = end;
            }

            public Range(short start, short end)
            {
                this.Start = start;
                this.End = end;
            }

            public bool OverlapWith(Range rB)
            {
                short maxLeft = this.Start > rB.Start ? this.Start : rB.Start;
                short minRight = this.End < rB.End ? this.End : rB.End;
                return maxLeft <= minRight;
            }

            public void Merge(Range rB)
            {
                this.Start = this.Start < rB.Start ? this.Start : rB.Start;
                this.End = this.End > rB.End ? this.End : rB.End;
            }
        }

    }
}
