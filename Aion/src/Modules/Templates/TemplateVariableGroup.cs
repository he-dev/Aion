using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Scriban.Runtime;

namespace Aion.Modules.Templates;

public abstract class TemplateVariableGroup(string key) : IEnumerable<KeyValuePair<string, object?>>
{
    private static IEqualityComparer<string> Comparer => StringComparer.OrdinalIgnoreCase;

    public string Key => key;

    public ScriptObject ToScriptObject()
    {
        var members = new ScriptObject(Comparer);
        members.Import(this.ToDictionary(Comparer));
        // meta: Selectors like "parent.child" require nested ScriptObjects.
        return new ScriptObject(Comparer) { [key] = members };
    }

    public abstract IEnumerator<KeyValuePair<string, object?>> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}