int currentLength = 5;
string pluPart = "1051".PadLeft(currentLength, '0');
string fillerPart = new string('1', 12 - currentLength);
string strCode = (pluPart + fillerPart).Substring(0, 12);
Console.WriteLine("strCode=" + strCode);
