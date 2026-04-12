using System;
using System.IO;

string path = @"d:\Antigravity Proyectos\trasmisionBalanzaQ\SM192.168.1.50F37.DAT";
if (!File.Exists(path)) { Console.WriteLine("File not found"); return; }

string content = File.ReadAllText(path).Trim();
Console.WriteLine($"Total Length: {content.Length}");

string plu1 = "00000022";
string plu2 = "00000033";
string plu3 = "00000036";

int idx1 = content.IndexOf(plu1);
int idx2 = content.IndexOf(plu2);
int idx3 = content.IndexOf(plu3);

Console.WriteLine($"PLU1 ({plu1}) at {idx1}");
Console.WriteLine($"PLU2 ({plu2}) at {idx2}");
Console.WriteLine($"PLU3 ({plu3}) at {idx3}");

Console.WriteLine($"Distance 1-2: {idx2 - idx1}");
Console.WriteLine($"Distance 2-3: {idx3 - idx2}");

if (idx1 >= 0 && idx2 > idx1) {
    string rec1 = content.Substring(idx1, idx2 - idx1);
    Console.WriteLine($"Record 1 Length: {rec1.Length} chars ({rec1.Length / 2} bytes)");
    Console.WriteLine($"Record 1 Tail: {rec1.Substring(rec1.Length - 20)}");
}

Console.WriteLine($"File Ends with: {content.Substring(content.Length - 20)}");
