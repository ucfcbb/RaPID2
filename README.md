# RaPID2

**RaPID2** is a high-performance IBD detection tool based on the **Positional Burrows-Wheeler Transform (PBWT)**. It is designed to efficiently detect shared genomic segments in large-scale datasets and supports both high-throughput computing and stable deployment environments.

This work builds on the original [RaPID v1](https://github.com/ZhiGroup/RaPID) [1] and uses the [HP-PBWT engine](https://github.com/ucfcbb/HP-PBWT) at its core [2]. RaPID2 retains the core algorithmic strengths of its predecessor while significantly improving performance, flexibility, and scalability.


## Modes of Operation

- **HPC Mode**  
  Optimized for high-performance computing clusters. This mode uses advanced memory-aware parallelization strategies and is ideal for large datasets and benchmarking scenarios.
  
 ⚠️ **Note:** HPC mode assumes access to a large machine with substantial memory (on the order of terabytes). On smaller machines, it may not perform well or could run out of memory and crash.
- **Stable Mode**  
  A lightweight, resource-efficient version suitable for general-purpose analysis or production environments with limited computational resources.

## About This Project

RaPID2 is part of an academic research project. While it has been extensively tested and delivers strong performance in practice, users should be aware that the software may contain bugs or incomplete features. Feedback and contributions are welcome.

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



## Build
Create a working directory (e.g., RaPID2).

Download all files from the desired source folder (HPC/ or Stable/) into this directory.

Open a terminal in the directory and run:
```bash
  dotnet build --configuration Release
```


## Run
After building, the compiled executable will be located at:
```bash
./bin/Release/net8.0/RaPIDv2_HPC-1.0
```
**Note:** The folder name (e.g., `net8.0`) and the executable file name may vary depending on the installed .NET version and whether you are using the HPC or Stable mode of RaPID2.




## References

[1] RaPID: Naseri, Ardalan, et al. "RaPID: ultra-fast, powerful, and accurate detection of segments identical by descent (IBD) in biobank-scale cohorts." Genome biology 20 (2019): 1-15. 

[2] HP-PBWT: Tang, Kecong, et al. "Haplotype-based Parallel PBWT for Biobank Scale Data." bioRxiv (2025): 2025-02.


