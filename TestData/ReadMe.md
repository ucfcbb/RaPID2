# TestData

This folder contains a small **toy dataset** intended solely for testing whether RaPID v2 is installed and running correctly.  
It is not meant to represent any biologically meaningful data or to be used for actual IBD detection.

### Contents

- `dummy.vcf.gz`  
  A dummy VCF file (compressed) containing synthetic genotype data for basic testing.

- `plink.chr20.GRCh38.map`  
  A PLINK-format genetic map file for chromosome 20 (GRCh38), used here only to satisfy input requirements.

---

### Usage

1. **Unzip the VCF file**:
   You can use the following command or any other method of unzipping `.gz` files:
   ```bash
   gunzip dummy.vcf.gz
   ```
2. **Run the Stable version**:
```bash
  RaPID_v2_Stable-1.0 dummy.vcf plink.chr20.GRCh38.map 3.0 test_output.ibd 2 F 3 0 0 1
```
3. **Run the HPC version**:
```bash
  RaPID_v2_HPC-1.0 dummy.vcf plink.chr20.GRCh38.map 3.0 test_output.ibd 2 F 3 0 0 1
```
These commands will run a minimal partition (0 of 1 in total) using 2 writers and a fixed window size of 3.
