using System;
using System.Linq;
using System.Reflection;
var asm = Assembly.LoadFrom(@"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64\sts2.dll");
var t = asm.GetType("MegaCrit.Sts2.Core.Models.CardModel");
Console.WriteLine(t);
foreach (var p in t.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(p=>p.Name)) Console.WriteLine($"P {p.PropertyType.FullName} {p.Name} get={(p.GetMethod?.IsPublic==true?"pub":"non")} set={(p.SetMethod?.IsPublic==true?"pub":p.SetMethod!=null?"non":"-")}");
foreach (var f in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(f=>f.Name)) Console.WriteLine($"F {f.FieldType.FullName} {f.Name}");
