# RaPID2

**RaPID2** is a high-performance IBD detection tool based on the **Positional Burrows-Wheeler Transform (PBWT)**. It is designed to efficiently detect shared genomic segments in large-scale datasets and supports both high-throughput computing and stable deployment environments. This work builds on the original [RaPID](https://github.com/ZhiGroup/RaPID)[1] and uses the [HP-PBWT engine](https://github.com/ucfcbb/HP-PBWT)[2] at its core . RaPID2 retains the core algorithmic strengths of its predecessor while significantly improving performance, flexibility, and scalability.

RaPID2 is part of an academic research project. While it has been extensively tested and delivers strong performance in practice, users should be aware that the software may contain bugs or incomplete features. Feedback and contributions are welcome.


## Modes of Operation

- **HPC Mode**  
  Optimized for high-performance computing clusters. This mode uses advanced memory-aware parallelization strategies and is ideal for large datasets and benchmarking scenarios.
  
 ⚠️ **Note:** HPC mode assumes access to a large machine with substantial memory (on the order of terabytes). On smaller machines, it may not perform well or could run out of memory and crash.
- **Stable Mode**  
  A lightweight, resource-efficient version suitable for general-purpose analysis or production environments with limited computational resources.

## Partitioning Strategy

- **Partition-Based Execution**  
  Both HPC and Stable modes use a partitioning approach to manage memory and workload. Increasing the number of partitions will reduce peak memory usage but may increase total runtime due to overhead.

- **Distributed Execution**  
  RaPID2 can be run in a distributed fashion by assigning different partition ranges to different machines. For example, one node can run partitions 0–3 while another runs 4–7.

- **Fail-Safe and Recovery**  
  If a specific partition fails (e.g., partition 0 of 10), you can recover by subdividing it further. For example, re-run with 20 total partitions and execute just partition 0 and 1 — these correspond to the original partition 0, now split in two.

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


For additional details, refer to the official guide:
https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?tabs=dotnet9&pivots=os-linux-ubuntu-2410



### Build
Create a working directory (e.g., RaPID2).

Download all files from the desired source folder (HPC/ or Stable/) into this directory.

Open a terminal in the directory and run:
```bash
  dotnet build --configuration Release
```


### Run
After building, the compiled executable will be located at:
```bash
./bin/Release/net8.0/RaPIDv2_HPC-1.0
```
**Note:** The folder name (e.g., `net8.0`) and the executable file name may vary depending on the installed .NET version and whether you are using the HPC or Stable mode of RaPID2.

### Command-Line Parameters

RaPID v2 requires the following positional arguments:

| **Parameter** | **Type**       | **Description** |
|---------------|----------------|-----------------|
| `VCF Path`             | `string`        | Path to the input VCF file containing phased genotypes. |
| `Genetic Map Path`     | `string`        | Path to the PLINK-formatted genetic map file. |
| `IBD Length Threshold` | `decimal`       | Minimum IBD segment length to report (in centiMorgans, cM). |
| `IBD Output Path`      | `string`        | Path to save the output `.ibd` file. |
| `Number of Writers`    | `int`           | Number of parallel file writers to use during output. |
| `Projection Method`    | `char`          | Method for random projection: `'F'` = fixed window, `'D'` = dynamic window. |
| `Window Size`          | `int or decimal`| Window size for the projection method. Use an integer for fixed windows; use a decimal (cM) for dynamic windows. |
| `First Partition`      | `int`           | Index of the first partition to process (inclusive). |
| `Last Partition`       | `int`           | Index of the last partition to process (inclusive). |
| `Total Partitions`     | `int`           | Total number of partitions into which the input is divided. |

### Example

```bash
RaPID_v2_HPC-1.0 my.vcf my.gmap 3.0 output.ibd 4 F 3 0 7 8
```
This runs RaPID v2 using:

a 3.0 cM threshold,

4 output writers,

fixed-window projection (F) with window size 3,

partition range 0 to 7 (inclusive),

across a total of 8 partitions.


## Citation

If you use RaPID2 in your research, we would appreciate a citation.  
The associated paper is currently in preparation and the link will be provided here once available.


## References

[1] RaPID: Naseri, Ardalan, et al. "RaPID: ultra-fast, powerful, and accurate detection of segments identical by descent (IBD) in biobank-scale cohorts." Genome biology 20 (2019): 1-15. 

[2] HP-PBWT: Tang, Kecong, et al. "Haplotype-based Parallel PBWT for Biobank Scale Data." bioRxiv (2025): 2025-02.




