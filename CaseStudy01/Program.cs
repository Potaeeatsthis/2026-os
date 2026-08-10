// Case Study 01 - Multithreading
// Demonstrates static range partitioning and dynamic chunk scheduling.
using System.Diagnostics;
using CalculatingFunctions;

class Program
{
    private const int DataLength = 11_000_001;
    private const long CalculationCount = 10_000_000;
    private const int CalculationRounds = 30;
    private const int MaxThreads = 22;
    private const int ChunkSize = 50_000;

    private static decimal[] Data = new decimal[DataLength];
    private static decimal[] localResults = Array.Empty<decimal>();
    private static long[] processedValues = Array.Empty<long>();
    private static int[] processedChunks = Array.Empty<int>();
    private static int threadCount;
    private static long nextChunkStart;

    private enum SchedulingMode
    {
        StaticRanges = 1,
        DynamicChunks = 2
    }

    private static void StaticRangeWork(object? state)
    {
        int id = (int)state!;
        long startIndex = CalculationCount * id / threadCount;
        long endIndex = CalculationCount * (id + 1) / threadCount;

        localResults[id] = CalculateRange(startIndex, endIndex);
        processedValues[id] = endIndex - startIndex;
        processedChunks[id] = 1;
    }

    private static void DynamicChunkWork(object? state)
    {
        int id = (int)state!;
        decimal localResult = 0;
        long localValueCount = 0;
        int localChunkCount = 0;

        while (true)
        {
            // Interlocked makes claiming the next chunk an atomic operation.
            // Every chunk is therefore owned by exactly one worker.
            long startIndex = Interlocked.Add(ref nextChunkStart, ChunkSize) - ChunkSize;
            if (startIndex >= CalculationCount)
            {
                break;
            }

            long endIndex = Math.Min(startIndex + ChunkSize, CalculationCount);
            localResult += CalculateRange(startIndex, endIndex);
            localValueCount += endIndex - startIndex;
            localChunkCount++;
        }

        localResults[id] = localResult;
        processedValues[id] = localValueCount;
        processedChunks[id] = localChunkCount;
    }

    private static decimal CalculateRange(long startIndex, long endIndex)
    {
        CalClass calculator = new CalClass();
        decimal localResult = 0;

        for (int round = 0; round < CalculationRounds; round++)
        {
            long localIndex = startIndex;
            while (localIndex < endIndex)
            {
                localResult += calculator.Calculate1(ref Data, ref localIndex);
            }
        }

        return localResult;
    }

    private static void LoadData()
    {
        Console.WriteLine("Loading data...");

        using FileStream stream = new FileStream("data.bin", FileMode.Open, FileAccess.Read);
        using BinaryReader reader = new BinaryReader(stream);

        for (int i = 0; i < Data.Length; i++)
        {
            float value = reader.ReadSingle();
            Data[i] = (decimal)(value * 36);
        }

        Console.WriteLine("Data loaded successfully.\n");
    }

    private static int ReadNumber(string prompt, int minimum, int maximum)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out int value) &&
                value >= minimum && value <= maximum)
            {
                return value;
            }

            Console.WriteLine($"Invalid input. Please enter a number from {minimum} to {maximum}.");
        }
    }

    private static void Main()
    {
        LoadData();

        threadCount = ReadNumber($"Enter number of threads (1-{MaxThreads}): ", 1, MaxThreads);

        Console.WriteLine("\nScheduling ideas:");
        Console.WriteLine("1. Static ranges  - each thread receives one fixed range");
        Console.WriteLine("2. Dynamic chunks - threads claim new chunks when they become free");
        SchedulingMode mode = (SchedulingMode)ReadNumber("Choose a scheduling idea (1-2): ", 1, 2);

        Thread[] threads = new Thread[threadCount];
        localResults = new decimal[threadCount];
        processedValues = new long[threadCount];
        processedChunks = new int[threadCount];
        nextChunkStart = 0;

        ParameterizedThreadStart worker = mode == SchedulingMode.StaticRanges
            ? StaticRangeWork
            : DynamicChunkWork;

        Console.WriteLine($"\nCalculation starting with {mode}...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(worker)
            {
                Name = $"Worker-{i + 1}"
            };
            threads[i].Start(i);
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        stopwatch.Stop();

        decimal result = 0;
        long totalProcessedValues = 0;
        int totalProcessedChunks = 0;

        for (int i = 0; i < threadCount; i++)
        {
            result += localResults[i];
            totalProcessedValues += processedValues[i];
            totalProcessedChunks += processedChunks[i];
            Console.WriteLine(
                $"Worker {i + 1,2}: {processedValues[i],10:N0} values, {processedChunks[i],3} chunk(s)");
        }

        if (totalProcessedValues != CalculationCount)
        {
            throw new InvalidOperationException(
                $"Expected {CalculationCount:N0} values, but workers processed {totalProcessedValues:N0}.");
        }

        Console.WriteLine($"Total: {totalProcessedValues:N0} values in {totalProcessedChunks:N0} chunk(s)");
        Console.WriteLine($"\nCalculation finished in {stopwatch.ElapsedMilliseconds:N0} ms.");
        Console.WriteLine($"Result: {result:F2}");
    }
}
