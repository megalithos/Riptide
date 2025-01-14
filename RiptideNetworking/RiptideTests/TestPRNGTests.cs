// This file is provided under The MIT License as part of RiptideNetworking.
// Copyright (c) Tom Weiland
// For additional information please see the included LICENSE.md file or view it on GitHub:
// https://github.com/RiptideNetworking/Riptide/blob/main/LICENSE.md

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiptideTests
{
    internal class TestPRNGTests
    {
        [Test]
        public void test()
        {
            CountPRNGOccurrences(10, 1_000_000);
        }

        public static void CountPRNGOccurrences(int maxExclusive, int iterations)
        {
            if (maxExclusive <= 0)
            {
                Console.WriteLine("Invalid range for PRNG.");
                return;
            }

            TestUtil.TestPRNG prng = new TestUtil.TestPRNG(1337);

            // Initialize a dictionary to count occurrences
            Dictionary<int, long> occurrences = new Dictionary<int, long>();
            for (int i = 0; i < maxExclusive; i++)
            {
                occurrences[i] = 0;
            }

            // Call the PRNG function one billion times
            for (int i = 0; i < iterations; i++)
            {
                int number = (int)prng.Next(maxExclusive);
                occurrences[number]++;

                if (i < 10)
                    Console.WriteLine("num: " + number.ToString());
            }

            // Log the results
            Console.WriteLine($"Results after {iterations} iterations:");
            foreach (var kvp in occurrences)
            {
                Console.WriteLine($"Number {kvp.Key}: {((kvp.Value / (float)iterations) * 100f):F2} %");
            }
        }

    }
}
