using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LS.MapClean.Addin.MapClean
{
    /// <summary>
    /// Status for check result.
    /// </summary>
    public enum Status
    {
        /// <summary>
        /// The map clean check result is pending (need to be fixed or rejected)
        /// </summary>
        Pending,

        /// <summary>
        /// The map clean check result is fixed.
        /// </summary>
        Fixed,

        /// <summary>
        /// The map clean check result is rejected (no need to fixed)
        /// </summary>
        Rejected,

        /// <summary>
        /// The map clean check result is invalid.
        /// For example, check result A and check result B both references to Entity X,
        /// if A is fixed, that is, X is modified, then B need to be set invalid.
        /// </summary>
        Invalid,

        /// <summary>
        /// Failed to fix a check result.
        /// </summary>
        Failed,

        /// <summary>
        /// No fix method
        /// </summary>
        NoFixMethod
    }

    public static class StatusUtils
    {
        public static string ToChineseName(this Status status)
        {
            var result = "";
            switch (status)
            {
                case Status.Pending:
                    break;
                case Status.Fixed:
                    result = "已修复";
                    break;
                case Status.Rejected:
                    result = "忽略";
                    break;
                case Status.Invalid:
                    result = "失效";
                    break;
                case Status.Failed:
                    result = "修复失败";
                    break;
                case Status.NoFixMethod:
                    result = "不提供修复";
                    break;
            }
            return result;
        }
    }
}
