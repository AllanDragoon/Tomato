using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.MapClean
{
    public class CheckResultGroup : IDisposable
    {
        private readonly ActionType _actionType;
        private List<CheckResult> _checkResults = new List<CheckResult>(); 
        public CheckResultGroup(ActionType actionType, IEnumerable<CheckResult> checkResults)
        {
            _actionType = actionType;
            _checkResults.AddRange(checkResults);
        }

        public MapClean.ActionType ActionType
        {
            get { return _actionType; }
        }

        public IEnumerable<CheckResult> CheckResults
        {
            get { return _checkResults; }
        }

        public void FixAll()
        {
            foreach (var checkResult in CheckResults)
            {
                if (checkResult.Status == Status.Fixed || checkResult.Status == Status.Rejected)
                    continue;
            }
        }

        public void Dispose()
        {
            foreach (var checkResult in CheckResults)
            {
                checkResult.Dispose();
            }
        }
    }
}
