using System.Reflection;
using System.Runtime.Loader;

// dotnet ApiList.dll ASSEMBLY: the assembly's identity on the first line, then every public (and
// protected) type and member, one line each, sorted. Names carry no assembly versions, so two builds
// of one API print the same lines.
var path = Path.GetFullPath(args[0]);
var context = new AssemblyLoadContext("api", isCollectible: true);
context.Resolving += (c, name) =>
{
    var beside = Path.Combine(Path.GetDirectoryName(path), name.Name + ".dll");
    return File.Exists(beside) ? c.LoadFromAssemblyPath(beside) : null;
};
var asm = context.LoadFromAssemblyPath(path);
var id = asm.GetName();
var token = id.GetPublicKeyToken();
Console.WriteLine($"assembly {id.Name} {id.Version} key {(token == null || token.Length == 0 ? "none" : Convert.ToHexString(token))}");

const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
var lines = new List<string>();
foreach (var t in asm.GetExportedTypes())
{
    var kind = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" : typeof(Delegate).IsAssignableFrom(t.BaseType) ? "delegate"
        : t.IsAbstract && t.IsSealed ? "static class" : t.IsAbstract ? "abstract class" : t.IsSealed ? "sealed class" : "class";
    var bases = new List<string>();
    if (t.BaseType != null && !t.IsEnum && !t.IsValueType && kind != "delegate")
        bases.Add(Name(t.BaseType));
    bases.AddRange(t.GetInterfaces().Select(Name).OrderBy(x => x, StringComparer.Ordinal));
    lines.Add($"{Attributes(t.CustomAttributes)}{kind} {Name(t)}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}");
    if (kind == "delegate")
    {
        var invoke = t.GetMethod("Invoke");
        lines.Add($"  {Name(t)}: {Name(invoke.ReturnType)} ({Parameters(invoke)})");
        continue;
    }
    foreach (var m in t.GetMembers(declared))
    {
        if (!Visible(m) || (m is MethodInfo special && special.IsSpecialName && !special.Name.StartsWith("op_", StringComparison.Ordinal)))
            continue;
        if (t.IsEnum && m is FieldInfo { IsLiteral: false })
            continue; // value__
        if (m is MethodInfo { Name: "Finalize" } finalizer && finalizer.GetParameters().Length == 0)
            continue; // a finalizer: no code can call it
        string sig = m switch
        {
            MethodInfo mi => $"{Modifiers(mi)}{Name(mi.ReturnType)} {mi.Name}{Generic(mi)}({Parameters(mi)})",
            ConstructorInfo ci => $"{(ci.IsStatic ? "static " : "")}{(ci.IsFamily ? "protected " : "")}.ctor({Parameters(ci)})",
            PropertyInfo pi => $"{Modifiers(pi.GetMethod ?? pi.SetMethod)}{Name(pi.PropertyType)} {pi.Name}{Indexer(pi)} {{ {Accessor("get", pi.GetMethod)}{Accessor("set", pi.SetMethod)}}}",
            FieldInfo fi => $"{(fi.IsFamily ? "protected " : "")}{(fi.IsLiteral ? "const " : fi.IsStatic ? "static " : "")}{(fi.IsInitOnly ? "readonly " : "")}{Name(fi.FieldType)} {fi.Name}{(fi.IsLiteral ? " = " + fi.GetRawConstantValue() : "")}",
            EventInfo ei => $"{Modifiers(ei.AddMethod)}event {Name(ei.EventHandlerType)} {ei.Name}",
            Type nested => $"nested {Name(nested)}",
            _ => m.ToString(),
        };
        lines.Add($"  {Name(t)}: {Attributes(m.CustomAttributes)}{sig}");
    }
}
foreach (var line in lines.OrderBy(x => x, StringComparer.Ordinal))
    Console.WriteLine(line);

static bool Visible(MemberInfo m) => m switch
{
    MethodBase b => b.IsPublic || b.IsFamily || b.IsFamilyOrAssembly,
    FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
    PropertyInfo p => new[] { p.GetMethod, p.SetMethod }.Any(a => a != null && (a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly)),
    EventInfo e => e.AddMethod != null && (e.AddMethod.IsPublic || e.AddMethod.IsFamily || e.AddMethod.IsFamilyOrAssembly),
    Type n => n.IsNestedPublic || n.IsNestedFamily || n.IsNestedFamORAssem,
    _ => false,
};

static string Accessor(string name, MethodInfo a) =>
    a == null ? "" : a.IsPublic ? name + "; " : a.IsFamily || a.IsFamilyOrAssembly ? $"protected {name}; " : "";

static string Modifiers(MethodInfo m)
{
    if (m == null)
        return "";
    var s = m.IsFamily || m.IsFamilyOrAssembly ? "protected " : "";
    if (m.IsStatic)
        return s + "static ";
    if (m.DeclaringType.IsInterface)
        return s;
    if (m.IsAbstract)
        return s + "abstract ";
    if (m.IsVirtual && !m.IsFinal)
        return s + (m.GetBaseDefinition() != m ? "override " : "virtual ");
    return s;
}

static string Generic(MethodInfo m) => m.IsGenericMethodDefinition ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";

static string Indexer(PropertyInfo p)
{
    var index = p.GetIndexParameters();
    return index.Length == 0 ? "" : "[" + string.Join(", ", index.Select(i => Name(i.ParameterType))) + "]";
}

static string Parameters(MethodBase m) => string.Join(", ", m.GetParameters().Select(p =>
    $"{(p.ParameterType.IsByRef ? (p.IsOut ? "out " : p.IsIn ? "in " : "ref ") : "")}{(p.CustomAttributes.Any(a => a.AttributeType.Name == "ParamArrayAttribute") ? "params " : "")}"
    + $"{Name(p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType)} {p.Name}{(p.HasDefaultValue ? " = " + (p.DefaultValue ?? "default") : "")}"));

// attributes that change what code may do or how DinkToPdf reads a setting: [Obsolete], [Flags], DinkToPdf's [WkHtml("...")]
static string Attributes(IEnumerable<CustomAttributeData> attributes)
{
    var kept = attributes.Where(a => a.AttributeType.FullName is "System.ObsoleteAttribute" or "System.FlagsAttribute" || a.AttributeType.Namespace == "DinkToPdf")
        .Select(a => $"[{a.AttributeType.Name.Replace("Attribute", "")}({string.Join(", ", a.ConstructorArguments.Select(v => v.Value is string s ? $"\"{s}\"" : v.Value))})] ")
        .OrderBy(x => x, StringComparer.Ordinal);
    return string.Concat(kept);
}

static string Name(Type t)
{
    if (t.IsByRef)
        return Name(t.GetElementType()) + "&";
    if (t.IsPointer)
        return Name(t.GetElementType()) + "*";
    if (t.IsArray)
        return Name(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
    if (t.IsGenericParameter)
        return t.Name;
    var name = t.IsNested ? Name(t.DeclaringType) + "+" + t.Name : (t.Namespace == null ? "" : t.Namespace + ".") + t.Name;
    if (t.IsGenericType)
    {
        name = name.Substring(0, name.IndexOf('`') < 0 ? name.Length : name.IndexOf('`'));
        name += "<" + string.Join(", ", t.GetGenericArguments().Select(Name)) + ">";
    }
    return name;
}
