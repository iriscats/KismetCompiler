using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using KismetKompiler.Compiler;
using KismetKompiler.Library.Compiler.Exceptions;
using KismetKompiler.Library.Compiler.Processing;
using KismetKompiler.Decompiler;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.IO;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.IO;
using UAssetAPI.Kismet;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;
using KismetKompiler.Library.Parser;
using KismetKompiler.Library.Packaging;
using KismetKompiler.Library.Compiler;
using System.Collections.Generic;
using System.Xml;

Console.OutputEncoding = Encoding.Unicode;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

if (args.Length == 0)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  -d <file>     Decompile .uasset to .kms");
    Console.WriteLine("  -c <file>     Compile .kms to .uasset");
    return;
}

var mode = args[0];
var path = args.Length > 1 ? args[1] : "";

if (string.IsNullOrEmpty(path))
{
    Console.WriteLine("Error: No file specified");
    return;
}

switch (mode)
{
    case "-d":
        DecompileOnly(path, EngineVersion.VER_UE4_27);
        break;
    case "-c":
        CompileOnly(path, EngineVersion.VER_UE4_27);
        break;
    default:
        Console.WriteLine($"Error: Unknown mode '{mode}'. Use -d for decompile or -c for compile");
        break;
}

static void DecompileOne(string path, EngineVersion ver, string? usmapPath = default)
{
    UnrealPackage asset;
    if (!string.IsNullOrEmpty(usmapPath))
    {
        var usmap = new Usmap(usmapPath);
        asset = new ZenAsset(path, ver, usmap);
    }
    else
    {
        asset = LoadAsset(path, ver);
    }

    var kmsPath = Path.ChangeExtension(path, ".kms");
    DecompileClass(asset, kmsPath);

    var script = CompileClass(kmsPath, ver);
    var newAsset = new UAssetLinker((UAsset)asset)
        .LinkCompiledScript(script)
        .Build();

    var old = ((FunctionExport)newAsset.Exports.Where(x => x is FunctionExport).FirstOrDefault());
    KismetSerializer.asset = newAsset;
    DumpOldAndNew(path, newAsset, script);

    newAsset.Write(Path.ChangeExtension(path, ".new.uasset"));
}


static UAsset LoadAsset(string filePath, EngineVersion version)
{
    var asset = new UAsset(filePath, version);
    asset.VerifyBinaryEquality();
    return asset;
}

static void DecompileClass(UnrealPackage asset, string outPath)
{
    using var outWriter = new StreamWriter(outPath, false, Encoding.Unicode);
    var decompiler = new KismetDecompiler(outWriter);
    decompiler.Decompile(asset);
}

static void PrintSyntaxError(int lineNumber, int startIndex, int endIndex, string[] lines)
{
    if (lineNumber < 1 || lineNumber > lines.Length)
    {
        throw new ArgumentOutOfRangeException(nameof(lineNumber), "Invalid line number.");
    }

    string line = lines[lineNumber - 1];
    int lineLength = line.Length;

    if (startIndex < 0 || endIndex < 0 || startIndex >= lineLength || endIndex >= lineLength)
    {
        throw new ArgumentOutOfRangeException(nameof(startIndex), "Invalid character index.");
    }

    string highlightedLine = line.Substring(0, startIndex) +
                             new string('^', endIndex - startIndex + 1) +
                             line.Substring(endIndex + 1);

    var messagePrefix = $"Syntax error at line {lineNumber}:";
    Console.WriteLine($"{messagePrefix}{line}");
    Console.WriteLine(new string(' ', messagePrefix.Length) + highlightedLine);
}

static CompiledScriptContext CompileClass(string inPath, EngineVersion engineVersion)
{
    try
    {
        var parser = new KismetScriptASTParser();
        using var reader = new StreamReader(inPath, Encoding.Unicode);
        var compilationUnit = parser.Parse(reader);
        var typeResolver = new TypeResolver();
        typeResolver.ResolveTypes(compilationUnit);
        var compiler = new KismetScriptCompiler();
        compiler.EngineVersion = engineVersion;
        var script = compiler.CompileCompilationUnit(compilationUnit);
        return script;
    }
    catch (ParseCanceledException ex)
    {
        if (ex.InnerException is InputMismatchException innerEx)
        {
            var lines = File.ReadAllLines(inPath);
            PrintSyntaxError(innerEx.OffendingToken.Line, 
            innerEx.OffendingToken.Column, 
            innerEx.OffendingToken.Column + innerEx.OffendingToken.Text.Length - 1,
            lines);
        }

        throw;
    }
}

static void DecompileOnly(string path, EngineVersion ver, string? usmapPath = default)
{
    UnrealPackage asset;
    if (!string.IsNullOrEmpty(usmapPath))
    {
        var usmap = new Usmap(usmapPath);
        asset = new ZenAsset(path, ver, usmap);
    }
    else
    {
        asset = LoadAsset(path, ver);
    }

    var kmsPath = Path.ChangeExtension(path, ".kms");
    DecompileClass(asset, kmsPath);
    Console.WriteLine($"Decompiled: {path} -> {kmsPath}");
}

static void CompileOnly(string path, EngineVersion ver, string? assetPath = default)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"Error: Input file {path} does not exist");
        return;
    }

    var script = CompileClass(path, ver);

    string outputPath;
    if (!string.IsNullOrEmpty(assetPath))
    {
        outputPath = assetPath;
    }
    else
    {
        // Load the original asset if we're compiling a .kms file
        var originalAssetPath = Path.ChangeExtension(path, ".uasset");
        if (File.Exists(originalAssetPath))
        {
            var asset = LoadAsset(originalAssetPath, ver);
            var newAsset = new UAssetLinker((UAsset)asset)
                .LinkCompiledScript(script)
                .Build();
            outputPath = Path.ChangeExtension(path, ".compiled.uasset");
            newAsset.Write(outputPath);
        }
        else
        {
            Console.WriteLine($"Error: Cannot find original asset file {originalAssetPath} for compilation");
            return;
        }
    }

    Console.WriteLine($"Compiled: {path} -> {outputPath}");
}

static void DumpOldAndNew(string fileName, UnrealPackage asset, CompiledScriptContext script)
{
    KismetSerializer.asset = asset;

    var oldJsons = asset.Exports
        .Where(x => x is FunctionExport)
        .Cast<FunctionExport>()
        .OrderBy(x => asset.GetClassExport()?.FuncMap.IndexOf(x.ObjectName))
        .Select(x => (x.ObjectName.ToString(), JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.ScriptBytecode), Newtonsoft.Json.Formatting.Indented)));

    var newJsons = script.Classes
        .SelectMany(x => x.Functions)
        .Select(x => (x.Symbol.Name, JsonConvert.SerializeObject(KismetSerializer.SerializeScript(x.Bytecode.ToArray()), Newtonsoft.Json.Formatting.Indented)));

    var oldJsonText = string.Join("\n", oldJsons);
    var newJsonText = string.Join("\n", newJsons);

    File.WriteAllText($"old.json", oldJsonText);
    File.WriteAllText($"new.json", newJsonText);

    if (oldJsonText != newJsonText)
        Console.WriteLine($"Verification failed: {fileName}");
}