# RaPID2

Contact Author: Kecong Tang (Kecong.Tang@ucf.edu or Benjamin.KT.vln@gmail.com)

**RaPID2** is a high-performance IBD detection tool based on the **Positional Burrows-Wheeler Transform (PBWT)**. It is designed to efficiently detect shared genomic segments in large-scale datasets and supports both high-throughput computing and stable deployment environments. This work builds on the original [RaPID](https://github.com/ZhiGroup/RaPID)[1] and uses the [HP-PBWT engine](https://github.com/ucfcbb/HP-PBWT)[2] at its core. RaPID2 retains the core algorithmic strengths of its predecessor while significantly improving performance, flexibility, and scalability.

The implementation is fully standalone and written in C#, with no external dependencies required. This makes it easy to compile, run, and deploy across environments without relying on third-party libraries or frameworks.

RaPID2 is part of an academic research project. While it has been extensively tested and delivers strong performance in practice, users should be aware that the software may contain bugs or incomplete features. Feedback and contributions are welcome.


## Modes of Operation

- **HPC Mode**  
  Optimized for high-performance computing clusters. This mode uses advanced memory-aware parallelization strategies and is ideal for large datasets and benchmarking scenarios.
  
 ⚠️ **Note:** HPC mode assumes access to a large machine with substantial memory (on the order of terabytes). On smaller machines, it may not perform well or could run out of memory and crash.
- **Stable Mode**  
  A lightweight, resource-efficient version suitable for general-purpose analysis or production environments with limited computational resources.

## Partitioning Strategy

  Both HPC and Stable modes use a partitioning approach to manage memory and workload. Increasing the number of partitions will reduce peak memory usage but may increase total runtime due to overhead.

- **Distributed Execution**  
  RaPID2 can be run in a distributed fashion by assigning different partition ranges to different machines. For example, one node can run partitions 0–3 while another runs 4–7.

- **Fail-Safe and Recovery**  
  If a specific partition fails (e.g., partition 0 of 10), you can recover by subdividing it further.
  
  RaPID2 uses modular partitioning based on the formula:

  ```csharp
  KeyA % totalPartition == currentPartition
  ```
  If a specific partition fails (e.g., partition p out of N), you can re-run it using a finer partitioning with a new total M, where M is a multiple of N (e.g., 2N, 4N, etc).
  To recover partition p of N using M partitions:
  ```bash
  Partitions to run =
  { p + k × N } for k = 0 to (M / N) – 1
  ```
  Examples
  
  Recovering partition 0 of 10 using 20 partitions:
  
    Step = 20 / 10 = 2
    
    Run: 0 and 10
  
  Recovering partition 1 of 10 using 20 partitions:
  
    Run: 1 and 11
  
  Recovering partition 3 of 8 using 32 partitions:
  
    Step = 32 / 8 = 4
  
    Run: 3, 11, 19, 27

## Compilation and Execution (C#)

### Install .NET SDK

You will need the .NET 8.0 SDK to build and run RaPID2.

- **Windows**:  
  Follow the installation instructions at the official .NET site:  
  https://learn.microsoft.com/en-us/dotnet/core/install/windows

- **Linux (Ubuntu example)**:  
  ```bash
  sudo apt-get update
  sudo apt-get install -y dotnet-sdk-8.0
  ```
For additional details, refer to the official guide:
https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?tabs=dotnet9&pivots=os-linux-ubuntu-2410



### Build
Create a working directory (e.g., RaPID2).

Download all files from the desired source folder (HPC/ or Stable/) into this directory.

Open a terminal in the directory and run:
```bash
  dotnet build --configuration Release
```


#### Building on Windows with Visual Studio

If you are using a Windows machine with Visual Studio installed, you can build RaPID2 for most operating system (Windows, Linux, or macOS) using the .NET SDK's cross-platform capabilities. Visual Studio supports multi-target builds and publishing for different platforms.

For instructions on publishing to other operating systems, refer to the official Microsoft documentation:  
https://learn.microsoft.com/en-us/dotnet/core/deploying/



### Run
After building, the compiled executable will be located at:
```bash
./bin/Release/net8.0/RaPIDv2_HPC-1.0
```
**Note:** The folder name (e.g., `net8.0`) and the executable file name may vary depending on the installed .NET version and whether you are using the HPC or Stable mode of RaPID2.

### Command-Line Parameters

RaPID2 requires the following positional arguments:

| **Parameter** | **Type**       | **Description** |
|---------------|----------------|-----------------|
| VCF Path             | `string`        | Path to the input VCF file containing phased genotypes. |
| Genetic Map Path     | `string`        | Path to the PLINK-formatted genetic map file. |
| IBD Length Threshold | `decimal`       | Minimum IBD segment length to report (in centiMorgans, cM). |
| IBD Output Path      | `string`        | Path to save the output `.ibd` file. |
| Number of Writers    | `int`           | Number of parallel file writers to use during output. |
| Projection Method    | `char`          | Method for random projection: `'F'` = fixed window, `'D'` = dynamic window. |
| Window Size          | `int` or `decimal`| Window size for the projection method. Use an integer for fixed windows; use a decimal (cM) for dynamic windows. |
| First Partition      | `int`           | Index of the first partition to process (inclusive). |
| Last Partition       | `int`           | Index of the last partition to process (inclusive). |
| Total Partitions     | `int`           | Total number of partitions into which the input is divided. |

### Examples and Toy Case

A minimal **toy dataset** is included in the [`TestData/`](./TestData) directory.  
It is intended solely for verifying that RaPID v2 is installed and functioning properly.  
You can use it to run a quick test without needing a real dataset.

Example (Stable Mode):

```bash
RaPID_v2_Stable-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 0 7 8
```
This runs RaPID v2 using:

a 3.0 cM threshold,

4 output writers,

fixed-window projection (F) with window size 3,

partition range 0 to 7 (inclusive),

across a total of 8 partitions.


## Resource Management and Execution Strategy

### Memory

Memory requirements for RaPID v2 vary significantly depending on:
- The IBD segment density of your dataset (which depends on the data panel).
- The memory management behavior of your operating system and hardware.

Because of this variability, we strongly recommend a **safe test run** before processing the full dataset.

### Suggested Test Procedure

1. Start with a higher number of partitions (e.g., 100).
2. Run **only the first partition** (`partition 0 of 100`) as a test:

```bash
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 0 0 100
```
3. Monitor your machine's memory usage during this run.

4. Based on the observed memory consumption:

   If memory usage is low: You can reduce the number of partitions for faster overall performance.

   If memory usage is high or your machine struggles: Increase the number of partitions for safer, more memory-efficient execution.

### Batch Run vs. Continuous Run

If your system does not release memory efficiently, or your environment has strict memory constraints, we recommend using a batch run approach:

Run one partition at a time, restarting RaPID2 for each partition. Example for 5 partitions:

Run partitions individually:
```bash
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 0 0 5
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 1 1 5
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 2 2 5
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 3 3 5
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 4 4 5
```
This ensures clean memory state between partitions.

If your machine releases memory efficiently or has sufficient memory, you can run multiple partitions in a single execution by setting a range in the partition parameters:
Example to process partitions 0 through 4 (5 partitions total) in one go:

The last three parameters control the partition:

First Partition Index

Last Partition Index

Total Number of Partitions


### CPU Utilization and Parallel Instances

Different datasets and machine architectures can lead to varying CPU utilization patterns when running RaPID v2. In some cases, especially on HPC environments, you may observe that RaPID2 uses only a fraction of your total CPU resources (e.g., 30–40%). This behavior can depend on data panel characteristics, memory bottlenecks, or OS-level scheduling.

> ⚠️ **Minimum Recommended Cores:**  
> RaPID2 is designed for parallel processing environments. We recommend a **minimum of 10 CPU cores** for effective performance. Systems with fewer cores may experience inefficient CPU usage and longer runtimes.

To maximize throughput on larger systems, we recommend running **multiple RaPID2 instances in parallel**, each processing different partitions.

### Strategy for Multi-Instance Parallel Execution

- If RaPID2 is only using ~30% of your available CPU, consider running **3 parallel instances**, each working on different partition ranges.
- This approach ensures full utilization of your compute resources and minimizes total processing time.

You can choose:
- **Multiple Continuous Runs**  
  (e.g., 3 instances, each processing 5 partitions at once).
- **Multiple Batch Runs**  
  (e.g., 3 independent batch pipelines, each handling separate partitions one-by-one).

Each instance should be assigned a **different partition range** to avoid overlapping computation.

---

#### Example 1: 3 Continuous Instances

```bash
# Instance 1: partitions 0–4
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 0 4 15

# Instance 2: partitions 5–9
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 5 9 15

# Instance 3: partitions 10–14
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 10 14 15
```

#### Example 2: 3 Parallel Batch Runs
```bash
# Instance 1 (Batch Run for partitions 0–4):
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 0 0 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 1 1 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 2 2 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 3 3 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 4 4 15

# Instance 2 (Batch Run for partitions 5–9):
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 5 5 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 6 6 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 7 7 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 8 8 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 9 9 15

# Instance 3 (Batch Run for partitions 10–14):
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 10 10 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 11 11 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 12 12 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 13 13 15
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 1 F 3 14 14 15
```

Choose continuous or batch mode based on your system’s memory behavior and operational preferences. Both methods can be parallelized across multiple RaPID2 instances.

## Citation

If you use RaPID2 in your research, we would appreciate a citation.  
The associated paper is currently in preparation and the link will be provided here once available.


## References

[1] RaPID: Naseri, Ardalan, et al. "RaPID: ultra-fast, powerful, and accurate detection of segments identical by descent (IBD) in biobank-scale cohorts." Genome biology 20 (2019): 1-15. 

[2] HP-PBWT: Tang, Kecong, et al. "Haplotype-based Parallel PBWT for Biobank Scale Data." bioRxiv (2025): 2025-02.




