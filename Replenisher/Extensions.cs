using System;
using System.Linq;

namespace Replenisher {
    public static class Extensions {
        public static string FirstCharToUpper(this string input) {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("String cannot be empty.");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }
    }
}
