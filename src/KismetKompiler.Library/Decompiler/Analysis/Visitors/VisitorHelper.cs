using System.Linq;
using UAssetAPI.Kismet.Bytecode;

namespace KismetKompiler.Library.Decompiler.Analysis.Visitors;

public static class VisitorHelper
{
    public static Symbol? EnsurePropertySymbolCreated(FunctionAnalysisContext context, KismetPropertyPointer pointer)
    {
        if (pointer.Old != null)
        {
            if (pointer.Old.IsImport())
            {
                var import = pointer.Old.ToImport(context.Asset)
                    ?? throw new InvalidOperationException("Invalid import");
                var symbol = context.Symbols.Where(x => x.Import == import).SingleOrDefault();
                return symbol ?? throw new InvalidOperationException();
            }
            else if (pointer.Old.IsExport())
            {
                var export = pointer.Old.ToExport(context.Asset)
                    ?? throw new InvalidOperationException("Invalid export");
                var symbol = context.Symbols.Where(x => x.Export == export).SingleOrDefault();
                return symbol ?? throw new InvalidOperationException();
            }
            else if (pointer.Old.IsNull())
            {
                return null;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
        else
        {
            if (pointer.New.Path.Length == 0) return null;

            // Get the owner symbol
            Symbol ownerSymbol;
            if (pointer.New.ResolvedOwner.IsImport())
            {
                var import = pointer.New.ResolvedOwner.ToImport(context.Asset)
                    ?? throw new InvalidOperationException("Invalid import");
                ownerSymbol = context.Symbols.Where(x => x.Import == import).SingleOrDefault()
                    ?? throw new InvalidOperationException("Invalid import");
            }
            else if (pointer.New.ResolvedOwner.IsExport())
            {
                var export = pointer.New.ResolvedOwner.ToExport(context.Asset)
                    ?? throw new InvalidOperationException("Invalid export");
                ownerSymbol = context.Symbols.Where(x => x.Export == export).SingleOrDefault()
                    ?? throw new InvalidOperationException("Invalid import");
            }
            else
            {
                throw new InvalidOperationException();
            }

            // Handle nested property paths by walking through the path
            Symbol currentOwner = ownerSymbol;

            for (int i = 0; i < pointer.New.Path.Length; i++)
            {
                var propertyName = pointer.New.Path[i].ToString();
                var symbol = currentOwner.GetMember(propertyName);

                if (symbol == null)
                {
                    // This property has no matching symbol, so create a fake one
                    symbol = new Symbol()
                    {
                        Name = propertyName,
                        Class = context.Symbols.Where(x => x.Name == "ObjectProperty").FirstOrDefault(),
                        Parent = currentOwner,
                        Flags = SymbolFlags.InferredFromKismetPropertyPointer | SymbolFlags.UnresolvedClass,
                    };
                    context.InferredSymbols.Add(symbol);
                }

                // If this is the last property in the path, return it
                if (i == pointer.New.Path.Length - 1)
                {
                    return symbol;
                }

                // Otherwise, use this symbol as the owner for the next property in the path
                currentOwner = symbol;
            }

            return null; // Should not reach here
        }
    }
}
