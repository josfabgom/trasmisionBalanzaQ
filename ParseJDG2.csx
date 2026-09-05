using System;
using System.IO;
using System.Linq;

var lines = File.ReadAllLines("Jdate/INFO.JDG").Skip(2).Take(2);
foreach (var l in lines) {
    if (l.Length > 71) {
        Console.WriteLine("Line: " + l);
        Console.WriteLine("76-1: " + l.Substring(76,1));
        Console.WriteLine("77-12: " + l.Substring(77,12));
        Console.WriteLine("89-12: " + l.Substring(89,12));
        Console.WriteLine("101-12: " + l.Substring(101,12));
        Console.WriteLine("113-12: " + l.Substring(113,12));
        Console.WriteLine("125-17: " + l.Substring(125));
    }
}
