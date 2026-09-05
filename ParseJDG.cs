using System;
using System.IO;
using System.Linq;

class Program {
    static void Main() {
        var lines = File.ReadAllLines("Jdate/INFO.JDG").Take(15);
        foreach (var l in lines) {
            if (l.Length > 71) {
                string plu = l.Substring(7,6);
                string after = l.Substring(71);
                Console.WriteLine($"{plu} -> {after}");
            }
        }
    }
}
