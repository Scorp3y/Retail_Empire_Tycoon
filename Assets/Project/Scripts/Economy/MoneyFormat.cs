using System;
using System.Globalization;

namespace RetailEmpireTycoon.Economy
{
    public static class MoneyFormat
    {
        public static string Compact(long value)
        {
            double amount = value;
            string suffix = "";
            if (Math.Abs(amount) >= 1000000000) { amount /= 1000000000; suffix = "B"; }
            else if (Math.Abs(amount) >= 1000000) { amount /= 1000000; suffix = "M"; }
            if (suffix == "M" && Math.Abs(amount) >= 999.95) { amount /= 1000; suffix = "B"; }
            return "$" + amount.ToString(suffix.Length == 0 ? "0" : "0.#", CultureInfo.InvariantCulture) + suffix;
        }
    }
}
