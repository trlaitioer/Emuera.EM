namespace MinorShift.Emuera.Runtime.Utils.PluginSystem;

public interface IPluginMethod
{
	abstract string Name { get; }
	abstract string Description { get; }
	abstract void Execute(PluginMethodParameter[] args);
}