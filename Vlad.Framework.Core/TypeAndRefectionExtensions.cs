﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Aspectacular
{
    public enum ParamDirectionEnum
    {
        In = 1, Out, RefInOut
    }


    public static class TypeAndRefectionExtensions
    {
        private static readonly string[][] simpleParmTypeNames = 
        {
            new[] { "Void", "void" },
            new[] { "String", "string" },
            new[] { "Byte", "byte" },
            new[] { "SByte", "sbyte" },
            new[] { "Int16", "short" },
            new[] { "UInt16", "ushort" },
            new[] { "Int32", "int" },
            new[] { "UInt32", "uint" },
            new[] { "Int64", "long" },
            new[] { "UInt64", "ulong" },
            new[] { "Boolean", "bool" },
            new[] { "Decimal", "decimal" },
            new[] { "Double", "double" },
            new[] { "Single", "float" },
            new[] { "Char", "char" },
        };

        private static readonly Dictionary<string, string> stdTypeCSharpNames = new Dictionary<string, string>();

        static TypeAndRefectionExtensions()
        {
            foreach (string[] pair in simpleParmTypeNames)
                stdTypeCSharpNames.Add(pair[0], pair[1]);
        }

        public static bool IsNullOrDefault<T>(this T? val) where T : struct, IEquatable<T>
        {
            return val == null || val.Value.Equals(default(T));
        }

        //public static bool IsNullOrDefault<T>(this T? val) where T : struct
        //{
        //    return val == null || val.Value.Equals(default(T));
        //}

        #region Type extensions

        /// <summary>
        /// Returns true if type is one of the following:
        /// void, string, byte, sbyte, short, ushort, int, uint, long, ulong, bool, decimal, double, float, char
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsSimpleCSharpType(this Type type)
        {
            string typeName = type.Name.TrimEnd('&');
            return stdTypeCSharpNames.ContainsKey(typeName);
        }

        /// <summary>
        /// Returns type name formatted according to C# language. Properly supports generics.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="fullyQualified"></param>
        /// <returns></returns>
        public static string FormatCSharp(this Type type, bool fullyQualified = false)
        {
            string typeName = TypeToCSharpString(type, fullyQualified);

            string stdName;
            if (stdTypeCSharpNames.TryGetValue(typeName, out stdName))
                typeName = stdName;

            return typeName;
        }

        private static string TypeToCSharpString(Type type, bool fullyQualified = false)
        {
            string typeName = (fullyQualified ? type.FullName : type.Name).TrimEnd('&');

            if (!type.IsGenericType)
                return typeName;

            if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                Type underlyingType = type.GetGenericArguments()[0];
                return string.Format("{0}?", TypeToCSharpString(underlyingType));
            }
            string baseName = typeName.Substring(0, typeName.IndexOf("`", StringComparison.InvariantCulture));
            string generic = string.Join(", ", type.GetGenericArguments().Select(paramType => TypeToCSharpString(paramType, fullyQualified)));
            string fullName = string.Format("{0}<{1}>", baseName, generic);
            return fullName;
        }

        public static ParamDirectionEnum GetDirection(this ParameterInfo parmInfo)
        {
            bool isRef = parmInfo.ParameterType.Name.EndsWith("&");

            if (isRef)
                return parmInfo.IsOut ? ParamDirectionEnum.Out : ParamDirectionEnum.RefInOut;

            return ParamDirectionEnum.In;
        }

        /// <summary>
        /// Returns type name formatted according to C# language. Properly supports generics.
        /// </summary>
        /// <param name="parmInfo"></param>
        /// <param name="fullyQualified"></param>
        /// <returns></returns>
        public static string FormatCSharpType(this ParameterInfo parmInfo, bool fullyQualified = false)
        {
            string refOrOut;

            switch(parmInfo.GetDirection())
            {
                case ParamDirectionEnum.Out:
                    refOrOut = "out ";
                    break;
                case ParamDirectionEnum.RefInOut:
                    refOrOut = "ref ";
                    break;
                default:
                    refOrOut = string.Empty;
                    break;
            }

            return string.Format("{0}{1}", refOrOut, parmInfo.ParameterType.FormatCSharp(fullyQualified));
        }

        /// <summary>
        /// Very slow! Evaluates expression by turning it into a function expression, compiling it, and then calling using Reflection.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public static object EvaluateExpressionVerySlow(this Expression expression)
        {
            // This is really, veeery, terribly slow. 
            // The performance loss double-whammy is expression compilation plus reflection invocation.
            LambdaExpression lx = Expression.Lambda(expression);
            Delegate caller = lx.Compile(); // Slowness #1 - compilation is slow.
            var val = caller.DynamicInvoke();  // Slowness # 2 - dynamic (Reflection) invocation. 
            return val;
        }

        /// <summary>
        /// Returns true if classType is derived from interface TInterface
        /// </summary>
        /// <typeparam name="TInterface"></typeparam>
        /// <param name="classType"></param>
        /// <returns></returns>
        public static bool IsDerivedFromInterface<TInterface>(this Type classType) 
            where TInterface : class
        {
            Type interfaceType = typeof(TInterface);

            if(!interfaceType.IsInterface)
                throw new Exception("\"{0}\" is not an interface.".SmartFormat(interfaceType.FormatCSharp()));

            return interfaceType.IsAssignableFrom(classType);
        }

        public static object GetPropertyValue(this Type type, string propertyName, object instance)
        {
            if (instance != null && instance.GetType() != type)
                throw new Exception("Type mismatch between type and instance.GetType().");

            PropertyInfo propInfo = type.GetProperty(propertyName);
            if (propInfo == null)
                throw new Exception("Type \"{0}\" has no \"{1}\" property.".SmartFormat(type.FormatCSharp(), propertyName));

            object val = propInfo.GetValue(instance, null);
            return val;
        }

        /// <summary>
        /// Returns value of an instance property.
        /// Uses reflection, somewhat slow.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static T GetPropertyValue<T>(this object instance, string propertyName)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");

            object val = GetPropertyValue(instance.GetType(), propertyName, instance);
            return (T)val;
        }

        /// <summary>
        /// Returns value of a static property.
        /// Uses reflection, somewhat slow.
        /// </summary>
        /// <typeparam name="T">Type of returned value.</typeparam>
        /// <param name="type"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static T GetPropertyValue<T>(this Type type, string propertyName)
        {
            object val = GetPropertyValue(type, propertyName, instance: null);
            return (T)val;
        }


        public static object GetMemberFieldValue(this Type type, string fieldName, object instance)
        {
            if (instance != null && instance.GetType() != type)
                throw new Exception("Type mismatch between type and instance.GetType().");

            FieldInfo fieldInfo = type.GetField(fieldName);
            if (fieldInfo == null)
                throw new Exception("Type \"{0}\" has no \"{1}\" field.".SmartFormat(type.FormatCSharp(), fieldName));

            object val = fieldInfo.GetValue(instance);
            return val;
        }

        /// <summary>
        /// Returns value of an instance field.
        /// Uses reflection, somewhat slow.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static T GetMemberFieldValue<T>(this object instance, string fieldName)
        {
            if (instance == null)
                throw new ArgumentNullException("instance");

            object val = GetMemberFieldValue(instance.GetType(), fieldName, instance);
            return (T)val;
        }

        /// <summary>
        /// Returns value of a static field.
        /// Uses reflection, somewhat slow.
        /// </summary>
        /// <typeparam name="T">Type of returned value.</typeparam>
        /// <param name="type"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        public static T GetMemberFieldValue<T>(this Type type, string fieldName)
        {
            object val = GetMemberFieldValue(type, fieldName, instance: null);
            return (T)val;
        }

        #endregion Type extensions

        /// <summary>
        /// Converts default value of T to T? with no value.
        /// If value is not default, returns value itself.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="val"></param>
        /// <returns></returns>
        public static T? NullIfDefault<T>(this T? val) where T : struct
        {
            if (val == null)
                return val;

            return val.Value.Equals(default(T)) ? null : val;
        }

        /// <summary>
        /// Returns true if all bits of the flag value are present.
        /// </summary>
        /// <param name="valueToCheck"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool IsFlagOn(this int valueToCheck, int flag)
        {
            return (valueToCheck & flag) == flag;
        }

        /// <summary>
        /// Returns true if any bits of the flag value are present.
        /// </summary>
        /// <param name="valueToCheck"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool IsAnyFlagOn(this int valueToCheck, int flag)
        {
            return (valueToCheck & flag) != 0;
        }

        /// <summary>
        /// Returns true if all bits of the flag value are present.
        /// </summary>
        /// <param name="valueToCheck"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool IsFlagOn(this Enum valueToCheck, Enum flag)
        {
            return ((int)(object)valueToCheck & (int)(object)flag) == (int)(object)flag;
        }
        
        /// <summary>
        /// Returns true if any bits of the flag value are present.
        /// </summary>
        /// <param name="valueToCheck"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool IsAnyFlagOn(this Enum valueToCheck, Enum flag)
        {
            return ((int)(object)valueToCheck & (int)(object)flag) != 0;
        }

        /// <summary>
        /// Serializes object to XML
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="defaultNamespace"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static string ToXml(this object obj, string defaultNamespace = null, XmlWriterSettings settings = null)
        {
            if (obj == null)
                return null;

            if (obj is string)
                throw new ArgumentException("Cannot convert string to XML.");

            //if(!obj.GetType().IsSerializable)
            //    throw new ArgumentException("Object must be serializable in order to be converted to XML.");

            if (settings == null)
                settings = new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "\t",
                    Encoding = Encoding.UTF8,
                    OmitXmlDeclaration = true,
                };

            XmlSerializer ser = new XmlSerializer(obj.GetType(), defaultNamespace);

            StringBuilder sb = new StringBuilder();
            
            using (var writer = XmlWriter.Create(sb, settings))
            {
                ser.Serialize(writer, obj);
            }

            string xml = sb.ToString();
            return xml;
        }
    }
}
