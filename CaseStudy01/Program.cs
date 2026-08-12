// Case Study 01 - optimized heterogeneous-core scheduling
// Based on Program.cs.orig. Each value is completed before the next value starts.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CalculatingFunctions;

class Program
{
    private const int DataLength = 11_000_001;
    private const int CalculationCount = 10_000_000;
    private const int CalculationRounds = 30;
    private const decimal ExpectedResult = 4_686_980_924_312m;

    // Small chunks let faster P-cores claim more work while slower E-cores claim less.
    private const int ChunkSize = 16_384;

    // Under Calculate1's documented rules, values below this magnitude return zero.
    // Multiplication by 0.1 only makes later rounds smaller, so they can be skipped.
    private const decimal NearZeroThreshold = 5m;

    // Calculate1 requires ref decimal[] and ref long; changing either breaks exact compatibility.
    private static decimal[] Data = new decimal[DataLength];
    private static decimal[] localResults = Array.Empty<decimal>();
    private static int[] processedValues = Array.Empty<int>();
    private static int[] processedChunks = Array.Empty<int>();
    private static int nextChunkStart;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static decimal CalculateValue(CalClass calculator, int index)
    {
        decimal result = 0m;
        long calculatorIndex = index;

        for (int round = 0; round < CalculationRounds; round++)
        {
            if (Math.Abs(Data[index]) < NearZeroThreshold)
            {
                break;
            }

            // Calculate1 advances its ref index, so reset it to repeat this same value.
            calculatorIndex = index;
            result += calculator.Calculate1(ref Data, ref calculatorIndex);
        }

        return result;
    }

    private static void DynamicWorker(object? state)
    {
        int workerId = (int)state!;
        CalClass calculator = new();
        decimal localResult = 0m;
        int localValueCount = 0;
        int localChunkCount = 0;

        while (true)
        {
            int startIndex = Interlocked.Add(ref nextChunkStart, ChunkSize) - ChunkSize;
            if (startIndex >= CalculationCount)
            {
                break;
            }

            int endIndex = Math.Min(startIndex + ChunkSize, CalculationCount);
            for (int index = startIndex; index < endIndex; index++)
            {
                localResult += CalculateValue(calculator, index);
            }

            localValueCount += endIndex - startIndex;
            localChunkCount++;
        }

        localResults[workerId] = localResult;
        processedValues[workerId] = localValueCount;
        processedChunks[workerId] = localChunkCount;
    }

    private static void LoadData()
    {
        Console.WriteLine("Loading data...");

        using FileStream stream = new("data.bin", FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new(stream);

        for (int index = 0; index < Data.Length; index++)
        {
            Data[index] = (decimal)(reader.ReadSingle() * 36.0f);
        }

        Console.WriteLine("Data loaded successfully.\n");
    }

    private static int ReadWorkerCount(int availableWorkers)
    {
        while (true)
        {
            Console.Write($"Enter worker count (1-{availableWorkers}, Enter = {availableWorkers}): ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return availableWorkers;
            }

            if (int.TryParse(input, out int workerCount) &&
                workerCount >= 1 && workerCount <= availableWorkers)
            {
                return workerCount;
            }

            Console.WriteLine($"Invalid input. Please enter a number from 1 to {availableWorkers}.");
        }
    }

    private static void Main()
    {
        LoadData();

        int availableWorkers = Math.Max(1, Environment.ProcessorCount);
        Console.WriteLine($"Detected {availableWorkers} available logical processor(s).");
        Console.WriteLine(
            "Dynamic chunks let faster P-cores process more chunks and keep E-cores useful.");

        int workerCount = ReadWorkerCount(availableWorkers);
        Thread[] threads = new Thread[workerCount];
        localResults = new decimal[workerCount];
        processedValues = new int[workerCount];
        processedChunks = new int[workerCount];
        nextChunkStart = 0;

        Console.WriteLine($"\nCalculation starting with {workerCount} dynamic worker(s)...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int workerId = 0; workerId < workerCount; workerId++)
        {
            threads[workerId] = new Thread(DynamicWorker)
            {
                Name = $"Worker-{workerId + 1}"
            };
            threads[workerId].Start(workerId);
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        stopwatch.Stop();

        decimal result = 0m;
        int totalProcessedValues = 0;
        int totalProcessedChunks = 0;

        for (int workerId = 0; workerId < workerCount; workerId++)
        {
            result += localResults[workerId];
            totalProcessedValues += processedValues[workerId];
            totalProcessedChunks += processedChunks[workerId];
            Console.WriteLine(
                $"Worker {workerId + 1,2}: {processedValues[workerId],10:N0} values, " +
                $"{processedChunks[workerId],4:N0} chunk(s)");
        }

        if (totalProcessedValues != CalculationCount)
        {
            throw new InvalidOperationException(
                $"Expected {CalculationCount:N0} values, but workers processed " +
                $"{totalProcessedValues:N0}.");
        }

        if (result != ExpectedResult)
        {
            throw new InvalidOperationException(
                $"Expected result {ExpectedResult:F2}, but calculated {result:F2}.");
        }

        Console.WriteLine(
            $"Total: {totalProcessedValues:N0} values in {totalProcessedChunks:N0} chunk(s)");
        Console.WriteLine($"Calculation finished in {stopwatch.ElapsedMilliseconds:N0} ms.");
        Console.WriteLine($"Result: {result:F2}");
    }
}
