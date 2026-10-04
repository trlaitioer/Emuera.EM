using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MinorShift.Emuera.GameData.Function;

/// <summary>
/// Names one registered builtin method instance and is the only place a builtin name is written: the registry
/// in <see cref="FunctionMethodCreator"/> is built by scanning these attributes. A candidate is a parameterless
/// static method returning a concrete <c>FunctionMethod</c> subclass and its name occurs exactly once in the
/// assembly (compared with <see cref="StringComparer.OrdinalIgnoreCase"/>); because the scan only yields static
/// methods, an attributed method that forgot `static` would be dropped silently - BuiltinMethodRegistryTests
/// pins its absence instead. The factory body carries that name's construction arguments and must return a new
/// instance per call: the name is per-instance state - the behaviour of several methods is dispatched on it and
/// error messages quote it - so two names must never share an instance.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
internal sealed class BuiltinMethodAttribute(string name) : Attribute
{
	public string Name { get; } = name;

	// Factories are private or internal by convention (their implementation classes are internal), so the
	// scan has to ask for non-public members explicitly; DeclaredOnly keeps inherited members out of the walk.
	private const BindingFlags FactoryFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

	/// <summary>
	/// Enumerates every static factory method carrying this attribute, in reflection metadata order - an
	/// order nothing may depend on (the registry does not sort). The result is a deferred query over the
	/// whole assembly, so every enumeration re-walks the types and re-creates the attribute instances -
	/// enumerate it once.
	/// </summary>
	public static IEnumerable<(BuiltinMethodAttribute attr, MethodInfo factory)> GetDefinitions()
	{
		return typeof(BuiltinMethodAttribute).Assembly.GetTypes()
			.SelectMany(type => type.GetMethods(FactoryFlags))
			.SelectMany(
				factory => factory.GetCustomAttributes<BuiltinMethodAttribute>(false),
				(factory, attr) => (attr, factory)
			);
	}
}
