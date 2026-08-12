// Case Study 01 - optimized heterogeneous-core scheduling
// Based on Program.cs.orig. Each value is completed before the next value starts.
using System.Diagnostics;
using System.Runtime.CompilerServices;

class Program
{
    private const int DataLength = 11_000_001;
    private const int CalculationCount = 10_000_000;
    private const int CalculationRounds = 30;

    // Small chunks let faster P-cores claim more work while slower E-cores claim less.
    private const int ChunkSize = 16_384;

    // For |value| < 5, the original algorithm returns zero. Multiplying it by 0.1
    // only makes every later round smaller, so the remaining rounds can be skipped.
    private const double NearZeroThreshold = 5.0;

    private static readonly float[] Data = new float[DataLength];
    private static double[] localResults = Array.Empty<double>();
    private static int[] processedValues = Array.Empty<int>();
    private static int[] processedChunks = Array.Empty<int>();
    private static int nextChunkStart;

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static double CalculateValue(ref float storedValue)
    {
        double value = storedValue;
        double result = 0.0;

        for (int round = 0; round < CalculationRounds; round++)
        {
            if (Math.Abs(value) < NearZeroThreshold)
            {
                break;
            }

            int wholeValue = (int)value;
            double sum;

            if ((wholeValue & 1) == 0)
            {
                sum = value * 0.2;
            }
            else if (wholeValue % 3 == 0)
            {
                sum = value * 0.3;
            }
            else if (wholeValue % 5 == 0)
            {
                sum = value * 0.5;
            }
            else if (wholeValue % 7 == 0)
            {
                sum = value * 0.7;
            }
            else
            {
                sum = value * 0.1;
            }

            result += (((long)sum & 1L) == 0)
                ? Math.Round(sum * 0.5)
                : Math.Round(-sum * 0.3);

            value *= 0.1;
        }

        storedValue = (float)value;
        return result;
    }

    private static void DynamicWorker(object? state)
    {
        int workerId = (int)state!;
        double localResult = 0.0;
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
                localResult += CalculateValue(ref Data[index]);
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
            Data[index] = reader.ReadSingle() * 36.0f;
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
        localResults = new double[workerCount];
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

        double result = 0.0;
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

        Console.WriteLine(
            $"Total: {totalProcessedValues:N0} values in {totalProcessedChunks:N0} chunk(s)");
        Console.WriteLine($"Calculation finished in {stopwatch.ElapsedMilliseconds:N0} ms.");
        Console.WriteLine($"Result: {result:F2}");
    }
}
