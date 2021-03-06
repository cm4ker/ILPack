﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Lokad.ILPack.Metadata;

namespace Lokad.ILPack
{
    public static class Extensions
    {
        private static PrimitiveTypeCode GetPrimitiveTypeCode(Type type)
        {
            // What is PrimitiveTypeCode.TypedReference? It's not used for now

            if (type == typeof(bool))
            {
                return PrimitiveTypeCode.Boolean;
            }

            if (type == typeof(byte))
            {
                return PrimitiveTypeCode.Byte;
            }

            if (type == typeof(char))
            {
                return PrimitiveTypeCode.Char;
            }

            if (type == typeof(double))
            {
                return PrimitiveTypeCode.Double;
            }

            if (type == typeof(short))
            {
                return PrimitiveTypeCode.Int16;
            }

            if (type == typeof(int))
            {
                return PrimitiveTypeCode.Int32;
            }

            if (type == typeof(long))
            {
                return PrimitiveTypeCode.Int64;
            }

            if (type == typeof(IntPtr))
            {
                return PrimitiveTypeCode.IntPtr;
            }

            if (type == typeof(object))
            {
                return PrimitiveTypeCode.Object; // this strange, because Object is not primitive type
            }

            if (type == typeof(sbyte))
            {
                return PrimitiveTypeCode.SByte;
            }

            if (type == typeof(float))
            {
                return PrimitiveTypeCode.Single;
            }

            if (type == typeof(string))
            {
                return PrimitiveTypeCode.String; // this strange, because String is not primitive type
            }

            if (type == typeof(ushort))
            {
                return PrimitiveTypeCode.UInt16;
            }

            if (type == typeof(uint))
            {
                return PrimitiveTypeCode.UInt32;
            }

            if (type == typeof(ulong))
            {
                return PrimitiveTypeCode.UInt64;
            }

            if (type == typeof(UIntPtr))
            {
                return PrimitiveTypeCode.UIntPtr;
            }

            if (type == typeof(void))
            {
                return PrimitiveTypeCode.Void;
            }

            throw new ArgumentException($"Type is unknown: {MetadataHelper.GetFriendlyName(type)}", nameof(type));
        }

        internal static void FromSystemType(
            this ReturnTypeEncoder typeEncoder,
            Type type,
            IAssemblyMetadata metadata)
        {
            if (type == typeof(void))
            {
                typeEncoder.Void();
            }
            else if (type.IsByRef)
            {
                typeEncoder.Type(true).FromSystemType(type.GetElementType(), metadata);
            }
            else
            {
                typeEncoder.Type(false).FromSystemType(type, metadata);
            }
        }

        // Add a range of local variables to a local signature encoder
        internal static void AddRange(this LocalVariablesEncoder sig, IEnumerable<LocalVariableInfo> localVariables, IAssemblyMetadata metadata)
        {
            foreach (var v in localVariables)
            {
                Add(sig, v, metadata);
            }
        }

        // Add a local variable to a local variable signature encoder
        internal static void Add(this LocalVariablesEncoder sig, LocalVariableInfo localVariableInfo, IAssemblyMetadata metadata)
        {
            if (localVariableInfo.LocalType.IsByRef)
            {
                sig.AddVariable().Type(
                        true,
                        localVariableInfo.IsPinned)
                    .FromSystemType(localVariableInfo.LocalType.GetElementType(), metadata);
            }
            else
            {
                sig.AddVariable().Type(
                        false,
                        localVariableInfo.IsPinned)
                    .FromSystemType(localVariableInfo.LocalType, metadata);
            }
        }


        internal static void FromSystemType(this SignatureTypeEncoder typeEncoder, Type type,
            IAssemblyMetadata metadata)
        {
            if (type.IsByRef)
            {
                throw new ArgumentException("ByRef types should be handled by parameter encoder or return type encoder");
            }
            else if (type.IsPointer)
            {
                typeEncoder.Pointer().FromSystemType(type.GetElementType(), metadata);
            }
            else if (type.IsPrimitive)
            {
                typeEncoder.PrimitiveType(GetPrimitiveTypeCode(type));
            }
            else if (type == typeof(string))
            {
                typeEncoder.String();
            }
            else if (type == typeof(object))
            {
                typeEncoder.Object();
            }
            else if (type == typeof(void))
            {
                throw new ArgumentException(
                    "Void type is not allowed in SignatureTypeEncoder. Please, use FromSystemType from ReturnTypeEncoder.",
                    nameof(type));
            }
            else if (type.IsArray)
            {
                var elementType = type.GetElementType();

                if (type.GetArrayRank() == 1)
                {
                    typeEncoder.SZArray().FromSystemType(elementType, metadata);
                }
                else
                {
                    typeEncoder.Array(
                        x => x.FromSystemType(elementType, metadata),
                        x => x.Shape(
                            type.GetArrayRank(),
                            ImmutableArray.Create<int>(),
                            ImmutableArray.CreateRange<int>(Enumerable.Repeat(0, type.GetArrayRank())) // better matches metadata from C#
                            ));
                }
            }
            else if (type.IsGenericType)
            {
                var genericTypeDef = type.GetGenericTypeDefinition();
                var typeHandler = metadata.GetTypeHandle(genericTypeDef);
                var genericArguments = type.GetGenericArguments();

                var inst = typeEncoder.GenericInstantiation(typeHandler, genericArguments.Length, type.IsValueType);
                foreach (var ga in genericArguments)
                {
                    if (ga.IsGenericParameter)
                    {
                        inst.AddArgument().GenericTypeParameter(ga.GenericParameterPosition);
                    }
                    else
                    {
                        inst.AddArgument().FromSystemType(ga, metadata);
                    }
                }
            }
            else if (type.IsGenericMethodParameter)
            {
                typeEncoder.GenericMethodTypeParameter(type.GenericParameterPosition);
            }
            else if (type.IsGenericParameter)
            {
                typeEncoder.GenericTypeParameter(type.GenericParameterPosition);
            }
            else
            {
                var typeHandler = metadata.GetTypeHandle(type);
                typeEncoder.Type(typeHandler, type.IsValueType);
            }
        }
    }
}