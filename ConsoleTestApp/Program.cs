using QKD_Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            TestClass testclass = new TestClass();
            testclass.StartAsync();

            Thread.Sleep(3000);

            testclass.Cancel();


            while (true) ;

        }

    }
}
