using System;
using System.IO;
using System.Linq;

var l = File.ReadAllLines("Jdate/INFO.JDG").Skip(3).First();
Console.WriteLine("Line length: " + l.Length);
