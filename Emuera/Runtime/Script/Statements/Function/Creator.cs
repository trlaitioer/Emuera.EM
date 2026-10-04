using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace MinorShift.Emuera.GameData.Function;

internal static class FunctionMethodCreator
{
	// Upstream merge recipe: upstream registers a builtin method with a `["NAME"] = new XxxMethod(...)` line in
	// Creator.cs plus its class in Creator.Method.cs. Neither belongs here: this file stays literal-free and
	// Creator.Method.cs stays deleted. Move the class into Function/BuiltinMethod/ and add a factory at the top
	// of its class body - `[BuiltinMethod("NAME")]` followed by `private static XxxMethod CreateNAME()`
	// returning a new instance - so the name sits next to the implementation (see BuiltinMethodAttribute.cs
	// for the mechanism).
	static FunctionMethodCreator()
	{
		methodList = new Dictionary<string, FunctionMethod>(Config.StrComper);
		// Uniqueness is enforced with OrdinalIgnoreCase whatever Config.StrComper says: with IgnoreCase=NO the
		// comparer is ordinal, so two names differing only in case would both register and one would shadow
		// the other silently - the shape this mechanism exists to remove. Today no builtin has a case variant,
		// so both rules agree; `Add` (not the indexer) keeps the dictionary comparer as a second safety net.
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

		// No sort: the registry order is the scan order (reflection metadata order) and is deliberately not
		// pinned - the only place it is observable is the order the overwrite report (ErbLoader's
		// OverWriteSystemFuncWarn) prints the names in, and that order carries no meaning.
		foreach ((BuiltinMethodAttribute attr, MethodInfo factory) in BuiltinMethodAttribute.GetDefinitions())
		{
			if (!seen.Add(attr.Name))
			{
				throw new ExeEE($"duplicate builtin method name: {attr.Name} ({FactoryName(factory)})");
			}

			methodList.Add(attr.Name, CreateMethod(attr.Name, factory));
		}

		//1823 自分の関数名を知っていた方が何かと便利なので覚えさせることにした
		foreach (KeyValuePair<string, FunctionMethod> pair in methodList)
		{
			pair.Value.SetMethodName(pair.Key);
		}
	}

	/// <summary>
	/// Runs one factory, checking the shape the mechanism relies on - a parameterless method returning a
	/// concrete <see cref="FunctionMethod"/>. The scan (<see cref="BuiltinMethodAttribute.GetDefinitions"/>)
	/// only yields static methods, so "no attributed method is non-static" cannot be checked here; the registry
	/// gate test pins it instead (BuiltinMethodRegistryTests). A malformed factory is a programming error (there
	/// is no script position to attach it to), so the message names the factory. Note that a throw from here
	/// leaves the type initializer broken and reaches the user as a TypeInitializationException, so these
	/// invariants are covered by tests rather than by in-game diagnostics.
	/// </summary>
	private static FunctionMethod CreateMethod(string name, MethodInfo factory)
	{
		Type returnType = factory.ReturnType;
		if (factory.GetParameters().Length != 0
		    || returnType.IsAbstract
		    || !typeof(FunctionMethod).IsAssignableFrom(returnType))
		{
			throw new ExeEE($"[BuiltinMethod(\"{name}\")] {FactoryName(factory)} must be a parameterless "
			                + "static method returning a concrete FunctionMethod subclass");
		}

		try
		{
			return (FunctionMethod)factory.Invoke(null, null)!;
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			// Unwrap so the failure inside the factory - not the reflection call - is what surfaces.
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw; // unreachable: Throw() always throws
		}
	}

	/// <summary>`DeclaringType.Name.MethodName`, for messages about a factory that cannot name itself.</summary>
	private static string FactoryName(MethodInfo factory)
	{
		return $"{factory.DeclaringType?.Name}.{factory.Name}";
	}

	private static readonly Dictionary<string, FunctionMethod> methodList;

	public static Dictionary<string, FunctionMethod> GetMethodList()
	{
		return methodList;
	}
}
