using MoonSharp.Interpreter;
using UsableComputer.Logic;

int checks = 0;
void Check(bool condition) { if (!condition) throw new Exception($"Studio assertion {checks + 1} failed"); checks++; }
Check(StudioSource.LineStart("one\r\ntwo\nthree", 2) == 5);
Check(StudioSource.LineStart("one", 200) == 3);
Check(StudioSource.LineAt("one\ntwo", 4) == 2);
Check(StudioSource.DiagnosticLine("No line location") == 0);
Check(StudioSource.DiagnosticLine("script:(150,2-4): invalid") == 150);
Check(StudioSource.DescribeDiagnostic("C:\\Apps\\unsaved.lua:(150,2-4): invalid") == "Line 150: invalid");
try { new Script(CoreModules.Preset_HardSandbox).DoString("-- first\nreturn {", null, "test.lua"); throw new Exception("Expected syntax failure"); }
catch (SyntaxErrorException error) { Check(StudioSource.DiagnosticLine(error.DecoratedMessage) == 2); }
var workspace = new StudioWorkspace();
var first = workspace.Open("first.lua", "original");
first.Source = "edited";
Check(first.IsModified);
var second = workspace.Open("second.lua", "template");
Check(ReferenceEquals(workspace.Next(second, -1), first));
Check(ReferenceEquals(workspace.Next(second, 1), first));
Check(workspace.Open("first.lua", "replacement").Source == "edited");
first.SavedSource = first.Source;
Check(!first.IsModified);
for (int i = 2; i < 16; i++) workspace.Open(i.ToString(), "");
try { workspace.Open("overflow", ""); throw new Exception("Expected draft limit"); }
catch (InvalidOperationException) { checks++; }
Console.WriteLine($"PASS | Studio verifier | {checks} assertions");
