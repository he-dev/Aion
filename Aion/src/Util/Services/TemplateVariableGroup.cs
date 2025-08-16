using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Scriban.Runtime;

namespace Aion.Util.Services;

public abstract class TemplateVariableGroup(string name) : IEnumerable<KeyValuePair<string, object?>>
{
    private static IEqualityComparer<string> Comparer => StringComparer.OrdinalIgnoreCase;

    public ScriptObject ToScriptObject()
    {
        var members = new ScriptObject(Comparer);
        members.Import(this.ToDictionary(Comparer));
        // meta: Selectors like "parent.child" require nested ScriptObjects.
        return new ScriptObject(Comparer) { [name] = members };
    }

    public abstract IEnumerator<KeyValuePair<string, object?>> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}