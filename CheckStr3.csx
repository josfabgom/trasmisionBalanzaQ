using System;
using System.IO;
using System.Linq;

var l = File.ReadAllLines("Jdate/INFO.JDG").First(x => x.Contains("PANCETA"));
Console.WriteLine("String: " + l);
for(int i=0; i<l.Length; i+=10) {
    Console.WriteLine($"[{i}]: {l.Substring(i, Math.Min(10, l.Length - i))}");
}
