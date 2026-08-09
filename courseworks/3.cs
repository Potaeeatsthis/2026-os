using System;
using System.Threading;

namespace ex03
{
    class Program
    {
        static int resource = 10000;

        static void TestTh01()
        {
            resource = 55555;
        }


        static void Main(string[] args)
        {
            Thread th1 = new Thread(TestTh01);
            th1.Start();
            Thread.Sleep(1000);
            Console.WriteLine("resource={0}", resource);
        }
    }
}

