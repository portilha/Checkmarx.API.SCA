using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Checkmarx.API.SCA.Client;

namespace Checkmarx.API.SCA
{
    public class PackageEqualizer : IEqualityComparer<PackageStateGet>
    {
        private static PackageEqualizer _instance;
        public static PackageEqualizer Instance
        {
            get { return _instance ?? (_instance = new PackageEqualizer()); }
        }

        public bool Equals(PackageStateGet x, PackageStateGet y)
        {
            return x.vulnerabilityId == y.vulnerabilityId;
        }

        public int GetHashCode([DisallowNull] PackageStateGet obj)
        {
            return obj.vulnerabilityId.GetHashCode();
        }


    }
}
