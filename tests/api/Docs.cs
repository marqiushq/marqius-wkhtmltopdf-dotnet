using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// The help an editor shows while you type, checked against the assembly it describes (ApiList docs).
public static class Docs
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static int Check(string assemblyPath, string helpPath)
    {
        var path = Path.GetFullPath(assemblyPath);
        var context = new AssemblyLoadContext("docs", isCollectible: true);
        context.Resolving += (c, name) =>
        {
            var beside = Path.Combine(Path.GetDirectoryName(path), name.Name + ".dll");
            return File.Exists(beside) ? c.LoadFromAssemblyPath(beside) : null;
        };
        var asm = context.LoadFromAssemblyPath(path);
        var help = XDocument.Load(helpPath).Descendants("member").ToDictionary(m => (string)m.Attribute("name"), m => m);
        var gaps = new List<string>();
        int checkedCount = 0;
        foreach (var t in asm.GetExportedTypes())
        {
            checkedCount++;
            Need(DocId(t), t.Name, gaps, help, typeof(Delegate).IsAssignableFrom(t.BaseType) ? t.GetMethod("Invoke").GetParameters() : null, t.IsGenericTypeDefinition ? t.GetGenericArguments() : null);
            if (typeof(Delegate).IsAssignableFrom(t.BaseType))
                continue;
            foreach (var m in t.GetMembers(Declared))
            {
                if (!Visible(m) || m is Type)
                    continue;
                if (m is MethodInfo special && special.IsSpecialName && !special.Name.StartsWith("op_", StringComparison.Ordinal))
                    continue; // property and event accessors: the property or event has the help
                if (m is MethodInfo { Name: "Finalize" } || (t.IsEnum && m is FieldInfo { IsLiteral: false }))
                    continue;
                checkedCount++;
                ParameterInfo[] ps = m switch { MethodBase b => b.GetParameters(), PropertyInfo p => p.GetIndexParameters(), _ => null };
                Need(DocId(m), $"{t.Name}.{m.Name}", gaps, help, ps, m is MethodInfo g && g.IsGenericMethodDefinition ? g.GetGenericArguments() : null);
            }
        }
        foreach (var g in gaps)
            Console.WriteLine($"GAP\t{asm.GetName().Name}\t{g}");
        Console.WriteLine($"{asm.GetName().Name}: {checkedCount - gaps.Select(g => g.Split('\t')[0]).Distinct().Count()} of {checkedCount} types and members with complete help");
        return gaps.Count == 0 ? 0 : 1;
    }

    private static void Need(string id, string label, List<string> gaps, Dictionary<string, XElement> help, ParameterInfo[] ps, Type[] typeParameters)
    {
        if (!help.TryGetValue(id, out var doc))
        {
            gaps.Add($"{label}\tno help at all ({id})");
            return;
        }
        if (doc.Element("inheritdoc") != null)
            gaps.Add($"{label}\tborrows its help with <inheritdoc/>, which some editors show blank");
        var summary = doc.Element("summary");
        if (summary == null || !Regex.IsMatch(Text(summary), @"\w"))
            gaps.Add($"{label}\tno summary");
        foreach (var p in ps ?? Array.Empty<ParameterInfo>())
        {
            var tag = doc.Elements("param").FirstOrDefault(e => (string)e.Attribute("name") == p.Name);
            if (tag == null || !Regex.IsMatch(Text(tag), @"\w"))
                gaps.Add($"{label}\tno help for the parameter '{p.Name}'");
        }
        foreach (var tp in typeParameters ?? Array.Empty<Type>())
        {
            if (!doc.Elements("typeparam").Any(e => (string)e.Attribute("name") == tp.Name))
                gaps.Add($"{label}\tno help for the type parameter '{tp.Name}'");
        }
    }

    // the words a tooltip shows: the text, and the names <see cref="..."/> and <paramref/> put in it
    private static string Text(XElement e) => string.Concat(e.DescendantNodesAndSelf().Select(n => n switch
    {
        XText x => x.Value,
        XElement { Name.LocalName: "see" or "paramref" or "typeparamref" } r => (string)r.Attribute("cref") ?? (string)r.Attribute("name") ?? (string)r.Attribute("langword") ?? "",
        _ => "",
    }));

    private static bool Visible(MemberInfo m) => m switch
    {
        MethodBase b => b.IsPublic || b.IsFamily || b.IsFamilyOrAssembly,
        FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
        PropertyInfo p => new[] { p.GetMethod, p.SetMethod }.Any(a => a != null && (a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly)),
        EventInfo e => e.AddMethod != null && (e.AddMethod.IsPublic || e.AddMethod.IsFamily || e.AddMethod.IsFamilyOrAssembly),
        _ => false,
    };

    // the documentation ID the compiler writes in the help file: T:, M:, P:, F: and E: names
    private static string DocId(MemberInfo m) => m switch
    {
        Type t => "T:" + Declaring(t),
        ConstructorInfo c => "M:" + Declaring(c.DeclaringType) + "." + (c.IsStatic ? "#cctor" : "#ctor") + Params(c.GetParameters()),
        MethodInfo mi => "M:" + Declaring(mi.DeclaringType) + "." + mi.Name + (mi.IsGenericMethodDefinition ? "``" + mi.GetGenericArguments().Length : "")
            + Params(mi.GetParameters()) + (mi.Name is "op_Implicit" or "op_Explicit" ? "~" + Param(mi.ReturnType) : ""),
        PropertyInfo p => "P:" + Declaring(p.DeclaringType) + "." + p.Name + Params(p.GetIndexParameters()),
        FieldInfo f => "F:" + Declaring(f.DeclaringType) + "." + f.Name,
        EventInfo e => "E:" + Declaring(e.DeclaringType) + "." + e.Name,
        _ => m.Name,
    };

    private static string Declaring(Type t) => t.IsNested ? Declaring(t.DeclaringType) + "." + t.Name : (t.Namespace == null ? "" : t.Namespace + ".") + t.Name;

    private static string Params(ParameterInfo[] ps) => ps.Length == 0 ? "" : "(" + string.Join(",", ps.Select(p => Param(p.ParameterType))) + ")";

    private static string Param(Type t)
    {
        if (t.IsByRef)
            return Param(t.GetElementType()) + "@";
        if (t.IsPointer)
            return Param(t.GetElementType()) + "*";
        if (t.IsArray)
            return Param(t.GetElementType()) + (t.GetArrayRank() == 1 ? "[]" : "[" + string.Join(",", Enumerable.Repeat("0:", t.GetArrayRank())) + "]");
        if (t.IsGenericParameter)
            return (t.DeclaringMethod != null ? "``" : "`") + t.GenericParameterPosition;
        var name = t.IsNested ? Param(t.DeclaringType) + "." + t.Name : (t.Namespace == null ? "" : t.Namespace + ".") + t.Name;
        if (t.IsGenericType)
        {
            name = name.Substring(0, name.IndexOf('`') < 0 ? name.Length : name.IndexOf('`'));
            name += "{" + string.Join(",", t.GetGenericArguments().Select(Param)) + "}";
        }
        return name;
    }
}
