using System;
using System.IO;
using System.Linq;

var lines = File.ReadAllLines("Jdate/INFO.JDG").Take(3);
foreach (var l in lines) {
    if (l.Length > 71) {
        Console.WriteLine("Line: " + l);
        Console.WriteLine("0-3: " + l.Substring(0,3));
        Console.WriteLine("3-1: " + l.Substring(3,1));
        Console.WriteLine("4-3: " + l.Substring(4,3));
        Console.WriteLine("7-6: " + l.Substring(7,6));
        Console.WriteLine("13-3: " + l.Substring(13,3));
        Console.WriteLine("16-3: " + l.Substring(16,3));
        Console.WriteLine("19-52: " + l.Substring(19,52));
        Console.WriteLine("71-5: " + l.Substring(71,5));
        Console.WriteLine("76-1: " + l.Substring(76,1));
        Console.WriteLine("77-12: " + l.Substring(77,12));
        Console.WriteLine("89-rest: " + l.Substring(89));
    }
}
