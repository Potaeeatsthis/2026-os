# New Multithreading Idea: Dynamic Chunk Scheduling

## Idea

The original implementation uses **static range partitioning**: each worker receives one large, fixed range. The new option uses **dynamic chunk scheduling**. It divides the 10,000,000 values into chunks of 50,000 values. Whenever a worker finishes a chunk, it claims the next available one.

```text
                         shared nextChunkStart
                                  |
                  Interlocked.Add claims one chunk
                                  |
              +-------------------+-------------------+
              |                   |                   |
          Worker 1            Worker 2            Worker 3
          chunk 0             chunk 1             chunk 2
              |                   |                   |
          finishes             finishes             finishes
              |                   |                   |
          chunk 3             chunk 4             chunk 5
```

This is useful when some parts of a dataset take longer to process than others. A worker that finishes early does not stay idle; it takes more work from the shared pool.

## Atomic chunk claiming

Each worker claims work with:

```csharp
long startIndex = Interlocked.Add(ref nextChunkStart, ChunkSize) - ChunkSize;
```

`Interlocked.Add` performs the read and update as one atomic operation. Two threads cannot receive the same starting index, so chunks do not overlap and no array element is processed by two workers.

The final chunk is protected by:

```csharp
long endIndex = Math.Min(startIndex + ChunkSize, CalculationCount);
```

This keeps the ending index inside the calculation range even when the total number of values is not exactly divisible by the chunk size.

## Thread safety

The dynamic design avoids a lock around the expensive calculation:

- Each chunk has exactly one owner.
- Each worker accumulates its answer in a private `localResult`.
- Each worker writes to its own position in `localResults`.
- The main thread calls `Join()` before combining results.
- Only claiming the next chunk uses synchronization.

Because `Calculate1()` changes array values, every chunk completes all 30 rounds before another chunk is claimed. This preserves exclusive ownership of each array range for the entire calculation.

## Static versus dynamic scheduling

| Feature | Static ranges | Dynamic chunks |
|---|---|---|
| Work assignment | Once, at startup | Repeatedly while running |
| Synchronization overhead | Very low | One atomic operation per chunk |
| Load balancing | Depends on equal ranges taking equal time | Fast workers automatically take more chunks |
| Best use | Uniform calculations | Uneven or unpredictable calculations |
| Number of work units | One per thread | 200 chunks |

Dynamic scheduling is not always faster. If every value takes exactly the same time, the original static version can win because it performs fewer atomic operations. The program includes both modes so their execution times can be compared on the same computer.

## Run the experiment

```bash
dotnet run -c Release
```

Then:

1. Enter a thread count from 1 to 32.
2. Choose `1` for static ranges or `2` for dynamic chunks.
3. Compare elapsed time and the work summary printed for each worker.
4. Run each setup at least three times because operating-system scheduling can change between runs.

For a fair comparison, restart the program before each test so `data.bin` is loaded into a fresh array.
