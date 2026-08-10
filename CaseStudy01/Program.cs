// Case Study 01 - Multithreading 
// Updated: 2026-08-02
using System;
using System.IO;
using CalculatingFunctions;
using System.Threading;
using System;
using System.Text.Json;
using System.Diagnostics;

class Program
{
    static decimal[] data = new decimal[11000001];
    static decimal result = 0;
    const long CalculationCount = 10000000;
    const int MaxThreads = 22;
    static int threadCount = 1;
    static decimal[] localResults = Array.Empty<decimal>();

    //Algorithm of Calculate1(ref decimal[] value, ref long idx)
    /*      {
              long i, _j, _value1;
              decimal _value, result = 0, sum = 0;

              i = idx;
              if (i >= value.Length)
              {
                  i = value.Length - 1;

              }
              if ((int)value[i] % 2 == 0)
              {
                  sum += (decimal)((double)value[i] * 0.2);
              }
              else if ((int)value[i] % 3 == 0)
              {
                  sum += (decimal)((double)value[i] * 0.3);
              }
              else if ((int)value[i] % 5 == 0)
              {
                  sum += (decimal)((double)value[i] * 0.5);
              }
              else if ((int)value[i] % 7 == 0)
              {
                  sum += (decimal)((double)value[i] * 0.7);
              }
              else
              {
                  sum += (decimal)((double)value[i] * 0.1);
              }

              if ((long)sum % 2 == 0)
              {
                  result = Math.Round(sum * (decimal)0.5);
              }
              else
              {
                  result = Math.Round((sum * (-1)) * (decimal)0.3);
              } 

              value[i] *= (decimal)0.1;
              idx++;
              return result;
          }
      */

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

    private static void LoadData()
    {
        Console.WriteLine("Loading data...");
        FileStream fs = new FileStream("data.bin", FileMode.Open);
        BinaryReader br = new BinaryReader(fs);
        for (int i = 0; i < data.Length; i++)
        {
            Single f = br.ReadSingle();
            data[i] = (decimal)(f * 36);
        }
        Console.WriteLine("Data loaded successfully.\n\n");
    }

    private static void Main(string[] args)
    {
        LoadData();

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

        Console.WriteLine("Calculation start ...");

        Thread[] threads = new Thread[threadCount];
        localResults = new decimal[threadCount];

        Stopwatch _st = new Stopwatch();
        _st.Start();

        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(ThreadWork);
            threads[i].Start(i);
        }

        for (int i = 0; i < threadCount; i++)
        {
            threads[i].Join(); // Wait for every thread to finish
        }

        _st.Stop();
        result = 0;
        for (int i = 0; i < threadCount; i++)
        {
            result += localResults[i];
        }
        Console.WriteLine($"Calculation finished in {_st.ElapsedMilliseconds} ms. Result: {(result).ToString("F2")}");
    }
}