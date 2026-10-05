// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Reserved for use by a compiler for tracking metadata.
    /// This attribute should not be used by developers in source code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter, Inherited = false)]
    public sealed class NullableAttribute : Attribute
    {
        /// <summary>Flags specifying metadata related to nullable reference types.</summary>
        public readonly byte[] NullableFlags;

        /// <summary>Initializes the attribute.</summary>
        /// <param name="value">The flags value.</param>
        public NullableAttribute(byte value)
        {
            NullableFlags = new[] { value };
        }

        /// <summary>Initializes the attribute.</summary>
        /// <param name="value">The flags value.</param>
        public NullableAttribute(byte[] value)
        {
            NullableFlags = value;
        }
    }

    /// <summary>
    /// Supplies the nullable context metadata expected by the compiler when game-generated
    /// Unity assemblies contain a stripped copy of this compiler attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Interface | AttributeTargets.Module | AttributeTargets.Delegate, Inherited = false)]
    public sealed class NullableContextAttribute : Attribute
    {
        /// <summary>Flags specifying the nullable context.</summary>
        public readonly byte Flag;

        /// <summary>Initializes the attribute.</summary>
        /// <param name="value">The nullable context flag.</param>
        public NullableContextAttribute(byte value)
        {
            Flag = value;
        }
    }
}
