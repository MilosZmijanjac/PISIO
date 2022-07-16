using Coravel.Invocable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanUpService
{
    public class Worker : IInvocable
    {
        private readonly string folder = @"C:\Users\SnakeKD\Desktop\PISIO\Storage";
        public Task Invoke()
        {
            foreach (var d in new DirectoryInfo(folder).GetDirectories())
                if (d.CreationTime < DateTime.Now.AddDays(-1))
                    clearFolder(d);
            return Task.CompletedTask;
        }

        private void clearFolder(DirectoryInfo dir)
        {
            foreach (FileInfo fi in dir.GetFiles())
            {
                fi.Delete();
            }

            foreach (DirectoryInfo di in dir.GetDirectories())
            {
                clearFolder(di);
                di.Delete();
            }
        }
    }
}
