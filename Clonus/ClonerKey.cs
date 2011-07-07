using System;

namespace Clonus
{
    internal class ClonerKey : IEquatable<ClonerKey>
    {
        public Type SourceType;
        public CloneMethod CloneMethod;
        public bool Equals(ClonerKey other)
        {
            return other != null && other.SourceType == SourceType && other.CloneMethod == CloneMethod;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != GetType())
                return false;
            return Equals((ClonerKey)obj);
        }

        public override int GetHashCode()
        {
            return SourceType.GetHashCode() + CloneMethod.GetHashCode();
        }
    }
}