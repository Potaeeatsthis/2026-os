# Multithreading Pipeline

## 1. Purpose

This program uses manually managed threads to calculate data faster. The user can choose from 1 to 32 threads. The program divides the work into separate ranges, so every thread processes a different part of the array.

The program does not use `Parallel.For`. It creates, starts, and joins each `Thread` manually.

## 2. Complete Pipeline

```text
Start the program
        |
        v
Load values from data.bin
        |
        v
Ask the user for 1-32 threads
        |
        v
Validate the input
        |
        v
Create Thread[] and localResults[]
        |
        v
Divide 10,000,000 indexes between the threads
        |
        v
Start every thread
        |
        v
Each thread calculates its own range for 30 rounds
        |
        v
Each thread saves one local result
        |
        v
The main thread calls Join() and waits for all workers
        |
        v
Combine all local results
        |
        v
Display the calculation time and final result
```

## 3. Load the Data

`LoadData()` reads 11,000,001 values from `data.bin`. Every value in the file is a 32-bit `float`.

```csharp
Single f = br.ReadSingle();
data[i] = (decimal)(f * 36);
```

One value follows this pipeline:

```text
Read 4 bytes
    -> convert them to float
    -> multiply the value by 36
    -> convert it to decimal
    -> save it in data[i]
```

The array contains 11,000,001 values, but the calculation uses the first 10,000,000 values.

## 4. Read and Validate the Thread Count

The program repeatedly asks the user for a number from 1 to 32:

```csharp
while (true)
{
    Console.Write("Enter number of threads (1-32): ");

    if (int.TryParse(Console.ReadLine(), out threadCount) &&
        threadCount >= 1 && threadCount <= MaxThreads)
    {
        break;
    }

    Console.WriteLine("Invalid input. Please enter a number from 1 to 32.");
}
```

Examples:

```text
Input: abc -> rejected because it is not an integer
Input: 0   -> rejected because it is less than 1
Input: 33  -> rejected because it is greater than 32
Input: 4   -> accepted
```

## 5. Prepare the Threads

The program creates two arrays after it receives valid input:

```csharp
Thread[] threads = new Thread[threadCount];
localResults = new decimal[threadCount];
```

If the user selects four threads, both arrays have four positions:

```text
threads[0]       localResults[0]
threads[1]       localResults[1]
threads[2]       localResults[2]
threads[3]       localResults[3]
```

Every thread stores its answer in a different position of `localResults`.

## 6. Divide the Work

Each thread receives an ID from `0` to `threadCount - 1`. It uses this ID to calculate its range:

```csharp
long startIndex = CalculationCount * id / threadCount;
long endIndex = CalculationCount * (id + 1) / threadCount;
```

The range includes `startIndex`, but it does not include `endIndex`.

### Example: Two Threads

| Thread | Start index | End index | Values processed |
|---:|---:|---:|---:|
| 0 | 0 | 5,000,000 | 5,000,000 |
| 1 | 5,000,000 | 10,000,000 | 5,000,000 |

Thread 0 processes indexes `0` to `4,999,999`. Thread 1 processes indexes `5,000,000` to `9,999,999`.

### Example: Three Threads

Ten million cannot be divided equally by three. The formula still divides all the work correctly:

| Thread | Start index | End index | Values processed |
|---:|---:|---:|---:|
| 0 | 0 | 3,333,333 | 3,333,333 |
| 1 | 3,333,333 | 6,666,666 | 3,333,333 |
| 2 | 6,666,666 | 10,000,000 | 3,333,334 |

There are no missing indexes and no overlapping ranges.

### Example: Thirty-Two Threads

```text
10,000,000 / 32 = 312,500 values per thread
```

Every thread receives exactly 312,500 values in this case.

## 7. Start the Threads Manually

The main thread creates and starts every worker:

```csharp
for (int i = 0; i < threadCount; i++)
{
    threads[i] = new Thread(ThreadWork);
    threads[i].Start(i);
}
```

`Start(i)` sends the thread ID to `ThreadWork()`.

For four threads, the calls are similar to:

```text
threads[0].Start(0)
threads[1].Start(1)
threads[2].Start(2)
threads[3].Start(3)
```

The operating system can then run these workers on different CPU cores.

## 8. Worker Process

Every worker executes `ThreadWork()`:

```csharp
private static void ThreadWork(object? threadNumber)
{
    CalClass CF = new CalClass();
    int id = (int)threadNumber!;
    long startIndex = CalculationCount * id / threadCount;
    long endIndex = CalculationCount * (id + 1) / threadCount;
    decimal localResult = 0;
    int i = 0;

    while (i < 30)
    {
        long localIndex = startIndex;

        while (localIndex < endIndex)
        {
            localResult += CF.Calculate1(ref data, ref localIndex);
        }

        i++;
    }

    localResults[id] = localResult;
}
```

