using System;
using System.Threading;

namespace ex04
{
    class Program
    {
        static int resource = 1000;

        static void TestTh01()
        {
            int i;
            for (i = 0; i < 8555; i++)
            {
                resource++;
                Console.Write(".");
            }
        }


        static void Main(string[] args)
        {
            Thread th1 = new Thread(TestTh01);
            th1.Start();
            th1.Join();
            Console.WriteLine("resource={0}", resource);
        }
    }
}

