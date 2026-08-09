using System;
using System.Threading;

namespace Ex02
{
    class Program
    {
        static int resource = 10000;

        static void TestTh1()
        {
            Console.WriteLine("Thread#1 resource={0}", resource);
        }

        static void TestTh2()
        {
            Console.WriteLine("Thread#2 resource={0}", resource);
        }

        static void Main(string[] args)
        {
            Thread th1 = new Thread(TestTh1);
            Thread th2 = new Thread(TestTh2);

            th1.Start();
            th2.Start();

            Console.WriteLine("Main resource={0}", resource);
        }
    }
}
