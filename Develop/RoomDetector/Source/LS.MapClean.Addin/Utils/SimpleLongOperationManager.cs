using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.Runtime;

namespace LS.MapClean.Addin.Utils
{
    // A simple class to handle the long operation using ProgressMeter
    // 
    //  using (var lom = new SimpleLongOperationManager("Testing handle operation"))
    //  {
    //      lom.SetTotalOperations(1000);
    //      for (int i = 0; i <= 1000; i++)
    //      {
    //          Thread.Sleep(5);
    //          lom.Tick();
    //      }
    //  }
    public class SimpleLongOperationManager : IDisposable, ILongOperation
    {
        // Internal members for metering progress
        readonly ProgressMeter _pm;
        public SimpleLongOperationManager(string message)
        {
            _pm = new ProgressMeter();
            _pm.Start(message);
        }

        // System.IDisposable.Dispose
        public void Dispose()
        {
            _pm.Stop();
            _pm.Dispose();
        }

        // Set the total number of operations
        public void SetTotalOperations(int totalOps)
        {
            _pm.SetLimit(totalOps);
        }

        // This function is called whenever an operation
        // is performed
        public void Tick()
        {
            _pm.MeterProgress();
            System.Windows.Forms.Application.DoEvents();
        }
    }

    public interface ILongOperation
    {
        void SetTotalOperations(int totalOps);
        void Tick();
    }
}
