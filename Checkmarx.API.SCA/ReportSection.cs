using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Checkmarx.API.SCA
{
    [Flags]
    public enum ReportSection
    {
        Packages = 1,
        Licenses = 2,
        Policies = 4,
        Vulnerabilities = 8,
        All = 15
    }
}
