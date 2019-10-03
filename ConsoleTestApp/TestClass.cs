using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleTestApp
{
    class TestClass
    {
        CancellationTokenSource cts;


        public async void StartAsync()
        {
            cts = new CancellationTokenSource();


            await DoAsyncStuff(cts.Token);
                     
        }

        public void Cancel()
        {
            cts?.Cancel();
        }

        private async Task DoAsyncStuff(CancellationToken ct)
        {
           
            Task t = Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(100);
                }
            });


            Thread.Sleep(1000);

            t.Dispose();
        }


    }
}
