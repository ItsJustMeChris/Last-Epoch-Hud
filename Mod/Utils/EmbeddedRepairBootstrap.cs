using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using MelonLoader;
using MelonLoader.Utils;
using Mono.Cecil;

namespace Mod.Utils;

#pragma warning disable CA2255
internal static class EmbeddedRepairBootstrap
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		try
		{
			var coreModulePath = Path.Combine(MelonEnvironment.Il2CppAssembliesDirectory, "UnityEngine.CoreModule.dll");
			MelonLogger.Msg($"[LEHud] {CoreModuleRepair.Repair(coreModulePath)}");
		}
		catch (Exception exception)
		{
			MelonLogger.Error($"[LEHud] UnityEngine.CoreModule normalization failed: {exception}");
		}
	}
}
#pragma warning restore CA2255

internal static class CoreModuleRepair
{
	internal static string Repair(string path)
	{
		path = Path.GetFullPath(path);
		if (!File.Exists(path))
			throw new FileNotFoundException("Generated UnityEngine.CoreModule.dll was not found.", path);

		var markerPath = path + ".lehud-repair.sha256";
		var originalBytes = File.ReadAllBytes(path);
		var originalHash = Convert.ToHexString(SHA256.HashData(originalBytes));
		if (File.Exists(markerPath) && File.ReadAllText(markerPath).Trim() == originalHash)
			return "UnityEngine.CoreModule already normalized; no changes.";

		var signatures = RewriteAndGetSignatures(originalBytes, out var rewrittenBytes);
		var rewrittenSignatures = ReadSignatures(rewrittenBytes);
		if (!signatures.SequenceEqual(rewrittenSignatures, StringComparer.Ordinal))
			throw new InvalidDataException("UnityEngine.CoreModule rewrite changed type or member signatures; original retained.");

		var rewrittenHash = Convert.ToHexString(SHA256.HashData(rewrittenBytes));
		var backupPath = path + ".lehud-original-" + originalHash[..16] + ".bak";
		if (!File.Exists(backupPath))
		{
			File.WriteAllBytes(backupPath, originalBytes);
		}
		else if (!SHA256.HashData(File.ReadAllBytes(backupPath)).SequenceEqual(SHA256.HashData(originalBytes)))
		{
			throw new InvalidDataException("Existing UnityEngine.CoreModule backup differs from the original; refusing to replace it.");
		}

		var temporaryPath = path + ".lehud-" + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllBytes(temporaryPath, rewrittenBytes);
			File.Replace(temporaryPath, path, null);
		}
		finally
		{
			if (File.Exists(temporaryPath))
				File.Delete(temporaryPath);
		}

		File.WriteAllText(markerPath, rewrittenHash);
		return $"UnityEngine.CoreModule metadata normalized; original backed up to {Path.GetFileName(backupPath)}.";
	}

	private static string[] RewriteAndGetSignatures(byte[] source, out byte[] rewritten)
	{
		using var sourceStream = new MemoryStream(source);
		using var assembly = AssemblyDefinition.ReadAssembly(sourceStream);
		var signatures = GetSignatures(assembly);
		using var rewrittenStream = new MemoryStream();
		assembly.Write(rewrittenStream);
		rewritten = rewrittenStream.ToArray();
		return signatures;
	}

	private static string[] ReadSignatures(byte[] source)
	{
		using var stream = new MemoryStream(source);
		using var assembly = AssemblyDefinition.ReadAssembly(stream);
		return GetSignatures(assembly);
	}

	private static string[] GetSignatures(AssemblyDefinition assembly)
	{
		var signatures = new List<string>();
		foreach (var module in assembly.Modules)
		{
			signatures.Add($"A:{assembly.Name.FullName}:{module.Name}");
			AddTypeSignatures(module.Types, signatures);
		}

		return signatures.OrderBy(signature => signature, StringComparer.Ordinal).ToArray();
	}

	private static void AddTypeSignatures(IEnumerable<TypeDefinition> types, List<string> signatures)
	{
		foreach (var type in types)
		{
			signatures.Add($"T:{type.FullName}:{type.Attributes}:{type.BaseType?.FullName}");
			foreach (var field in type.Fields)
				signatures.Add($"F:{field.FullName}:{field.Attributes}");
			foreach (var method in type.Methods)
				signatures.Add($"M:{method.FullName}:{method.Attributes}");
			foreach (var property in type.Properties)
				signatures.Add($"P:{property.FullName}:{property.Attributes}");
			foreach (var @event in type.Events)
				signatures.Add($"E:{@event.FullName}:{@event.Attributes}");

			AddTypeSignatures(type.NestedTypes, signatures);
		}
	}
}