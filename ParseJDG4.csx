using System;
using System.IO;
using System.Linq;

var lines = File.ReadAllLines("Jdate/INFO.JDG").Skip(3).Take(1);
foreach (var l in lines) {
    if (l.Length > 71) {
        Console.WriteLine("Line: " + l);
        Console.WriteLine("76-1: " + l.Substring(76,1));
        Console.WriteLine("77-7: " + l.Substring(77,7));
        Console.WriteLine("84-5: " + l.Substring(84,5));
    }
}