Each worker has its own:

- `CalClass` object
- `localIndex`
- `localResult`
- range of array indexes

The worker returns to `startIndex` at the beginning of every round. Therefore, it processes its complete range 30 times.

## 9. The Calculation Algorithm

`Calculate1()` processes one array value at a time.

First, it converts the value to an integer and checks the following conditions in order:

| Condition | Calculation |
|---|---:|
| Divisible by 2 | value x 0.2 |
| Otherwise, divisible by 3 | value x 0.3 |
| Otherwise, divisible by 5 | value x 0.5 |
| Otherwise, divisible by 7 | value x 0.7 |
| No condition matches | value x 0.1 |

Only the first matching condition is used.

Next, the algorithm checks whether the calculated sum is even or odd:

```text
Even -> Round(sum x 0.5)
Odd  -> Round(-sum x 0.3)
```

It then changes the original array value:

```csharp
value[i] *= 0.1m;
```

Finally, it increases the index and returns the calculated result.

### Calculation Example

Suppose the current value is `30`:

1. `30` is divisible by 2.
2. The first calculation is `30 x 0.2 = 6`.
3. `6` is even.
4. The returned result is `Round(6 x 0.5) = 3`.
5. The array value changes from `30` to `3`.
6. The index moves to the next value.

In the next round, the same position starts with `3`, not `30`, because the algorithm changes the array.

## 10. Why Local Results Are Important

This operation is unsafe when many threads share `result`:

```csharp
result += CF.Calculate1(ref data, ref index);
```

For example, two threads could read the same old result:

```text
Old result = 100

Thread 0 reads 100 and calculates 100 + 20
Thread 1 reads 100 and calculates 100 + 30

Thread 0 writes 120
Thread 1 writes 130
```

The correct result should be 150, but one update is lost.

The improved program gives each thread a private `localResult`. After its work is complete, the thread writes once to its own position:

```csharp
localResults[id] = localResult;
```

The threads also process separate array ranges. Therefore, two threads do not change the same `data` element.

## 11. Wait for All Threads

The main thread uses `Join()`:

```csharp
for (int i = 0; i < threadCount; i++)
{
    threads[i].Join();
}
```

`Join()` means that the main thread waits until the selected worker has finished. The final result must not be calculated before all workers are complete.

```text
Worker 0 finishes --+
Worker 1 finishes --+
Worker 2 finishes --+--> Main continues
Worker 3 finishes --+
```

## 12. Combine the Results

After every `Join()` is complete, the main thread combines the local results:

```csharp
result = 0;

for (int i = 0; i < threadCount; i++)
{
    result += localResults[i];
}
```

Example:

```text
localResults[0] = 1,000
localResults[1] = 2,000
localResults[2] = 3,000

Final result = 1,000 + 2,000 + 3,000
Final result = 6,000
```

## 13. Stopwatch Process

The stopwatch starts before the worker threads are created and started. It stops after every worker has finished.

The measured time includes:

- Creating the worker threads
- Starting the threads
- Performing the calculations
- Waiting with `Join()`

It does not include:

- Loading `data.bin`
- Waiting for user input
- Combining the local results

## 14. Performance Test Example

More threads do not always produce a faster result. The best number depends on the number of CPU cores and the cost of thread scheduling.

Run the program in Release mode:

```bash
dotnet run -c Release
```

Test each thread count at least three times and compare the middle result.

Example results:

| Threads | Time | Speedup |
|---:|---:|---:|
| 1 | 40 seconds | 1.00x |
| 2 | 22 seconds | 1.82x |
| 4 | 12 seconds | 3.33x |
| 8 | 10 seconds | 4.00x |
| 16 | 11 seconds | 3.64x |
| 32 | 14 seconds | 2.86x |

These are example values, not real measurements from every computer.

Use this formula:

```text
Speedup = time with one thread / time with multiple threads
```

For example:

```text
Speedup with four threads = 40 / 12 = 3.33x
```

In this example, eight threads are the fastest. Sixteen and thirty-two threads are slower because the computer spends more time managing threads.

## 15. Final Summary

The improved pipeline is faster and thread-safe because:

1. The user can select from 1 to 32 threads.
2. The program creates and manages every thread manually.
3. Every thread receives a separate data range.
4. Every thread uses a private index and local result.
5. The main thread waits for all workers with `Join()`.
6. Results are combined only after all workers finish.
7. No `lock` is needed inside the main calculation loop.

The best thread count should be found by testing. Using more threads than the CPU can handle may make the program slower.
