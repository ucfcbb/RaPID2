


using RaPIDv2_Stable_1._0;
using static RaPIDv2_Stable_1._0.Utl;

internal class Program
{
    public static Utl.BufferWriterV3s BW;
    public static Utl.G_Map gMap;
    public static string VCF_Path;
    public static string GMap_Path;
    public static string Out_Path;
    public static RandomProjection rdp;
    public static Utl.VCF_Mem_v10rtr panel;

    public static HPPBWT pal;


    public static int currentPartition = 0;
    public static int currentRDP = 0;
    public static int nWorkerMatchUpdate = 10;
    public static int rtrBlockSize = 500000000;
    public static int msDA_Wait = 10;
    public static int BW_BufferSize = 500;
    public static int ms_L_Wait = 5;
    public static int minSitePerWnd = 3;
    public static int nSiteFixWnd = 3;
    public static bool fixWnd_RDP = false;
    public static int GC_Running = 0;

    public static byte nRDP = 10;
    public static int nOverlapTHD = 1;// -s 2 logic
    public static double RDP_window_cM = 0.05;

    public static double LLM_GLen = 5;
    public static int nPartition = 2;
    public static int firstPartition = 0;
    public static int lastPartition = 1;

    public static int nThread_PD;
    public static int nThread_LM = 10;
    public static int nThread_VO;
    public static int nLM_Bucket = 100;
    public static int nThread_Sort = 100;
    public static int nThread_BitConv = -1;
    public static int nThread_MAF = -1;

    public static int nWriter = 1;
    public static int nInstanceHP_PBWT = 1;


    static int phyCol = 3;
    static int gRateCol = 2;
    static char delim = ' ';

    public static void Main(string[] args)
    {
        Console.WriteLine("RaPID v2 Stable-1.0");
        #region user inputs

        if (args.Length != 10)
        {
            ShowManual();
            return;
        }

        int nCore = Environment.ProcessorCount;
        DateTime st = DateTime.Now;

        VCF_Path = args[0];
        GMap_Path = args[1];
        LLM_GLen = Convert.ToDouble(args[2]);
        Out_Path = args[3] + ".ibd";
       
        nWriter = Convert.ToInt32(args[4]);

        if (args[5].ToLower().StartsWith('f'))
        {
            fixWnd_RDP = true;
            nSiteFixWnd = Convert.ToInt32(args[6]);
        }
        else
        {
            fixWnd_RDP = false;
            RDP_window_cM = Convert.ToDouble(args[6]);
        }

        firstPartition = Convert.ToInt32(args[7]);
        lastPartition = Convert.ToInt32(args[8]);
        nPartition = Convert.ToInt32(args[9]);


        Console.WriteLine("\nThe system has " + nCore + " logical cores.");

        nThread_MAF = nCore / 6;
        nThread_BitConv = nCore - 2 - nThread_MAF;


        nThread_PD = nCore / nInstanceHP_PBWT;
        nThread_LM = nThread_PD;
        nThread_VO = nCore - nWriter;
        nThread_Sort = nCore - 2;
        nLM_Bucket = nThread_Sort * 3;


        Console.WriteLine("VCF_Path: " + VCF_Path);
        Console.WriteLine("GMap_Path: " + GMap_Path);
        Console.WriteLine("Genetic Length Cutoff: >=" + LLM_GLen);
        Console.WriteLine("Out_Path: " + Out_Path);
        Console.WriteLine("nWriter: " + nWriter);

        if (fixWnd_RDP)
        {
            Console.WriteLine("RDP: F" + nSiteFixWnd);
        }
        else
        {
            Console.WriteLine("RDP: D" + RDP_window_cM.ToString("F4"));
        }

        Console.WriteLine("nThread_MAF: " + nThread_MAF);
        Console.WriteLine("nThread_BitConv: " + nThread_BitConv);
        Console.WriteLine("Partition: [" + firstPartition + "," + lastPartition + "]/" + nPartition);
        Console.WriteLine("nLM_Bucket: " + nLM_Bucket);
        Console.WriteLine("nInstanceHP_PBWT: " + nInstanceHP_PBWT);

        Console.WriteLine();

        #endregion

        panel = new Utl.VCF_Mem_v10rtr(VCF_Path);


        Task t1 = Task.Run(() => panel.Read());

        panel.Set_L1_rPTR();

        Task t2 = Task.Run(() => panel.LineSplitter());

        Task t3 = Task.Run(() => panel.BitConverter());

        Task t4 = Task.Run(() => panel.MafCalculator());

        Task.WaitAll(t1, t2, t3, t4);

        Program.gMap = new G_Map(Program.GMap_Path, phyCol, gRateCol, delim);


        Program.gMap.Preload_Phy(panel.phyTags);
        Program.rdp = new RandomProjection();
        Program.gMap.Preload_Wnd();

        

        if (lastPartition - firstPartition + 1 != nPartition)
        {
            Program.BW = new BufferWriterV3s(Program.Out_Path + ".part_"
                + Program.firstPartition + "_" + Program.lastPartition + "_of_" + Program.nPartition);
        }
        else
        {
            Program.BW = new BufferWriterV3s(Program.Out_Path);
        }
            
        Task t5 = Task.Run(() => Program.BW.Run(false));

        pal = new HPPBWT();
        Task t6 = Task.Run(() => pal.Run());
        

        Task.WaitAll(t5,t6);

        DateTime et = DateTime.Now;
        TimeSpan span = et - st;
        Console.WriteLine(DateTime.Now + " Done. " + span.TotalSeconds + " sec.");
    }


    public static void ShowManual()
    {
        Console.WriteLine("Required Inputs:");
        Console.WriteLine("  [string]  VCF Path");
        Console.WriteLine("  [string]  PLINK Genetic Map Path");
        Console.WriteLine("  [decimal] IBD Length Threshold (in cM)");
        Console.WriteLine("  [string]  IBD Output Path");
        Console.WriteLine("  [int]     Number of Writers");
        Console.WriteLine("  [char]    Random Projection Method: 'F' = Fixed window, 'D' = Dynamic window");
        Console.WriteLine("  [int or decimal]    Windows Size: Use int Fixed window, Use decimal (in cM) for Dynamic window");
        Console.WriteLine("  [int]     First Partition (inclusive)");
        Console.WriteLine("  [int]     Last Partition (inclusive)");
        Console.WriteLine("  [int]     Total Number of Partitions");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("RaPID_v2 my.vcf my.gmap 3.0 output.ibd 4 F 3 0 7 8");

    }
}
